"""User / role management models."""
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


class UserCreateIn(BaseModel):
    name: str = Field(..., min_length=1, max_length=64)
    password: str = Field(..., min_length=1, max_length=256)
    if_not_exists: bool = True


class UserDeleteIn(BaseModel):
    name: str
    if_exists: bool = True


class PasswordChangeIn(BaseModel):
    name: str
    old_password: str
    new_password: str = Field(..., min_length=1, max_length=256)


class PasswordResetIn(BaseModel):
    name: str
    new_password: str = Field(..., min_length=1, max_length=256)


class RoleAssignIn(BaseModel):
    user: str
    space: Optional[str] = Field(None, description="space name; omit for GOD role")
    role: str = Field(..., description="GOD | ADMIN | DBA | USER | GUEST")

    @field_validator("role")
    @classmethod
    def _role(cls, v: str) -> str:
        v = v.strip().upper()
        if v not in {"GOD", "ADMIN", "DBA", "USER", "GUEST"}:
            raise ValueError("role must be GOD, ADMIN, DBA, USER or GUEST")
        if v == "GOD" and cls is not None:
            # GOD is global; space should not be supplied.
            pass
        return v


class RoleRevokeIn(BaseModel):
    user: str
    space: str
    role: str
