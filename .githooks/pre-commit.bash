#!/usr/bin/env bash
#
# Pre-commit hook to validate GPL v3 license headers in C#, Swift, and Kotlin files
# All require full GPL v3 license text. See developers/guidelines/CODING_STANDARDS.md
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

# Get list of staged C#, Swift, and Kotlin files
STAGED_CS_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)
STAGED_SWIFT_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.swift$' || true)
STAGED_KT_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.kt$' || true)

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

# --- Swift files (full GPL v3 text required) ---
if [ -n "$STAGED_SWIFT_FILES" ]; then
    echo "Checking license headers in staged Swift files..."
    for FILE in $STAGED_SWIFT_FILES; do
        if [ ! -f "$FILE" ]; then continue; fi
        HEADER=$(head -n 35 "$FILE")
        MISSING=()
        echo "$HEADER" | grep -qE "Copyright \(c\) 2007-$CURRENT_YEAR ShareX Team\.?" || MISSING+=("copyright year $CURRENT_YEAR")
        echo "$HEADER" | grep -qE "XerahS Mobile \(Swift\)|XerahS Share Extension" || MISSING+=("XerahS Mobile (Swift) or XerahS Share Extension")
        echo "$HEADER" | grep -q "$EXPECTED_GPL_START" || MISSING+=("GPL v3 license text")
        if [ ${#MISSING[@]} -gt 0 ]; then
            VIOLATIONS=$((VIOLATIONS + 1))
            VIOLATION_FILES+=("$FILE")
            echo -e "${RED}FAIL: $FILE${NC}"
            echo -e "  Missing: ${MISSING[*]}"
        fi
    done
fi

# --- Kotlin files (full GPL v3 text required) ---
if [ -n "$STAGED_KT_FILES" ]; then
    echo "Checking license headers in staged Kotlin files..."
    for FILE in $STAGED_KT_FILES; do
        if [ ! -f "$FILE" ]; then continue; fi
        HEADER=$(head -n 35 "$FILE")
        MISSING=()
        echo "$HEADER" | grep -q "$EXPECTED_PROJECT" || MISSING+=("project name")
        echo "$HEADER" | grep -q "$EXPECTED_COPYRIGHT" || MISSING+=("copyright year $CURRENT_YEAR")
        echo "$HEADER" | grep -q "$EXPECTED_GPL_START" || MISSING+=("GPL v3 license text")
        if [ ${#MISSING[@]} -gt 0 ]; then
            VIOLATIONS=$((VIOLATIONS + 1))
            VIOLATION_FILES+=("$FILE")
            echo -e "${RED}FAIL: $FILE${NC}"
            echo -e "  Missing: ${MISSING[*]}"
        fi
    done
fi

if [ -z "$STAGED_CS_FILES" ] && [ -z "$STAGED_SWIFT_FILES" ] && [ -z "$STAGED_KT_FILES" ]; then
    echo -e "${GREEN}OK: No C#, Swift, or Kotlin files to check${NC}"
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
    echo "C# / Swift / Kotlin: All require full GPL v3 license text. See developers/guidelines/CODING_STANDARDS.md"
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
KT_COUNT=0
for _ in $STAGED_KT_FILES; do KT_COUNT=$((KT_COUNT + 1)); done
echo -e "${GREEN}OK: All staged C#, Swift, and Kotlin files have valid GPL v3 license headers (C#: $CS_COUNT, Swift: $SWIFT_COUNT, Kotlin: $KT_COUNT)${NC}"
exit 0