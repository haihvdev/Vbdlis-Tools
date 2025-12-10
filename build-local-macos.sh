#!/bin/bash
# Local macOS build script with auto-incrementing version
# Build DMG on your Mac with automatic version management

set -e

CONFIGURATION="${1:-Release}"
ARCH="${2:-arm64}"

echo "=== Local macOS Build Script with Auto-Increment Version ==="
echo "Configuration: $CONFIGURATION"
echo "Architecture: $ARCH"
echo "Build Mode: LOCAL (auto-increment version)"
echo "Playwright browsers: NOT bundled (app will auto-download)"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop"
PROJECT_FILE="$PROJECT_PATH/Haihv.Vbdlis.Tools.Desktop.csproj"
DIST_PATH="$SCRIPT_DIR/dist/velopack-macos-local"
VERSION_LOG_FILE="$SCRIPT_DIR/build/version.json"

# Read version from version.json and auto-increment
echo -e "\nReading and calculating version..."
if [ ! -f "$VERSION_LOG_FILE" ]; then
    echo "ERROR: version.json not found!"
    echo "Creating default version.json..."
    mkdir -p "$SCRIPT_DIR/build"
    cat > "$VERSION_LOG_FILE" << 'EOF'
{
  "majorMinor": "1.0",
  "currentVersion": "1.0.0",
  "assemblyVersion": "1.0.0.0",
  "lastBuildDate": "",
  "buildNumber": 0,
  "platforms": {
    "windows": {
      "lastBuilt": "",
      "version": ""
    },
    "macos": {
      "lastBuilt": "",
      "version": ""
    }
  }
}
EOF
fi

