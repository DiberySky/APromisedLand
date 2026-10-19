"""
Graph API Router - Core CRUD and Query Endpoints
"""
from fastapi import APIRouter, Depends, HTTPException, status, Query
from typing import List, Optional, Dict, Any

from app.dependencies import get_nebula_service, get_nebula_client
from app.models.schemas import (
    QueryRequest, QueryResult, BulkInsertRequest,
    VertexData, EdgeData, SpaceInfo, TagSchema, EdgeSchema
)
from app.services.nebula_service import nebula_service

router = APIRouter(prefix="/graph", tags=["Graph Operations"])


@router.post("/query", response_model=QueryResult, summary="Execute nGQL Query")
async def execute_query(
    request: QueryRequest,
    service=Depends(get_nebula_service)
):
    """
    Execute a native nGQL query against NebulaGraph.

    Supports all nGQL statements: MATCH, GO, FETCH, LOOKUP, INSERT, etc.

    Example request:
    ```json
    {
        "space": "basketballplayer",
        "query": "MATCH (v:player) RETURN v.player.name AS name, v.player.age AS age LIMIT 10"
    }
    ```
    """
    return service.query(request)


@router.post("/query/json", response_model=Dict[str, Any], summary="Execute Query (JSON Response)")
async def execute_query_json(
    request: QueryRequest,
    service=Depends(get_nebula_service)
):
    """
    Execute query and return raw JSON response from NebulaGraph.
    """
    from app.services.nebula_client import nebula_client
    return nebula_client.execute_json(
        query=request.query,
        space=request.space
    )


@router.get("/spaces", response_model=List[SpaceInfo], summary="List Graph Spaces")
async def list_spaces(service=Depends(get_nebula_service)):
    """Get all graph spaces in the NebulaGraph cluster"""
    return service.get_spaces()


@router.get("/spaces/{space_name}", response_model=SpaceInfo, summary="Get Space Detail")
async def get_space_detail(
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
async def list_tags(
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """List all tags (vertex types) in the specified space"""
    return service.get_tags(space)


@router.get("/tags/{tag_name}", response_model=TagSchema, summary="Get Tag Schema")
async def get_tag_schema(
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
async def list_edges(
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """List all edge types in the specified space"""
    return service.get_edge_types(space)


@router.get("/edges/{edge_name}", response_model=EdgeSchema, summary="Get Edge Schema")
async def get_edge_schema(
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
async def insert_vertex(
    vertex: VertexData,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """
    Insert a single vertex into the graph.

    Example:
    ```json
    {
        "vid": "player100",
        "tags": ["player"],
        "properties": {
            "name": "Tim Duncan",
            "age": 42
        }
    }
    ```
    """
    return service.insert_vertex(vertex, space)


@router.post("/edges", response_model=QueryResult, summary="Insert Edge")
async def insert_edge(
    edge: EdgeData,
    space: Optional[str] = Query(None, description="Graph space name"),
    service=Depends(get_nebula_service)
):
    """
    Insert a single edge into the graph.

    Example:
    ```json
    {
        "src_vid": "player100",
        "dst_vid": "team200",
        "edge_type": "serve",
        "rank": 0,
        "properties": {
            "start_year": 1997,
            "end_year": 2016
        }
    }
    ```
    """
    return service.insert_edge(edge, space)


@router.post("/bulk", response_model=Dict[str, Any], summary="Bulk Insert")
async def bulk_insert(
    request: BulkInsertRequest,
    service=Depends(get_nebula_service)
):
    """
    Bulk insert vertices and/or edges in batches.

    Efficient for large data imports with configurable batch size.
    """
    return service.bulk_insert(request)
