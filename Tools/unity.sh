#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
editor='/mnt/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe'
project="$(wslpath -w "$root")"
mkdir -p "$root/Logs"
export XR_RUNTIME_JSON='C:\battle-lab-no-xr.json'
export WSLENV="XR_RUNTIME_JSON${WSLENV:+:$WSLENV}"
case "${1:-setup}" in
 setup) args=(-executeMethod BattleLabBuilder.Setup -quit) ;;
 build) args=(-executeMethod BattleLabBuilder.Build -quit) ;;
 tests) args=(-runTests -testPlatform EditMode -testResults "$project\\Logs\\tests.xml") ;;
 *) echo 'usage: unity.sh setup|build|tests'; exit 2 ;;
esac
"$editor" -batchmode -projectPath "$project" -logFile "$project\\Logs\\${1:-setup}.log" "${args[@]}"
