#!/bin/bash
set -e

IPA="$1"

if [ ! -f "$IPA" ]; then
  echo "Shiny.Music: IPA not found at '$IPA' - skipping SwiftSupport injection"
  exit 0
fi

WORK=$(mktemp -d)
SWIFT_LIBS="$(xcode-select -p)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/iphoneos"

if [ ! -d "$SWIFT_LIBS" ]; then
  echo "Shiny.Music: Swift libs not found at '$SWIFT_LIBS' - skipping SwiftSupport injection"
  rm -rf "$WORK"
  exit 0
fi

echo "Shiny.Music: Injecting SwiftSupport into '$IPA'"
unzip -q "$IPA" -d "$WORK"
mkdir -p "$WORK/SwiftSupport/iphoneos"

APP=$(find "$WORK/Payload" -maxdepth 1 -name "*.app" -type d | head -1)

# Copy Swift dylibs matching bundled frameworks
if [ -d "$APP/Frameworks" ]; then
  for fw in "$APP/Frameworks"/*.framework; do
    [ -d "$fw" ] || continue
    name=$(basename "$fw" .framework)
    lib="$SWIFT_LIBS/libswift${name}.dylib"
    if [ -f "$lib" ]; then
      echo "  Adding libswift${name}.dylib"
      cp "$lib" "$WORK/SwiftSupport/iphoneos/"
    fi
  done
fi

# Always include core Swift runtime libs
for lib in libswiftCore libswiftDarwin libswiftFoundation libswiftObjectiveC libswiftSwiftOnoneSupport libswiftUIKit; do
  src="$SWIFT_LIBS/${lib}.dylib"
  if [ -f "$src" ] && [ ! -f "$WORK/SwiftSupport/iphoneos/${lib}.dylib" ]; then
    echo "  Adding core: ${lib}.dylib"
    cp "$src" "$WORK/SwiftSupport/iphoneos/"
  fi
done

rm "$IPA"
cd "$WORK"
zip -qr "$IPA" .
rm -rf "$WORK"

echo "Shiny.Music: SwiftSupport injection complete"
