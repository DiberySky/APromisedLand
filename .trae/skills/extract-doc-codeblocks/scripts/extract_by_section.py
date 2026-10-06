#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
按「## 章节」逐字提取指定序号的 fenced 代码块，覆盖写入目标文件，
并读回字节比对。适用于新版设计文档：
  - 章节标题为中文数字「## 三、`Name.cs`（改造）」或「## 3.1 `Name.cs`」
  - 目标路径写在章节正文「路径：`...`」里，标题不含路径
  - 一个章节内可能有多个 fenced 块（如 razor + css），按块序号选取

保真原则：
  - 只做 fenced 边界切片，保留原始换行（write_bytes，不做 newline 规范化）
  - 写入后立即读回字节比对，MISMATCH 时退出码非零

用法（可重复 --map）：
  python extract_by_section.py "<文档>" --root "d:\\APromisedLand" \
      --map "三|0|TreeGraph.Blazor\\Components\\Pages\\X.razor" \
      --map "六|1|TreeGraph.Blazor.Shared\\Responsive\\X.razor.css" \
      [--dry-run]

map 格式：<章节键>|<块序号0基>|<相对项目根的目标路径>
章节键：中文数字（三）或 N.N（1.1），与标题一致即可，忽略反引号文件名。

退出码：0 全部成功；1 提取/比对失败；2 文档或章节定位失败
"""
import argparse
import re
import sys
from pathlib import Path

CN_NUM = "一二三四五六七八九十"
SEC_RE = re.compile(rf"^##\s+(?:([{CN_NUM}]+)、|(\d+\.\d+)(?:\s|`|$))")


def parse_sections(lines):
    """返回 [(key, start_idx)]，章节边界为下一个 ## 标题。"""
    secs = []
    for i, ln in enumerate(lines):
        m = SEC_RE.match(ln)
        if m:
            secs.append((m.group(1) or m.group(2), i))
    return secs


def section_bounds(secs, key):
    for i, (k, start) in enumerate(secs):
        if k == key:
            end = secs[i + 1][1] if i + 1 < len(secs) else None
            return start, end
    return None, None


def nth_fence(lines, start, end, block_idx):
    """取章节内第 block_idx（0 基）个 fenced 块的精确文本（含原始换行）。"""
    blocks, cur, capturing = [], [], False
    span = lines[start:end]
    for ln in span:
        stripped = ln.lstrip()
        if not capturing:
            if stripped.startswith("```"):
                capturing = True
                cur = []
            continue
        if stripped.startswith("```"):
            blocks.append("".join(cur))
            capturing = False
            cur = []
            continue
        cur.append(ln)
    if block_idx < 0 or block_idx >= len(blocks):
        return None, len(blocks)
    return blocks[block_idx], len(blocks)


def main():
    ap = argparse.ArgumentParser(description="按章节序号逐字提取 fenced 代码块并落盘")
    ap.add_argument("doc", help="设计文档 txt 完整路径（可含 # 与空格）")
    ap.add_argument("--root", required=True, help="项目根目录")
    ap.add_argument("--map", action="append", default=[],
                    help="<章节键>|<块序号>|<相对路径>，可重复")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    doc = Path(args.doc)
    if not doc.is_file():
        print(f"[ERROR] 文档不存在: {doc}", file=sys.stderr)
        sys.exit(2)

    root = Path(args.root)
    lines = doc.read_text(encoding="utf-8").splitlines(keepends=True)
    secs = parse_sections(lines)
    if not secs:
        print("[ERROR] 未找到「## 三、」或「## 1.1」式章节标题", file=sys.stderr)
        sys.exit(2)
    print(f"文档: {doc.name}；章节: {[k for k, _ in secs]}")

    rc = 0
    for item in args.map:
        parts = item.split("|", 2)
        if len(parts) != 3:
            print(f"[ERROR] --map 格式应为 键|序号|路径：{item}", file=sys.stderr)
            rc = 1
            continue
        key, idx_s, rel = parts[0].strip(), parts[1].strip(), parts[2].strip()
        try:
            idx = int(idx_s)
        except ValueError:
            print(f"[ERROR] 块序号必须是数字：{item}", file=sys.stderr)
            rc = 1
            continue

        start, end = section_bounds(secs, key)
        if start is None:
            print(f"[ERROR] 章节不存在: {key}", file=sys.stderr)
            rc = 1
            continue

        content, total = nth_fence(lines, start + 1, end, idx)
        if content is None:
            print(f"[ERROR] 章节 {key} 只有 {total} 个块，取不到 #{idx}", file=sys.stderr)
            rc = 1
            continue

        target = root / rel.replace("/", "\\")
        data = content.encode("utf-8")
        if args.dry_run:
            print(f"[DRY  ] {key}#{idx} -> {rel} ({len(data)} 字节)")
            continue

        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)  # newline="" 语义：逐字节写入，不规范化换行
        ok = target.read_bytes() == data
        if not ok:
            rc = 1
        print(f"[{'OK   ' if ok else 'MISMATCH'}] {key}#{idx} -> {rel} ({len(data)} 字节)")

    sys.exit(rc)


if __name__ == "__main__":
    main()
