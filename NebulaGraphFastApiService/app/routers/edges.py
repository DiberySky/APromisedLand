"""Edge data endpoints."""
from fastapi import APIRouter

from ..schemas.edge import (
    EdgeDeleteIn,
    EdgeFetchIn,
    EdgeInsertIn,
    EdgeUpdateIn,
    EdgeUpsertIn,
)
from ..services import edge_service
from ..utils.response import ok

router = APIRouter(tags=["edges"])


@router.post("/spaces/{space}/edges", summary="Insert edges")
def insert_edges(space: str, body: EdgeInsertIn):
    body = body.model_copy(update={"space": space})
    return ok(edge_service.insert(body))


@router.post("/spaces/{space}/edges/fetch", summary="Fetch edge properties")
def fetch_edges(space: str, body: EdgeFetchIn):
    body = body.model_copy(update={"space": space})
    return ok(edge_service.fetch(body))


@router.post("/spaces/{space}/edges/upsert", summary="Upsert an edge")
def upsert_edge(space: str, body: EdgeUpsertIn):
    body = body.model_copy(update={"space": space})
    return ok(edge_service.upsert(body))


@router.post("/spaces/{space}/edges/update", summary="Update an edge")
def update_edge(space: str, body: EdgeUpdateIn):
    body = body.model_copy(update={"space": space})
    return ok(edge_service.update(body))


@router.delete("/spaces/{space}/edges", summary="Delete edges")
def delete_edges(space: str, body: EdgeDeleteIn):
    body = body.model_copy(update={"space": space})
    return ok(edge_service.delete(body))
