"""Query service: GO / LOOKUP / FIND PATH / GET SUBGRAPH / raw nGQL."""
from typing import Optional

from ..database import db, validate_ident
from ..schemas.query import (
    FindPathIn,
    GetSubgraphIn,
    GoIn,
    LookupIn,
)
from ..utils.ngql import format_vid


class QueryService:

    # ----------------------------------------------------------------- #
    # Raw nGQL
    # ----------------------------------------------------------------- #
    def raw(self, statement: str, space: Optional[str] = None) -> dict:
        with db.session_scope(space) as s:
            return s.query_with_meta(statement)

    def explain(self, statement: str, space: Optional[str] = None) -> dict:
        return self._plan("EXPLAIN", statement, space)

    def profile(self, statement: str, space: Optional[str] = None) -> dict:
        return self._plan("PROFILE", statement, space)

    def _plan(self, prefix: str, statement: str, space: Optional[str]) -> dict:
        stmt = f"{prefix} {statement.strip().rstrip(';')}"
        with db.session_scope(space) as s:
            result = s.execute(stmt)
        plan = result.plan_desc()
        return {
            "succeeded": True,
            "latency_us": result.latency(),
            "plan_desc": str(plan) if plan is not None else None,
        }

    # ----------------------------------------------------------------- #
    # GO
    # ----------------------------------------------------------------- #
    def go(self, body: GoIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.edge, "edge")
        direction = GoIn._norm_direction(body.direction)
        dir_kw = {"OUT": "", "IN": "REVERSELY", "BIDIRECT": "BIDIRECT"}[direction]
        parts = [
            f"GO {body.steps} STEPS",
            f"OVER `{body.edge}`",
        ]
        if dir_kw:
            parts.append(dir_kw)
        parts.append(f"FROM {format_vid(body.from_vid)}")
        if body.where:
            parts.append(f"WHERE {body.where}")
        if body.yield_:
            parts.append(f"YIELD {body.yield_}")
        else:
            parts.append("YIELD id(vertex) AS _vid, dst(edge) AS _dst, src(edge) AS _src, properties(edge) AS _props")
        if body.sample:
            parts.append(f"SAMPLE {body.sample}")
        if body.limit:
            parts.append(f"LIMIT {body.limit}")
        stmt = " ".join(parts)
        with db.session_scope(body.space) as s:
            return s.query_with_meta(stmt)

    # ----------------------------------------------------------------- #
    # LOOKUP
    # ----------------------------------------------------------------- #
    def lookup(self, body: LookupIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "lookup target")
        kind = body.kind.strip().lower()
        if kind not in {"tag", "edge"}:
            raise ValueError("kind must be 'tag' or 'edge'")
        parts = [f"LOOKUP ON `{body.name}`"]
        if body.where:
            parts.append(f"WHERE {body.where}")
        if body.yield_:
            parts.append(f"YIELD {body.yield_}")
        else:
            parts.append("YIELD id(vertex) AS _vid" if kind == "tag"
                         else "YIELD src(edge) AS _src, dst(edge) AS _dst")
        if body.limit:
            parts.append(f"LIMIT {body.limit}")
        stmt = " ".join(parts)
        with db.session_scope(body.space) as s:
            return s.query_with_meta(stmt)

    # ----------------------------------------------------------------- #
    # FIND PATH
    # ----------------------------------------------------------------- #
    def find_path(self, body: FindPathIn) -> dict:
        validate_ident(body.space, "space")
        mode = "SHORTEST" if body.single_shortest else ("NOLOOP" if body.no_loop else "ALL")
        parts = [f"FIND {mode} PATH"]
        if body.with_prop:
            parts.append("WITH PROP")
        parts.append(f"FROM {format_vid(body.src)} TO {format_vid(body.dst)}")
        if body.edge:
            validate_ident(body.edge, "edge")
            parts.append(f"OVER `{body.edge}`")
        dir_map = {"OUT": "", "IN": "REVERSELY", "BIDIRECT": "BIDIRECT"}
        dir_kw = dir_map.get(body.direction.upper(), "BIDIRECT")
        if dir_kw:
            parts.append(dir_kw)
        parts.append(f"UPTO {body.steps} STEPS")
        parts.append("YIELD path AS p")
        stmt = " ".join(parts)
        with db.session_scope(body.space) as s:
            return s.query_with_meta(stmt)

    # ----------------------------------------------------------------- #
    # GET SUBGRAPH
    # ----------------------------------------------------------------- #
    def get_subgraph(self, body: GetSubgraphIn) -> dict:
        validate_ident(body.space, "space")
        parts = ["GET SUBGRAPH"]
        if body.with_prop:
            parts.append("WITH PROP")
        parts.append(f"{body.steps} STEPS")
        parts.append(f"FROM {format_vid(body.vid)}")
        edge_clauses = []
        if body.in_edges:
            edge_clauses.append("IN " + ", ".join(f"`{e}`" for e in body.in_edges))
        if body.out_edges:
            edge_clauses.append("OUT " + ", ".join(f"`{e}`" for e in body.out_edges))
        if body.both_edges:
            edge_clauses.append("BOTH " + ", ".join(f"`{e}`" for e in body.both_edges))
        if edge_clauses:
            parts.append(", ".join(edge_clauses))
        # Let nebula return its default `vertices` / `edges` columns; the
        # parser converts vertex/edge values into structured dicts.
        stmt = " ".join(parts)
        with db.session_scope(body.space) as s:
            return s.query_with_meta(stmt)

    # ----------------------------------------------------------------- #
    # Statistics helper (SHOW STATS)
    # ----------------------------------------------------------------- #
    def show_stats(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW STATS;")
