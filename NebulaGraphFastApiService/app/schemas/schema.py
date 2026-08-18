"""Tag / Edge / Index schema request/response models and DDL builders."""
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator, model_validator

# Nebula scalar types we recognise for normalisation.
_SCALAR_TYPES = {
    "INT8", "INT16", "INT32", "INT64", "STRING", "BOOL", "DOUBLE", "FLOAT",
    "DATE", "TIME", "DATETIME", "TIMESTAMP", "GEOGRAPHY", "INT",
}


def _normalise_type(raw: str) -> str:
    """Normalise a property type string for nGQL DDL output."""
    s = raw.strip()
    low = s.lower().replace(" ", "").replace("_", "")
    # FIXED_STRING(n) / FIXEDSTRING(n) -> canonical FIXED_STRING(n)
    if low.startswith("fixedstring"):
        if "(" in s and ")" in s:
            inner = s[s.index("(") + 1: s.rindex(")")]
            return f"FIXED_STRING({inner.strip()})"
        return "FIXED_STRING(8)"
    up = s.upper()
    if up in {"INT", "BIGINT"}:
        return "INT64"
    if up in _SCALAR_TYPES:
        return up
    # Pass through unknown forms unchanged so advanced users can use
    # types the validator may not yet enumerate.
    return s


class PropertyDef(BaseModel):
    name: str = Field(..., min_length=1, max_length=128)
    type: str = Field(..., description="e.g. string, int64, double, bool, date, "
                                      "fixed_string(16), geography")
    nullable: bool = True
    default: Optional[str] = Field(None, description="Raw nGQL default expression")
    comment: Optional[str] = None

    def to_ddl(self) -> str:
        t = _normalise_type(self.type)
        parts = [f"{self.name} {t}"]
        parts.append("NULL" if self.nullable else "NOT NULL")
        if self.default is not None:
            parts.append(f"DEFAULT {self.default}")
        if self.comment:
            parts.append(f"COMMENT '{self.comment}'")
        return " ".join(parts)


class _SchemaCreateBase(BaseModel):
    space: str
    name: str = Field(..., min_length=1, max_length=128)
    properties: List[PropertyDef] = Field(default_factory=list)
    ttl_duration: Optional[int] = Field(None, ge=0, description="seconds, 0 disables TTL")
    ttl_col: Optional[str] = None
    comment: Optional[str] = None
    if_not_exists: bool = True

    @model_validator(mode="after")
    def _check_ttl(self):
        if self.ttl_duration and self.ttl_duration > 0 and not self.ttl_col:
            raise ValueError("ttl_col is required when ttl_duration > 0")
        return self


class TagCreateIn(_SchemaCreateBase):
    pass


class EdgeCreateIn(_SchemaCreateBase):
    pass


class AlterSchemaIn(BaseModel):
    space: str
    name: str
    kind: str = Field(..., description='"tag" or "edge"')
    add: List[PropertyDef] = Field(default_factory=list)
    change: List[PropertyDef] = Field(default_factory=list)
    drop: List[str] = Field(default_factory=list)
    ttl_duration: Optional[int] = Field(None, ge=0)
    ttl_col: Optional[str] = None
    comment: Optional[str] = None

    @field_validator("kind")
    @classmethod
    def _kind(cls, v: str) -> str:
        v = v.strip().lower()
        if v not in {"tag", "edge"}:
            raise ValueError("kind must be 'tag' or 'edge'")
        return v

    @model_validator(mode="after")
    def _require_change(self):
        if not (self.add or self.change or self.drop
                or self.ttl_duration is not None or self.comment is not None):
            raise ValueError("at least one alteration must be supplied")
        return self


class _IndexCreateBase(BaseModel):
    space: str
    name: str = Field(..., min_length=1, max_length=128)
    fields: List[str] = Field(..., min_length=1,
                              description="Property names, optionally with length "
                                          "e.g. ['name(64)']")
    if_not_exists: bool = True
    rebuild: bool = True


class TagIndexCreateIn(_IndexCreateBase):
    tag: str


class EdgeIndexCreateIn(_IndexCreateBase):
    edge: str


class FulltextIndexCreateIn(BaseModel):
    space: str
    name: str
    kind: str = Field(..., description='"tag" or "edge"')
    schema_name: str = Field(..., description="the tag or edge to index")
    fields: List[str] = Field(..., min_length=1)
    if_not_exists: bool = True

    @field_validator("kind")
    @classmethod
    def _kind(cls, v: str) -> str:
        v = v.strip().lower()
        if v not in {"tag", "edge"}:
            raise ValueError("kind must be 'tag' or 'edge'")
        return v
