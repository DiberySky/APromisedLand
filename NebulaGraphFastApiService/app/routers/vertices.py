"""Vertex data endpoints."""
from fastapi import APIRouter

from ..schemas.vertex import (
    VertexDeleteIn,
    VertexFetchIn,
    VertexInsertIn,
    VertexUpdateIn,
    VertexUpsertIn,
)
from ..services import vertex_service
from ..utils.response import ok

router = APIRouter(tags=["vertices"])


@router.post("/spaces/{space}/vertices", summary="Insert vertices")
def insert_vertices(space: str, body: VertexInsertIn):
    body = body.model_copy(update={"space": space})
    return ok(vertex_service.insert(body))


@router.post("/spaces/{space}/vertices/fetch", summary="Fetch vertex properties")
def fetch_vertices(space: str, body: VertexFetchIn):
    body = body.model_copy(update={"space": space})
    return ok(vertex_service.fetch(body))


@router.post("/spaces/{space}/vertices/upsert", summary="Upsert a vertex")
def upsert_vertex(space: str, body: VertexUpsertIn):
    body = body.model_copy(update={"space": space})
    return ok(vertex_service.upsert(body))


@router.post("/spaces/{space}/vertices/update", summary="Update a vertex")
def update_vertex(space: str, body: VertexUpdateIn):
    body = body.model_copy(update={"space": space})
    return ok(vertex_service.update(body))


@router.delete("/spaces/{space}/vertices", summary="Delete vertices (and their edges)")
def delete_vertices(space: str, body: VertexDeleteIn):
    body = body.model_copy(update={"space": space})
    return ok(vertex_service.delete(body))
