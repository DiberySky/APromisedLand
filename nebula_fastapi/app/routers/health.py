"""
Health Check and Monitoring Router
"""
from fastapi import APIRouter, Depends
from datetime import datetime

from app.dependencies import get_nebula_client
from app.models.schemas import HealthResponse
from app.config import settings
from app.services.nebula_client import nebula_client

router = APIRouter(prefix="/health", tags=["Health & Monitoring"])


@router.get("", response_model=HealthResponse, summary="Service Health Check")
async def health_check():
    """
    Comprehensive health check including NebulaGraph connectivity.

    Returns service status, version, and database connection info.
    """
    nebula_health = nebula_client.health_check()

    return HealthResponse(
        status="healthy" if nebula_health["healthy"] else "degraded",
        version=settings.APP_VERSION,
        nebula_connected=nebula_health["healthy"],
        nebula_hosts=[f"{h[0]}:{h[1]}" for h in settings.nebula_hosts],
        timestamp=datetime.now().isoformat()
    )


@router.get("/nebula", summary="NebulaGraph Health Details")
async def nebula_health():
    """Detailed NebulaGraph cluster health information"""
    return nebula_client.health_check()


@router.get("/pool", summary="Connection Pool Statistics")
async def pool_stats():
    """Get connection pool usage statistics"""
    return nebula_client.pool_stats
