"""NebulaGraph connection pool manager and session lifecycle.

nebula-python sessions are NOT thread-safe; each request must borrow a fresh
session from the pool, use it, and release it back. The ``session_scope``
context manager handles that automatically.
"""
import logging
import re
from contextlib import contextmanager
from threading import Lock
from typing import Iterator, Optional

from nebula3.Config import Config
from nebula3.gclient.net import ConnectionPool
from nebula3.data.ResultSet import ResultSet

from .config import settings

logger = logging.getLogger("nebula_api.db")

# nGQL identifiers (space / tag / edge / index names) are wrapped in backticks.
# We forbid backticks inside identifiers to prevent statement injection.
_IDENT_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


class NebulaError(Exception):
    """Raised when a nebula statement fails to execute."""

    def __init__(self, error_code: int, error_msg: str, statement: str = ""):
        self.error_code = error_code
        self.error_msg = error_msg
        self.statement = statement
        super().__init__(f"[{error_code}] {error_msg}")


class NebulaDB:
    """Singleton wrapper around a nebula-python ``ConnectionPool``."""

    def __init__(self) -> None:
        self._pool: Optional[ConnectionPool] = None
        self._lock = Lock()

    # ------------------------------------------------------------------ #
    # Lifecycle
    # ------------------------------------------------------------------ #
    def connect(self) -> None:
        with self._lock:
            if self._pool is not None:
                return
            config = Config()
            config.max_connection_pool_size = settings.nebula_pool_size
            config.timeout = settings.nebula_timeout
            config.interval_idle = settings.nebula_interval_idle
            config.time_wait = settings.nebula_time_wait

            pool = ConnectionPool()
            ok = pool.init(settings.nebula_endpoints, config)
            if not ok:
                raise RuntimeError(
                    f"Failed to init nebula connection pool to "
                    f"{settings.nebula_endpoints}"
                )
            logger.info(
                "Nebula connection pool initialised against %s",
                settings.nebula_endpoints,
            )
            self._pool = pool

    def close(self) -> None:
        with self._lock:
            if self._pool is not None:
                self._pool.close()
                self._pool = None
                logger.info("Nebula connection pool closed.")

    @property
    def pool(self) -> ConnectionPool:
        if self._pool is None:
            raise RuntimeError("Nebula pool is not initialised. Call connect().")
        return self._pool

    # ------------------------------------------------------------------ #
    # Session management
    # ------------------------------------------------------------------ #
    @contextmanager
    def session_scope(
        self, space: Optional[str] = None
    ) -> Iterator["SessionWrapper"]:
        """Borrow a session, optionally switch to a space, release on exit."""
        session = self.pool.get_session(
            settings.nebula_username, settings.nebula_password
        )
        wrapper = SessionWrapper(session)
        try:
            if space:
                wrapper.use_space(space)
            yield wrapper
        finally:
            try:
                session.release()
            except Exception:  # pragma: no cover - defensive
                logger.warning("Failed to release nebula session", exc_info=True)

    def ping(self) -> bool:
        """Lightweight connectivity check via ``SHOW SPACES``."""
        try:
            with self.session_scope() as s:
                r = s.execute("SHOW SPACES;")
                return r.is_succeeded()
        except Exception:
            logger.exception("Ping failed")
            return False


class SessionWrapper:
    """Thin convenience wrapper around a nebula-python ``Session``.

    Centralises statement execution, error translation and result parsing so
    that the service layer stays clean.
    """

    def __init__(self, session) -> None:
        self._session = session

    # ------------------------------------------------------------------ #
    # Execution helpers
    # ------------------------------------------------------------------ #
    def execute(self, stmt: str) -> ResultSet:
        stmt = stmt.strip()
        if not stmt.endswith(";"):
            stmt = stmt + ";"
        result = self._session.execute(stmt)
        if not result.is_succeeded():
            raise NebulaError(
                result.error_code(),
                result.error_msg(),
                statement=stmt,
            )
        return result

    def execute_raw(self, stmt: str) -> ResultSet:
        """Execute without auto-appending semicolon / error translation."""
        return self._session.execute(stmt)

    def use_space(self, space: str) -> None:
        validate_ident(space, "space")
        self.execute(f"USE `{space}`")

    # ------------------------------------------------------------------ #
    # Convenience: execute + parse to list[dict]
    # ------------------------------------------------------------------ #
    def query(self, stmt: str) -> list:
        from .utils.nebula_parser import parse_result_set

        result = self.execute(stmt)
        return parse_result_set(result)

    def query_with_meta(self, stmt: str) -> dict:
        """Return parsed rows plus latency / column meta."""
        from .utils.nebula_parser import parse_result_set

        result = self.execute(stmt)
        try:
            columns = list(result.keys())
        except Exception:
            columns = []
        try:
            latency_us = result.latency()
        except Exception:
            latency_us = None
        try:
            space = result.space_name()
        except Exception:
            space = None
        try:
            comment = result.comment()
        except Exception:
            comment = None
        rows = parse_result_set(result)
        return {
            "columns": columns,
            "rows": rows,
            "row_count": len(rows),
            "latency_us": latency_us,
            "space": space or None,
            "comment": comment or None,
        }


# ---------------------------------------------------------------------- #
# Identifier validation
# ---------------------------------------------------------------------- #
def validate_ident(name: str, kind: str = "identifier") -> None:
    """Validate an nGQL identifier to avoid backtick injection."""
    if name is None or not _IDENT_RE.match(name):
        raise ValueError(f"Invalid {kind} name: {name!r}")


# Shared singleton
db = NebulaDB()
