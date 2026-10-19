"""
Pydantic schemas for request/response validation
"""
from pydantic import BaseModel, Field, ConfigDict
from typing import List, Dict, Any, Optional, Union
from enum import Enum


class QueryType(str, Enum):
    """Supported query types"""
    NATIVE = "native"      # Raw nGQL
    MATCH = "match"        # MATCH statement
    GO = "go"              # GO statement
    FETCH = "fetch"        # FETCH statement
    LOOKUP = "lookup"      # LOOKUP statement
    INSERT = "insert"      # INSERT statement
    UPDATE = "update"      # UPDATE statement
    DELETE = "delete"      # DELETE statement


class VertexData(BaseModel):
    """Vertex data model"""
    model_config = ConfigDict(extra="allow")

    vid: str = Field(..., description="Vertex ID")
    tags: Optional[List[str]] = Field(default=None, description="Vertex tags")
    properties: Optional[Dict[str, Any]] = Field(default=None, description="Vertex properties")


class EdgeData(BaseModel):
    """Edge data model"""
    model_config = ConfigDict(extra="allow")

    src_vid: str = Field(..., description="Source vertex ID")
    dst_vid: str = Field(..., description="Destination vertex ID")
    edge_type: str = Field(..., description="Edge type name")
    rank: Optional[int] = Field(default=0, description="Edge rank")
    properties: Optional[Dict[str, Any]] = Field(default=None, description="Edge properties")


class QueryRequest(BaseModel):
    """Graph query request"""
    model_config = ConfigDict(extra="allow")

    space: Optional[str] = Field(default=None, description="Graph space name (overrides default)")
    query: str = Field(..., min_length=1, description="nGQL query string")
    params: Optional[Dict[str, Any]] = Field(default=None, description="Query parameters")
    timeout: Optional[int] = Field(default=None, description="Query timeout in milliseconds")


class BulkInsertRequest(BaseModel):
    """Bulk insert request"""
    model_config = ConfigDict(extra="allow")

    space: Optional[str] = Field(default=None, description="Graph space name")
    vertices: Optional[List[VertexData]] = Field(default=None, description="Vertices to insert")
    edges: Optional[List[EdgeData]] = Field(default=None, description="Edges to insert")
    batch_size: int = Field(default=100, ge=1, le=1000, description="Batch size for insertion")


class SchemaRequest(BaseModel):
    """Schema management request"""
    model_config = ConfigDict(extra="allow")

    space: Optional[str] = Field(default=None, description="Graph space name")
    action: str = Field(..., description="Schema action: create_tag, create_edge, alter_tag, etc.")
    name: str = Field(..., description="Tag or edge type name")
    properties: Optional[List[Dict[str, Any]]] = Field(default=None, description="Property definitions")
    ttl: Optional[Dict[str, Any]] = Field(default=None, description="TTL configuration")


class QueryResult(BaseModel):
    """Query execution result"""
    model_config = ConfigDict(extra="allow")

    success: bool = Field(..., description="Whether query succeeded")
    error_code: int = Field(default=0, description="Error code")
    error_msg: Optional[str] = Field(default=None, description="Error message")
    latency_us: Optional[int] = Field(default=None, description="Query latency in microseconds")
    data: Optional[List[Dict[str, Any]]] = Field(default=None, description="Result data rows")
    columns: Optional[List[str]] = Field(default=None, description="Result column names")
    row_count: int = Field(default=0, description="Number of rows returned")
    space_name: Optional[str] = Field(default=None, description="Space used for query")


class HealthResponse(BaseModel):
    """Health check response"""
    model_config = ConfigDict(extra="allow")

    status: str = Field(..., description="Service status")
    version: str = Field(..., description="API version")
    nebula_connected: bool = Field(..., description="NebulaGraph connection status")
    nebula_hosts: List[str] = Field(default=[], description="Connected NebulaGraph hosts")
    timestamp: str = Field(..., description="Current timestamp")


class SpaceInfo(BaseModel):
    """Graph space information"""
    model_config = ConfigDict(extra="allow")

    name: str = Field(..., description="Space name")
    vid_type: Optional[str] = Field(default=None, description="Vertex ID type")
    partition_num: Optional[int] = Field(default=None, description="Number of partitions")
    replica_factor: Optional[int] = Field(default=None, description="Replica factor")


class TagSchema(BaseModel):
    """Tag schema information"""
    model_config = ConfigDict(extra="allow")

    name: str = Field(..., description="Tag name")
    properties: List[Dict[str, Any]] = Field(default=[], description="Property definitions")


class EdgeSchema(BaseModel):
    """Edge type schema information"""
    model_config = ConfigDict(extra="allow")

    name: str = Field(..., description="Edge type name")
    properties: List[Dict[str, Any]] = Field(default=[], description="Property definitions")
