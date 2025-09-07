#!/usr/bin/env bash
set -euo pipefail
shopt -s nullglob

# Discover a candidate DLL if one not explicitly provided
if [[ -n "${FKS_NINJA_DLL:-}" ]]; then
  DLL="$FKS_NINJA_DLL"
else
  DLL=$(ls *.dll 2>/dev/null | grep -E '(FKS|Service|App)' | head -n1 || true)
fi
if [[ -z "${DLL:-}" ]]; then
  DLL="FKSService.dll"
fi
echo "[ninja] Starting dotnet $DLL" >&2
exec dotnet "$DLL"
