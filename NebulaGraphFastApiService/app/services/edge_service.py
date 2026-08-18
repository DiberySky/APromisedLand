"""Edge (relationship) data service."""
from ..database import db, validate_ident
from ..schemas.edge import (
    EdgeDeleteIn,
    EdgeFetchIn,
    EdgeInsertIn,
    EdgeUpdateIn,
    EdgeUpsertIn,
)
from ..utils.ngql import format_literal, format_vid


def _edge_ref(edge: str) -> str:
    validate_ident(edge, "edge")
    return f"`{edge}`"


class EdgeService:

    def insert(self, body: EdgeInsertIn) -> dict:
        validate_ident(body.space, "space")
        edge = _edge_ref(body.edge)
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        first_props = body.edges[0].props
        keys = list(first_props.keys())
        col_clause = ", ".join(f"`{k}`" for k in keys)
        values = []
        for e in body.edges:
            vals = ", ".join(format_literal(e.props[k]) for k in keys)
            values.append(
                f"{format_vid(e.src)}->{format_vid(e.dst)}@{e.ranking}: ({vals})"
            )
        stmt = f"INSERT EDGE {kw}{edge} ({col_clause}) VALUES " + ", ".join(values)
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"space": body.space, "edge": body.edge, "inserted": len(body.edges)}

    def fetch(self, body: EdgeFetchIn) -> list:
        validate_ident(body.space, "space")
        edge = _edge_ref(body.edge)
        refs = []
        for p in body.pairs:
            src = format_vid(p["src"])
            dst = format_vid(p["dst"])
            rank = int(p.get("ranking", 0))
            refs.append(f"{src}->{dst}@{rank}")
        ref_str = ", ".join(refs)
        yield_clause = f"YIELD `{body.edge}`.{body.prop}" if body.prop else ""
        stmt = f"FETCH PROP ON {edge} {ref_str} {yield_clause}".strip()
        with db.session_scope(body.space) as s:
            return s.query(stmt)

    def delete(self, body: EdgeDeleteIn) -> dict:
        validate_ident(body.space, "space")
        edge = _edge_ref(body.edge)
        refs = []
        for p in body.pairs:
            src = format_vid(p["src"])
            dst = format_vid(p["dst"])
            rank = int(p.get("ranking", 0))
            refs.append(f"{src}->{dst}@{rank}")
        stmt = f"DELETE EDGE {edge} " + ", ".join(refs)
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"space": body.space, "edge": body.edge, "deleted": len(body.pairs)}

    def update(self, body: EdgeUpdateIn) -> dict:
        validate_ident(body.space, "space")
        edge = _edge_ref(body.edge)
        sets = ", ".join(f"`{k}`={v}" for k, v in body.set.items())
        ref = f"{format_vid(body.src)}->{format_vid(body.dst)}@{body.ranking}"
        stmt = f"UPDATE EDGE ON {edge} {ref} SET {sets}"
        if body.when:
            stmt += f" WHEN {body.when}"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"edge": body.edge, "ref": ref, "updated": True}

    def upsert(self, body: EdgeUpsertIn) -> dict:
        validate_ident(body.space, "space")
        edge = _edge_ref(body.edge)
        sets = ", ".join(f"`{k}`={v}" for k, v in body.set.items())
        ref = f"{format_vid(body.src)}->{format_vid(body.dst)}@{body.ranking}"
        stmt = f"UPSERT EDGE ON {edge} {ref} SET {sets}"
        if body.when:
            stmt += f" WHEN {body.when}"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"edge": body.edge, "ref": ref, "upserted": True}
