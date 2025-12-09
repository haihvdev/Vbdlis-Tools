#!/bin/bash
# Script to build and package macOS application with Velopack support
# Velopack provides auto-update functionality for macOS apps
# Requires: .NET 10.0 SDK, Velopack CLI (vpk)

set -e

CONFIGURATION="${1:-Release}"
ARCH="${3:-arm64}"  # x64, arm64, or both

echo "=== Building VBDLIS Tools for macOS with Velopack ==="
echo "Configuration: $CONFIGURATION"
echo "Architecture: $ARCH"

# Check if Velopack is installed
echo -e "\nChecking for Velopack CLI..."
if ! command -v vpk &> /dev/null; then
    echo "Velopack CLI not found. Installing..."
    dotnet tool install --global vpk
else
    echo "Velopack CLI found!"
fi

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/../src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop"
PROJECT_FILE="$PROJECT_PATH/Haihv.Vbdlis.Tools.Desktop.csproj"
DIST_PATH="$SCRIPT_DIR/../dist/velopack-macos"
VERSION_LOG_FILE="$SCRIPT_DIR/version.json"

# Read or create version log
echo -e "\nReading version log..."
if [ -f "$VERSION_LOG_FILE" ]; then
    echo "Found existing version log"
    MAJOR_MINOR=$(grep -o '"majorMinor"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
    LAST_BUILD_DATE=$(grep -o '"lastBuildDate"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
    LAST_BUILD_NUMBER=$(grep -o '"buildNumber"[[:space:]]*:[[:space:]]*[0-9]*' "$VERSION_LOG_FILE" | grep -o '[0-9]*$')
    echo "Major.Minor: $MAJOR_MINOR"
    echo "Last build date: $LAST_BUILD_DATE"
    echo "Last build number: $LAST_BUILD_NUMBER"
else
    echo "Creating new version log"
    MAJOR_MINOR="1.0"
    LAST_BUILD_DATE=""
    LAST_BUILD_NUMBER=0
fi

# Read Major.Minor from .csproj if not in log
if [ -z "$MAJOR_MINOR" ] || [ "$MAJOR_MINOR" = "null" ]; then
    echo "Reading Major.Minor from .csproj..."
    MAJOR_MINOR=$(grep -oP '<Version>\K[^<]+' "$PROJECT_FILE" | head -1 | cut -d'.' -f1-2)
    if [ -z "$MAJOR_MINOR" ]; then
        MAJOR_MINOR="1.0"
    fi
    echo "Using Major.Minor: $MAJOR_MINOR"
fi

# Generate date parts
DATE_STRING=$(date +%y%m%d)
TODAY_STRING=$(date +%Y-%m-%d)
DAY_STRING=$(date +%d)

# Check if version is locked (already set by prepare-release.ps1)
IS_VERSION_LOCKED=false
if [ "$LAST_BUILD_DATE" = "$TODAY_STRING" ]; then
    # Check if .csproj matches version.json
    CSPROJ_VERSION=$(grep -oP '<Version>\K[^<]+' "$PROJECT_FILE" | head -1)
    if [ -n "$CSPROJ_VERSION" ]; then
        CSPROJ_PATCH=$(echo "$CSPROJ_VERSION" | cut -d'.' -f4)
        LOG_ASSEMBLY_VERSION=$(grep -o '"assemblyVersion"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
        LOG_PATCH=$(echo "$LOG_ASSEMBLY_VERSION" | cut -d'.' -f4)
        
        if [ "$CSPROJ_PATCH" = "$LOG_PATCH" ]; then
            IS_VERSION_LOCKED=true
            echo "Version is LOCKED - using existing version from .csproj"
            ASSEMBLY_VERSION="$CSPROJ_VERSION"
            LOG_PACKAGE_VERSION=$(grep -o '"currentVersion"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
            PACKAGE_VERSION="$LOG_PACKAGE_VERSION"
            BUILD_NUM=$LAST_BUILD_NUMBER
            echo "Locked Version: $ASSEMBLY_VERSION (Assembly)"
            echo "Locked Package Version: $PACKAGE_VERSION (Velopack 3-part SemVer2)"
            echo "Build Number: $BUILD_NUM"
        fi
    fi
fi

if [ "$IS_VERSION_LOCKED" = false ]; then
    # Calculate build number from version log
    echo "Calculating build number for today..."
    if [ "$LAST_BUILD_DATE" = "$TODAY_STRING" ]; then
        # Same day, increment build number
        BUILD_NUM=$((LAST_BUILD_NUMBER + 1))
        echo "Same day build detected. Incrementing to build #$BUILD_NUM"
    else
        # New day, reset to 1
        BUILD_NUM=1
        echo "New day detected. Starting with build #$BUILD_NUM"
    fi

    BUILD_NUM_STRING=$(printf "%02d" $BUILD_NUM)

    # Create two different version formats
    # 1. Assembly version (4-part): Major.Minor.YYMM.DDBB
    YEAR_MONTH_STRING=$(date +%y%m)
    DAY_BUILD_STRING="$DAY_STRING$BUILD_NUM_STRING"
    ASSEMBLY_VERSION="$MAJOR_MINOR.$YEAR_MONTH_STRING.$DAY_BUILD_STRING"

    # 2. Package version (3-part SemVer2): Major.Minor.YYMMDDBB
    PATCH_NUMBER="$DATE_STRING$BUILD_NUM_STRING"
    PACKAGE_VERSION="$MAJOR_MINOR.$PATCH_NUMBER"

    echo "Version: $ASSEMBLY_VERSION (Assembly)"
    echo "Package Version: $PACKAGE_VERSION (Velopack 3-part SemVer2)"
    echo "Build Number: $BUILD_NUM"

    # Update .csproj with assembly version
    echo -e "\nUpdating version in .csproj to $ASSEMBLY_VERSION..."
    sed -i.bak "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|g" "$PROJECT_FILE"
    sed -i.bak "s|<FileVersion>.*</FileVersion>|<FileVersion>$ASSEMBLY_VERSION</FileVersion>|g" "$PROJECT_FILE"
    sed -i.bak "s|<Version>.*</Version>|<Version>$ASSEMBLY_VERSION</Version>|g" "$PROJECT_FILE"
    rm -f "${PROJECT_FILE}.bak"
    echo "Updated .csproj: AssemblyVersion=$ASSEMBLY_VERSION"
fi

# Clean previous builds
echo -e "\nCleaning previous builds..."
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"

# Function to build and package for specific architecture
build_for_arch() {
    local RUNTIME=$1
    local ARCH_NAME=$2

    echo -e "\n=== Building for $ARCH_NAME with Velopack ==="

    local PUBLISH_PATH="$PROJECT_PATH/bin/publish/velopack-$ARCH_NAME"

    # Clean
    rm -rf "$PUBLISH_PATH"

    # Step 1: Publish application
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
        echo "Build failed for $ARCH_NAME!"
        exit 1
    fi

    # Update version log after successful build
    echo "Updating version log for macOS $ARCH_NAME..."
    TIMESTAMP=$(date +%Y-%m-%dT%H:%M:%S)
    
    # Update version.json using jq if available, otherwise use sed
    if command -v jq &> /dev/null; then
        # Use jq for better JSON handling
        jq --arg mm "$MAJOR_MINOR" \
           --arg cv "$PACKAGE_VERSION" \
           --arg av "$ASSEMBLY_VERSION" \
           --arg date "$TODAY_STRING" \
           --argjson bn "$BUILD_NUM" \
           --arg ts "$TIMESTAMP" \
           --arg pv "$PACKAGE_VERSION" \
           '.majorMinor = $mm | 
            .currentVersion = $cv | 
            .assemblyVersion = $av | 
            .lastBuildDate = $date | 
            .buildNumber = $bn | 
            .platforms.macos.lastBuilt = $ts | 
            .platforms.macos.version = $pv' \
           "$VERSION_LOG_FILE" > "${VERSION_LOG_FILE}.tmp" && \
        mv "${VERSION_LOG_FILE}.tmp" "$VERSION_LOG_FILE"
    else
        # Fallback: simple sed-based update
        cat > "$VERSION_LOG_FILE" << EOF
{
  "majorMinor": "$MAJOR_MINOR",
  "currentVersion": "$PACKAGE_VERSION",
  "assemblyVersion": "$ASSEMBLY_VERSION",
  "lastBuildDate": "$TODAY_STRING",
  "buildNumber": $BUILD_NUM,
  "platforms": {
    "windows": $(grep -A2 '"windows"' "$VERSION_LOG_FILE" 2>/dev/null || echo '{"lastBuilt": null, "version": null}'),
    "macos": {
      "lastBuilt": "$TIMESTAMP",
      "version": "$PACKAGE_VERSION"
    }
  }
}
EOF
    fi
    
    echo "Version log updated: $VERSION_LOG_FILE"

    # Remove Playwright browsers (but keep driver files)
    echo "Removing Playwright browsers from output..."
    find "$PUBLISH_PATH/.playwright" -type d -name "chromium-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "firefox-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "webkit-*" -exec rm -rf {} + 2>/dev/null || true
    find "$PUBLISH_PATH/.playwright" -type d -name "ffmpeg-*" -exec rm -rf {} + 2>/dev/null || true

    # Step 2: Create DMG installer for macOS
    echo "Creating DMG installer for macOS $ARCH_NAME..."

    # Create app bundle structure
    APP_NAME="VbdlisTools"
    APP_BUNDLE="$DIST_PATH/$ARCH_NAME/$APP_NAME.app"
    APP_CONTENTS="$APP_BUNDLE/Contents"
    APP_MACOS="$APP_CONTENTS/MacOS"
    APP_RESOURCES="$APP_CONTENTS/Resources"

    mkdir -p "$APP_MACOS"
    mkdir -p "$APP_RESOURCES"

    # Copy published files to app bundle
    cp -R "$PUBLISH_PATH/"* "$APP_MACOS/"

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
    ICON_PATH=""
    if [ -f "$PROJECT_PATH/Assets/appicon.icns" ]; then
        ICON_PATH="$PROJECT_PATH/Assets/appicon.icns"
        cp "$ICON_PATH" "$APP_RESOURCES/appicon.icns"
        echo "Using icon: $ICON_PATH"
    else
        echo "Warning: No .icns file found in Assets folder"
    fi

    # Create DMG using hdiutil
    DMG_NAME="VbdlisTools-$PACKAGE_VERSION-osx-$ARCH_NAME.dmg"
    DMG_PATH="$DIST_PATH/$ARCH_NAME/$DMG_NAME"
    TEMP_DMG="$DIST_PATH/$ARCH_NAME/temp.dmg"

    echo "Creating DMG: $DMG_NAME..."

    # Create temporary DMG
    hdiutil create -volname "VBDLIS Tools" -srcfolder "$APP_BUNDLE" -ov -format UDRW "$TEMP_DMG"

    # Mount temporary DMG
    MOUNT_DIR=$(hdiutil attach "$TEMP_DMG" | grep "/Volumes/" | sed 's/.*\/Volumes/\/Volumes/')

    # Create symbolic link to Applications folder
    ln -s /Applications "$MOUNT_DIR/Applications"

    # Create README
    cat > "$MOUNT_DIR/README.txt" << EOF
VBDLIS Tools - macOS Version
Version: $PACKAGE_VERSION
=============================

INSTALLATION:
1. Drag VbdlisTools.app to Applications folder
2. Open Terminal and run this command:
   xattr -cr /Applications/VbdlisTools.app
3. Now you can open the app normally

⚠️ "App is damaged" ERROR?
If you see "VbdlisTools is damaged and can't be opened":
1. Open Terminal (Applications → Utilities → Terminal)
2. Copy and paste this command:
   xattr -cr /Applications/VbdlisTools.app
3. Press Enter
4. Now open VbdlisTools normally

WHY THIS HAPPENS:
- App is not signed with Apple Developer certificate
- macOS Gatekeeper blocks unsigned apps
- The command removes quarantine attribute

ALTERNATIVE METHOD:
1. Right-click VbdlisTools.app → Show Package Contents
2. Or: sudo spctl --master-disable (disable Gatekeeper - not recommended)

FEATURES:
- Native Apple Silicon ($ARCH_NAME) support
- Auto-update via Velopack
- No admin rights required

FIRST RUN:
- Playwright browsers will download on first use
- May take a few minutes

For more info: https://github.com/haitnmt/Vbdlis-Tools
EOF

    # Unmount
    hdiutil detach "$MOUNT_DIR"

    # Convert to compressed read-only DMG
    hdiutil convert "$TEMP_DMG" -format UDZO -o "$DMG_PATH"
    rm "$TEMP_DMG"

    # Clean up app bundle (keep only DMG)
    rm -rf "$APP_BUNDLE"

    echo "✅ DMG created: $DMG_NAME"
    echo "DMG location: $DMG_PATH"
}

# Build based on architecture selection
# Note: Only arm64 (Apple Silicon) is supported
if [ "$ARCH" = "x64" ]; then
    echo "⚠️  Warning: x64 (Intel) build is deprecated and not recommended."
    echo "    Modern Macs use Apple Silicon (arm64)."
    echo "    Use 'arm64' instead, or contact support if Intel build is required."
    exit 1
fi

if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "both" ]; then
    build_for_arch "osx-arm64" "arm64"
fi

# Create deployment guide
README_PATH="$DIST_PATH/README.txt"
cat > "$README_PATH" << EOF
VBDLIS Tools - DMG Installer for macOS
Version: $PACKAGE_VERSION
=======================================

OUTPUT FILES (per architecture):
--------------------------------
  - VbdlisTools-$PACKAGE_VERSION-osx-[arch].dmg  - DMG installer with drag-and-drop

DEPLOYMENT:
----------
1. For NEW users:
   - Download VbdlisTools-$PACKAGE_VERSION-osx-[arch].dmg
   - Open DMG file
   - Drag VbdlisTools.app to Applications folder
   - First run: Right-click → Open (to bypass Gatekeeper)

2. For GitHub Release:
   - Upload DMG file to GitHub Releases
   - Users download and install directly

INSTALLATION:
------------
- Installs to: /Applications/VbdlisTools.app
- No admin rights required
- Drag-and-drop installation

AUTO-UPDATE:
-----------
- App checks for updates on startup (via Velopack)
- Downloads updates from GitHub Releases
- Restart to apply updates

PUBLISHING NEW VERSION:
----------------------
1. Build new version:
   ./build/macos.sh [Configuration] [Architecture]
   Example: ./build/macos.sh Release arm64

2. Upload to GitHub Releases:
   - Tag format: v$PACKAGE_VERSION
   - Upload DMG file
   - Users download new DMG to update

VERSION FORMAT:
--------------
Major.Minor.YYMMDDBB (3-part SemVer2)
- Major.Minor: From .csproj (e.g., 1.0)
- YYMMDDBB: Patch combining date + build (e.g., 25120901)

UNINSTALL:
---------
- Drag VbdlisTools.app to Trash
- Clean app data: ~/Library/Application Support/VbdlisTools/

For more info: https://github.com/haitnmt/Vbdlis-Tools
EOF

echo -e "\n=== Build Completed Successfully ===" 
echo "Version: $PACKAGE_VERSION"
echo "Output folder: $DIST_PATH"
echo -e "\nGenerated files:"
ls -lh "$DIST_PATH"/*/*.dmg 2>/dev/null || echo "No DMG files found"
echo -e "\nNext steps:"
echo "1. Test DMG installer on macOS"
echo "2. Upload DMG to GitHub Releases"
echo "3. Users can download and drag-drop to install"
echo -e "\nNote: Playwright browsers NOT included. Downloaded on first run."
