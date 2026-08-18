"""Edge (relationship) request/response models."""
from typing import Any, Dict, List, Optional, Union

from pydantic import BaseModel, Field


class EdgeInsertItem(BaseModel):
    src: Union[str, int]
    dst: Union[str, int]
    ranking: int = 0
    props: Dict[str, Any] = Field(default_factory=dict)


class EdgeInsertIn(BaseModel):
    space: str
    edge: str
    edges: List[EdgeInsertItem] = Field(..., min_length=1)
    if_not_exists: bool = True


class EdgeFetchIn(BaseModel):
    space: str
    edge: str
    pairs: List[Dict[str, Union[str, int]]] = Field(
        ..., min_length=1,
        description="List of {'src':..,'dst':..,'ranking':0} objects"
    )
    prop: Optional[str] = None


class EdgeDeleteIn(BaseModel):
    space: str
    edge: str
    pairs: List[Dict[str, Union[str, int]]] = Field(..., min_length=1)


class EdgeUpdateIn(BaseModel):
    space: str
    edge: str
    src: Union[str, int]
    dst: Union[str, int]
    ranking: int = 0
    set: Dict[str, str]
    when: Optional[str] = None


class EdgeUpsertIn(BaseModel):
    space: str
    edge: str
    src: Union[str, int]
    dst: Union[str, int]
    ranking: int = 0
    set: Dict[str, str]
    when: Optional[str] = None
    if_not_exists: bool = False
    update: bool = True
