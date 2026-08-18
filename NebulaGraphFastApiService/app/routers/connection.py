"""Connection / health endpoints."""
from fastapi import APIRouter

from ..services import connection_service
from ..utils.response import ok

router = APIRouter(prefix="/connection", tags=["connection"])


@router.get("/health", response_model=None, summary="NebulaGraph connectivity check")
def health():
    status = connection_service.status()
    return ok(status)


@router.get("/spaces", summary="List all graph spaces")
def list_spaces():
    spaces = connection_service.show_spaces()
    return ok({"spaces": spaces, "total": len(spaces)})
