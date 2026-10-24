#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
app="dist/Steam Record Manager.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
xcrun swiftc -swift-version 5 -O -target "$(uname -m)-apple-macosx13.0" -framework AppKit -framework CryptoKit macos/SteamRecordManager.swift -o "$app/Contents/MacOS/SteamRecordManager"
cp macos/Info.plist "$app/Contents/Info.plist"
cp LICENSE "$app/Contents/Resources/LICENSE"
codesign --force --sign - "$app"
codesign --verify --deep --strict "$app"
"$app/Contents/MacOS/SteamRecordManager" --self-test
"$app/Contents/MacOS/SteamRecordManager" --ui-smoke-test
echo "Built native $(uname -m) app. Ad-hoc signed only; not Apple Developer ID signed or notarized."
