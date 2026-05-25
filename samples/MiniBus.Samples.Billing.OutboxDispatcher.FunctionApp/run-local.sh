#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
functions_port="${MINIBUS_BILLING_DISPATCHER_FUNCTIONS_PORT:-7073}"

if ! command -v func >/dev/null 2>&1; then
  echo "Azure Functions Core Tools is required because this script runs 'func start'." >&2
  exit 1
fi

cat <<EOF
Starting the Billing SQL outbox dispatcher Function App on port $functions_port.
Use this with the SQL-backed Billing workflow after applying the MiniBus SQL schema.
EOF

cd "$sample_root"
exec func start --port "$functions_port"
