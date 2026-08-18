"""API routers aggregating all nebula operations."""
from .connection import router as connection_router
from .spaces import router as spaces_router
from .schema import router as schema_router
from .vertices import router as vertices_router
from .edges import router as edges_router
from .query import router as query_router
from .jobs import router as jobs_router
from .users import router as users_router

__all__ = [
    "connection_router", "spaces_router", "schema_router", "vertices_router",
    "edges_router", "query_router", "jobs_router", "users_router",
]
