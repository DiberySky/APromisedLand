"""Graph space management endpoints."""
from fastapi import APIRouter

from ..schemas.space import SpaceAlterCommentIn, SpaceCreateIn
from ..services import space_service
from ..utils.response import ok

router = APIRouter(prefix="/spaces", tags=["spaces"])


@router.get("", summary="List all graph spaces")
def list_spaces():
    names = space_service.list_spaces()
    return ok({"spaces": names, "total": len(names)})


@router.post("", summary="Create a graph space")
def create_space(body: SpaceCreateIn):
    return ok(space_service.create_space(body))


@router.get("/{name}", summary="Describe a graph space")
def desc_space(name: str):
    return ok(space_service.desc_space(name))


@router.get("/{name}/ddl", summary="Show CREATE SPACE statement")
def show_create_space(name: str):
    return ok({"ddl": space_service.show_create_space(name)})


@router.put("/{name}/comment", summary="Alter a graph space comment")
def alter_comment(name: str, body: SpaceAlterCommentIn):
    return ok(space_service.alter_space_comment(name, body.comment))


@router.delete("/{name}", summary="Drop a graph space")
def drop_space(name: str, if_exists: bool = True):
    return ok(space_service.drop_space(name, if_exists=if_exists))
