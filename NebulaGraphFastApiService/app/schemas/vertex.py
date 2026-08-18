"""Vertex (point) request/response models."""
from typing import Any, Dict, List, Optional, Union

from pydantic import BaseModel, Field


class VertexInsertItem(BaseModel):
    vid: Union[str, int] = Field(..., description="Vertex id; string or int64")
    tags: Dict[str, Dict[str, Any]] = Field(
        ..., description="Map of tag name -> property dict"
    )


class VertexInsertIn(BaseModel):
    space: str
    vertices: List[VertexInsertItem] = Field(..., min_length=1)
    if_not_exists: bool = True


class VertexFetchIn(BaseModel):
    space: str
    vids: List[Union[str, int]] = Field(..., min_length=1)
    tag: Optional[str] = Field(
        None, description="Filter properties to a specific tag; "
                          "omit to fetch with all tags"
    )
    prop: Optional[str] = None


class VertexDeleteIn(BaseModel):
    space: str
    vids: List[Union[str, int]] = Field(..., min_length=1)


class VertexUpsertIn(BaseModel):
    """UPSERT VERTEX - update or insert based on a WHEN condition."""
    space: str
    vid: Union[str, int]
    tag: str
    set: Dict[str, str] = Field(..., description="nGQL SET expressions, e.g. {'age': 'age+1'}")
    when: Optional[str] = Field(None, description="WHEN condition expression")
    if_not_exists: bool = False
    update: bool = True


class VertexUpdateIn(BaseModel):
    """UPDATE VERTEX ON tag vid SET ..."""
    space: str
    vid: Union[str, int]
    tag: str
    set: Dict[str, str]
    when: Optional[str] = None
