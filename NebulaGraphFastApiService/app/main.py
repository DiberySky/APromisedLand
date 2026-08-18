"""FastAPI application factory: lifecycle, routers, docs, middleware."""
import logging
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .auth import verify_token
from .config import settings
from .database import db
from .exceptions import register_exception_handlers
from .routers import (
    connection_router,
    edges_router,
    jobs_router,
    query_router,
    schema_router,
    spaces_router,
    users_router,
    vertices_router,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s :: %(message)s",
)
logger = logging.getLogger("nebula_api")


@asynccontextmanager
async def lifespan(_: FastAPI):
    logger.info("Initialising Nebula connection pool ...")
    db.connect()
    logger.info("Nebula connection pool ready.")
    yield
    logger.info("Closing Nebula connection pool ...")
    db.close()


def create_app() -> FastAPI:
    app = FastAPI(
        title="NebulaGraph Web API Service",
        description=(
            "A comprehensive REST wrapper around `nebula-python` for "
            "NebulaGraph: connection, spaces, schema (tags / edges / indexes), "
            "vertices, edges, nGQL queries (GO / FETCH / LOOKUP / FIND PATH / "
            "GET SUBGRAPH), jobs and users / roles.\n\n"
            "All responses use the envelope `{code, message, data}`."
        ),
        version="1.0.0",
        lifespan=lifespan,
        dependencies=[Depends(verify_token)],
    )

    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    register_exception_handlers(app)

    for router in (
        connection_router,
        spaces_router,
        schema_router,
        vertices_router,
        edges_router,
        query_router,
        jobs_router,
        users_router,
    ):
        app.include_router(router)

    @app.get("/healthz", tags=["system"], summary="Process liveness probe")
    def healthz():
        return {"status": "alive"}

    @app.get("/", tags=["system"], summary="Service root")
    def root():
        return {
            "service": "NebulaGraph Web API",
            "version": "1.0.0",
            "docs": "/docs",
        }

    return app


app = create_app()
