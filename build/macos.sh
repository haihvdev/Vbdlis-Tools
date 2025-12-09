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
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|g" "$PROJECT_FILE"
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$ASSEMBLY_VERSION</FileVersion>|g" "$PROJECT_FILE"
sed -i "s|<Version>.*</Version>|<Version>$ASSEMBLY_VERSION</Version>|g" "$PROJECT_FILE"
echo "Updated .csproj: AssemblyVersion=$ASSEMBLY_VERSION"

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

    # Step 2: Create Velopack release for macOS
    echo "Creating Velopack release for macOS $ARCH_NAME..."

    # Find icon file
    ICON_PATH=""
    if [ -f "$PROJECT_PATH/Assets/appicon.icns" ]; then
        ICON_PATH="$PROJECT_PATH/Assets/appicon.icns"
        echo "Using icon: $ICON_PATH"
    else
        echo "Warning: No .icns file found in Assets folder"
    fi

    # Build vpk command
    VPK_CMD=(
        "vpk" "pack"
        "--packId" "VbdlisTools"
        "--packVersion" "$PACKAGE_VERSION"
        "--packDir" "$PUBLISH_PATH"
        "--mainExe" "Haihv.Vbdlis.Tools.Desktop"
        "--outputDir" "$DIST_PATH/$ARCH_NAME"
        "--packTitle" "VBDLIS Tools"
        "--packAuthors" "vpdkbacninh.vn"
        "--runtime" "$RUNTIME"
    )

    if [ -n "$ICON_PATH" ]; then
        VPK_CMD+=("--icon" "$ICON_PATH")
    fi

    echo "Running: ${VPK_CMD[@]}"
    "${VPK_CMD[@]}"

    if [ $? -ne 0 ]; then
        echo "Velopack packaging failed for $ARCH_NAME!"
        exit 1
    fi

    echo "Velopack package created for $ARCH_NAME: $DIST_PATH/$ARCH_NAME"
}

# Build based on architecture selection
if [ "$ARCH" = "x64" ] || [ "$ARCH" = "both" ]; then
    build_for_arch "osx-x64" "x64"
fi

if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "both" ]; then
    build_for_arch "osx-arm64" "arm64"
fi

# Create deployment guide
README_PATH="$DIST_PATH/README.txt"
cat > "$README_PATH" << EOF
VBDLIS Tools - Velopack Installer for macOS
Version: $PACKAGE_VERSION
============================================

OUTPUT FILES (per architecture):
--------------------------------
  - VbdlisTools-$PACKAGE_VERSION-osx-[arch].zip  - macOS app bundle for installation
  - RELEASES                                      - Update manifest

DEPLOYMENT:
----------
1. For NEW users:
   - Download and extract VbdlisTools-$PACKAGE_VERSION-osx-[arch].zip
   - Drag VbdlisTools.app to Applications folder
   - Run the app

2. For AUTO-UPDATE:
   - Upload all files from each architecture folder to web server
   - URL example: https://your-server.com/vbdlis-tools/mac/arm64/
   - Or for x64: https://your-server.com/vbdlis-tools/mac/x64/

3. Update Configuration:
   - Velopack SDK is already integrated in the app
   - Update source is configured to use GitHub Releases
   - App will check for updates automatically

INSTALLATION:
------------
- Installs to: /Applications/VbdlisTools.app
- No admin rights required
- First run may require "Open anyway" in System Preferences > Security

AUTO-UPDATE:
-----------
- App checks for updates on startup
- Downloads delta updates (only changed files)
- Updates in background
- Restart to apply updates

PUBLISHING NEW VERSION:
----------------------
1. Build new version:
   ./build/macos.sh [Configuration] [Architecture]
   Example: ./build/macos.sh Release both

2. Upload to GitHub Releases:
   - Tag format: v$PACKAGE_VERSION
   - Upload all files from dist/velopack-macos/
   - Users auto-update on next launch

VERSION FORMAT:
--------------
Major.Minor.YYMMDDBB (3-part SemVer2)
- Major.Minor: From .csproj (e.g., 1.0)
- YYMMDDBB: Patch combining date + build (e.g., 25120901)

UNINSTALL:
---------
- Drag VbdlisTools.app to Trash
- Clean app data: ~/Library/Application Support/VbdlisTools/

For more info: https://docs.velopack.io/
EOF

echo -e "\n=== Build Completed Successfully ===" 
echo "Version: $PACKAGE_VERSION"
echo "Output folder: $DIST_PATH"
echo -e "\nNext steps:"
echo "1. Test installation on macOS"
echo "2. Upload to GitHub Releases for auto-update"
echo -e "\nNote: Playwright browsers NOT included. Downloaded on first run."
