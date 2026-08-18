"""Convert nebula-python ``ResultSet`` objects into JSON-safe Python structures.

Uses the high-level ``ValueWrapper`` / ``Node`` / ``Relationship`` /
``PathWrapper`` helpers shipped by ``nebula3.data.DataObject`` so that graph
results (vertices, edges, paths) are returned with rich structure instead of
opaque strings.
"""
import logging
from typing import Any

logger = logging.getLogger("nebula_api.parser")


def _scalar(vw) -> Any:
    """Extract a JSON-safe scalar from a ``ValueWrapper``."""
    # The library guarantees cast_primitive() for every scalar-ish type
    # (bool / int / double / string / date / time / datetime / geography /
    # duration). We convert known datetime-ish objects to ISO strings.
    val = vw.cast_primitive()
    iso = getattr(val, "isoformat", None)
    if callable(iso):
        try:
            return iso()
        except Exception:
            return str(val)
    return val


def parse_value(vw) -> Any:
    """Recursively convert a ``ValueWrapper`` into a JSON-safe value."""
    try:
        if vw.is_null() or vw.is_empty():
            return None
        if vw.is_vertex():
            return _parse_node(vw.as_node())
        if vw.is_edge():
            return _parse_relationship(vw.as_relationship())
        if vw.is_path():
            return _parse_path(vw.as_path())
        if vw.is_list():
            return [parse_value(x) for x in vw.as_list()]
        if vw.is_set():
            as_set = getattr(vw, "as_set", None)
            items = as_set() if callable(as_set) else []
            return [parse_value(x) for x in items]
        if vw.is_map():
            as_map = getattr(vw, "as_map", None)
            if callable(as_map):
                return {k: parse_value(v) for k, v in as_map().items()}
            return _scalar(vw)
        return _scalar(vw)
    except Exception:
        logger.exception("Failed to parse nebula value, falling back to primitive")
        try:
            return _scalar(vw)
        except Exception:
            return str(vw)


def _parse_node(node) -> dict:
    """Convert a ``Node`` (vertex) to a dict."""
    tags = list(node.tags())
    props: dict = {}
    for tag in tags:
        try:
            tag_props = node.properties(tag) or {}
        except Exception:
            tag_props = {}
        for k, v in tag_props.items():
            props.setdefault(k, parse_value(v))
    return {
        "_type": "vertex",
        "id": parse_value(node.get_id()),
        "tags": tags,
        "properties": props,
    }


def _parse_relationship(rel) -> dict:
    """Convert a ``Relationship`` (edge) to a dict."""
    try:
        props = rel.properties() or {}
        props = {k: parse_value(v) for k, v in props.items()}
    except Exception:
        props = {}
    return {
        "_type": "edge",
        "src": parse_value(rel.start_vertex_id()),
        "dst": parse_value(rel.end_vertex_id()),
        "name": rel.edge_name(),
        "ranking": rel.ranking(),
        "properties": props,
    }


def _parse_path(path) -> dict:
    """Convert a ``PathWrapper`` to a dict of nodes + edges."""
    return {
        "_type": "path",
        "nodes": [_parse_node(n) for n in path.nodes()],
        "edges": [_parse_relationship(r) for r in path.relationships()],
    }


def parse_result_set(result) -> list:
    """Parse a ``nebula3.data.ResultSet.ResultSet`` into ``list[dict]``.

    Falls back to ``ResultSet.as_primitive()`` if the structured walk fails.
    """
    try:
        keys = list(result.keys())
        if not keys or result.row_size() == 0:
            return []
        rows = []
        for i in range(result.row_size()):
            row_vals = result.row_values(i)
            rows.append(
                {keys[c]: parse_value(v) for c, v in enumerate(row_vals)}
            )
        return rows
    except Exception:
        logger.exception("Structured parse failed, falling back to as_primitive()")
        try:
            return result.as_primitive()
        except Exception:
            return []


def parse_for_vis(result) -> dict:
    """Return a graph-visualisation friendly structure (nodes/edges).

    Wraps ``ResultSet.dict_for_vis()`` and stringifies nested ``ValueWrapper``
    props that the library leaves un-cast.
    """
    try:
        return result.dict_for_vis()
    except Exception:
        logger.exception("dict_for_vis failed")
        return {"nodes": [], "edges": [], "nodes_count": 0, "edges_count": 0}
