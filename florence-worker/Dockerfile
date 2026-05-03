FROM ubuntu:24.04 AS runtime

ENV DEBIAN_FRONTEND=noninteractive \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    HF_HOME=/cache/hf

# Runtime: python + ca-certificates (HF downloads) + curl (compose healthcheck).
# Build-only: gnupg/gpg-agent/wget, marked auto-installed and stripped after the
# Intel apt key is registered.
RUN apt-get update && apt-get install -y --no-install-recommends \
      python3.12 python3-pip python3.12-venv \
      ca-certificates curl && \
    apt-get install -y --no-install-recommends --mark-auto \
      gnupg gpg-agent wget && \
    wget -qO - https://repositories.intel.com/gpu/intel-graphics.key \
      | gpg --dearmor -o /usr/share/keyrings/intel-graphics.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/intel-graphics.gpg] https://repositories.intel.com/gpu/ubuntu noble unified" \
      > /etc/apt/sources.list.d/intel-gpu.list && \
    apt-get update && apt-get install -y --no-install-recommends \
      intel-level-zero-gpu \
      level-zero \
      ocl-icd-libopencl1 && \
    apt-get autoremove -y --purge && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Set python3.12 as default python3
RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.12 1 && \
    update-alternatives --install /usr/bin/python python /usr/bin/python3.12 1

WORKDIR /app

# requirements.txt has --extra-index-url https://download.pytorch.org/whl/cpu
# pinned at the top, so torch resolves to the +cpu wheel (no NVIDIA libs).
COPY florence-worker/requirements.txt .
RUN pip install --break-system-packages -r requirements.txt
COPY florence-worker/ov_florence2_helper.py florence-worker/app.py ./

RUN mkdir -p /cache
ENV MODEL_DIR=/cache/florence-ov

EXPOSE 9000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "9000"]
