"""Domain-specific exception types for the nebula API service."""


class NebulaAPIError(Exception):
    """Base class for all service-layer errors."""

    def __init__(self, message: str, code: int = 1, status_code: int = 400):
        self.message = message
        self.code = code
        self.status_code = status_code
        super().__init__(message)


class ValidationError(NebulaAPIError):
    """Raised when request input fails validation beyond pydantic checks."""

    def __init__(self, message: str):
        super().__init__(message, code=1001, status_code=422)


class NotFoundError(NebulaAPIError):
    """Raised when a requested nebula resource does not exist."""

    def __init__(self, message: str):
        super().__init__(message, code=1002, status_code=404)


class NebulaExecutionError(NebulaAPIError):
    """Raised when nebula returns an execution error code."""

    def __init__(self, error_code: int, error_msg: str, statement: str = ""):
        self.error_code = error_code
        self.statement = statement
        super().__init__(
            error_msg,
            code=2000,
            status_code=_status_for_error_code(error_code),
        )


def _status_for_error_code(error_code: int) -> int:
    """Map nebula error codes to sensible HTTP status codes."""
    # Authentication
    if error_code == -1001:  # E_BAD_USERNAME_PASSWORD
        return 401
    # Not found family
    if error_code in (
        -5,   # E_SPACE_NOT_FOUND
        -6,   # E_TAG_NOT_FOUND
        -7,   # E_EDGE_NOT_FOUND
        -8,   # E_INDEX_NOT_FOUND
        -17,  # E_KEY_NOT_FOUND
        -18,  # E_USER_NOT_FOUND
    ):
        return 404
    # Conflict / already exists
    if error_code == -2002:  # E_EXISTED
        return 409
    # Syntax / semantic
    if error_code in (-1004, -1009):  # E_SYNTAX_ERROR, E_SEMANTIC_ERROR
        return 400
    # Permission
    if error_code == -1008:  # E_BAD_PERMISSION
        return 403
    return 400
