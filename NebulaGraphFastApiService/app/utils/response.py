"""Standard JSON response envelope for all API endpoints."""
from typing import Any, Optional

from fastapi.responses import JSONResponse


def ok(data: Any = None, message: str = "ok", meta: Optional[dict] = None) -> dict:
    """Successful response payload."""
    body: dict = {"code": 0, "message": message, "data": data}
    if meta:
        body["meta"] = meta
    return body


def fail(
    message: str,
    code: int = 1,
    error_code: Optional[int] = None,
    data: Any = None,
    status_code: int = 400,
) -> JSONResponse:
    """Error response payload wrapped in a JSONResponse."""
    body: dict = {"code": code, "message": message}
    if error_code is not None:
        body["error_code"] = error_code
    if data is not None:
        body["data"] = data
    return JSONResponse(status_code=status_code, content=body)


def accepted(message: str = "job submitted", data: Any = None) -> dict:
    """Response for asynchronous / accepted operations."""
    return {"code": 0, "message": message, "data": data}
