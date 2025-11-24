#!/bin/bash
cd "$(dirname "$0")"
#!/bin/bash
cd "$(dirname "$0")"
# Define paths
APP_NAME="TenSecondTom.Extensions.MacOS"
APP_DIR="$APP_NAME.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
BIN_DIR="../../../bin"

# Clean previous build
rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR"

# 1. Compile directly into the .app
swiftc notifier.swift -o "$MACOS_DIR/notifier"

# 2. Copy Info.plist
cp Info.plist "$CONTENTS_DIR/"

# 2.5 Generate and Copy AppIcon
if [ -f "AppIcon.png" ]; then
    echo "Generating AppIcon.icns..."
    ICONSET_DIR="AppIcon.iconset"
    mkdir -p "$ICONSET_DIR"
    
    # Generate standard sizes
    sips -z 16 16     -s format png AppIcon.png --out "${ICONSET_DIR}/icon_16x16.png" > /dev/null
    sips -z 32 32     -s format png AppIcon.png --out "${ICONSET_DIR}/icon_16x16@2x.png" > /dev/null
    sips -z 32 32     -s format png AppIcon.png --out "${ICONSET_DIR}/icon_32x32.png" > /dev/null
    sips -z 64 64     -s format png AppIcon.png --out "${ICONSET_DIR}/icon_32x32@2x.png" > /dev/null
    sips -z 128 128   -s format png AppIcon.png --out "${ICONSET_DIR}/icon_128x128.png" > /dev/null
    sips -z 256 256   -s format png AppIcon.png --out "${ICONSET_DIR}/icon_128x128@2x.png" > /dev/null
    sips -z 256 256   -s format png AppIcon.png --out "${ICONSET_DIR}/icon_256x256.png" > /dev/null
    sips -z 512 512   -s format png AppIcon.png --out "${ICONSET_DIR}/icon_256x256@2x.png" > /dev/null
    sips -z 512 512   -s format png AppIcon.png --out "${ICONSET_DIR}/icon_512x512.png" > /dev/null
    sips -z 1024 1024 -s format png AppIcon.png --out "${ICONSET_DIR}/icon_512x512@2x.png" > /dev/null
    
    # Convert to icns
    iconutil -c icns "$ICONSET_DIR"
    
    # Copy to Resources
    RESOURCES_DIR="$CONTENTS_DIR/Resources"
    mkdir -p "$RESOURCES_DIR"
    cp AppIcon.icns "$RESOURCES_DIR/"
    
    # Cleanup
    rm -rf "$ICONSET_DIR"
    rm AppIcon.icns
fi

# 3. Sign the whole app bundle with entitlements
codesign --force --sign - --entitlements entitlements.plist --identifier "com.tensecondtom.extensions.macos" "$APP_DIR"

# 4. Move to bin directory
mkdir -p "$BIN_DIR"
rm -rf "$BIN_DIR/$APP_DIR"
cp -R "$APP_DIR" "$BIN_DIR/"
