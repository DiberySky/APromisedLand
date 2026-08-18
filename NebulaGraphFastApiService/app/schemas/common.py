"""Shared / common pydantic models."""
from typing import Any, Optional

from pydantic import BaseModel, Field


class MessageOut(BaseModel):
    message: str


class SpaceHeader(BaseModel):
    """A header row from ``SHOW SPACES``."""
    name: str


class EmptyOut(BaseModel):
    """Response for statements that return no rows."""
    succeeded: bool = True


class QueryResult(BaseModel):
    columns: list = Field(default_factory=list)
    rows: list = Field(default_factory=list)
    latency_us: Optional[int] = None
    space: Optional[str] = None
    comment: Optional[str] = None
    row_count: int = 0


class RawStmtIn(BaseModel):
    """Generic raw nGQL execution body."""
    statement: str = Field(..., description="One or more nGQL statements separated by `;`")
    space: Optional[str] = Field(None, description="Switch to this space before running")


class PageParams(BaseModel):
    """Lightweight limit/offset pagination params."""
    limit: int = Field(100, ge=1, le=10000)
    offset: int = Field(0, ge=0)


class HealthOut(BaseModel):
    status: str
    connected: bool
    host: str
    port: int
    pool_size: int
    spaces: int = 0


class SuccessOut(BaseModel):
    code: int = 0
    message: str = "ok"
    data: Any = None
