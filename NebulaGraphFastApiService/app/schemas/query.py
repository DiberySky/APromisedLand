"""Query (GO / FETCH / LOOKUP / FIND PATH / GET SUBGRAPH / raw nGQL) models."""
from typing import List, Optional, Union

from pydantic import BaseModel, ConfigDict, Field


class GoIn(BaseModel):
    """GO N STEPS OVER edge FROM vid [WHERE|YIELD]."""
    model_config = ConfigDict(populate_by_name=True)
    space: str
    steps: int = Field(1, ge=1, le=100)
    edge: str
    from_vid: Union[str, int]
    direction: str = Field("BIDIRECT", description="BIDIRECT | OUT | IN")
    where: Optional[str] = None
    yield_: Optional[str] = Field(None, alias="yield", description="YIELD expression")
    limit: Optional[int] = Field(None, ge=1, le=100000)
    sample: Optional[int] = Field(None, ge=1, le=100000)

    @staticmethod
    def _norm_direction(d: str) -> str:
        d = d.strip().upper()
        if d not in {"BIDIRECT", "OUT", "IN"}:
            raise ValueError("direction must be BIDIRECT, OUT or IN")
        return d


class FetchVertexIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    space: str
    vids: List[Union[str, int]] = Field(..., min_length=1)
    tag: Optional[str] = None
    yield_: Optional[str] = Field(None, alias="yield")


class FetchEdgeIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    space: str
    edge: str
    pairs: List[dict] = Field(..., min_length=1)
    yield_: Optional[str] = Field(None, alias="yield")


class LookupIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    space: str
    kind: str = Field(..., description='"tag" or "edge"')
    name: str
    where: Optional[str] = None
    yield_: Optional[str] = Field(None, alias="yield")
    limit: Optional[int] = Field(None, ge=1, le=100000)


class FindPathIn(BaseModel):
    space: str
    src: Union[str, int]
    dst: Union[str, int]
    edge: Optional[str] = None
    steps: int = Field(5, ge=1, le=100)
    direction: str = "BIDIRECT"
    single_shortest: bool = Field(False, description="use SHORTEST instead of ALL")
    with_prop: bool = True
    no_loop: bool = False


class GetSubgraphIn(BaseModel):
    space: str
    vid: Union[str, int]
    steps: int = Field(1, ge=1, le=100)
    in_edges: Optional[List[str]] = None
    out_edges: Optional[List[str]] = None
    both_edges: Optional[List[str]] = None
    with_prop: bool = True
