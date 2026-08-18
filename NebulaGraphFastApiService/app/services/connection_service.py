"""Connection / health service."""
from ..config import settings
from ..database import db


class ConnectionService:
    def ping(self) -> bool:
        return db.ping()

    def show_spaces(self) -> list:
        with db.session_scope() as s:
            rows = s.query("SHOW SPACES;")
        # SHOW SPACES returns rows like {'Name': 'basketballplayer'}
        names = []
        for r in rows:
            if isinstance(r, dict):
                v = next(iter(r.values())) if r else None
                if isinstance(v, str):
                    names.append(v)
                else:
                    names.append(str(v))
        return names

    def status(self) -> dict:
        connected = db.ping()
        spaces = 0
        if connected:
            try:
                spaces = len(self.show_spaces())
            except Exception:
                spaces = 0
        return {
            "status": "ok" if connected else "down",
            "connected": connected,
            "host": settings.nebula_host,
            "port": settings.nebula_port,
            "pool_size": settings.nebula_pool_size,
            "spaces": spaces,
        }
