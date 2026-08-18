"""Entry point: ``python run.py`` launches the uvicorn server."""
import uvicorn

from app.config import settings


def main() -> None:
    uvicorn.run(
        "app.main:app",
        host=settings.api_host,
        port=settings.api_port,
        log_level=settings.api_log_level,
        reload=settings.api_reload,
    )


if __name__ == "__main__":
    main()
