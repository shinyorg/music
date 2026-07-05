#!/usr/bin/env bash
#
# Downloads the proprietary Spotify App Remote SDK binaries into the binding
# projects. These are NOT redistributed in this repo (see .gitignore).
#
#   Android: spotify-app-remote-release-*.aar  -> Shiny.Spotify.AppRemote.Android/Jars/
#   iOS:     SpotifyiOS.xcframework            -> Shiny.Spotify.AppRemote.iOS/
#
# Runs automatically at build time (see each binding .csproj), or manually:
#   bindings/fetch-spotify-sdks.sh [android|ios|all]   (default: all)
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

ANDROID_TAG="v0.8.0-appremote_v2.1.0-auth"
ANDROID_AAR="spotify-app-remote-release-0.8.0.aar"
IOS_TAG="v5.0.1"

fetch_android() {
  echo "==> Android App Remote AAR ($ANDROID_AAR)"
  mkdir -p "$DIR/Shiny.Spotify.AppRemote.Android/Jars"
  curl -fSL --retry 3 \
    "https://github.com/spotify/android-sdk/releases/download/${ANDROID_TAG}/${ANDROID_AAR}" \
    -o "$DIR/Shiny.Spotify.AppRemote.Android/Jars/${ANDROID_AAR}"
}

fetch_ios() {
  echo "==> iOS SpotifyiOS.xcframework (ios-sdk ${IOS_TAG})"
  local TMP
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP"' EXIT
  curl -fSL --retry 3 \
    "https://github.com/spotify/ios-sdk/archive/refs/tags/${IOS_TAG}.tar.gz" \
    -o "$TMP/ios-sdk.tar.gz"
  tar -xzf "$TMP/ios-sdk.tar.gz" -C "$TMP"
  local XC
  XC="$(find "$TMP" -type d -name 'SpotifyiOS.xcframework' | head -1)"
  if [ -z "$XC" ]; then
    echo "ERROR: SpotifyiOS.xcframework not found in the ios-sdk archive." >&2
    exit 1
  fi
  rm -rf "$DIR/Shiny.Spotify.AppRemote.iOS/SpotifyiOS.xcframework"
  cp -R "$XC" "$DIR/Shiny.Spotify.AppRemote.iOS/SpotifyiOS.xcframework"
}

case "${1:-all}" in
  android) fetch_android ;;
  ios)     fetch_ios ;;
  all)     fetch_android; fetch_ios ;;
  *) echo "Usage: $0 [android|ios|all]" >&2; exit 2 ;;
esac

echo "Done. Spotify SDK binaries are in place."
