#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

NAMESPACE="longevity"
DB_USER="longevity"
DB_NAME="longevity"
SECRET_NAME="postgres-credentials"
REPO_ROOT="$(cd "$(dirname "$0")/../../../" && pwd)"
BACKUP_DIR="$REPO_ROOT/backups/postgres"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/longevity_backup_$TIMESTAMP.sql"

echo -e "${YELLOW}PostgreSQL Backup Script${NC}"
echo "================================"
echo ""

# Step 1: Get the password
echo -e "${YELLOW}Step 1: Retrieving PostgreSQL password...${NC}"
DB_PASSWORD=$(kubectl get secret "$SECRET_NAME" -n "$NAMESPACE" -o jsonpath='{.data.password}' | base64 -d 2>/dev/null)
if [ -z "$DB_PASSWORD" ]; then
    echo -e "${RED}Error: Could not retrieve PostgreSQL password${NC}"
    echo "Make sure you're connected to the cluster and the secret exists."
    exit 1
fi
echo -e "${GREEN}✓ Password retrieved${NC}"
echo ""

# Step 2: Find the pod
echo -e "${YELLOW}Step 2: Finding PostgreSQL pod...${NC}"
POD_NAME=$(kubectl get pods -n "$NAMESPACE" -o name | grep postgres-deployment | head -1 | cut -d'/' -f2)
if [ -z "$POD_NAME" ]; then
    echo -e "${RED}Error: Could not find PostgreSQL pod${NC}"
    echo "Make sure the PostgreSQL deployment is running in namespace '$NAMESPACE'"
    exit 1
fi
echo -e "${GREEN}✓ Found pod: $POD_NAME${NC}"
echo ""

# Step 3: Create backup
echo -e "${YELLOW}Step 3: Creating backup...${NC}"
echo "Output file: $BACKUP_FILE"
echo ""

PGPASSWORD="$DB_PASSWORD" kubectl exec -n "$NAMESPACE" "$POD_NAME" -- \
  pg_dump -U "$DB_USER" -d "$DB_NAME" --no-password > "$BACKUP_FILE"

if [ -f "$BACKUP_FILE" ]; then
    SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    echo -e "${GREEN}✓ Backup completed${NC}"
    echo -e "${GREEN}✓ File: $BACKUP_FILE ($SIZE)${NC}"
    echo ""
    echo -e "${GREEN}Next steps:${NC}"
    echo "  - Commit the backup: git add backups/postgres/ && git commit -m 'Backup postgres - $TIMESTAMP'"
    echo "  - Push to repo: git push"
else
    echo -e "${RED}✗ Backup failed - no file created${NC}"
    exit 1
fi
