"""Optional simple bearer-token guard.

Enabled only when ``API_TOKEN`` is configured. Skips ``/healthz`` and the
OpenAPI docs so liveness probes and the docs UI remain reachable.
"""
import secrets

from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from .config import settings

_bearer = HTTPBearer(auto_error=False)


async def verify_token(
    request: Request,
    credentials: HTTPAuthorizationCredentials = Depends(_bearer),
):
    if not settings.api_token:
        return None  # guard disabled
    if request.url.path in {"/", "/healthz", "/docs", "/openapi.json", "/redoc"}:
        return None
    if credentials is None or not secrets.compare_digest(
        credentials.credentials or "", settings.api_token
    ):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing bearer token",
        )
    return credentials.credentials
