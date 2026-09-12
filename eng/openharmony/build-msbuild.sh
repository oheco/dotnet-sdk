#!/usr/bin/env bash
# Build the vendored MSBuild dependency, retaining the upstream full build.
set -euo pipefail
msbuild_source=${1:?Usage: build-msbuild.sh source-directory log-file}
msbuild_log=${2:?Missing log file}
msbuild_source=$(cd -- "$msbuild_source" && pwd)
: "${DOTNET_INSTALL_DIR:?Set DOTNET_INSTALL_DIR to the fixed Linux .NET SDK 10.0.300}"
: "${NUGET_PACKAGES:?Set NUGET_PACKAGES to an isolated dependency cache}"
: "${DOTNET_CLI_HOME:?Set DOTNET_CLI_HOME to an isolated CLI home}"
: "${DOTNET_OHOS_MSBUILD_FEED:?Prepare the fixed MSBuild dependency feed first}"
[[ -d "$DOTNET_INSTALL_DIR/sdk/10.0.300" ]] || { echo 'Missing fixed SDK 10.0.300' >&2; exit 2; }
[[ -f "$DOTNET_OHOS_MSBUILD_FEED/NuGet.Config" ]] || { echo 'Missing local NuGet.Config' >&2; exit 2; }
[[ ! -e "$msbuild_log" ]] || { echo 'Use a new log file' >&2; exit 2; }
mkdir -p -- "$(dirname -- "$msbuild_log")"
msbuild_log=$(cd -- "$(dirname -- "$msbuild_log")" && pwd)/$(basename -- "$msbuild_log")
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export HTTP_PROXY=${DOTNET_OHOS_PROXY:-socks5://127.0.0.1:10808}
export HTTPS_PROXY=$HTTP_PROXY
cd -- "$msbuild_source"
# Use the MSBuild file logger as requested by the vendored repository's AGENTS.md.
exec bash build.sh -configuration Release -pack -v quiet /m:1 \
    /p:BuildInParallel=false /p:UseSharedCompilation=false /p:OfficialBuild=false \
    "/p:OfficialBuildId=${DOTNET_OHOS_BUILD_ID:-20260912.1}" /p:NuGetAudit=false \
    "/p:RestoreConfigFile=$DOTNET_OHOS_MSBUILD_FEED/NuGet.Config" \
    "/flp:verbosity=normal;LogFile=$msbuild_log"
