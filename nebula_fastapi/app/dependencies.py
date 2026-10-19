"""
FastAPI Dependencies for NebulaGraph Service
"""
from fastapi import Request, HTTPException, status
from typing import Optional

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


def verify_space_access(space: Optional[str] = None):
    """
    Dependency factory to verify space access.
    Can be extended with RBAC logic.
    """
    async def _verify(request: Request):
        # Add custom authorization logic here
        # e.g., check if user has access to the space
        pass
    return _verify
