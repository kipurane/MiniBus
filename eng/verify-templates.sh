#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_output="$repo_root/artifacts/packages"
template_package="$package_output/MiniBus.Templates.0.1.0-preview.1.nupkg"
work_root="$(mktemp -d "${TMPDIR:-/tmp}/minibus-template-verify.XXXXXX")"

cleanup() {
  rm -rf "$work_root"
}

trap cleanup EXIT

export DOTNET_CLI_HOME="$work_root/dotnet-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

mkdir -p "$DOTNET_CLI_HOME" "$package_output"

dotnet pack "$repo_root/src/MiniBus.Core/MiniBus.Core.csproj" -c Release -o "$package_output"
dotnet pack "$repo_root/src/MiniBus.AzureServiceBus/MiniBus.AzureServiceBus.csproj" -c Release -o "$package_output"
dotnet pack "$repo_root/src/MiniBus.AzureFunctions/MiniBus.AzureFunctions.csproj" -c Release -o "$package_output"
dotnet pack "$repo_root/src/MiniBus.Analyzers/MiniBus.Analyzers.csproj" -c Release -o "$package_output"
dotnet pack "$repo_root/src/MiniBus.Templates/MiniBus.Templates.csproj" -c Release -o "$package_output"

dotnet new install "$template_package" --force
dotnet new minibus-functionapp -n TemplateVerificationApp -o "$work_root/generated"
dotnet build "$work_root/generated/TemplateVerificationApp.csproj" \
  -c Release \
  --source "$package_output" \
  --source https://api.nuget.org/v3/index.json
