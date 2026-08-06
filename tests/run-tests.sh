#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd -P)"
upstream="$repo_dir/upstream-sbgECom"
oracle="$repo_dir/tests/c-reference/oracle"
if [[ ! -d "$upstream" ]]; then
  echo "Missing pinned upstream-sbgECom source directory." >&2
  exit 2
fi
cc -std=c11 -O2 \
  -I"$upstream/common" -I"$upstream/src" \
  "$repo_dir/tests/c-reference/oracle.c" \
  "$upstream/common/crc/sbgCrc.c" \
  "$upstream/common/streamBuffer/sbgStreamBuffer.c" \
  "$upstream/src/logs/sbgEComLogImu.c" \
  -lm -o "$oracle"
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet-sbg}" dotnet restore \
  "$repo_dir/tests/SbgECom.Tests/SbgECom.Tests.csproj" --ignore-failed-sources >/dev/null
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet-sbg}" dotnet run \
  -p:UseSharedCompilation=false --project "$repo_dir/tests/SbgECom.Tests/SbgECom.Tests.csproj" -- "$oracle"
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet-sbg}" dotnet publish \
  "$repo_dir/tests/SbgECom.AotSmoke/SbgECom.AotSmoke.csproj" -c Release -o /tmp/sbgecom-aot-smoke --nologo
/tmp/sbgecom-aot-smoke/SbgECom.AotSmoke
