"""
NebulaGraph FastAPI Web Service - Aspire 集成版本
"""
import logging
import os
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.config import settings
from app.routers import graph, health
from app.services.nebula_client import nebula_client

# Aspire OpenTelemetry 集成（可选，仅在环境变量存在时启用）
try:
    from telemetry import configure_telemetry
    HAS_TELEMETRY = True
except ImportError:
    HAS_TELEMETRY = False

logging.basicConfig(
    level=logging.INFO if not settings.DEBUG else logging.DEBUG,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """应用生命周期管理"""
    logger.info(f"Starting {settings.APP_NAME} v{settings.APP_VERSION}")

    # 启动 NebulaGraph 连接池
    success = nebula_client.initialize()
    if not success:
        logger.error("Failed to initialize NebulaGraph connection pool!")
    else:
        logger.info("NebulaGraph connection pool initialized successfully")

    yield

    # 关闭
    logger.info("Shutting down service...")
    nebula_client.close()
    logger.info("NebulaGraph connection pool closed")


def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.APP_NAME,
        version=settings.APP_VERSION,
        description="NebulaGraph FastAPI Service - Integrated with .NET Aspire",
        docs_url="/docs",
        redoc_url="/redoc",
        openapi_url="/openapi.json",
        lifespan=lifespan
    )

    # CORS
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.CORS_ORIGINS,
        allow_credentials=settings.CORS_ALLOW_CREDENTIALS,
        allow_methods=settings.CORS_ALLOW_METHODS,
        allow_headers=settings.CORS_ALLOW_HEADERS,
    )

    # 注册路由
    app.include_router(health.router, prefix="/api/v1")
    app.include_router(graph.router, prefix="/api/v1")

    @app.get("/", tags=["Root"])
    async def root():
        return {
            "name": settings.APP_NAME,
            "version": settings.APP_VERSION,
            "docs": "/docs",
            "health": "/api/v1/health",
            "graph_api": "/api/v1/graph"
        }

    # Aspire OpenTelemetry 集成
    if HAS_TELEMETRY and os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT"):
        configure_telemetry(app, service_name="nebula-graph-api")
        logger.info("OpenTelemetry configured for Aspire Dashboard")

    return app


app = create_app()


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "app.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
        log_level="debug" if settings.DEBUG else "info"
    )
