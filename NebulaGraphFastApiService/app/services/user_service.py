"""User and role management service."""
from ..database import db, validate_ident
from ..schemas.user import (
    PasswordChangeIn,
    PasswordResetIn,
    RoleAssignIn,
    RoleRevokeIn,
    UserCreateIn,
    UserDeleteIn,
)


class UserService:

    def create(self, body: UserCreateIn) -> dict:
        validate_ident(body.name, "user")
        kw = "IF NOT EXISTS " if body.if_not_exists else ""
        # Password is passed as a quoted literal (escaped by format_literal).
        from ..utils.ngql import format_literal
        stmt = f"CREATE USER {kw}`{body.name}` WITH PASSWORD {format_literal(body.password)}"
        with db.session_scope() as s:
            s.execute(stmt)
        return {"name": body.name, "created": True}

    def delete(self, body: UserDeleteIn) -> dict:
        validate_ident(body.name, "user")
        kw = "IF EXISTS " if body.if_exists else ""
        with db.session_scope() as s:
            s.execute(f"DROP USER {kw}`{body.name}`")
        return {"name": body.name, "dropped": True}

    def list_users(self) -> list:
        with db.session_scope() as s:
            return s.query("SHOW USERS;")

    def describe_user(self, name: str) -> list:
        validate_ident(name, "user")
        with db.session_scope() as s:
            return s.query(f"DESCRIBE USER `{name}`")

    def change_password(self, body: PasswordChangeIn) -> dict:
        validate_ident(body.name, "user")
        from ..utils.ngql import format_literal
        stmt = (f"CHANGE PASSWORD `{body.name}` FROM "
                f"{format_literal(body.old_password)} TO "
                f"{format_literal(body.new_password)}")
        with db.session_scope() as s:
            s.execute(stmt)
        return {"name": body.name, "password_changed": True}

    def reset_password(self, body: PasswordResetIn) -> dict:
        validate_ident(body.name, "user")
        from ..utils.ngql import format_literal
        stmt = (f"ALTER USER `{body.name}` WITH PASSWORD "
                f"{format_literal(body.new_password)}")
        with db.session_scope() as s:
            s.execute(stmt)
        return {"name": body.name, "password_reset": True}

    # ----------------------------------------------------------------- #
    # Roles
    # ----------------------------------------------------------------- #
    def show_roles_in_space(self, space: str) -> list:
        validate_ident(space, "space")
        with db.session_scope() as s:
            return s.query(f"SHOW ROLES IN `{space}`")

    def show_user_roles(self, name: str) -> list:
        validate_ident(name, "user")
        with db.session_scope() as s:
            return s.query(f"SHOW ROLES IN * WHERE user IS `{name}`")

    def grant_role(self, body: RoleAssignIn) -> dict:
        validate_ident(body.user, "user")
        if body.role == "GOD":
            stmt = f"GRANT ROLE GOD TO `{body.user}`"
        else:
            validate_ident(body.space, "space")
            stmt = (f"GRANT ROLE {body.role} ON `{body.space}` TO `{body.user}`")
        with db.session_scope() as s:
            s.execute(stmt)
        return {"user": body.user, "role": body.role,
                "space": body.space, "granted": True}

    def revoke_role(self, body: RoleRevokeIn) -> dict:
        validate_ident(body.user, "user")
        validate_ident(body.space, "space")
        stmt = (f"REVOKE ROLE {body.role} ON `{body.space}` FROM `{body.user}`")
        with db.session_scope() as s:
            s.execute(stmt)
        return {"user": body.user, "role": body.role,
                "space": body.space, "revoked": True}
