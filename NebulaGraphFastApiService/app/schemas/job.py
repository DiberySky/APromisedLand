"""Job management (compact / flush / stats / rebuild / stop) models."""
from typing import List, Optional

from pydantic import BaseModel, Field


class CompactIn(BaseModel):
    space: Optional[str] = Field(None, description="If omitted, compacts all spaces")
    graph: Optional[str] = Field(None, description="Optional sub-graph filter")


class FlushIn(BaseModel):
    space: Optional[str] = None
    graph: Optional[str] = None


class JobInfo(BaseModel):
    id: int
    command: Optional[str] = None
    status: Optional[str] = None
    start_time: Optional[str] = None
    stop_time: Optional[str] = None


class JobListOut(BaseModel):
    jobs: List[dict]
    total: int


class RebuildIndexIn(BaseModel):
    space: str
    name: str
