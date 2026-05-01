FROM python:3.12-slim AS runtime

ENV DEBIAN_FRONTEND=noninteractive \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    HF_HOME=/cache/hf

# Intel GPU runtime for Arc (Alchemist) — required for OpenVINO GPU inference.
# Pin intel-opencl-icd to 25.13.33276.16: newer compute-runtime 25.18.x has reported
# A380 OpenCL regressions. Re-evaluate at the next OpenVINO LTS.
RUN apt-get update && apt-get install -y --no-install-recommends \
      gpg-agent wget ca-certificates curl && \
    wget -qO - https://repositories.intel.com/gpu/intel-graphics.key \
      | gpg --dearmor -o /usr/share/keyrings/intel-graphics.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/intel-graphics.gpg] https://repositories.intel.com/gpu/ubuntu jammy unified" \
      > /etc/apt/sources.list.d/intel-gpu.list && \
    apt-get update && apt-get install -y --no-install-recommends \
      intel-opencl-icd=25.13.33276.16-1099~22.04 \
      intel-level-zero-gpu \
      level-zero \
      ocl-icd-libopencl1 && \
    apt-mark hold intel-opencl-icd && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY florence-worker/requirements.txt .
RUN pip install --upgrade pip && pip install -r requirements.txt

COPY florence-worker/ov_florence2_helper.py florence-worker/app.py ./

RUN mkdir -p /cache
ENV MODEL_DIR=/cache/florence-ov

EXPOSE 9000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "9000"]
