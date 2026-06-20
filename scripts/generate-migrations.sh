#!/usr/bin/env bash
# Generates the initial EF Core migration for each service that uses SQL Server.
# Run this ONCE locally (with the .NET 8 SDK installed) before first `docker compose up`,
# since migration files are not checked in and Program.cs calls Database.Migrate() on startup.
#
# Usage:
#   chmod +x scripts/generate-migrations.sh
#   ./scripts/generate-migrations.sh

set -e

# Ensure dotnet-ef tool is available
if ! dotnet tool list -g | grep -q dotnet-ef; then
  echo "Installing dotnet-ef global tool..."
  dotnet tool install --global dotnet-ef
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> Generating migration for WorkflowService.API"
dotnet ef migrations add InitialCreate \
  --project "$ROOT_DIR/src/Services/WorkflowService/WorkflowService.API" \
  --startup-project "$ROOT_DIR/src/Services/WorkflowService/WorkflowService.API" \
  --context WorkflowDbContext \
  --output-dir Migrations

echo "==> Generating migration for TriggerService.API"
dotnet ef migrations add InitialCreate \
  --project "$ROOT_DIR/src/Services/TriggerService/TriggerService.API" \
  --startup-project "$ROOT_DIR/src/Services/TriggerService/TriggerService.API" \
  --context TriggerDbContext \
  --output-dir Migrations

echo "==> Generating migration for ExecutionService.API"
dotnet ef migrations add InitialCreate \
  --project "$ROOT_DIR/src/Services/ExecutionService/ExecutionService.API" \
  --startup-project "$ROOT_DIR/src/Services/ExecutionService/ExecutionService.API" \
  --context ExecutionDbContext \
  --output-dir Migrations

echo "==> Done. Migrations created under each service's Migrations/ folder."
echo "    These will be applied automatically on container startup (Database.Migrate())."
