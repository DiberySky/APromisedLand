"""Tag / edge / index schema management service."""
from typing import List

from ..database import db, validate_ident
from ..schemas.schema import (
    AlterSchemaIn,
    EdgeCreateIn,
    EdgeIndexCreateIn,
    FulltextIndexCreateIn,
    PropertyDef,
    TagCreateIn,
    TagIndexCreateIn,
)


def _build_prop_list(props: List[PropertyDef]) -> str:
    return ", ".join(p.to_ddl() for p in props)


def _ttl_clause(ttl_duration, ttl_col) -> str:
    if ttl_duration is None or ttl_col is None:
        return ""
    return f" TTL={ttl_duration} TTL_COL=\"{ttl_col}\""


class SchemaService:

    # ----------------------------------------------------------------- #
    # Tags
    # ----------------------------------------------------------------- #
    def create_tag(self, body: TagCreateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "tag")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        col_clause = f"({_build_prop_list(body.properties)})" if body.properties else ""
        ttl = _ttl_clause(body.ttl_duration, body.ttl_col)
        comment = f" COMMENT='{body.comment}'" if body.comment else ""
        stmt = (f"CREATE TAG {kw}`{body.name}` {col_clause}{ttl}{comment}")
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"kind": "tag", "name": body.name, "space": body.space, "created": True}

    def drop_tag(self, space: str, name: str, if_exists: bool = True) -> dict:
        validate_ident(space, "space"); validate_ident(name, "tag")
        kw = "IF EXISTS " if if_exists else ""
        with db.session_scope(space) as s:
            s.execute(f"DROP TAG {kw}`{name}`")
        return {"kind": "tag", "name": name, "dropped": True}

    def desc_tag(self, space: str, name: str) -> list:
        validate_ident(space, "space"); validate_ident(name, "tag")
        with db.session_scope(space) as s:
            return s.query(f"DESC TAG `{name}`")

    def show_tags(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW TAGS;")

    def show_create_tag(self, space: str, name: str) -> str:
        validate_ident(space, "space"); validate_ident(name, "tag")
        with db.session_scope(space) as s:
            rows = s.query(f"SHOW CREATE TAG `{name}`")
        if not rows:
            return ""
        for v in rows[0].values():
            if isinstance(v, str):
                return v
        return ""

    def alter_tag(self, body: AlterSchemaIn) -> dict:
        if body.kind != "tag":
            raise ValueError("alter_tag expects kind='tag'")
        return self._alter_schema(body)

    # ----------------------------------------------------------------- #
    # Edges
    # ----------------------------------------------------------------- #
    def create_edge(self, body: EdgeCreateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "edge")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        col_clause = f"({_build_prop_list(body.properties)})" if body.properties else ""
        ttl = _ttl_clause(body.ttl_duration, body.ttl_col)
        comment = f" COMMENT='{body.comment}'" if body.comment else ""
        stmt = f"CREATE EDGE {kw}`{body.name}` {col_clause}{ttl}{comment}"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"kind": "edge", "name": body.name, "space": body.space, "created": True}

    def drop_edge(self, space: str, name: str, if_exists: bool = True) -> dict:
        validate_ident(space, "space"); validate_ident(name, "edge")
        kw = "IF EXISTS " if if_exists else ""
        with db.session_scope(space) as s:
            s.execute(f"DROP EDGE {kw}`{name}`")
        return {"kind": "edge", "name": name, "dropped": True}

    def desc_edge(self, space: str, name: str) -> list:
        validate_ident(space, "space"); validate_ident(name, "edge")
        with db.session_scope(space) as s:
            return s.query(f"DESC EDGE `{name}`")

    def show_edges(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW EDGES;")

    def show_create_edge(self, space: str, name: str) -> str:
        validate_ident(space, "space"); validate_ident(name, "edge")
        with db.session_scope(space) as s:
            rows = s.query(f"SHOW CREATE EDGE `{name}`")
        if not rows:
            return ""
        for v in rows[0].values():
            if isinstance(v, str):
                return v
        return ""

    def alter_edge(self, body: AlterSchemaIn) -> dict:
        if body.kind != "edge":
            raise ValueError("alter_edge expects kind='edge'")
        return self._alter_schema(body)

    # ----------------------------------------------------------------- #
    # Shared alter logic
    # ----------------------------------------------------------------- #
    def _alter_schema(self, body: AlterSchemaIn) -> dict:
        validate_ident(body.space, "space"); validate_ident(body.name, "schema")
        kind = body.kind.upper()  # TAG or EDGE
        clauses = []
        if body.add:
            clauses.append(f"ADD ({_build_prop_list(body.add)})")
        if body.change:
            clauses.append(f"CHANGE ({_build_prop_list(body.change)})")
        if body.drop:
            dropped = ", ".join(f"`{c}`" for c in body.drop)
            clauses.append(f"DROP ({dropped})")
        if body.ttl_duration is not None and body.ttl_col:
            clauses.append(f"TTL={body.ttl_duration} TTL_COL=\"{body.ttl_col}\"")
        if body.comment is not None:
            clauses.append(f"COMMENT='{body.comment}'")
        if not clauses:
            raise ValueError("no alteration clauses produced")
        stmt = f"ALTER {kind} `{body.name}` " + ", ".join(clauses)
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"kind": body.kind, "name": body.name, "space": body.space, "altered": True}

    # ----------------------------------------------------------------- #
    # Indexes
    # ----------------------------------------------------------------- #
    def create_tag_index(self, body: TagIndexCreateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "index")
        validate_ident(body.tag, "tag")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        fields = ", ".join(self._field_token(f) for f in body.fields)
        stmt = f"CREATE TAG INDEX {kw}`{body.name}` ON `{body.tag}`({fields})"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
            if body.rebuild:
                try:
                    s.execute(f"REBUILD TAG INDEX `{body.name}`")
                except Exception:
                    pass
        return {"kind": "tag_index", "name": body.name, "created": True, "rebuilt": body.rebuild}

    def create_edge_index(self, body: EdgeIndexCreateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "index")
        validate_ident(body.edge, "edge")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        fields = ", ".join(self._field_token(f) for f in body.fields)
        stmt = f"CREATE EDGE INDEX {kw}`{body.name}` ON `{body.edge}`({fields})"
        with db.session_scope(body.space) as s:
            s.execute(stmt)
            if body.rebuild:
                try:
                    s.execute(f"REBUILD EDGE INDEX `{body.name}`")
                except Exception:
                    pass
        return {"kind": "edge_index", "name": body.name, "created": True, "rebuilt": body.rebuild}

    def rebuild_index(self, space: str, name: str, kind: str = "tag") -> dict:
        validate_ident(space, "space"); validate_ident(name, "index")
        kind = kind.strip().lower()
        if kind not in {"tag", "edge"}:
            raise ValueError("kind must be 'tag' or 'edge'")
        stmt = f"REBUILD {kind.upper()} INDEX `{name}`"
        with db.session_scope(space) as s:
            s.execute(stmt)
        return {"name": name, "kind": kind + "_index", "rebuilt": True}

    def drop_index(self, space: str, name: str, if_exists: bool = True) -> dict:
        validate_ident(space, "space"); validate_ident(name, "index")
        kw = "IF EXISTS " if if_exists else ""
        with db.session_scope(space) as s:
            s.execute(f"DROP INDEX {kw}`{name}`")
        return {"name": name, "dropped": True}

    def show_indexes(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW INDEXES;")

    def show_tag_indexes(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW TAG INDEXES;")

    def show_edge_indexes(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW EDGE INDEXES;")

    def desc_tag_index(self, space: str, name: str) -> list:
        validate_ident(space, "space"); validate_ident(name, "index")
        with db.session_scope(space) as s:
            return s.query(f"DESCRIBE TAG INDEX `{name}`")

    def desc_edge_index(self, space: str, name: str) -> list:
        validate_ident(space, "space"); validate_ident(name, "index")
        with db.session_scope(space) as s:
            return s.query(f"DESCRIBE EDGE INDEX `{name}`")

    # ----------------------------------------------------------------- #
    # Fulltext indexes (require Elasticsearch backend)
    # ----------------------------------------------------------------- #
    def create_fulltext_index(self, body: FulltextIndexCreateIn) -> dict:
        validate_ident(body.space, "space")
        validate_ident(body.name, "index")
        validate_ident(body.schema_name, "schema")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        kind = body.kind.upper()  # TAG or EDGE
        fields = ", ".join(self._field_token(f) for f in body.fields)
        stmt = (f"CREATE FULLTEXT {kind} INDEX {kw}`{body.name}` "
                f"ON `{body.schema_name}`({fields})")
        with db.session_scope(body.space) as s:
            s.execute(stmt)
        return {"kind": "fulltext_" + body.kind, "name": body.name, "created": True}

    def show_fulltext_indexes(self) -> list:
        with db.session_scope() as s:
            return s.query("SHOW FULLTEXT INDEXES;")

    def drop_fulltext_index(self, name: str, if_exists: bool = True) -> dict:
        validate_ident(name, "index")
        kw = "IF EXISTS " if if_exists else ""
        with db.session_scope() as s:
            s.execute(f"DROP FULLTEXT INDEX {kw}`{name}`")
        return {"name": name, "dropped": True}

    # ----------------------------------------------------------------- #
    # helpers
    # ----------------------------------------------------------------- #
    @staticmethod
    def _field_token(field: str) -> str:
        """Validate a field reference for indexes.

        Accepts ``prop`` or ``prop(length)`` forms.
        """
        f = field.strip()
        if not f:
            raise ValueError("empty index field")
        if "(" in f:
            base = f[: f.index("(")]
            rest = f[f.index("("):]
            validate_ident(base, "index field")
            return f"`{base}`{rest}"
        validate_ident(f, "index field")
        return f"`{f}`"
