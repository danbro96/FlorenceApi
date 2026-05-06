import asyncio
import io
import logging
import math
import os
import time
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Any

import huggingface_hub as hf_hub
import openvino as ov
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from PIL import Image
from transformers import AutoProcessor

from ov_florence2_helper import OVFlorence2Model, convert_florence2

LOG = logging.getLogger("florence-worker")
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

MODEL_ID = os.environ.get("MODEL_ID", "microsoft/Florence-2-base-ft")
MODEL_REVISION = os.environ.get("MODEL_REVISION", "main")
MODEL_DIR = os.environ.get("MODEL_DIR", "/cache/florence-ov")
DEVICE_PREFERENCE = os.environ.get("DEVICE", "")
MAX_IMAGE_BYTES = int(os.environ.get("MAX_IMAGE_BYTES", str(8 * 1024 * 1024)))

TASK_PROMPTS: dict[str, str] = {
    "caption": "<CAPTION>",
    "detailed_caption": "<DETAILED_CAPTION>",
    "more_detailed_caption": "<MORE_DETAILED_CAPTION>",
    "od": "<OD>",
    "dense_region_caption": "<DENSE_REGION_CAPTION>",
    "region_proposal": "<REGION_PROPOSAL>",
    "caption_to_phrase_grounding": "<CAPTION_TO_PHRASE_GROUNDING>",
    "referring_expression_segmentation": "<REFERRING_EXPRESSION_SEGMENTATION>",
    "ocr": "<OCR>",
    "ocr_with_region": "<OCR_WITH_REGION>",
}
TEXT_REQUIRED_TASKS = {"caption_to_phrase_grounding", "referring_expression_segmentation"}

# Single-GPU service: serialize inference so concurrent callers queue cleanly
# instead of contending for the device.
INFERENCE_LOCK = asyncio.Semaphore(1)


def _pick_device(core: ov.Core) -> str:
    devices = core.available_devices
    LOG.info("OpenVINO devices: %s", devices)
    if DEVICE_PREFERENCE and DEVICE_PREFERENCE in devices:
        return DEVICE_PREFERENCE
    for d in devices:
        if d.startswith("GPU"):
            return d
    LOG.warning("No GPU device found; falling back to CPU.")
    return "CPU"


def _download_pinned_snapshot(model_id: str, revision: str, target_dir: Path) -> None:
    # Mirrors download_original_model() in ov_florence2_helper.py but adds revision pinning,
    # which the helper does not expose. Keep the flash_attn patch in sync if you re-vendor.
    target_dir.mkdir(parents=True, exist_ok=True)
    hf_hub.snapshot_download(repo_id=model_id, revision=revision, local_dir=str(target_dir))
    modeling_file = target_dir / "modeling_florence2.py"
    orig_modeling_file = target_dir / f"orig_{modeling_file.name}"
    if not orig_modeling_file.exists():
        modeling_file.rename(orig_modeling_file)
    content = orig_modeling_file.read_text()
    content = content.replace("if is_flash_attn_2_available():", "")
    content = content.replace(
        "    from flash_attn.bert_padding import index_first_axis, pad_input, unpad_input", "")
    content = content.replace(
        "    from flash_attn import flash_attn_func, flash_attn_varlen_func", "")
    modeling_file.write_text(content)


def _load() -> tuple[OVFlorence2Model, Any, str]:
    model_dir = Path(MODEL_DIR)
    if not model_dir.exists() or not any(model_dir.iterdir()):
        LOG.info("Converting %s @ %s to OpenVINO IR at %s — first boot only, may take several minutes.",
                 MODEL_ID, MODEL_REVISION, model_dir)
        orig_dir = model_dir / "chkpt"
        if not orig_dir.exists():
            _download_pinned_snapshot(MODEL_ID, MODEL_REVISION, orig_dir)
        convert_florence2(MODEL_ID, str(model_dir), orig_model_dir=orig_dir)

    core = ov.Core()
    device = _pick_device(core)
    LOG.info("Loading Florence-2 IR from %s on device=%s", model_dir, device)
    model = OVFlorence2Model(str(model_dir), device)
    # Processor is saved into MODEL_DIR by convert_florence2; loading from there avoids
    # a re-fetch and stays consistent with the pinned revision used during conversion.
    processor = AutoProcessor.from_pretrained(str(model_dir), trust_remote_code=True)
    return model, processor, device


state: dict[str, Any] = {}


@asynccontextmanager
async def lifespan(_: FastAPI):
    state["model"], state["processor"], state["device"] = _load()
    LOG.info("Florence-2 ready on %s.", state["device"])
    yield


app = FastAPI(lifespan=lifespan)


@app.get("/healthz")
def healthz():
    if "model" not in state:
        raise HTTPException(503, "model not loaded")
    return {"status": "ok", "device": state["device"], "model": MODEL_ID}


