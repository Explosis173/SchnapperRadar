#!/usr/bin/env bash
# Einmalig beim Erstellen des Codespaces: Abhängigkeiten + Frontend bauen.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "▶ Frontend bauen …"
(cd web && npm ci --no-audit --no-fund && npm run build)

echo "▶ Backend bauen …"
dotnet build src/SchnapperRadar.Api -c Release -v q
