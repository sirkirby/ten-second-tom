#!/usr/bin/env bash
#
# cleanup-test-secrets.sh
# Cleanup script for orphaned test UserSecrets directories
#
# This script removes test UserSecrets directories that may be left behind
# when tests fail, timeout, or are interrupted (Ctrl+C).
#
# Usage:
#   .scripts/cleanup-test-secrets.sh [--dry-run] [--verbose]
#
# Options:
#   --dry-run    Show what would be deleted without actually deleting
#   --verbose    Show detailed output for each directory processed
#
# Examples:
#   .scripts/cleanup-test-secrets.sh                    # Delete all test secrets
#   .scripts/cleanup-test-secrets.sh --dry-run          # Preview what would be deleted
#   .scripts/cleanup-test-secrets.sh --dry-run --verbose # Preview with details

set -euo pipefail

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse command-line arguments
DRY_RUN=false
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--dry-run] [--verbose]"
            echo ""
            echo "Cleanup script for orphaned test UserSecrets directories"
            echo ""
            echo "Options:"
            echo "  --dry-run    Show what would be deleted without actually deleting"
            echo "  --verbose    Show detailed output for each directory processed"
            echo "  -h, --help   Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Error: Unknown option: $1${NC}" >&2
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Determine UserSecrets path based on OS
if [[ "$OSTYPE" == "darwin"* ]] || [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # macOS and Linux
    USER_SECRETS_DIR="$HOME/.microsoft/usersecrets"
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    # Windows (Git Bash)
    USER_SECRETS_DIR="$APPDATA/Microsoft/UserSecrets"
else
    echo -e "${RED}Error: Unsupported OS type: $OSTYPE${NC}" >&2
    exit 1
fi

# Verify UserSecrets directory exists
if [[ ! -d "$USER_SECRETS_DIR" ]]; then
    echo -e "${YELLOW}UserSecrets directory not found: $USER_SECRETS_DIR${NC}"
    echo "Nothing to clean up."
    exit 0
fi

# Find all test directories (both TenSecondTom-Test-* and tom-test-* patterns)
TEST_DIRS=()
while IFS= read -r -d '' dir; do
    TEST_DIRS+=("$dir")
done < <(find "$USER_SECRETS_DIR" -maxdepth 1 -type d \( -name "TenSecondTom-Test-*" -o -name "tom-test-*" \) -print0 2>/dev/null || true)

# Count directories
DIR_COUNT=${#TEST_DIRS[@]}

if [[ $DIR_COUNT -eq 0 ]]; then
    echo -e "${GREEN}No orphaned test directories found. All clean!${NC}"
    exit 0
fi

# Display header
echo -e "${BLUE}Ten Second Tom - Test Secrets Cleanup${NC}"
echo "=========================================="
echo ""
echo -e "Found ${YELLOW}$DIR_COUNT${NC} orphaned test director$([ $DIR_COUNT -eq 1 ] && echo "y" || echo "ies")"
echo ""

if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}DRY RUN MODE - Nothing will be deleted${NC}"
    echo ""
fi

# Process each directory
DELETED_COUNT=0
FAILED_COUNT=0

for dir in "${TEST_DIRS[@]}"; do
    DIR_NAME=$(basename "$dir")
    DIR_SIZE=$(du -sh "$dir" 2>/dev/null | cut -f1 || echo "unknown")

    if [[ "$VERBOSE" == true ]]; then
        echo -e "${BLUE}Processing:${NC} $DIR_NAME (${DIR_SIZE})"
    fi

    if [[ "$DRY_RUN" == true ]]; then
        echo "  [DRY RUN] Would delete: $dir"
        ((DELETED_COUNT++))
    else
        # Attempt deletion with retry logic
        RETRY_COUNT=0
        MAX_RETRIES=3
        DELETED=false

        while [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; do
            if rm -rf "$dir" 2>/dev/null; then
                if [[ "$VERBOSE" == true ]]; then
                    echo -e "  ${GREEN}✓${NC} Deleted successfully"
                fi
                ((DELETED_COUNT++))
                DELETED=true
                break
            else
                ((RETRY_COUNT++))
                if [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; then
                    if [[ "$VERBOSE" == true ]]; then
                        echo -e "  ${YELLOW}⚠${NC} Retry $RETRY_COUNT/$MAX_RETRIES after 100ms delay..."
                    fi
                    sleep 0.1
                fi
            fi
        done

        if [[ "$DELETED" == false ]]; then
            echo -e "  ${RED}✗${NC} Failed to delete: $DIR_NAME"
            ((FAILED_COUNT++))
        fi
    fi
done

# Display summary
echo ""
echo "=========================================="
echo -e "${BLUE}Summary${NC}"
echo "=========================================="

if [[ "$DRY_RUN" == true ]]; then
    echo -e "Would delete: ${YELLOW}$DELETED_COUNT${NC} director$([ $DELETED_COUNT -eq 1 ] && echo "y" || echo "ies")"
else
    echo -e "Successfully deleted: ${GREEN}$DELETED_COUNT${NC} director$([ $DELETED_COUNT -eq 1 ] && echo "y" || echo "ies")"

    if [[ $FAILED_COUNT -gt 0 ]]; then
        echo -e "Failed to delete: ${RED}$FAILED_COUNT${NC} director$([ $FAILED_COUNT -eq 1 ] && echo "y" || echo "ies")"
        echo ""
        echo -e "${YELLOW}Tip:${NC} Failed directories may be locked. Try:"
        echo "  1. Close any running test processes"
        echo "  2. Re-run this script"
        echo "  3. Manually delete remaining directories if needed"
    fi
fi

echo ""

# Exit with appropriate code
if [[ $FAILED_COUNT -gt 0 ]]; then
    exit 1
else
    exit 0
fi
