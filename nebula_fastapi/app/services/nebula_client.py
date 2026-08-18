"""
NebulaGraph Client Service - Connection Pool & Session Management
"""
import logging
import json
from typing import List, Dict, Any, Optional, Tuple
from contextlib import contextmanager
from datetime import datetime

from nebula3.gclient.net import ConnectionPool
from nebula3.Config import Config as NebulaConfig
from nebula3.data.ResultSet import ResultSet

from app.config import settings

logger = logging.getLogger(__name__)


class NebulaClient:
    """
    Singleton NebulaGraph client managing connection pool and sessions.

    Features:
    - Connection pool lifecycle management
    - Session context manager for auto-release
    - Query execution with result transformation
    - Health check and connection monitoring
    """

    _instance = None
    _initialized = False

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance

    def __init__(self):
        if NebulaClient._initialized:
            return

        self._pool: Optional[ConnectionPool] = None
        self._config: Optional[NebulaConfig] = None
        self._hosts: List[Tuple[str, int]] = []
        self._connected = False
        NebulaClient._initialized = True

    def initialize(self) -> bool:
        """
        Initialize the connection pool with configured hosts.

        Returns:
            bool: True if initialization successful
        """
        try:
            self._config = NebulaConfig()
            self._config.min_connection_pool_size = settings.NEBULA_MIN_CONN_POOL_SIZE
            self._config.max_connection_pool_size = settings.NEBULA_MAX_CONN_POOL_SIZE
            self._config.timeout = settings.NEBULA_TIMEOUT
            self._config.idle_time = settings.NEBULA_IDLE_TIME
            self._config.interval_check = settings.NEBULA_INTERVAL_CHECK

            self._hosts = settings.nebula_hosts
            self._pool = ConnectionPool()

            ok = self._pool.init(self._hosts, self._config)
            if ok:
                self._connected = True
                logger.info(f"NebulaGraph connection pool initialized: {self._hosts}")
                return True
            else:
                logger.error("Failed to initialize NebulaGraph connection pool")
                return False

        except Exception as e:
            logger.error(f"NebulaGraph initialization error: {e}")
            self._connected = False
            return False

    def close(self):
        """Close connection pool and release all resources"""
        if self._pool:
            try:
                self._pool.close()
                logger.info("NebulaGraph connection pool closed")
            except Exception as e:
                logger.error(f"Error closing connection pool: {e}")
            finally:
                self._pool = None
                self._connected = False

    @property
    def is_connected(self) -> bool:
        """Check if connection pool is active"""
        return self._connected and self._pool is not None

    @property
    def pool_stats(self) -> Dict[str, Any]:
        """Get connection pool statistics"""
        if not self._pool:
            return {"status": "not_initialized"}

        return {
            "status": "connected" if self._connected else "disconnected",
            "total_connections": self._pool.connects(),
            "in_use_connections": self._pool.in_used_connects(),
            "ok_servers": self._pool.get_ok_servers_num(),
            "hosts": [f"{h[0]}:{h[1]}" for h in self._hosts]
        }

    @contextmanager
    def session(self, space: Optional[str] = None):
        """
        Context manager for NebulaGraph session.

        Args:
            space: Optional graph space to USE

        Yields:
            Session: NebulaGraph session object

        Example:
            with nebula_client.session(space="basketballplayer") as sess:
                result = sess.execute("MATCH (v) RETURN v LIMIT 10")
        """
        session = None
        try:
            if not self._pool:
                raise RuntimeError("Connection pool not initialized")

            session = self._pool.get_session(
                settings.NEBULA_USER, 
                settings.NEBULA_PASSWORD
            )

            if space:
                session.execute(f"USE `{space}`")
            elif settings.NEBULA_SPACE:
                session.execute(f"USE `{settings.NEBULA_SPACE}`")

            yield session

        except Exception as e:
            logger.error(f"Session error: {e}")
            raise
        finally:
            if session:
                session.release()

    def execute(
        self, 
        query: str, 
        space: Optional[str] = None,
        params: Optional[Dict[str, Any]] = None
    ) -> ResultSet:
        """
        Execute nGQL query and return raw ResultSet.

        Args:
            query: nGQL query string
            space: Graph space name
            params: Query parameters for parameterized queries

        Returns:
            ResultSet: Raw NebulaGraph result set
        """
        with self.session(space) as session:
            if params:
                # Parameterized query
                result = session.execute_parameter(query, params)
            else:
                result = session.execute(query)
            return result

    def execute_json(
        self, 
        query: str, 
        space: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Execute query and return JSON response.

        Args:
            query: nGQL query string
            space: Graph space name

        Returns:
            Dict: Parsed JSON result
        """
        with self.session(space) as session:
            json_str = session.execute_json(query)
            return json.loads(json_str)

    def health_check(self) -> Dict[str, Any]:
        """Perform health check on NebulaGraph connection"""
        try:
            with self.session() as session:
                result = session.execute("SHOW HOSTS")
                is_ok = result.is_succeeded()

                return {
                    "healthy": is_ok,
                    "error": None if is_ok else result.error_msg(),
                    "pool_stats": self.pool_stats,
                    "timestamp": datetime.now().isoformat()
                }
        except Exception as e:
            return {
                "healthy": False,
                "error": str(e),
                "pool_stats": self.pool_stats,
                "timestamp": datetime.now().isoformat()
            }


# Global client instance
nebula_client = NebulaClient()
