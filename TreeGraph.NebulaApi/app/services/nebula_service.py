"""
NebulaGraph Business Logic Service
High-level operations built on top of NebulaClient
"""
import logging
import re
from typing import List, Dict, Any, Optional, Tuple

from app.services.nebula_client import nebula_client
from app.models.schemas import (
    QueryRequest, QueryResult, VertexData, EdgeData,
    BulkInsertRequest, SpaceInfo, TagSchema, EdgeSchema
)

logger = logging.getLogger(__name__)

# 合法标识符：字母/数字/下划线，字母或下划线开头（nGQL 反引号标识符的保守子集）
_IDENTIFIER_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def _ident(name: str) -> str:
    """校验并包装反引号标识符（tag/edge type/属性名），拒绝非法字符防注入。"""
    if not _IDENTIFIER_RE.match(name):
        raise ValueError(f"非法 nGQL 标识符: {name!r}")
    return f"`{name}`"


def _quote(value: str) -> str:
    """转义 nGQL 双引号字符串字面量：先反斜杠后双引号。"""
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


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
        result = self.client.execute(query=f"DESC SPACE {_ident(space_name)}")

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
            query=f"DESC TAG {_ident(tag_name)}",
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
            query=f"DESC EDGE {_ident(edge_name)}",
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
        """
        Insert（覆盖语义，幂等）单个顶点。支持多 tag、空属性。
        nGQL: INSERT VERTEX t1(a,b), t2() VALUES "vid":(v1,v2),()
        """
        if not vertex.tags:
            return QueryResult(
                success=False,
                error_code=-1,
                error_msg="tags 不能为空"
            )

        props = vertex.properties or {}
        # nGQL 多 tag 语法：每个 tag 单独列属性，VALUES 中每个 tag 一组值
        # INSERT VERTEX t1(a,b), t2(a,b) VALUES "vid":(v1,v2),(v1,v2)
        prop_tuple = f"({', '.join(self._format_value(v) for v in props.values())})"
        if props:
            prop_names = ", ".join(_ident(k) for k in props.keys())
            tag_list = ", ".join(f"{_ident(t)} ({prop_names})" for t in vertex.tags)
        else:
            tag_list = ", ".join(f"{_ident(t)} ()" for t in vertex.tags)
        value_tuples = ", ".join(prop_tuple for _ in vertex.tags)
        query = f"INSERT VERTEX {tag_list} VALUES {_quote(vertex.vid)}:{value_tuples}"

        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def insert_edge(self, edge: EdgeData, space: Optional[str] = None) -> QueryResult:
        """Insert（覆盖语义，幂等）单条边。允许空属性。"""
        props = edge.properties or {}
        rank = edge.rank or 0

        if props:
            prop_names = ", ".join(_ident(k) for k in props.keys())
            prop_values = ", ".join(self._format_value(v) for v in props.values())
            query = (
                f"INSERT EDGE {_ident(edge.edge_type)} ({prop_names}) "
                f"VALUES {_quote(edge.src_vid)} -> {_quote(edge.dst_vid)}@{rank}:({prop_values})"
            )
        else:
            query = (
                f"INSERT EDGE {_ident(edge.edge_type)} () "
                f"VALUES {_quote(edge.src_vid)} -> {_quote(edge.dst_vid)}@{rank}:()"
            )

        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def delete_vertex(
        self, vid: str, space: Optional[str] = None, with_edge: bool = True
    ) -> QueryResult:
        """删除顶点，默认连带删除其关联边（WITH EDGE）。"""
        suffix = " WITH EDGE" if with_edge else ""
        query = f"DELETE VERTEX {_quote(vid)}{suffix}"
        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def delete_edge(
        self,
        edge_type: str,
        src_vid: str,
        dst_vid: str,
        rank: int = 0,
        space: Optional[str] = None
    ) -> QueryResult:
        """删除指定类型、起点、终点、rank 的边。"""
        query = (
            f"DELETE EDGE {_ident(edge_type)} "
            f"{_quote(src_vid)} -> {_quote(dst_vid)}@{rank}"
        )
        result = self.client.execute(query=query, space=space)
        return self._transform_result(result)

    def bulk_insert(self, request: BulkInsertRequest) -> Dict[str, Any]:
        """
        批量写入：按签名分组后拼接单条多值 nGQL（每批一次网络往返）。
        顶点按 (tags, 属性键集合) 分组；边按 (edge_type, rank, 属性键集合) 分组。
        """
        results: Dict[str, Any] = {
            "vertices_total": len(request.vertices) if request.vertices else 0,
            "vertices_success": 0,
            "vertices_errors": [],
            "edges_total": len(request.edges) if request.edges else 0,
            "edges_success": 0,
            "edges_errors": []
        }

        if request.vertices:
            for key, group in self._group_vertices(request.vertices).items():
                for batch in self._chunks(group, request.batch_size):
                    query = self._build_vertex_batch(key, batch)
                    res = self._transform_result(
                        self.client.execute(query=query, space=request.space))
                    if res.success:
                        results["vertices_success"] += len(batch)
                    else:
                        results["vertices_errors"].append({
                            "vids": [v.vid for v in batch],
                            "error": res.error_msg
                        })

        if request.edges:
            for key, group in self._group_edges(request.edges).items():
                for batch in self._chunks(group, request.batch_size):
                    query = self._build_edge_batch(key, batch)
                    res = self._transform_result(
                        self.client.execute(query=query, space=request.space))
                    if res.success:
                        results["edges_success"] += len(batch)
                    else:
                        results["edges_errors"].append({
                            "edges": [f"{e.src_vid}->{e.dst_vid}" for e in batch],
                            "error": res.error_msg
                        })

        return results

    # ============================================================
    # 批量语句构造
    # ============================================================

    @staticmethod
    def _chunks(items: list, size: int):
        for i in range(0, len(items), size):
            yield items[i:i + size]

    @staticmethod
    def _group_vertices(vertices: List[VertexData]) -> Dict[Tuple, List[VertexData]]:
        groups: Dict[Tuple, List[VertexData]] = {}
        for v in vertices:
            key = (
                tuple(v.tags or ()),
                tuple((v.properties or {}).keys())
            )
            groups.setdefault(key, []).append(v)
        return groups

    @staticmethod
    def _group_edges(edges: List[EdgeData]) -> Dict[Tuple, List[EdgeData]]:
        groups: Dict[Tuple, List[EdgeData]] = {}
        for e in edges:
            key = (
                e.edge_type,
                e.rank or 0,
                tuple((e.properties or {}).keys())
            )
            groups.setdefault(key, []).append(e)
        return groups

    def _build_vertex_batch(
        self, key: Tuple, batch: List[VertexData]
    ) -> str:
        tags, prop_keys = key
        if prop_keys:
            prop_names = ", ".join(_ident(k) for k in prop_keys)
            tag_list = ", ".join(f"{_ident(t)} ({prop_names})" for t in tags)
        else:
            tag_list = ", ".join(f"{_ident(t)} ()" for t in tags)
        # 多 tag 时 VALUES 中每个 tag 一组值（同批顶点共享同一属性值）
        values = ", ".join(
            f"{_quote(v.vid)}:"
            + ", ".join(
                f"({', '.join(self._format_value((v.properties or {})[k]) for k in prop_keys)})"
                for _ in tags
            )
            for v in batch
        )
        return f"INSERT VERTEX {tag_list} VALUES {values}"

    def _build_edge_batch(
        self, key: Tuple, batch: List[EdgeData]
    ) -> str:
        edge_type, rank, prop_keys = key
        values = ", ".join(
            f"{_quote(e.src_vid)} -> {_quote(e.dst_vid)}@{rank}:"
            f"({', '.join(self._format_value((e.properties or {})[k]) for k in prop_keys)})"
            for e in batch
        )
        if prop_keys:
            prop_names = ", ".join(_ident(k) for k in prop_keys)
            return f"INSERT EDGE {_ident(edge_type)} ({prop_names}) VALUES {values}"
        return f"INSERT EDGE {_ident(edge_type)} () VALUES {values}"

    def _format_value(self, value: Any) -> str:
        """Format Python value for nGQL statement"""
        if value is None:
            return "NULL"
        elif isinstance(value, bool):
            return "true" if value else "false"
        elif isinstance(value, (int, float)):
            return str(value)
        elif isinstance(value, str):
            return _quote(value)
        elif isinstance(value, list):
            items = ", ".join([self._format_value(v) for v in value])
            return f"[{items}]"
        elif isinstance(value, dict):
            items = ", ".join([f"{k}: {self._format_value(v)}" for k, v in value.items()])
            return f"{{{items}}}"
        else:
            return _quote(str(value))


# Global service instance
nebula_service = NebulaService()
