"""Graph space related request/response models."""
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


class SpaceCreateIn(BaseModel):
    name: str = Field(..., min_length=1, max_length=128)
    partition_num: int = Field(100, ge=1, le=65535)
    replica_factor: int = Field(1, ge=1, le=9)
    vid_type: str = Field("FIXED_STRING(8)", description="e.g. FIXED_STRING(16) or INT64")
    comment: Optional[str] = Field(None, max_length=256)
    if_not_exists: bool = True

    @field_validator("vid_type")
    @classmethod
    def _check_vid_type(cls, v: str) -> str:
        v = v.strip().upper()
        if v.startswith("FIXED_STRING(") or v == "INT64":
            return v
        raise ValueError("vid_type must be FIXED_STRING(n) or INT64")


class SpaceAlterCommentIn(BaseModel):
    comment: str = Field(..., min_length=0, max_length=256)


class SpaceInfo(BaseModel):
    name: str
    partition_num: Optional[int] = None
    replica_factor: Optional[int] = None
    charset: Optional[str] = None
    collate: Optional[str] = None
    vid_type: Optional[str] = None
    comment: Optional[str] = None


class SpaceListOut(BaseModel):
    spaces: List[str]
    total: int
