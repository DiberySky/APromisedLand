FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends     gcc     && rm -rf /var/lib/apt/lists/*

# Copy requirements first for better caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY app/ ./app/

# Expose port
EXPOSE 8000

# Health check（启动期 lifespan 会循环重连 graphd 最长 ~3 分钟，期间端口未绑定，start-period 需覆盖该窗口）
HEALTHCHECK --interval=30s --timeout=10s --start-period=200s --retries=3     CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/api/v1/health')" || exit 1

# Run application（单 worker：连接池是进程级全局，多 worker 会各自建池）
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
