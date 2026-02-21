#!/usr/bin/env bash
#
# Pre-commit hook to validate license headers in C# and Swift files
# Checks staged .cs (GPL v3) and .swift (short header) for proper license headers
#
# To install: git config core.hooksPath .githooks
# To bypass: git commit --no-verify
#

set -e

# Colors for output
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Expected header components
CURRENT_YEAR=$(date +%Y)
EXPECTED_PROJECT="XerahS - The Avalonia UI implementation of ShareX"
EXPECTED_COPYRIGHT="Copyright (c) 2007-$CURRENT_YEAR ShareX Team"
EXPECTED_GPL_START="This program is free software"
EXPECTED_SWIFT_PROJECT="XerahS Mobile (Swift)"

# Get list of staged C# and Swift files
STAGED_CS_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)
STAGED_SWIFT_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.swift$' || true)

VIOLATIONS=0
VIOLATION_FILES=()

# --- C# files ---
if [ -n "$STAGED_CS_FILES" ]; then
    echo "Checking license headers in staged C# files..."
    for FILE in $STAGED_CS_FILES; do
        if [ ! -f "$FILE" ]; then continue; fi
        HEADER=$(head -n 30 "$FILE")
        MISSING=()
        echo "$HEADER" | grep -q "$EXPECTED_PROJECT" || MISSING+=("project name")
        echo "$HEADER" | grep -q "$EXPECTED_COPYRIGHT" || MISSING+=("copyright year $CURRENT_YEAR")
        echo "$HEADER" | grep -q "$EXPECTED_GPL_START" || MISSING+=("GPL v3 license text")
        echo "$HEADER" | grep -q "#region License Information" || MISSING+=("#region License Information tag")
        if [ ${#MISSING[@]} -gt 0 ]; then
            VIOLATIONS=$((VIOLATIONS + 1))
            VIOLATION_FILES+=("$FILE")
            echo -e "${RED}FAIL: $FILE${NC}"
            echo -e "  Missing: ${MISSING[*]}"
        fi
    done
fi

# --- Swift files ---
if [ -n "$STAGED_SWIFT_FILES" ]; then
    echo "Checking license headers in staged Swift files..."
    for FILE in $STAGED_SWIFT_FILES; do
        if [ ! -f "$FILE" ]; then continue; fi
        HEADER=$(head -n 20 "$FILE")
        MISSING=()
        echo "$HEADER" | grep -qE "Copyright \(c\) 2007-$CURRENT_YEAR ShareX Team\.?" || MISSING+=("copyright year $CURRENT_YEAR")
        echo "$HEADER" | grep -q "$EXPECTED_SWIFT_PROJECT" || MISSING+=("XerahS Mobile (Swift)")
        if [ ${#MISSING[@]} -gt 0 ]; then
            VIOLATIONS=$((VIOLATIONS + 1))
            VIOLATION_FILES+=("$FILE")
            echo -e "${RED}FAIL: $FILE${NC}"
            echo -e "  Missing: ${MISSING[*]}"
        fi
    done
fi

if [ -z "$STAGED_CS_FILES" ] && [ -z "$STAGED_SWIFT_FILES" ]; then
    echo -e "${GREEN}OK: No C# or Swift files to check${NC}"
    exit 0
fi

if [ $VIOLATIONS -gt 0 ]; then
    echo ""
    echo -e "${RED}==============================================================${NC}"
    echo -e "${RED}  LICENSE HEADER VALIDATION FAILED${NC}"
    echo -e "${RED}==============================================================${NC}"
    echo ""
    echo -e "${YELLOW}$VIOLATIONS file(s) have incorrect or missing license headers:${NC}"
    for FILE in "${VIOLATION_FILES[@]}"; do
        echo -e "  ${YELLOW}->${NC} $FILE"
    done
    echo ""
    echo "C#: Use #region License Information (GPL v3) with full GPL text. See developers/guidelines/CODING_STANDARDS.md"
    echo "Swift: Use short header with 'XerahS Mobile (Swift)' and 'Copyright (c) 2007-$CURRENT_YEAR ShareX Team.'"
    echo ""
    echo -e "${YELLOW}To bypass this check (NOT RECOMMENDED):${NC}"
    echo "  git commit --no-verify"
    echo ""
    exit 1
fi

CS_COUNT=0
for _ in $STAGED_CS_FILES; do CS_COUNT=$((CS_COUNT + 1)); done
SWIFT_COUNT=0
for _ in $STAGED_SWIFT_FILES; do SWIFT_COUNT=$((SWIFT_COUNT + 1)); done
echo -e "${GREEN}OK: All staged C# and Swift files have valid license headers (C#: $CS_COUNT, Swift: $SWIFT_COUNT)${NC}"
exit 0