#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sample_project="$sample_root/MiniBus.Samples.Billing.FunctionApp.csproj"
sample_dll="$sample_root/bin/Debug/net10.0/MiniBus.Samples.Billing.FunctionApp.dll"

dotnet build "$sample_project"
exec dotnet "$sample_dll" --drain-outbox
