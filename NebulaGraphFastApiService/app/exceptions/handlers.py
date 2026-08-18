"""Global FastAPI exception handlers translating errors to the JSON envelope."""
import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from nebula3.Exception import (
    AuthFailedException,
    ClientServerIncompatibleException,
    ExecutionErrorException,
    IOErrorException,
    InValidHostname,
    NoValidSessionException,
    NotValidConnectionException,
    SessionException,
)

from ..database import NebulaError
from .exceptions import NebulaAPIError, NebulaExecutionError

logger = logging.getLogger("nebula_api.errors")

# Nebula client exception types that all indicate transient / connectivity
# problems and should surface as HTTP 503.
_CONNECTION_ERRORS = (
    IOErrorException,
    AuthFailedException,
    ClientServerIncompatibleException,
    NotValidConnectionException,
    NoValidSessionException,
    InValidHostname,
    SessionException,
    ExecutionErrorException,
)


def register_exception_handlers(app: FastAPI) -> None:

    @app.exception_handler(NebulaExecutionError)
    async def _nebula_exec(_: Request, exc: NebulaExecutionError):
        return JSONResponse(
            status_code=exc.status_code,
            content={
                "code": exc.code,
                "message": exc.message,
                "error_code": exc.error_code,
                "statement": exc.statement or None,
            },
        )

    @app.exception_handler(NebulaAPIError)
    async def _api_error(_: Request, exc: NebulaAPIError):
        return JSONResponse(
            status_code=exc.status_code,
            content={"code": exc.code, "message": exc.message},
        )

    @app.exception_handler(NebulaError)
    async def _raw_nebula(_: Request, exc: NebulaError):
        # Translate the low-level connection-layer error.
        api_exc = NebulaExecutionError(exc.error_code, exc.error_msg, exc.statement)
        return JSONResponse(
            status_code=api_exc.status_code,
            content={
                "code": api_exc.code,
                "message": api_exc.message,
                "error_code": api_exc.error_code,
                "statement": api_exc.statement or None,
            },
        )

    @app.exception_handler(RequestValidationError)
    async def _validation(_: Request, exc: RequestValidationError):
        return JSONResponse(
            status_code=422,
            content={
                "code": 1001,
                "message": "Request validation error",
                "details": exc.errors(),
            },
        )

    async def _nebula_conn(_: Request, exc: Exception):
        logger.exception("Nebula connection error")
        return JSONResponse(
            status_code=503,
            content={"code": 2001, "message": f"Nebula connection error: {exc}"},
        )

    for _exc_type in _CONNECTION_ERRORS:
        app.add_exception_handler(_exc_type, _nebula_conn)

    @app.exception_handler(Exception)
    async def _unhandled(_: Request, exc: Exception):
        logger.exception("Unhandled exception")
        return JSONResponse(
            status_code=500,
            content={"code": 5000, "message": f"Internal server error: {exc}"},
        )
