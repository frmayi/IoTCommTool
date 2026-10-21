#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

APP=src/IoTCommTool.App/IoTCommTool.App.csproj
RIDS="${1:-win-x64 linux-x64 osx-x64}"

for rid in $RIDS; do
  echo "==> Publishing $rid"
  dotnet publish "$APP" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "publish/$rid"
done

echo "Done. Output under publish/"
