"""
NebulaGraph FastAPI Service Configuration
"""
from pydantic_settings import BaseSettings
from typing import List, Tuple


class Settings(BaseSettings):
    """Application settings loaded from environment variables"""

    # FastAPI Settings
    APP_NAME: str = "NebulaGraph API Service"
    APP_VERSION: str = "1.0.0"
    DEBUG: bool = False
    HOST: str = "0.0.0.0"
    PORT: int = 8000

    # NebulaGraph Settings
    NEBULA_HOSTS: str = "127.0.0.1:9669"  # Comma-separated: host1:port1,host2:port2
    NEBULA_USER: str = "root"
    NEBULA_PASSWORD: str = "nebula"
    NEBULA_SPACE: str = ""

    # Connection Pool Settings
    NEBULA_MIN_CONN_POOL_SIZE: int = 1
    NEBULA_MAX_CONN_POOL_SIZE: int = 10
    NEBULA_TIMEOUT: int = 0  # 0 means no timeout
    NEBULA_IDLE_TIME: int = 0
    NEBULA_INTERVAL_CHECK: int = -1

    # CORS Settings
    CORS_ORIGINS: List[str] = ["*"]
    CORS_ALLOW_CREDENTIALS: bool = True
    CORS_ALLOW_METHODS: List[str] = ["*"]
    CORS_ALLOW_HEADERS: List[str] = ["*"]

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        case_sensitive = True

    @property
    def nebula_hosts(self) -> List[Tuple[str, int]]:
        """Parse NEBULA_HOSTS string into list of (host, port) tuples"""
        hosts = []
        for host_str in self.NEBULA_HOSTS.split(","):
            host_str = host_str.strip()
            if ":" in host_str:
                host, port = host_str.rsplit(":", 1)
                hosts.append((host.strip(), int(port.strip())))
            else:
                hosts.append((host_str, 9669))
        return hosts


settings = Settings()