# Read current values
MAJOR_MINOR=$(grep -o '"majorMinor"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
LAST_BUILD_DATE=$(grep -o '"lastBuildDate"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
BUILD_NUMBER=$(grep -o '"buildNumber"[[:space:]]*:[[:space:]]*[0-9]*' "$VERSION_LOG_FILE" | grep -o '[0-9]*$')

# Calculate version - ALWAYS INCREMENT for local builds
TODAY_STRING=$(date +"%Y-%m-%d")
DATE_STRING=$(date +"%y%m%d")
YEAR_MONTH=$(date +"%y%m")
DAY_STRING=$(date +"%d")

# Increment build number
if [ "$LAST_BUILD_DATE" = "$TODAY_STRING" ]; then
    BUILD_NUMBER=$((BUILD_NUMBER + 1))
    echo "Same day build detected. Incrementing to build #$BUILD_NUMBER"
else
    BUILD_NUMBER=1
    echo "New day detected. Starting with build #$BUILD_NUMBER"
fi

BUILD_NUM_STRING=$(printf "%02d" $BUILD_NUMBER)

# Create version formats
DAY_BUILD="$DAY_STRING$BUILD_NUM_STRING"
ASSEMBLY_VERSION="$MAJOR_MINOR.$YEAR_MONTH.$DAY_BUILD"
PATCH_NUMBER="$DATE_STRING$BUILD_NUM_STRING"
PACKAGE_VERSION="$MAJOR_MINOR.$PATCH_NUMBER"

echo -e "\n=== VERSION CALCULATED ==="
echo "Assembly Version: $ASSEMBLY_VERSION (4-part for .NET)"
echo "Package Version:  $PACKAGE_VERSION (3-part SemVer2 for Velopack)"
echo "Build Number:     $BUILD_NUMBER"
echo "Date:             $TODAY_STRING"
echo ""

# Update version.json
TEMP_JSON=$(mktemp)
cat > "$TEMP_JSON" << EOF
{
  "majorMinor": "$MAJOR_MINOR",
  "currentVersion": "$PACKAGE_VERSION",
  "assemblyVersion": "$ASSEMBLY_VERSION",
  "lastBuildDate": "$TODAY_STRING",
  "buildNumber": $BUILD_NUMBER,
  "platforms": {
    "windows": $(grep -A 3 '"windows"' "$VERSION_LOG_FILE" | tail -n 3),
    "macos": {
      "lastBuilt": "$(date +"%Y-%m-%dT%H:%M:%S")",
      "version": "$PACKAGE_VERSION"
    }
  }
}
EOF

mv "$TEMP_JSON" "$VERSION_LOG_FILE"
echo "Version log updated!"

# Update .csproj with assembly version
echo -e "\nUpdating .csproj with new version..."
if [ -f "$PROJECT_FILE" ]; then
    sed -i.bak "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|" "$PROJECT_FILE"
    sed -i.bak "s|<FileVersion>.*</FileVersion>|<FileVersion>$ASSEMBLY_VERSION</FileVersion>|" "$PROJECT_FILE"
    sed -i.bak "s|<Version>.*</Version>|<Version>$ASSEMBLY_VERSION</Version>|" "$PROJECT_FILE"
    sed -i.bak "s|<InformationalVersion>.*</InformationalVersion>|<InformationalVersion>$PACKAGE_VERSION</InformationalVersion>|" "$PROJECT_FILE"
    rm -f "$PROJECT_FILE.bak"
    echo ".csproj updated with version $ASSEMBLY_VERSION"
fi

# Clean previous builds
echo -e "\nCleaning previous builds..."
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"

# Build
echo -e "\n=== Building for $ARCH ==="
RUNTIME="osx-$ARCH"
PUBLISH_PATH="$PROJECT_PATH/bin/publish/velopack-$ARCH-local"

rm -rf "$PUBLISH_PATH"

echo "Publishing application..."
dotnet publish "$PROJECT_PATH" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$PUBLISH_PATH" \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:Version="$ASSEMBLY_VERSION"

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo "Removing debug/symbol files to reduce package size..."
find "$PUBLISH_PATH" -type f \( -name "*.pdb" -o -name "*.mdb" -o -name "*.xml" -o -name "*.dbg" \) -delete 2>/dev/null || true
find "$PUBLISH_PATH" -type f -name "*.map" -delete 2>/dev/null || true

echo "Build completed successfully!"

# Playwright browsers - NOT bundled (app will auto-download on first run)
echo -e "\nPlaywright browsers: NOT bundled"
echo "   Application will auto-download Chromium on first run (~150MB)"
echo "   Users will need internet connection on first launch"

# Create app bundle structure
echo -e "\nCreating macOS app bundle..."
APP_NAME="VbdlisTools"
APP_BUNDLE="$DIST_PATH/$APP_NAME.app"
APP_CONTENTS="$APP_BUNDLE/Contents"
APP_MACOS="$APP_CONTENTS/MacOS"
APP_RESOURCES="$APP_CONTENTS/Resources"

mkdir -p "$APP_MACOS"
mkdir -p "$APP_RESOURCES"

# Copy published files (including hidden files like .playwright-browsers)
cp -R "$PUBLISH_PATH/"* "$APP_MACOS/"
cp -R "$PUBLISH_PATH/".* "$APP_MACOS/" 2>/dev/null || true

# Create Info.plist
cat > "$APP_CONTENTS/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>Haihv.Vbdlis.Tools.Desktop</string>
    <key>CFBundleIconFile</key>
    <string>appicon</string>
    <key>CFBundleIdentifier</key>
    <string>vn.vpdkbacninh.vbdlis-tools</string>
    <key>CFBundleName</key>
    <string>VBDLIS Tools</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$PACKAGE_VERSION</string>
    <key>CFBundleVersion</key>
    <string>$PACKAGE_VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

# Copy icon if exists
if [ -f "$PROJECT_PATH/Assets/appicon.icns" ]; then
    cp "$PROJECT_PATH/Assets/appicon.icns" "$APP_RESOURCES/appicon.icns"
    echo "Icon added to app bundle"
fi

echo "App bundle created: $APP_BUNDLE"

# Create unsigned DMG (no code signing)
echo -e "\n=== Creating Unsigned DMG ==="
echo "This DMG will work on all Macs but requires 'xattr -cr' fix"

DMG_NAME="VbdlisTools-$PACKAGE_VERSION-osx-$ARCH.dmg"
DMG_PATH="$DIST_PATH/$DMG_NAME"
TEMP_DMG="$DIST_PATH/temp.dmg"

# Create temporary DMG
hdiutil create -volname "VBDLIS Tools" -srcfolder "$APP_BUNDLE" -ov -format UDRW "$TEMP_DMG"

# Mount and customize DMG
MOUNT_DIR=$(hdiutil attach "$TEMP_DMG" | grep "/Volumes/" | sed 's/.*\/Volumes/\/Volumes/')
ln -s /Applications "$MOUNT_DIR/Applications"

# Create comprehensive README
cat > "$MOUNT_DIR/README.txt" << 'READMEEOF'
VBDLIS Tools - macOS (Unsigned)
================================

⚠️ "App is damaged and can't be opened" ERROR?

This is NORMAL for unsigned apps. Choose one of these methods:

────────────────────────────────────────────────────────────
METHOD 1: Terminal Command (RECOMMENDED - Easiest)
────────────────────────────────────────────────────────────

1. Drag VbdlisTools.app to Applications folder
2. Open Terminal (Applications → Utilities → Terminal)
3. Copy and paste this command:

   xattr -cr /Applications/VbdlisTools.app

4. Press Enter
5. Now open VbdlisTools normally (double-click or Spotlight)

✅ Done! The app will open without any issues.

────────────────────────────────────────────────────────────
METHOD 2: Right-Click (Alternative)
────────────────────────────────────────────────────────────

1. Drag VbdlisTools.app to Applications folder
2. DON'T double-click the app yet
3. Right-click (or Control+Click) on VbdlisTools.app
4. Select "Open" from the menu
5. Click "Open" in the security dialog
6. App will open and macOS will remember this choice

✅ Done! You can now open the app normally in the future.

────────────────────────────────────────────────────────────
WHY THIS HAPPENS?
────────────────────────────────────────────────────────────

• This app is NOT signed with an Apple Developer certificate ($99/year)
• macOS Gatekeeper blocks unsigned apps downloaded from internet
• This is a FREE open-source app, so we don't have Apple Developer signing
• The commands above safely bypass this security check
• Your app and data are safe - this is just a macOS security feature

────────────────────────────────────────────────────────────
FEATURES
────────────────────────────────────────────────────────────

✅ Auto-update via Velopack
   - App checks for updates on startup
   - Download updates from GitHub Releases
   - No need to re-download DMG manually

✅ Native Apple Silicon support
   - Optimized for M1/M2/M3/M4 chips
   - Fast and efficient

⚠️ Playwright Browsers - REQUIRED
   The app requires Chromium browser for web automation.

   If browsers are bundled in this DMG:
   ✅ No download needed! Browsers included (~200MB DMG size)
   ✅ Works offline immediately after installation
   ✅ Ready to use right away

   If browsers are NOT bundled in this DMG:
   ❌ App will NOT work without browsers
   ⚠️  You must download a DMG with bundled browsers
   ⚠️  No automatic installation - browsers must be pre-bundled

────────────────────────────────────────────────────────────
SYSTEM REQUIREMENTS
────────────────────────────────────────────────────────────

• macOS 10.15 (Catalina) or later
• Apple Silicon (M1/M2/M3/M4) - Intel Macs not supported
• ~200MB free disk space (for Playwright)
• Internet connection (first run only)

────────────────────────────────────────────────────────────
TROUBLESHOOTING
────────────────────────────────────────────────────────────

Q: Command not working?
A: Make sure you copied the FULL command including "/Applications/VbdlisTools.app"

Q: Still see error after xattr command?
A: Try Method 2 (right-click → Open)

Q: App crashes on startup?
A: Check logs at ~/Library/Logs/VbdlisTools/

Q: App doesn't work - missing browsers?
A: Make sure you downloaded a DMG with bundled browsers (~200MB size).
   If the DMG is small (~50MB), it doesn't include browsers.

   Check if browsers are included:
   ls -la /Applications/VbdlisTools.app/Contents/MacOS/.playwright-browsers/

Q: Does the app work offline?
A: Yes! If browsers are bundled in the DMG, app works 100% offline.

Q: Where are Playwright browsers stored?
A: Bundled browsers are copied from the app to:
   ~/Library/Caches/ms-playwright/

   This happens automatically on first run.

Q: Can I use pre-installed Playwright browsers?
A: Yes! If you already have Chromium in ~/Library/Caches/ms-playwright/,
   the app will detect and use them.

────────────────────────────────────────────────────────────
MORE INFORMATION
────────────────────────────────────────────────────────────

GitHub: https://github.com/haitnmt/Vbdlis-Tools
Issues: https://github.com/haitnmt/Vbdlis-Tools/issues

────────────────────────────────────────────────────────────

Enjoy using VBDLIS Tools! 🎉
READMEEOF

# Unmount and finalize
hdiutil detach "$MOUNT_DIR"
hdiutil convert "$TEMP_DMG" -format UDZO -o "$DMG_PATH"
rm "$TEMP_DMG"

echo "✅ Unsigned DMG created: $DMG_PATH"

# Clean up temporary app bundle
rm -rf "$APP_BUNDLE"
echo "Cleaned up temporary files"

# Summary
echo -e "\n═══════════════════════════════════════════════════════════════"
echo "                    BUILD SUMMARY"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "📦 Version: $PACKAGE_VERSION"
echo "🏗️  Architecture: $ARCH (Apple Silicon)"
echo "📁 DMG Path: $DMG_PATH"
echo "🔒 Signed: No (unsigned - FREE)"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "                    FEATURES"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "✅ Auto-update via Velopack"
echo "   • App checks for updates on startup"
echo "   • Downloads from GitHub Releases"
echo "   • Users never need to manually download DMG again"
echo ""
echo "✅ Works on ALL Macs (Apple Silicon)"
echo "   • No code signing required"
echo "   • Users run one simple command to install"
echo "   • See README.txt in DMG for instructions"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "                USER INSTALLATION (Simple!)"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "Users will see 'App is damaged' error. This is NORMAL."
echo "They just need to run ONE command:"
echo ""
echo "    xattr -cr /Applications/VbdlisTools.app"
echo ""
echo "Full instructions are in README.txt inside the DMG."
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "                    DISTRIBUTION"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "📤 Upload to GitHub Release:"
echo ""
echo "   Option 1 - Using gh CLI:"
echo "   gh release upload v$PACKAGE_VERSION \"$DMG_PATH\""
echo ""
echo "   Option 2 - Manual upload:"
echo "   https://github.com/haitnmt/Vbdlis-Tools/releases/new"
echo ""
echo "📝 After upload:"
echo "   • Users download DMG"
echo "   • Users run xattr command (one time)"
echo "   • App auto-updates forever after that!"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "                    NEXT STEPS"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "1. ✅ Test DMG on this Mac first"
echo "2. ✅ Test on another Mac (recommended)"
echo "3. ✅ Create GitHub Release with tag: v$PACKAGE_VERSION"
echo "4. ✅ Upload this DMG to the release"
echo "5. ✅ Share download link with users"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "🎉 Build completed successfully!"
echo ""
