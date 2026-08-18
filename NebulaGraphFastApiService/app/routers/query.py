"""Query endpoints: raw nGQL, GO, LOOKUP, FIND PATH, GET SUBGRAPH."""
from fastapi import APIRouter

from ..schemas.common import RawStmtIn
from ..schemas.query import (
    FindPathIn,
    GetSubgraphIn,
    GoIn,
    LookupIn,
)
from ..services import query_service
from ..utils.response import ok

router = APIRouter(prefix="/query", tags=["query"])


@router.post("/ngql", summary="Execute raw nGQL and return parsed rows")
def raw_ngql(body: RawStmtIn):
    return ok(query_service.raw(body.statement, body.space))


@router.post("/explain", summary="EXPLAIN a statement (return plan)")
def explain(body: RawStmtIn):
    return ok(query_service.explain(body.statement, body.space))


@router.post("/profile", summary="PROFILE a statement (return plan + latency)")
def profile(body: RawStmtIn):
    return ok(query_service.profile(body.statement, body.space))


@router.post("/go", summary="Run a GO traversal")
def go(body: GoIn):
    return ok(query_service.go(body))


@router.post("/lookup", summary="Run a LOOKUP (index-based search)")
def lookup(body: LookupIn):
    return ok(query_service.lookup(body))


@router.post("/find-path", summary="Find paths between two vertices")
def find_path(body: FindPathIn):
    return ok(query_service.find_path(body))


@router.post("/subgraph", summary="Get a subgraph around a vertex")
def get_subgraph(body: GetSubgraphIn):
    return ok(query_service.get_subgraph(body))


@router.get("/stats/{space}", summary="Show space statistics")
def show_stats(space: str):
    return ok(query_service.show_stats(space))
