"""
NebulaGraph FastAPI Web Service - Main Application
"""
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.config import settings
from app.routers import graph, health
from app.services.nebula_client import nebula_client

# Configure logging
logging.basicConfig(
    level=logging.INFO if not settings.DEBUG else logging.DEBUG,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan manager.
    Handles startup (connection pool init) and shutdown (cleanup).
    """
    # Startup
    logger.info(f"Starting {settings.APP_NAME} v{settings.APP_VERSION}")

    # Aspire WaitFor(graphd) 只等容器进程启动，不等 graphd 可接受会话；
    # 一次性 initialize 失败会让 _connected 永久为 False（全部请求 503）。
    # 故启动期循环重试，直到 graphd/metad 就绪（上限 ~3 分钟）。
    import time
    max_attempts = 36
    for attempt in range(1, max_attempts + 1):
        if nebula_client.initialize():
            logger.info("NebulaGraph connection pool initialized successfully")
            break
        logger.warning(
            f"NebulaGraph connection pool init failed ({attempt}/{max_attempts}), retry in 5s"
        )
        if attempt < max_attempts:
            time.sleep(5)
    else:
        logger.error("Failed to initialize NebulaGraph connection pool after all retries!")
        logger.error("Service will start but graph endpoints may fail")

    yield

    # Shutdown
    logger.info("Shutting down service...")
    nebula_client.close()
    logger.info("NebulaGraph connection pool closed")


def create_app() -> FastAPI:
    """Application factory pattern"""

    app = FastAPI(
        title=settings.APP_NAME,
        version=settings.APP_VERSION,
        description="""
        # NebulaGraph FastAPI Service

        A comprehensive Web API service for NebulaGraph operations.

        ## Features
        - **nGQL Query Execution**: Execute any nGQL statement
        - **Schema Management**: List spaces, tags, edges, and their schemas
        - **CRUD Operations**: Insert vertices and edges
        - **Bulk Operations**: Batch insert for large datasets
        - **Health Monitoring**: Connection pool and cluster health checks

        ## Authentication
        NebulaGraph credentials are configured server-side via environment variables.

        ## Connection Pool
        Uses nebula3-python ConnectionPool for efficient session management.
        """,
        docs_url="/docs",
        redoc_url="/redoc",
        openapi_url="/openapi.json",
        lifespan=lifespan
    )

    # CORS Middleware
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.CORS_ORIGINS,
        allow_credentials=settings.CORS_ALLOW_CREDENTIALS,
        allow_methods=settings.CORS_ALLOW_METHODS,
        allow_headers=settings.CORS_ALLOW_HEADERS,
    )

    # Include routers
    app.include_router(health.router, prefix="/api/v1")
    app.include_router(graph.router, prefix="/api/v1")

    @app.get("/", tags=["Root"])
    async def root():
        """Root endpoint with API information"""
        return {
            "name": settings.APP_NAME,
            "version": settings.APP_VERSION,
            "docs": "/docs",
            "health": "/api/v1/health",
            "graph_api": "/api/v1/graph"
        }

    return app


# Create application instance
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
