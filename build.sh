#!/usr/bin/env bash
# Nuke bootstrapper (POSIX counterpart of build.ps1). The Nuke global tool locates a build
# by searching for build.ps1/build.sh, so `nuke <Target>` does not work without these.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

dotnet run --project build/_build.csproj --no-launch-profile -- "$@"
