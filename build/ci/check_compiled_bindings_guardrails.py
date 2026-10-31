#!/usr/bin/env python3

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


TEMPLATE_TAG_PATTERNS = (
    re.compile(r"<DataTemplate\b[^>]*>", re.MULTILINE | re.DOTALL),
    re.compile(r"<TreeDataTemplate\b[^>]*>", re.MULTILINE | re.DOTALL),
)


def find_violations(root: Path, include_globs: list[str]) -> list[str]:
    violations: list[str] = []

    for glob_pattern in include_globs:
        for file_path in sorted(root.glob(glob_pattern)):
            if not file_path.is_file():
                continue

            text = file_path.read_text(encoding="utf-8")
            for pattern in TEMPLATE_TAG_PATTERNS:
                for match in pattern.finditer(text):
                    tag = match.group(0)
                    if "x:DataType=" in tag:
                        continue

                    line = text.count("\n", 0, match.start()) + 1
                    relative = file_path.relative_to(root)
                    violations.append(f"{relative}:{line}: missing x:DataType on template tag")

    return violations


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Ensure Avalonia templates are compile-typed with x:DataType."
    )
    parser.add_argument(
        "--repo-root",
        default=".",
        help="Repository root path (defaults to current directory).",
    )
    parser.add_argument(
        "--include",
        action="append",
        help="Glob pattern(s) to scan instead of the default UI and ImageEditor scopes. Can be provided multiple times.",
    )
    args = parser.parse_args()
    if args.include is None:
        args.include = [
            "src/desktop/app/XerahS.UI/**/*.axaml",
            "ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/**/*.axaml",
        ]
    return args


def main() -> int:
    args = parse_args()
    root = Path(args.repo_root).resolve()
    violations = find_violations(root, args.include)

    if violations:
        print("Compiled-binding guardrail check failed.")
        print("Every DataTemplate/TreeDataTemplate must include x:DataType.")
        for violation in violations:
            print(f"- {violation}")
        return 1

    print("Compiled-binding guardrail check passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

