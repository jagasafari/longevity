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

echo -e "${YELLOW}PostgreSQL Data Cleanup Script${NC}"
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

# Decide execution mode: local psql or in-pod psql
USE_LOCAL_PSQL=false
if command -v psql >/dev/null 2>&1; then
    USE_LOCAL_PSQL=true
fi

# Step 3: Set up DB access mode
if [ "$USE_LOCAL_PSQL" = true ]; then
    echo -e "${YELLOW}Step 3: Local psql found; setting up port-forward...${NC}"
    if lsof -Pi :5432 -sTCP:LISTEN -t >/dev/null 2>&1; then
        echo -e "${YELLOW}Port 5432 is already in use. Attempting to use it...${NC}"
    else
        # Start port-forward in background
        kubectl port-forward -n "$NAMESPACE" "pod/$POD_NAME" 5432:5432 > /dev/null 2>&1 &
        PF_PID=$!
        echo -e "${GREEN}✓ Port-forward started (PID: $PF_PID)${NC}"

        # Wait for port-forward to be ready
        sleep 2
    fi
else
    echo -e "${YELLOW}Step 3: Local psql not found; will run SQL inside pod via kubectl exec.${NC}"
fi
echo ""

run_sql() {
    local sql_text="$1"

    if [ "$USE_LOCAL_PSQL" = true ]; then
        PGPASSWORD="$DB_PASSWORD" psql -h localhost -U "$DB_USER" -d "$DB_NAME" << EOF
$sql_text
EOF
    else
        kubectl exec -i -n "$NAMESPACE" "pod/$POD_NAME" -- env PGPASSWORD="$DB_PASSWORD" psql -U "$DB_USER" -d "$DB_NAME" << EOF
$sql_text
EOF
    fi
}

# Step 4: Connect and cleanup
echo -e "${YELLOW}Step 4: Connecting to database and cleaning up...${NC}"
echo "This will delete all data from:"
echo "  - photo_group_categories"
echo "  - photo_group_members"
echo "  - photo_groups"
echo "  - categories"
echo ""
read -p "Are you sure? (type 'yes' to confirm): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo -e "${YELLOW}Cancelled.${NC}"
    if [ ! -z "$PF_PID" ]; then
        kill $PF_PID 2>/dev/null || true
    fi
    exit 0
fi
echo ""

# Run the cleanup
run_sql "DELETE FROM photo_group_categories;
DELETE FROM photo_group_members;
DELETE FROM photo_groups;
DELETE FROM categories;"

echo -e "${GREEN}✓ All data cleaned up successfully${NC}"
echo ""

# Step 5: Verify
echo -e "${YELLOW}Step 5: Verifying cleanup...${NC}"
run_sql "SELECT 'photo_group_categories' as table_name, COUNT(*) as row_count FROM photo_group_categories
UNION ALL
SELECT 'photo_group_members', COUNT(*) FROM photo_group_members
UNION ALL
SELECT 'photo_groups', COUNT(*) FROM photo_groups
UNION ALL
SELECT 'categories', COUNT(*) FROM categories;"
echo ""

# Cleanup port-forward if we started it
if [ ! -z "$PF_PID" ]; then
    kill $PF_PID 2>/dev/null || true
    echo -e "${GREEN}✓ Port-forward cleaned up${NC}"
fi

echo -e "${GREEN}Done!${NC}"
