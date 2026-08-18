"""Tag / Edge / Index schema management endpoints."""
from fastapi import APIRouter

from ..schemas.schema import (
    AlterSchemaIn,
    EdgeCreateIn,
    EdgeIndexCreateIn,
    FulltextIndexCreateIn,
    TagCreateIn,
    TagIndexCreateIn,
)
from ..services import schema_service
from ..utils.response import ok

router = APIRouter(tags=["schema"])

# ----------------------------------------------------------------------- #
# Tags
# ----------------------------------------------------------------------- #
@router.get("/spaces/{space}/tags", summary="List tags in a space")
def list_tags(space: str):
    return ok(schema_service.show_tags(space))


@router.post("/spaces/{space}/tags", summary="Create a tag")
def create_tag(space: str, body: TagCreateIn):
    body = body.model_copy(update={"space": space})
    return ok(schema_service.create_tag(body))


@router.get("/spaces/{space}/tags/{name}", summary="Describe a tag")
def desc_tag(space: str, name: str):
    return ok(schema_service.desc_tag(space, name))


@router.get("/spaces/{space}/tags/{name}/ddl", summary="Show CREATE TAG statement")
def show_create_tag(space: str, name: str):
    return ok({"ddl": schema_service.show_create_tag(space, name)})


@router.put("/spaces/{space}/tags/{name}", summary="Alter a tag")
def alter_tag(space: str, name: str, body: AlterSchemaIn):
    body = body.model_copy(update={"space": space, "name": name, "kind": "tag"})
    return ok(schema_service.alter_tag(body))


@router.delete("/spaces/{space}/tags/{name}", summary="Drop a tag")
def drop_tag(space: str, name: str, if_exists: bool = True):
    return ok(schema_service.drop_tag(space, name, if_exists=if_exists))


# ----------------------------------------------------------------------- #
# Edges
# ----------------------------------------------------------------------- #
@router.get("/spaces/{space}/edges", summary="List edges in a space")
def list_edges(space: str):
    return ok(schema_service.show_edges(space))


@router.post("/spaces/{space}/edges", summary="Create an edge type")
def create_edge(space: str, body: EdgeCreateIn):
    body = body.model_copy(update={"space": space})
    return ok(schema_service.create_edge(body))


@router.get("/spaces/{space}/edges/{name}", summary="Describe an edge type")
def desc_edge(space: str, name: str):
    return ok(schema_service.desc_edge(space, name))


@router.get("/spaces/{space}/edges/{name}/ddl", summary="Show CREATE EDGE statement")
def show_create_edge(space: str, name: str):
    return ok({"ddl": schema_service.show_create_edge(space, name)})


@router.put("/spaces/{space}/edges/{name}", summary="Alter an edge type")
def alter_edge(space: str, name: str, body: AlterSchemaIn):
    body = body.model_copy(update={"space": space, "name": name, "kind": "edge"})
    return ok(schema_service.alter_edge(body))


@router.delete("/spaces/{space}/edges/{name}", summary="Drop an edge type")
def drop_edge(space: str, name: str, if_exists: bool = True):
    return ok(schema_service.drop_edge(space, name, if_exists=if_exists))


# ----------------------------------------------------------------------- #
# Indexes
# ----------------------------------------------------------------------- #
@router.get("/spaces/{space}/indexes", summary="List all indexes in a space")
def list_indexes(space: str):
    return ok(schema_service.show_indexes(space))


@router.get("/spaces/{space}/indexes/tag", summary="List tag indexes")
def list_tag_indexes(space: str):
    return ok(schema_service.show_tag_indexes(space))


@router.get("/spaces/{space}/indexes/edge", summary="List edge indexes")
def list_edge_indexes(space: str):
    return ok(schema_service.show_edge_indexes(space))


@router.post("/spaces/{space}/indexes/tag", summary="Create a tag index")
def create_tag_index(space: str, body: TagIndexCreateIn):
    body = body.model_copy(update={"space": space})
    return ok(schema_service.create_tag_index(body))


@router.post("/spaces/{space}/indexes/edge", summary="Create an edge index")
def create_edge_index(space: str, body: EdgeIndexCreateIn):
    body = body.model_copy(update={"space": space})
    return ok(schema_service.create_edge_index(body))


@router.get("/spaces/{space}/indexes/tag/{name}", summary="Describe a tag index")
def desc_tag_index(space: str, name: str):
    return ok(schema_service.desc_tag_index(space, name))


@router.get("/spaces/{space}/indexes/edge/{name}", summary="Describe an edge index")
def desc_edge_index(space: str, name: str):
    return ok(schema_service.desc_edge_index(space, name))


@router.post("/spaces/{space}/indexes/{name}/rebuild", summary="Rebuild an index")
def rebuild_index(space: str, name: str, kind: str = "tag"):
    return ok(schema_service.rebuild_index(space, name, kind=kind))


@router.delete("/spaces/{space}/indexes/{name}", summary="Drop an index")
def drop_index(space: str, name: str, if_exists: bool = True):
    return ok(schema_service.drop_index(space, name, if_exists=if_exists))


# ----------------------------------------------------------------------- #
# Fulltext indexes
# ----------------------------------------------------------------------- #
@router.get("/fulltext-indexes", summary="List fulltext indexes")
def list_fulltext_indexes():
    return ok(schema_service.show_fulltext_indexes())


@router.post("/spaces/{space}/fulltext-indexes", summary="Create a fulltext index")
def create_fulltext_index(space: str, body: FulltextIndexCreateIn):
    body = body.model_copy(update={"space": space})
    return ok(schema_service.create_fulltext_index(body))


@router.delete("/fulltext-indexes/{name}", summary="Drop a fulltext index")
def drop_fulltext_index(name: str, if_exists: bool = True):
    return ok(schema_service.drop_fulltext_index(name, if_exists=if_exists))
