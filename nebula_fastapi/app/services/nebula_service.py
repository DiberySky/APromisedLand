"""
NebulaGraph Business Logic Service
High-level operations built on top of NebulaClient
"""
import logging
from typing import List, Dict, Any, Optional
import pandas as pd

from app.services.nebula_client import nebula_client
from app.models.schemas import (
    QueryRequest, QueryResult, VertexData, EdgeData,
    BulkInsertRequest, SpaceInfo, TagSchema, EdgeSchema
)

logger = logging.getLogger(__name__)


class NebulaService:
    """
    High-level service for NebulaGraph operations.
    Handles query building, result transformation, and business logic.
    """

    def __init__(self):
        self.client = nebula_client

    def _transform_result(self, result) -> QueryResult:
        """Transform NebulaGraph ResultSet to API response model"""
        if not result:
            return QueryResult(
                success=False,
                error_code=-1,
                error_msg="No result returned"
            )

        if not result.is_succeeded():
            return QueryResult(
                success=False,
                error_code=result.error_code(),
                error_msg=result.error_msg()
            )

        # Extract column names
        columns = result.keys()

        # Extract data rows
        data_rows = []
        for row_idx in range(result.row_size()):
            row = {}
            for col_idx, col_name in enumerate(columns):
                value = result.row_values(row_idx)[col_idx]
                row[col_name] = self._convert_value(value)
            data_rows.append(row)

        return QueryResult(
            success=True,
            error_code=0,
            latency_us=result.latency(),
            data=data_rows,
            columns=columns,
            row_count=len(data_rows),
            space_name=getattr(result, 'space_name', None)
        )

    def _convert_value(self, value) -> Any:
        """Convert NebulaGraph Value object to Python native type"""
        if value is None:
            return None

        # Handle different value types
        if hasattr(value, 'is_null') and value.is_null():
            return None

        # Try common type conversions
        try:
            if hasattr(value, 'as_string'):
                return value.as_string()
            elif hasattr(value, 'as_int'):
                return value.as_int()
            elif hasattr(value, 'as_double'):
                return value.as_double()
            elif hasattr(value, 'as_bool'):
                return value.as_bool()
            elif hasattr(value, 'as_datetime'):
                return str(value.as_datetime())
            elif hasattr(value, 'as_date'):
                return str(value.as_date())
            elif hasattr(value, 'as_time'):
                return str(value.as_time())
            elif hasattr(value, 'as_list'):
                return [self._convert_value(v) for v in value.as_list()]
            elif hasattr(value, 'as_set'):
                return list(value.as_set())
            elif hasattr(value, 'as_map'):
                return {k: self._convert_value(v) for k, v in value.as_map().items()}
            elif hasattr(value, 'as_vertex'):
                v = value.as_vertex()
                return {
                    "vid": v.get_id().as_string() if hasattr(v.get_id(), 'as_string') else str(v.get_id()),
                    "tags": [tag.tag_name for tag in v.tags]
                }
            elif hasattr(value, 'as_edge'):
                e = value.as_edge()
                return {
                    "src": e.src.as_string() if hasattr(e.src, 'as_string') else str(e.src),
                    "dst": e.dst.as_string() if hasattr(e.dst, 'as_string') else str(e.dst),
                    "type": e.name,
                    "rank": e.ranking
                }
            elif hasattr(value, 'as_path'):
                return str(value.as_path())
            else:
                return str(value)
        except Exception as e:
            logger.warning(f"Value conversion error: {e}")
            return str(value)

    def query(self, request: QueryRequest) -> QueryResult:
        """
        Execute a graph query.

        Args:
            request: QueryRequest with nGQL and optional parameters

        Returns:
            QueryResult: Structured query result
        """
        try:
            result = self.client.execute(
                query=request.query,
                space=request.space,
                params=request.params
            )
            return self._transform_result(result)
        except Exception as e:
            logger.error(f"Query execution error: {e}")
            return QueryResult(
                success=False,
                error_code=-1,
                error_msg=str(e)
            )

    def query_to_dataframe(self, request: QueryRequest) -> pd.DataFrame:
        """
        Execute query and return as pandas DataFrame.

        Args:
            request: QueryRequest

        Returns:
            pd.DataFrame: Query results as DataFrame
        """
        result = self.client.execute(
            query=request.query,
            space=request.space,
            params=request.params
        )

        if result and result.is_succeeded():
            return result.as_data_frame()
        else:
            error_msg = result.error_msg() if result else "No result"
            raise RuntimeError(f"Query failed: {error_msg}")

    def get_spaces(self) -> List[SpaceInfo]:
        """List all graph spaces"""
        result = self.client.execute(query="SHOW SPACES")

        spaces = []
        if result and result.is_succeeded():
            for row_idx in range(result.row_size()):
                row_values = result.row_values(row_idx)
                if row_values:
                    space_name = row_values[0].as_string() if hasattr(row_values[0], 'as_string') else str(row_values[0])
                    spaces.append(SpaceInfo(name=space_name))

        return spaces

    def get_space_detail(self, space_name: str) -> Optional[SpaceInfo]:
        """Get detailed information about a graph space"""
        result = self.client.execute(query=f"DESC SPACE `{space_name}`")

        if result and result.is_succeeded() and result.row_size() > 0:
            row = result.row_values(0)
            return SpaceInfo(
                name=space_name,
                vid_type=str(row[1]) if len(row) > 1 else None,
                partition_num=int(row[2]) if len(row) > 2 else None,
                replica_factor=int(row[3]) if len(row) > 3 else None
            )
        return None

    def get_tags(self, space: Optional[str] = None) -> List[TagSchema]:
        """List all tags in a space"""
        result = self.client.execute(query="SHOW TAGS", space=space)

        tags = []
        if result and result.is_succeeded():
            for row_idx in range(result.row_size()):
                row_values = result.row_values(row_idx)
                if row_values:
                    tag_name = str(row_values[0])
                    tags.append(TagSchema(name=tag_name))

        return tags

    def get_tag_schema(self, tag_name: str, space: Optional[str] = None) -> Optional[TagSchema]:
        """Get tag schema definition"""
        result = self.client.execute(
            query=f"DESC TAG `{tag_name}`",
            space=space
        )

        if result and result.is_succeeded():
            properties = []
            for row_idx in range(result.row_size()):
                row = result.row_values(row_idx)
                if len(row) >= 2:
                    properties.append({
                        "field": str(row[0]),
                        "type": str(row[1]),
                        "null": str(row[2]) if len(row) > 2 else "YES",
                        "default": str(row[3]) if len(row) > 3 else "",
                        "comment": str(row[4]) if len(row) > 4 else ""
                    })

            return TagSchema(name=tag_name, properties=properties)
        return None

    def get_edge_types(self, space: Optional[str] = None) -> List[EdgeSchema]:
        """List all edge types in a space"""
        result = self.client.execute(query="SHOW EDGES", space=space)

        edges = []
        if result and result.is_succeeded():
            for row_idx in range(result.row_size()):
                row_values = result.row_values(row_idx)
                if row_values:
                    edge_name = str(row_values[0])
                    edges.append(EdgeSchema(name=edge_name))

        return edges

    def get_edge_schema(self, edge_name: str, space: Optional[str] = None) -> Optional[EdgeSchema]:
        """Get edge type schema definition"""
        result = self.client.execute(
            query=f"DESC EDGE `{edge_name}`",
            space=space
        )

        if result and result.is_succeeded():
            properties = []
            for row_idx in range(result.row_size()):
                row = result.row_values(row_idx)
                if len(row) >= 2:
                    properties.append({
                        "field": str(row[0]),
                        "type": str(row[1]),
                        "null": str(row[2]) if len(row) > 2 else "YES",
                        "default": str(row[3]) if len(row) > 3 else "",
                        "comment": str(row[4]) if len(row) > 4 else ""
                    })

            return EdgeSchema(name=edge_name, properties=properties)
        return None

    def insert_vertex(self, vertex: VertexData, space: Optional[str] = None) -> QueryResult:
        """Insert a single vertex"""
        if not vertex.tags or not vertex.properties:
            return QueryResult(
                success=False,
                error_msg="Tags and properties are required for vertex insertion"
            )

        # Build INSERT VERTEX statement
        tag_name = vertex.tags[0]  # Support single tag for simplicity
        props = vertex.properties

        prop_names = ", ".join([f"`{k}`" for k in props.keys()])
        prop_values = ", ".join([self._format_value(v) for v in props.values()])

        query = f'INSERT VERTEX `{tag_name}` ({prop_names}) VALUES "{vertex.vid}":({prop_values})'

        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def insert_edge(self, edge: EdgeData, space: Optional[str] = None) -> QueryResult:
        """Insert a single edge"""
        if not edge.properties:
            return QueryResult(
                success=False,
                error_msg="Properties are required for edge insertion"
            )

        prop_names = ", ".join([f"`{k}`" for k in edge.properties.keys()])
        prop_values = ", ".join([self._format_value(v) for v in edge.properties.values()])

        rank_clause = f"@{edge.rank}" if edge.rank else ""

        query = (
            f'INSERT EDGE `{edge.edge_type}` ({prop_names}) '
            f'VALUES "{edge.src_vid}" -> "{edge.dst_vid}"{rank_clause}:({prop_values})'
        )

        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def bulk_insert(self, request: BulkInsertRequest) -> Dict[str, Any]:
        """
        Bulk insert vertices and edges.

        Args:
            request: BulkInsertRequest with vertices and/or edges

        Returns:
            Dict with success count and errors
        """
        results = {
            "vertices_total": len(request.vertices) if request.vertices else 0,
            "vertices_success": 0,
            "vertices_errors": [],
            "edges_total": len(request.edges) if request.edges else 0,
            "edges_success": 0,
            "edges_errors": []
        }

        # Insert vertices in batches
        if request.vertices:
            for i in range(0, len(request.vertices), request.batch_size):
                batch = request.vertices[i:i + request.batch_size]
                for vertex in batch:
                    try:
                        res = self.insert_vertex(vertex, request.space)
                        if res.success:
                            results["vertices_success"] += 1
                        else:
                            results["vertices_errors"].append({
                                "vid": vertex.vid,
                                "error": res.error_msg
                            })
                    except Exception as e:
                        results["vertices_errors"].append({
                            "vid": vertex.vid,
                            "error": str(e)
                        })

        # Insert edges in batches
        if request.edges:
            for i in range(0, len(request.edges), request.batch_size):
                batch = request.edges[i:i + request.batch_size]
                for edge in batch:
                    try:
                        res = self.insert_edge(edge, request.space)
                        if res.success:
                            results["edges_success"] += 1
                        else:
                            results["edges_errors"].append({
                                "src": edge.src_vid,
                                "dst": edge.dst_vid,
                                "error": res.error_msg
                            })
                    except Exception as e:
                        results["edges_errors"].append({
                            "src": edge.src_vid,
                            "dst": edge.dst_vid,
                            "error": str(e)
                        })

        return results

    def _format_value(self, value: Any) -> str:
        """Format Python value for nGQL statement"""
        if value is None:
            return "NULL"
        elif isinstance(value, bool):
            return "true" if value else "false"
        elif isinstance(value, (int, float)):
            return str(value)
        elif isinstance(value, str):
            # Escape quotes
            escaped = value.replace('"', '\"')
            return f'"{escaped}"'
        elif isinstance(value, list):
            items = ", ".join([self._format_value(v) for v in value])
            return f"[{items}]"
        elif isinstance(value, dict):
            items = ", ".join([f"{k}: {self._format_value(v)}" for k, v in value.items()])
            return f"{{{items}}}"
        else:
            return f'"{str(value)}"'


# Global service instance
nebula_service = NebulaService()
