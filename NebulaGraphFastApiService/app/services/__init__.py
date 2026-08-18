"""Service layer: business logic wrapping nebula-python sessions."""
from .connection_service import ConnectionService
from .space_service import SpaceService
from .schema_service import SchemaService
from .vertex_service import VertexService
from .edge_service import EdgeService
from .query_service import QueryService
from .job_service import JobService
from .user_service import UserService

# Shared singletons - stateless services reuse the global ``db`` pool.
connection_service = ConnectionService()
space_service = SpaceService()
schema_service = SchemaService()
vertex_service = VertexService()
edge_service = EdgeService()
query_service = QueryService()
job_service = JobService()
user_service = UserService()

__all__ = [
    "ConnectionService", "SpaceService", "SchemaService", "VertexService",
    "EdgeService", "QueryService", "JobService", "UserService",
    "connection_service", "space_service", "schema_service", "vertex_service",
    "edge_service", "query_service", "job_service", "user_service",
]
