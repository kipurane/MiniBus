#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
functions_port="${MINIBUS_INVENTORY_FUNCTIONS_PORT:-7072}"

if ! command -v func >/dev/null 2>&1; then
  echo "Azure Functions Core Tools is required because this script runs 'func start'." >&2
  exit 1
fi

cat <<EOF
Starting the Inventory Function App on port $functions_port.
Use the Billing sample emulator stack and seed command to drive this endpoint.
EOF

cd "$sample_root"
exec func start --port "$functions_port"
