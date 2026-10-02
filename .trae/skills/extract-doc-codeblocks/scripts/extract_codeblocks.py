#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从设计文档中逐字提取「## 文件 N / M：`相对路径`」章节下的第一个 fenced 代码块，
覆盖写入项目对应文件，并读回比对验证。

保真原则（与项目约定一致）：
  - 只做 fenced 边界切片，不合并换行、不改标点、不规范化任何字符
  - 写入后立即读回与内存内容比对，MISMATCH 时进程返回非零

用法：
  python extract_codeblocks.py "<文档路径>" [--root D:\\APromisedLand] [--only 1,2,4] [--dry-run]

退出码：0 全部成功；1 有提取/比对失败；2 文档中找不到任何文件章节
"""
import argparse
import re
import sys
from pathlib import Path

# 兼容中英文冒号；路径在反引号内
HEADER_RE = re.compile(r"^##\s*文件\s*(\d+)\s*/\s*\d+\s*[：:]\s*`([^`]+)`")


def parse_sections(lines):
    sections = []
    for i, ln in enumerate(lines):
        m = HEADER_RE.match(ln)
        if m:
            sections.append((int(m.group(1)), m.group(2), i))
    return sections


def extract_block(lines, hdr, end):
    """返回 (代码行列表, 起始围栏字符串)；章节内无 fenced 块时为 (None, None)。"""
    block, fence, capturing = [], None, False
    for j in range(hdr + 1, end):
        ln = lines[j]
        if not capturing:
            if ln.startswith("```"):
                capturing = True
                fence = ln
            continue
        if ln.rstrip() == "```":
            break
        block.append(ln)
    return (block, fence) if block else (None, None)


def main():
    ap = argparse.ArgumentParser(description="逐字提取设计文档中的代码文件并覆盖落盘")
    ap.add_argument("doc", help="设计文档 txt 的完整路径（可含 # 和空格）")
    ap.add_argument("--root", default=None, help="项目根目录，缺省用当前工作目录")
    ap.add_argument("--only", default=None,
                    help="逗号分隔的文件序号白名单，如 1,2,4；缺省提取全部")
    ap.add_argument("--dry-run", action="store_true", help="只报告不写入")
    args = ap.parse_args()

    doc = Path(args.doc)
    if not doc.is_file():
        print(f"[ERROR] 文档不存在: {doc}", file=sys.stderr)
        sys.exit(2)

    root = Path(args.root) if args.root else Path.cwd()
    want = None
    if args.only:
        want = {int(x) for x in args.only.split(",") if x.strip()}

    lines = doc.read_text(encoding="utf-8").splitlines(keepends=False)
    sections = parse_sections(lines)
    if not sections:
        print("[ERROR] 未找到「## 文件 N / M：`path`」章节标题", file=sys.stderr)
        sys.exit(2)

    print(f"文档: {doc.name}；共 {len(sections)} 个文件章节；根目录: {root}")
    rc = 0

    for idx, (num, rel, hdr) in enumerate(sections):
        end = sections[idx + 1][2] if idx + 1 < len(sections) else len(lines)

        if want is not None and num not in want:
            print(f"[SKIP ] 文件{num}: {rel}")
            continue

        block, fence = extract_block(lines, hdr, end)
        if block is None:
            print(f"[WARN ] 文件{num}: {rel} —— 章节内未找到 fenced 代码块",
                  file=sys.stderr)
            rc = 1
            continue

        # 统一为相对项目根的 Windows 路径
        target = root / rel.replace("/", "\\")
        content = "\n".join(block) + "\n"
        size = len(content.encode("utf-8"))

        if args.dry_run:
            print(f"[DRY  ] 文件{num}: {rel} "
                  f"({size} 字节, {len(block)} 行, 围栏={fence})")
            continue

        if not target.parent.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")
        ok = target.read_text(encoding="utf-8") == content
        if not ok:
            rc = 1
        print(f"[{'OK   ' if ok else 'MISMATCH'}] 文件{num}: {rel} "
              f"({size} 字节, {len(block)} 行, 围栏={fence})")

    sys.exit(rc)


if __name__ == "__main__":
    main()
