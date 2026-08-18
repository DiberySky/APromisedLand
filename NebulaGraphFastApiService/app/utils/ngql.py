"""Helpers for building nGQL fragments safely (literal formatting, vid escaping)."""
from typing import Any


def format_literal(value: Any) -> str:
    """Format a Python value as an nGQL literal.

    - ``None``           -> ``null``
    - ``bool``           -> ``true`` / ``false``
    - ``int`` / ``float``-> bare number
    - ``list``/``tuple`` -> ``[a, b, c]``
    - ``str`` starting with ``__raw:`` -> the suffix is emitted verbatim, letting
      callers pass expressions like ``date('2020-01-01')`` or ``datetime('...')``.
    - ``str``            -> single-quoted, escaped
    """
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return repr(value)
    if isinstance(value, str):
        if value.startswith("__raw:"):
            return value[len("__raw:"):]
        return "'" + value.replace("\\", "\\\\").replace("'", "\\'") + "'"
    if isinstance(value, (list, tuple)):
        return "[" + ", ".join(format_literal(v) for v in value) + "]"
    # Fallback: stringify as a quoted literal.
    s = str(value)
    return "'" + s.replace("\\", "\\\\").replace("'", "\\'") + "'"


def format_vid(vid: Any) -> str:
    """Format a vertex id: bare for ints, quoted for strings."""
    if isinstance(vid, bool):
        return str(int(vid))
    if isinstance(vid, int):
        return str(vid)
    # string vid
    return format_literal(vid)


def format_props(props: dict) -> str:
    """Format a property dict as ``key: value, ...`` for INSERT statements."""
    return ", ".join(f"{k}: {format_literal(v)}" for k, v in props.items())


def backtick_ident(name: str) -> str:
    """Wrap an identifier in backticks (nGQL style)."""
    return "`" + name + "`"
