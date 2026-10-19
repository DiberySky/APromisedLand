# NebulaGraph FastAPI Web Service

A production-ready Web API service that wraps [nebula-python](https://github.com/vesoft-inc/nebula-python) client with FastAPI, providing RESTful endpoints for all NebulaGraph operations.

## Features

- **Complete nGQL Support**: Execute any nGQL query via REST API
- **Schema Management**: List and describe spaces, tags, and edge types
- **CRUD Operations**: Insert vertices and edges with type-safe models
- **Bulk Operations**: Batch insert with configurable batch sizes
- **Connection Pooling**: Efficient session management using nebula3-python ConnectionPool
- **Health Monitoring**: Connection pool stats and cluster health checks
- **Pydantic Models**: Full request/response validation and OpenAPI docs
- **Docker Ready**: Includes Dockerfile and docker-compose for easy deployment

## Quick Start

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

### 2. Configure Environment

```bash
cp .env.example .env
# Edit .env with your NebulaGraph connection details
```

### 3. Run Service

```bash
# Development (with auto-reload)
python app/main.py

# Production
uvicorn app.main:app --host 0.0.0.0 --port 8000 --workers 4
```

### 4. Access API Documentation

- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

## API Endpoints

### Health & Monitoring
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/health` | Service health check |
| GET | `/api/v1/health/nebula` | NebulaGraph cluster health |
| GET | `/api/v1/health/pool` | Connection pool statistics |

### Graph Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/graph/query` | Execute nGQL query |
| POST | `/api/v1/graph/query/json` | Execute query (raw JSON) |
| GET | `/api/v1/graph/spaces` | List graph spaces |
| GET | `/api/v1/graph/spaces/{name}` | Get space details |
| GET | `/api/v1/graph/tags` | List tags |
| GET | `/api/v1/graph/tags/{name}` | Get tag schema |
| GET | `/api/v1/graph/edges` | List edge types |
| GET | `/api/v1/graph/edges/{name}` | Get edge schema |
| POST | `/api/v1/graph/vertices` | Insert vertex |
| POST | `/api/v1/graph/edges` | Insert edge |
| POST | `/api/v1/graph/bulk` | Bulk insert |

## Docker Deployment

```bash
# Build and run with docker-compose (includes NebulaGraph)
docker-compose up -d

# Or run API service only
docker build -t nebula-fastapi .
docker run -p 8000:8000 -e NEBULA_HOSTS=host:9669 nebula-fastapi
```

## Architecture

```
Client → FastAPI Router → NebulaService → NebulaClient → ConnectionPool → NebulaGraph
```

- **NebulaClient**: Singleton managing ConnectionPool lifecycle and sessions
- **NebulaService**: Business logic layer with result transformation
- **Routers**: FastAPI endpoints with dependency injection
- **Pydantic Models**: Type-safe request/response schemas

## Configuration

All settings via environment variables (see `.env.example`):

| Variable | Default | Description |
|----------|---------|-------------|
| `NEBULA_HOSTS` | `127.0.0.1:9669` | Comma-separated GraphD addresses |
| `NEBULA_USER` | `root` | NebulaGraph username |
| `NEBULA_PASSWORD` | `nebula` | NebulaGraph password |
| `NEBULA_SPACE` | `` | Default graph space |
| `NEBULA_MAX_CONN_POOL_SIZE` | `10` | Max connections in pool |
| `NEBULA_MIN_CONN_POOL_SIZE` | `1` | Min connections in pool |

## Testing

```bash
pytest tests/ -v
```

## License

MIT