@app.get("/options")
def options():
    return {
        "tasks": [
            {"id": tid, "needs_text": tid in TEXT_REQUIRED_TASKS}
            for tid in TASK_PROMPTS
        ],
        "device": state.get("device"),
        "model": MODEL_ID,
        "revision": MODEL_REVISION,
    }


def _loc_token_ids(tokenizer) -> set[int]:
    cached = state.get("loc_token_ids")
    if cached is not None:
        return cached
    ids = {tid for tok, tid in tokenizer.get_added_vocab().items() if tok.startswith("<loc_")}
    state["loc_token_ids"] = ids
    return ids


def _per_region_confidence(generated_ids, transition_scores, tokenizer) -> list[float]:
    """Mean per-token probability of each region's label tokens.

    Florence-2 emits OCR_WITH_REGION as `label <loc_*>x8 label <loc_*>x8 ...`.
    We average exp(log_prob) across the text tokens preceding each 8-token
    location burst. Location and special tokens are excluded from the mean.
    """
    loc_ids = _loc_token_ids(tokenizer)
    special_ids = set(tokenizer.all_special_ids)
    # transition_scores is aligned to the generated tokens (decoder_start excluded).
    score_list = transition_scores.tolist()
    token_list = generated_ids.tolist()[-len(score_list):]

    confidences: list[float] = []
    label_log_probs: list[float] = []
    loc_count = 0
    for token_id, log_prob in zip(token_list, score_list):
        if token_id in loc_ids:
            loc_count += 1
            if loc_count == 8:
                if label_log_probs:
                    confidences.append(math.exp(sum(label_log_probs) / len(label_log_probs)))
                else:
                    confidences.append(0.0)
                label_log_probs = []
                loc_count = 0
            continue
        if token_id in special_ids:
            continue
        if loc_count == 0:
            label_log_probs.append(log_prob)
    return confidences


def _run_inference(prompt: str, task_token: str, img: Image.Image):
    processor = state["processor"]
    model = state["model"]
    inputs = processor(text=prompt, images=img, return_tensors="pt")
    out = model.generate(
        input_ids=inputs["input_ids"],
        pixel_values=inputs["pixel_values"],
        max_new_tokens=1024,
        num_beams=3,
        do_sample=False,
        output_scores=True,
        return_dict_in_generate=True,
    )
    sequences = out.sequences
    generated_text = processor.batch_decode(sequences, skip_special_tokens=False)[0]
    parsed = processor.post_process_generation(
        generated_text, task=task_token, image_size=(img.width, img.height)
    )

    if task_token == "<OCR_WITH_REGION>":
        # compute_transition_scores handles beam search via beam_indices.
        transition_scores = model.language_model.compute_transition_scores(
            sequences, out.scores, out.beam_indices, normalize_logits=True
        )
        confidence = _per_region_confidence(sequences[0], transition_scores[0], processor.tokenizer)
        result = parsed.get(task_token, parsed)
        if isinstance(result, dict):
            # Pad/truncate so confidence is the same length as quad_boxes; the OCR parser
            # may emit fewer regions than the raw token stream produced 8-loc bursts for
            # (truncated output, malformed regions). Pad with 0.0 for safety.
            n = len(result.get("quad_boxes", []))
            if len(confidence) < n:
                confidence = confidence + [0.0] * (n - len(confidence))
            elif len(confidence) > n:
                confidence = confidence[:n]
            result["confidence"] = confidence
        return parsed

    return parsed


@app.post("/recognize")
async def recognize(
    image: UploadFile = File(...),
    task: str = Form(...),
    text_input: str | None = Form(None),
):
    if task not in TASK_PROMPTS:
        raise HTTPException(400, f"unknown task '{task}'")
    needs_text = task in TEXT_REQUIRED_TASKS
    if needs_text and not (text_input and text_input.strip()):
        raise HTTPException(400, f"task '{task}' requires text_input")

    raw = await image.read()
    if not raw:
        raise HTTPException(400, "empty image")
    if len(raw) > MAX_IMAGE_BYTES:
        raise HTTPException(413, f"image too large; max {MAX_IMAGE_BYTES} bytes")
    try:
        img = Image.open(io.BytesIO(raw)).convert("RGB")
    except Exception as e:
        raise HTTPException(400, f"could not decode image: {e}") from e

    task_token = TASK_PROMPTS[task]
    prompt = task_token if not needs_text else f"{task_token}{text_input.strip()}"

    started = time.perf_counter()
    async with INFERENCE_LOCK:
        # Inference is multi-second blocking work; offload from the event loop so /healthz
        # and other requests stay responsive while one inference is running.
        parsed = await asyncio.to_thread(_run_inference, prompt, task_token, img)
    elapsed_ms = int((time.perf_counter() - started) * 1000)

    result = parsed.get(task_token, parsed)
    return JSONResponse({
        "task": task,
        "image": {"width": img.width, "height": img.height},
        "result": result,
        "elapsed_ms": elapsed_ms,
    })
