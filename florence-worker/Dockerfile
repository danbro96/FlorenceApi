FROM ubuntu:24.04 AS runtime

ENV DEBIAN_FRONTEND=noninteractive \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    HF_HOME=/cache/hf

# Ubuntu 24.04 has Python 3.12 in default repos; add Intel GPU packages
RUN apt-get update && apt-get install -y --no-install-recommends \
      python3.12 python3-pip python3.12-venv \
      gnupg gpg-agent wget ca-certificates curl && \
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

# Set python3.12 as default python3
RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.12 1 && \
    update-alternatives --install /usr/bin/python python /usr/bin/python3.12 1

WORKDIR /app

COPY florence-worker/requirements.txt .
RUN pip install --upgrade pip && pip install -r requirements.txt

COPY florence-worker/ov_florence2_helper.py florence-worker/app.py ./

RUN mkdir -p /cache
ENV MODEL_DIR=/cache/florence-ov

EXPOSE 9000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "9000"]
