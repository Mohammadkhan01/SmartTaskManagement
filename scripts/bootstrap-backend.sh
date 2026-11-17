#!/usr/bin/env bash
set -euo pipefail

# Run from the repository root after installing the .NET 10 SDK.
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: .NET SDK is not installed or not on PATH." >&2
  exit 1
fi

mkdir -p src tests

dotnet new sln -n SmartTaskManagement --format sln --force

dotnet new webapi   -n SmartTask.API    -o src/SmartTask.API    --framework net10.0 --use-controllers
dotnet new classlib -n SmartTask.BLL    -o src/SmartTask.BLL    --framework net10.0
dotnet new classlib -n SmartTask.DAL    -o src/SmartTask.DAL    --framework net10.0
dotnet new classlib -n SmartTask.Domain -o src/SmartTask.Domain --framework net10.0
dotnet new classlib -n SmartTask.Common -o src/SmartTask.Common --framework net10.0

dotnet sln SmartTaskManagement.sln add \
  src/SmartTask.API/SmartTask.API.csproj \
  src/SmartTask.BLL/SmartTask.BLL.csproj \
  src/SmartTask.DAL/SmartTask.DAL.csproj \
  src/SmartTask.Domain/SmartTask.Domain.csproj \
  src/SmartTask.Common/SmartTask.Common.csproj

# Dependency direction: API -> BLL; API -> DAL only for composition root;
# BLL -> Domain/Common; DAL -> BLL abstractions + Domain/Common.
dotnet add src/SmartTask.API reference src/SmartTask.BLL src/SmartTask.DAL src/SmartTask.Common
dotnet add src/SmartTask.BLL reference src/SmartTask.Domain src/SmartTask.Common
dotnet add src/SmartTask.DAL reference src/SmartTask.BLL src/SmartTask.Domain src/SmartTask.Common

dotnet new xunit -n SmartTask.UnitTests -o tests/SmartTask.UnitTests --framework net10.0
dotnet sln SmartTaskManagement.sln add tests/SmartTask.UnitTests/SmartTask.UnitTests.csproj
dotnet add tests/SmartTask.UnitTests reference src/SmartTask.BLL src/SmartTask.Domain

mkdir -p tests/SmartTask.IntegrationTests tests/SmartTask.ArchitectureTests

echo "Backend solution scaffold created. Next: add packages and configure Identity/EF Core."
