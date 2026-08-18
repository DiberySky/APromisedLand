"""Vertex (point) data service."""
from ..database import db, validate_ident
from ..schemas.vertex import (
    VertexDeleteIn,
    VertexFetchIn,
    VertexInsertIn,
    VertexUpdateIn,
    VertexUpsertIn,
)
from ..utils.ngql import format_literal, format_vid


class VertexService:

    def insert(self, body: VertexInsertIn) -> dict:
        validate_ident(body.space, "space")
        inserted = 0
        with db.session_scope(body.space) as s:
            kw = "IF NOT EXISTS " if body.if_not_exists else ""
            for v in body.vertices:
                # Insert per tag, so each tag gets its own property set.
                for tag, props in v.tags.items():
                    validate_ident(tag, "tag")
                    cols = ", ".join(f"`{k}`" for k in props.keys())
                    vals = ", ".join(format_literal(val) for val in props.values())
                    stmt = (f"INSERT VERTEX {kw}`{tag}` ({cols}) VALUES "
                            f"{format_vid(v.vid)}: ({vals})")
                    s.execute(stmt)
                inserted += 1
        return {"space": body.space, "inserted": inserted}

    def fetch(self, body: VertexFetchIn) -> list:
        validate_ident(body.space, "space")
        vids = ", ".join(format_vid(v) for v in body.vids)
        if body.tag:
            validate_ident(body.tag, "tag")
            stmt = f"FETCH PROP ON `{body.tag}` {vids}"
            if body.prop:
                validate_ident(body.prop, "property")
                stmt += f" YIELD id(vertex), `{body.tag}`.{body.prop}"
        else:
            # All tags - fetch every tag's properties.
            stmt = f"FETCH PROP ON * {vids} YIELD id(vertex), tags(vertex)"
        with db.session_scope(body.space) as s:
            return s.query(stmt)

    def delete(self, body: VertexDeleteIn) -> dict:
        validate_ident(body.space, "space")
        vids = ", ".join(format_vid(v) for v in body.vids)
        with db.session_scope(body.space) as s:
            s.execute(f"DELETE VERTEX {vids} WITH EDGE")
        return {"space": body.space, "deleted": len(body.vids)}

    def update(self, body: VertexUpdateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.tag, "tag")
        sets = ", ".join(f"`{k}`={v}" for k, v in body.set.items())
        stmt = (f"UPDATE VERTEX ON `{body.tag}` {format_vid(body.vid)} SET {sets}")
        if body.when:
            stmt += f" WHEN {body.when}"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"vid": body.vid, "updated": True}

    def upsert(self, body: VertexUpsertIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.tag, "tag")
        sets = ", ".join(f"`{k}`={v}" for k, v in body.set.items())
        stmt = (f"UPSERT VERTEX ON `{body.tag}` {format_vid(body.vid)} SET {sets}")
        if body.when:
            stmt += f" WHEN {body.when}"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"vid": body.vid, "upserted": True}
