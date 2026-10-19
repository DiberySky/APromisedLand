"""
Test suite for Graph API endpoints
"""
import pytest
from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)


class TestHealthEndpoints:
    """Test health check endpoints"""

    def test_health_check(self):
        response = client.get("/api/v1/health")
        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert "version" in data

    def test_pool_stats(self):
        response = client.get("/api/v1/health/pool")
        assert response.status_code == 200
        data = response.json()
        assert "status" in data


class TestQueryEndpoints:
    """Test query execution endpoints"""

    def test_query_spaces(self):
        response = client.get("/api/v1/graph/spaces")
        assert response.status_code in [200, 503]  # 503 if Nebula not connected

    def test_query_execution(self):
        payload = {
            "query": "SHOW SPACES"
        }
        response = client.post("/api/v1/graph/query", json=payload)
        assert response.status_code in [200, 503]

    def test_invalid_query(self):
        payload = {
            "query": "INVALID NEBULA SYNTAX"
        }
        response = client.post("/api/v1/graph/query", json=payload)
        # Should return 200 with success=false, or 503
        assert response.status_code in [200, 503]
        if response.status_code == 200:
            data = response.json()
            assert data["success"] is False


class TestSchemaEndpoints:
    """Test schema management endpoints"""

    def test_list_tags(self):
        response = client.get("/api/v1/graph/tags")
        assert response.status_code in [200, 503]

    def test_list_edges(self):
        response = client.get("/api/v1/graph/edges")
        assert response.status_code in [200, 503]
