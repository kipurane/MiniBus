#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
compose_file="$sample_root/servicebus-emulator/compose.yaml"
readiness_timeout_seconds="${MINIBUS_BILLING_EMULATOR_TIMEOUT_SECONDS:-180}"

if [[ "${ACCEPT_EULA:-}" != "Y" ]]; then
  cat >&2 <<'EOF'
Set ACCEPT_EULA=Y to accept the Azure Service Bus emulator and SQL Server container license terms.

Example:
  ACCEPT_EULA=Y ./samples/MiniBus.Samples.Billing.FunctionApp/run-local.sh
EOF
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required to run the Billing sample emulator stack." >&2
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose is required to run the Billing sample emulator stack." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not reachable. Start Docker Desktop and try again." >&2
  exit 1
fi

if ! command -v func >/dev/null 2>&1; then
  echo "Azure Functions Core Tools is required because this script ends by running 'func start'." >&2
  exit 1
fi

compose() {
  docker compose -f "$compose_file" "$@"
}

has_emulator_ready_logs() {
  local logs
  logs="$(compose logs --no-color emulator 2>/dev/null || true)"

  grep -Fq "User defined entities created for SB Emulator" <<<"$logs" \
    && grep -Fq "Emulator Service is Successfully Up!" <<<"$logs"
}

service_is_running() {
  local service="$1"

  compose ps --services --status running | grep -Fxq "$service"
}

tcp_port_is_open() {
  local host="$1"
  local port="$2"

  (exec 3<>"/dev/tcp/$host/$port") >/dev/null 2>&1
}

wait_for_emulator() {
  local deadline=$((SECONDS + readiness_timeout_seconds))

  printf "Waiting for Billing emulator topology"
  while ((SECONDS < deadline)); do
    if service_is_running emulator \
      && service_is_running mssql \
      && service_is_running azurite \
      && tcp_port_is_open localhost 5672 \
      && tcp_port_is_open localhost 10000 \
      && has_emulator_ready_logs; then
      printf " ready.\n"
      return
    fi

    printf "."
    sleep 2
  done

  printf " timed out.\n" >&2
  compose ps >&2 || true
  compose logs --no-color emulator >&2 || true
  exit 1
}

compose up -d
wait_for_emulator

cat <<'EOF'
Billing emulator stack is ready.
Starting the Billing Function App in the foreground.

Start the Inventory Function App from another terminal with:
  ./samples/MiniBus.Samples.Inventory.FunctionApp/run-local.sh

Seed the first command from a third terminal with:
  ./samples/MiniBus.Samples.Billing.FunctionApp/seed-local.sh
EOF

cd "$sample_root"
exec func start
