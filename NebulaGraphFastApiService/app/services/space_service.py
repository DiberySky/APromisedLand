"""Graph space management service."""
from ..database import db, validate_ident


class SpaceService:
    def list_spaces(self) -> list:
        with db.session_scope() as s:
            rows = s.query("SHOW SPACES;")
        names = []
        for r in rows:
            if isinstance(r, dict) and r:
                v = next(iter(r.values()))
                names.append(v if isinstance(v, str) else str(v))
        return names

    def create_space(self, body) -> dict:
        validate_ident(body.name, "space")
        parts = [
            f"CREATE SPACE{' IF NOT EXISTS' if body.if_not_exists else ''} "
            f"`{body.name}`",
            f"PARTITION_NUM={body.partition_num}",
            f"REPLICA_FACTOR={body.replica_factor}",
            f"vid_type={body.vid_type}",
        ]
        if body.comment:
            parts.append(f"COMMENT='{body.comment}'")
        with db.session_scope() as s:
            s.execute(" ".join(parts))
        # Wait for space to be ready; DESC SPACE polls availability.
        return {"name": body.name, "created": True}

    def drop_space(self, name: str, if_exists: bool = True) -> dict:
        validate_ident(name, "space")
        kw = "IF EXISTS " if if_exists else ""
        with db.session_scope() as s:
            s.execute(f"DROP SPACE {kw}`{name}`")
        return {"name": name, "dropped": True}

    def desc_space(self, name: str) -> dict:
        validate_ident(name, "space")
        with db.session_scope() as s:
            rows = s.query(f"DESC SPACE `{name}`")
        if not rows:
            return {}
        # DESC SPACE returns a single row of named columns (Name, Partition
        # Number, Replica Factor, Vid Type, ...).
        first = rows[0]
        return first if isinstance(first, dict) else {"row": first}

    def alter_space_comment(self, name: str, comment: str) -> dict:
        validate_ident(name, "space")
        with db.session_scope() as s:
            s.execute(f"ALTER SPACE `{name}` COMMENT='{comment}'")
        return {"name": name, "comment": comment}

    def show_create_space(self, name: str) -> str:
        validate_ident(name, "space")
        with db.session_scope() as s:
            rows = s.query(f"SHOW CREATE SPACE `{name}`")
        if not rows:
            return ""
        # returns a single row with the Create Space column
        first = rows[0]
        for v in first.values():
            return v if isinstance(v, str) else str(v)
        return ""
