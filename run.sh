#!/bin/bash
# Builds the library into a local NuGet feed, builds the app for the iOS simulator, installs and launches it.
# Usage: ./run.sh [Debug|Release] [simulator name or UDID (default: the booted simulator)]
set -e
cd "$(dirname "$0")"
CONFIG=${1:-Debug}
DEVICE=${2:-booted}
BUNDLE_ID=com.companyname.ConstraintCrashRepro

rm -rf local-feed RangedTypes/bin RangedTypes/obj App/bin App/obj ~/.nuget/packages/rangedtypes
dotnet pack RangedTypes/RangedTypes.csproj -c Release -o local-feed --nologo -v:q
dotnet build App/ConstraintCrashRepro.csproj -f net11.0-ios -c "$CONFIG" -r iossimulator-arm64 --nologo -v:q "${@:3}"

APP="App/bin/$CONFIG/net11.0-ios/iossimulator-arm64/ConstraintCrashRepro.app"
xcrun simctl install "$DEVICE" "$APP"
xcrun simctl launch --console-pty --terminate-running-process "$DEVICE" "$BUNDLE_ID"
