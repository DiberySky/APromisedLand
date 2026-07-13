FROM python:3.10-slim

WORKDIR /app

# 安装系统依赖（libsndfile1 供 soundfile 使用，以及必要的编译工具）
RUN apt-get update && \
    apt-get install -y --no-install-recommends libsndfile1 && \
    rm -rf /var/lib/apt/lists/*

# 安装 PyTorch（CPU 版本，减小镜像体积）
RUN pip install --no-cache-dir -i https://pypi.tuna.tsinghua.edu.cn/simple \
    torch --index-url https://download.pytorch.org/whl/cpu

# 安装 ASR 相关 Python 包
RUN pip install --no-cache-dir -i https://pypi.tuna.tsinghua.edu.cn/simple \
    funasr fastapi uvicorn python-multipart soundfile

COPY server.py .

EXPOSE 8000
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]