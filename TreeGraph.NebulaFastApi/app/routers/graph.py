"""
Graph API Router - Core CRUD and Query Endpoints

注：nebula3-python 的 execute 为同步阻塞调用，
路由统一使用同步 def，由 FastAPI 放入线程池执行，避免阻塞事件循环。
"""
from fastapi import APIRouter, Depends, HTTPException, status, Query
from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field

from app.dependencies import get_nebula_service, get_nebula_client
from app.models.schemas import (
    QueryRequest, QueryResult, BulkInsertRequest,
    VertexData, EdgeData, SpaceInfo, TagSchema, EdgeSchema
)

router = APIRouter(prefix="/graph", tags=["Graph Operations"])


class DeleteEdgeRequest(BaseModel):
    """删除边请求"""
    edge_type: str = Field(..., description="Edge type name")
    src_vid: str = Field(..., description="Source vertex ID")
    dst_vid: str = Field(..., description="Destination vertex ID")
    rank: int = Field(default=0, description="Edge rank")
    space: Optional[str] = Field(default=None, description="Graph space name")


@router.post("/query", response_model=QueryResult, summary="Execute nGQL Query")
def execute_query(
    request: QueryRequest,
    service=Depends(get_nebula_service)
):
    """
    Execute a native nGQL query against NebulaGraph.

    Supports all nGQL statements: MATCH, GO, FETCH, LOOKUP, INSERT, etc.
    """
    return service.query(request)


@router.post("/query/json", response_model=Dict[str, Any], summary="Execute Query (JSON Response)")
def execute_query_json(
    request: QueryRequest,
    client=Depends(get_nebula_client)
):
    """
    Execute query and return raw JSON response from NebulaGraph.
    """
    return client.execute_json(
        query=request.query,
        space=request.space
    )


@router.get("/spaces", response_model=List[SpaceInfo], summary="List Graph Spaces")
def list_spaces(service=Depends(get_nebula_service)):
    """Get all graph spaces in the NebulaGraph cluster"""
    return service.get_spaces()


@router.get("/spaces/{space_name}", response_model=SpaceInfo, summary="Get Space Detail")
def get_space_detail(
    space_name: str,
    service=Depends(get_nebula_service)
):
    """Get detailed information about a specific graph space"""
    space = service.get_space_detail(space_name)
    if not space:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Space '{space_name}' not found"
        )
    return space


@router.get("/tags", response_model=List[TagSchema], summary="List Tags")
def list_tags(
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """List all tags (vertex types) in the specified space"""
    return service.get_tags(space)


@router.get("/tags/{tag_name}", response_model=TagSchema, summary="Get Tag Schema")
def get_tag_schema(
    tag_name: str,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """Get schema definition for a specific tag"""
    schema = service.get_tag_schema(tag_name, space)
    if not schema:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Tag '{tag_name}' not found"
        )
    return schema


@router.get("/edges", response_model=List[EdgeSchema], summary="List Edge Types")
def list_edges(
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """List all edge types in the specified space"""
    return service.get_edge_types(space)


@router.get("/edges/{edge_name}", response_model=EdgeSchema, summary="Get Edge Schema")
def get_edge_schema(
    edge_name: str,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """Get schema definition for a specific edge type"""
    schema = service.get_edge_schema(edge_name, space)
    if not schema:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Edge type '{edge_name}' not found"
        )
    return schema


@router.post("/vertices", response_model=QueryResult, summary="Insert Vertex")
def insert_vertex(
    vertex: VertexData,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """
    Insert a single vertex into the graph.
    INSERT 对已存在 vid 为覆盖语义（last-write-wins），可安全用于幂等同步。
    """
    return service.insert_vertex(vertex, space)


@router.delete("/vertices/{vid}", response_model=QueryResult, summary="Delete Vertex")
def delete_vertex(
    vid: str,
    space: Optional[str] = Query(None, description="Graph space name"),
    with_edge: bool = Query(True, description="同时删除关联边（WITH EDGE）"),
    service=Depends(get_nebula_service)
):
    """删除顶点，默认连带删除其所有关联边。"""
    return service.delete_vertex(vid, space, with_edge)


@router.post("/edges", response_model=QueryResult, summary="Insert Edge")
def insert_edge(
    edge: EdgeData,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """
    Insert a single edge into the graph.
    INSERT 对同 src/dst/rank 的边为覆盖语义，可安全用于幂等同步。
    """
    return service.insert_edge(edge, space)


@router.delete("/edges", response_model=QueryResult, summary="Delete Edge")
def delete_edge(
    request: DeleteEdgeRequest,
    service=Depends(get_nebula_service)
):
    """删除指定类型、起点、终点、rank 的边。"""
    return service.delete_edge(
        edge_type=request.edge_type,
        src_vid=request.src_vid,
        dst_vid=request.dst_vid,
        rank=request.rank,
        space=request.space
    )


@router.post("/bulk", response_model=Dict[str, Any], summary="Bulk Insert")
def bulk_insert(
    request: BulkInsertRequest,
    service=Depends(get_nebula_service)
):
    """
    Bulk insert vertices and/or edges in batches.
    同签名数据拼接为单条多值 nGQL，每批一次网络往返。
    """
    return service.bulk_insert(request)
