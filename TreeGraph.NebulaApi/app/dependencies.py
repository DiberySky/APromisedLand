"""
FastAPI Dependencies for NebulaGraph Service
"""
from fastapi import HTTPException, status

from app.services.nebula_client import nebula_client
from app.services.nebula_service import nebula_service


async def get_nebula_client():
    """Dependency to get NebulaGraph client instance"""
    if not nebula_client.is_connected:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="NebulaGraph connection pool not available"
        )
    return nebula_client


async def get_nebula_service():
    """Dependency to get NebulaGraph service instance"""
    if not nebula_client.is_connected:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="NebulaGraph connection pool not available"
        )
    return nebula_service
