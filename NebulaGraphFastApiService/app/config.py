"""Application configuration loaded from environment / .env."""
from functools import lru_cache
from typing import List, Optional

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env", env_file_encoding="utf-8", extra="ignore"
    )

    # Nebula connection
    nebula_host: str = Field("127.0.0.1", alias="NEBULA_HOST")
    nebula_port: int = Field(9669, alias="NEBULA_PORT")
    nebula_username: str = Field("root", alias="NEBULA_USERNAME")
    nebula_password: str = Field("nebula", alias="NEBULA_PASSWORD")
    nebula_default_space: str = Field("", alias="NEBULA_DEFAULT_SPACE")

    # Connection pool
    nebula_pool_size: int = Field(20, alias="NEBULA_POOL_SIZE")
    nebula_timeout: int = Field(10000, alias="NEBULA_TIMEOUT")
    nebula_interval_idle: int = Field(30000, alias="NEBULA_INTERVAL_IDLE")
    nebula_time_wait: int = Field(2000, alias="NEBULA_TIME_WAIT")

    # API server
    api_host: str = Field("0.0.0.0", alias="API_HOST")
    api_port: int = Field(8000, alias="API_PORT")
    api_log_level: str = Field("info", alias="API_LOG_LEVEL")
    api_reload: bool = Field(False, alias="API_RELOAD")

    # Optional simple bearer token guard
    api_token: Optional[str] = Field(None, alias="API_TOKEN")

    @property
    def nebula_endpoints(self) -> List[tuple]:
        return [(self.nebula_host, self.nebula_port)]


@lru_cache
def get_settings() -> Settings:
    return Settings()


settings = get_settings()
