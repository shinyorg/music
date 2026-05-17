#!/bin/bash
set -e

IPA="$1"

if [ ! -f "$IPA" ]; then
  echo "Shiny.Music: IPA not found at '$IPA' - skipping SwiftSupport injection"
  exit 0
fi

STDLIB_TOOL=$(xcrun --find swift-stdlib-tool 2>/dev/null || true)
if [ -z "$STDLIB_TOOL" ]; then
  echo "Shiny.Music: swift-stdlib-tool not found - skipping SwiftSupport injection"
  exit 0
fi

DEVELOPER_DIR=$(xcode-select -p 2>/dev/null || true)
TOOLCHAIN_DIR="$DEVELOPER_DIR/Toolchains/XcodeDefault.xctoolchain"

# Modern Xcode (16+/26+) ships Swift ABI-stable back-deployment dylibs
# under usr/lib/swift-5.0/<platform>/. The default search path that
# swift-stdlib-tool uses (usr/lib/swift/<platform>/) no longer contains
# any .dylib files, only static archives and modules.
SOURCE_LIBS=""
for candidate in \
  "$TOOLCHAIN_DIR/usr/lib/swift-5.0/iphoneos" \
  "$TOOLCHAIN_DIR/usr/lib/swift-5.5/iphoneos" \
  "$TOOLCHAIN_DIR/usr/lib/swift/iphoneos"; do
  if [ -f "$candidate/libswiftCore.dylib" ]; then
    SOURCE_LIBS="$candidate"
    break
  fi
done

if [ -z "$SOURCE_LIBS" ]; then
  echo "Shiny.Music: no Swift dylibs found in Xcode toolchain - skipping SwiftSupport injection"
  echo "  (Swift runtime is provided by the OS on iOS 12.2+; SwiftSupport is no longer required)"
  exit 0
fi

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

echo "Shiny.Music: Injecting SwiftSupport into '$IPA'"
unzip -q "$IPA" -d "$WORK"

APP=$(find "$WORK/Payload" -maxdepth 1 -name "*.app" -type d | head -1)
if [ -z "$APP" ]; then
  echo "Shiny.Music: no .app found inside Payload - skipping SwiftSupport injection"
  exit 0
fi

# Resolve the app binary via CFBundleExecutable (falls back to .app basename)
EXE_NAME=$(/usr/libexec/PlistBuddy -c "Print :CFBundleExecutable" "$APP/Info.plist" 2>/dev/null || true)
if [ -z "$EXE_NAME" ]; then
  EXE_NAME=$(basename "$APP" .app)
fi
APP_BINARY="$APP/$EXE_NAME"

if [ ! -f "$APP_BINARY" ]; then
  echo "Shiny.Music: app binary not found at '$APP_BINARY' - skipping SwiftSupport injection"
  exit 0
fi

DEST="$WORK/SwiftSupport/iphoneos"
mkdir -p "$DEST"

SCAN_ARGS=(--scan-executable "$APP_BINARY")
if [ -d "$APP/Frameworks" ]; then
  SCAN_ARGS+=(--scan-folder "$APP/Frameworks")
  for fw in "$APP/Frameworks"/*.framework; do
    [ -d "$fw" ] || continue
    fw_name=$(basename "$fw" .framework)
    fw_bin="$fw/$fw_name"
    [ -f "$fw_bin" ] && SCAN_ARGS+=(--scan-executable "$fw_bin")
  done
fi

"$STDLIB_TOOL" \
  --copy \
  --verbose \
  --platform iphoneos \
  --source-libraries "$SOURCE_LIBS" \
  --destination "$DEST" \
  --unsigned-destination "$DEST" \
  "${SCAN_ARGS[@]}"

COUNT=$(find "$DEST" -name "*.dylib" -type f | wc -l | tr -d ' ')
if [ "$COUNT" = "0" ]; then
  echo "Shiny.Music: app has no @rpath Swift runtime references - SwiftSupport not required, skipping injection"
  exit 0
fi
echo "Shiny.Music: SwiftSupport contains $COUNT dylib(s) (sourced from $SOURCE_LIBS)"

rm "$IPA"
cd "$WORK"
zip -qry "$IPA" .

echo "Shiny.Music: SwiftSupport injection complete"
