#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

dotnet restore DddDemo.sln
dotnet tool restore
dotnet build DddDemo.sln --no-restore
