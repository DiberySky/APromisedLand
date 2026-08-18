"""User and role management endpoints."""
from fastapi import APIRouter

from ..schemas.user import (
    PasswordChangeIn,
    PasswordResetIn,
    RoleAssignIn,
    RoleRevokeIn,
    UserCreateIn,
    UserDeleteIn,
)
from ..services import user_service
from ..utils.response import ok

router = APIRouter(prefix="/users", tags=["users"])


@router.get("", summary="List all users")
def list_users():
    return ok(user_service.list_users())


@router.post("", summary="Create a user")
def create_user(body: UserCreateIn):
    return ok(user_service.create(body))


@router.get("/{name}", summary="Describe a user")
def describe_user(name: str):
    return ok(user_service.describe_user(name))


@router.delete("/{name}", summary="Delete a user")
def delete_user(name: str, if_exists: bool = True):
    return ok(user_service.delete(UserDeleteIn(name=name, if_exists=if_exists)))


@router.put("/{name}/password", summary="Change a user's password")
def change_password(name: str, body: PasswordChangeIn):
    body = body.model_copy(update={"name": name})
    return ok(user_service.change_password(body))


@router.post("/{name}/password/reset", summary="Reset a user's password (admin)")
def reset_password(name: str, body: PasswordResetIn):
    body = body.model_copy(update={"name": name})
    return ok(user_service.reset_password(body))


# ----------------------------------------------------------------------- #
# Roles
# ----------------------------------------------------------------------- #
@router.get("/roles/{space}", summary="Show roles in a space")
def show_roles_in_space(space: str):
    return ok(user_service.show_roles_in_space(space))


@router.get("/{name}/roles", summary="Show a user's roles across spaces")
def show_user_roles(name: str):
    return ok(user_service.show_user_roles(name))


@router.post("/roles/grant", summary="Grant a role to a user")
def grant_role(body: RoleAssignIn):
    return ok(user_service.grant_role(body))


@router.post("/roles/revoke", summary="Revoke a role from a user")
def revoke_role(body: RoleRevokeIn):
    return ok(user_service.revoke_role(body))
