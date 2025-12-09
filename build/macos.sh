#!/bin/bash
# Script to build and package macOS application
# Requires: .NET 10.0 SDK

set -e

CONFIGURATION="${1:-Release}"
VERSION="${2:-1.0.0}"
ARCH="${3:-arm64}"  # x64, arm64, or both

echo "=== Building VBDLIS Tools for macOS ==="
echo "Configuration: $CONFIGURATION"
echo "Version: $VERSION"
echo "Architecture: $ARCH"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/../src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop"
DIST_PATH="$SCRIPT_DIR/../dist/macos"

# Clean previous builds
echo -e "\nCleaning previous builds..."
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"

# Function to build for specific architecture
build_for_arch() {
    local RUNTIME=$1
    local ARCH_NAME=$2

    echo -e "\n=== Building for $ARCH_NAME ==="

    local PUBLISH_PATH="$PROJECT_PATH/bin/publish/osx-$ARCH_NAME"
    local APP_NAME="VbdlisTools.app"
    local APP_PATH="$DIST_PATH/$APP_NAME-$ARCH_NAME"

    # Clean
    rm -rf "$PUBLISH_PATH"

    # Publish
    echo "Publishing application..."
    dotnet publish "$PROJECT_PATH" \
        --configuration "$CONFIGURATION" \
        --runtime "$RUNTIME" \
        --self-contained true \
        --output "$PUBLISH_PATH" \
        -p:PublishSingleFile=false \
        -p:PublishTrimmed=false \
        -p:Version="$VERSION"

    # Remove Playwright browsers (but keep driver files)
    echo "Removing Playwright browsers from output..."
    # Keep .playwright/node and .playwright/package (needed for installation)
    # Remove only browser binaries if they exist
    find "$PUBLISH_PATH/.playwright" -type d -name "chromium-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "firefox-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "webkit-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "ffmpeg-*" -exec rm -rf {} + 2>/dev/null || true

    # Create .app bundle structure
    echo "Creating .app bundle..."
    mkdir -p "$APP_PATH/Contents/MacOS"
    mkdir -p "$APP_PATH/Contents/Resources"

    # Copy binaries (including hidden files like .playwright)
    # Use rsync to copy everything including dotfiles
    rsync -av "$PUBLISH_PATH/" "$APP_PATH/Contents/MacOS/"

    # Make executable
    chmod +x "$APP_PATH/Contents/MacOS/Haihv.Vbdlis.Tools.Desktop"

    # Copy app icon
    if [ -f "$PROJECT_PATH/Assets/appicon.icns" ]; then
        echo "Copying app icon..."
        cp "$PROJECT_PATH/Assets/appicon.icns" "$APP_PATH/Contents/Resources/AppIcon.icns"
    fi

    # Create Info.plist
    cat > "$APP_PATH/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>VBDLIS Tools</string>
    <key>CFBundleDisplayName</key>
    <string>VBDLIS Tools</string>
    <key>CFBundleIdentifier</key>
    <string>com.haihv.vbdlis.tools</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>Haihv.Vbdlis.Tools.Desktop</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

    # Create DMG with Applications symlink
    echo "Creating DMG..."
    local DMG_NAME="VbdlisTools-macOS-$ARCH_NAME-v$VERSION.dmg"
    local DMG_PATH="$DIST_PATH/$DMG_NAME"

    rm -f "$DMG_PATH"

    # Create temporary folder for DMG
    local TEMP_DMG_DIR="$DIST_PATH/temp_dmg_$ARCH_NAME"
    mkdir -p "$TEMP_DMG_DIR"

    # Copy app bundle
    cp -r "$APP_PATH" "$TEMP_DMG_DIR/$APP_NAME"

    # Create symbolic link to Applications folder
    ln -s /Applications "$TEMP_DMG_DIR/Applications"

    # Create DMG using hdiutil
    hdiutil create -volname "VBDLIS Tools" \
        -srcfolder "$TEMP_DMG_DIR" \
        -ov -format UDZO \
        "$DMG_PATH"

    # Clean up temp
    rm -rf "$TEMP_DMG_DIR"

    echo "Created: $DMG_PATH"
}

# Build based on architecture selection
if [ "$ARCH" = "x64" ] || [ "$ARCH" = "both" ]; then
    build_for_arch "osx-x64" "x64"
fi

if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "both" ]; then
    build_for_arch "osx-arm64" "arm64"
fi

echo -e "\n=== Build Completed Successfully ==="
echo "Output folder: $DIST_PATH"
echo -e "\nNote: Playwright browsers are NOT included. They will be downloaded automatically on first run."
