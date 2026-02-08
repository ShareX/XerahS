#!/usr/bin/env python3
"""
License Header Compliance Audit Script
Checks all C# files for correct GPL v3 license header
"""

import os
import sys
from pathlib import Path

# Authoritative license header
EXPECTED_HEADER = """#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)"""

def check_file(filepath):
    """
    Check a single file for license compliance
    Returns: (status, issue_description, first_30_lines)
    status: COMPLIANT, MISSING, INCORRECT, MISPLACED
    """
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except Exception as e:
        return "ERROR", f"Cannot read file: {e}", []

    # Get first 30 lines
    first_30 = ''.join(lines[:30])

    # Check if file is empty
    if not lines:
        return "MISSING", "Empty file", first_30

    # Join all lines for easier searching
    content = ''.join(lines)

    # Check if license header exists at all
    if "#region License Information (GPL v3)" not in content:
        return "MISSING", "No license header found", first_30

    # Check if header is at the beginning (before any using statements or namespace)
    header_start_line = None
    for i, line in enumerate(lines):
        if "#region License Information (GPL v3)" in line:
            header_start_line = i
            break

    # Check if there's code before the header (excluding blank lines and comments)
    if header_start_line is not None:
        for i in range(header_start_line):
            line = lines[i].strip()
            if line and not line.startswith('//'):
                return "MISPLACED", f"Code found before license header at line {i+1}: {line}", first_30

    # Extract the actual header from the file
    if header_start_line is not None:
        header_end_line = None
        for i in range(header_start_line + 1, min(len(lines), header_start_line + 30)):
            if "#endregion License Information (GPL v3)" in lines[i]:
                header_end_line = i
                break

        if header_end_line is None:
            return "INCORRECT", "License header region not properly closed", first_30

        actual_header = ''.join(lines[header_start_line:header_end_line+1]).rstrip()

        # Compare with expected header
        if actual_header == EXPECTED_HEADER:
            return "COMPLIANT", "", first_30
        else:
            # Try to identify specific differences
            issues = []
            if "2025" in actual_header and "2026" not in actual_header:
                issues.append("Uses 2025 instead of 2026")
            if "ShareX.Avalonia" in actual_header:
                issues.append("Uses 'ShareX.Avalonia' instead of 'XerahS'")
            if "ShareX -" in actual_header and "XerahS -" not in actual_header:
                issues.append("First line doesn't use 'XerahS' project name")
            if not issues:
                issues.append("Header text doesn't match exactly")

            return "INCORRECT", "; ".join(issues), first_30

    return "MISSING", "Could not find license header", first_30

def main():
    # Read the file list
    list_file = sys.argv[1] if len(sys.argv) > 1 else "cs_files_list.txt"

    with open(list_file, 'r') as f:
        files = [line.strip() for line in f if line.strip()]

    # Track statistics
    compliant = []
    missing = []
    incorrect = []
    misplaced = []
    errors = []

    # Check each file
    for filepath in files:
        status, issue, first_30 = check_file(filepath)

        file_info = {
            'path': filepath,
            'issue': issue,
            'preview': first_30
        }

        if status == "COMPLIANT":
            compliant.append(file_info)
        elif status == "MISSING":
            missing.append(file_info)
        elif status == "INCORRECT":
            incorrect.append(file_info)
        elif status == "MISPLACED":
            misplaced.append(file_info)
        else:
            errors.append(file_info)

    # Output results
    total = len(files)
    print(f"=== LICENSE HEADER COMPLIANCE AUDIT ===")
    print(f"\nTotal C# files scanned: {total}")
    print(f"Compliant: {len(compliant)}")
    print(f"Non-compliant: {total - len(compliant)}")
    print(f"  - Missing: {len(missing)}")
    print(f"  - Incorrect: {len(incorrect)}")
    print(f"  - Misplaced: {len(misplaced)}")
    print(f"  - Errors: {len(errors)}")

    # Output non-compliant files
    if missing:
        print(f"\n\n=== MISSING LICENSE HEADER ({len(missing)} files) ===")
        for f in missing:
            print(f"\n{f['path']}")
            print(f"Issue: {f['issue']}")
            print(f"First 30 lines:\n{f['preview']}")
            print("-" * 80)

    if incorrect:
        print(f"\n\n=== INCORRECT LICENSE HEADER ({len(incorrect)} files) ===")
        for f in incorrect:
            print(f"\n{f['path']}")
            print(f"Issue: {f['issue']}")
            print(f"First 30 lines:\n{f['preview']}")
            print("-" * 80)

    if misplaced:
        print(f"\n\n=== MISPLACED LICENSE HEADER ({len(misplaced)} files) ===")
        for f in misplaced:
            print(f"\n{f['path']}")
            print(f"Issue: {f['issue']}")
            print(f"First 30 lines:\n{f['preview']}")
            print("-" * 80)

    if errors:
        print(f"\n\n=== ERRORS ({len(errors)} files) ===")
        for f in errors:
            print(f"\n{f['path']}")
            print(f"Issue: {f['issue']}")
            print("-" * 80)

if __name__ == "__main__":
    main()
