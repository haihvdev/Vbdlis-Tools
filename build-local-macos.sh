#!/bin/bash
# Script to build locally with auto-incrementing version
# This script builds the application for LOCAL TESTING on macOS
# - Auto-increments version based on date and build number
# - Updates version.json with new version
# - Use this for development and testing
#
# For RELEASE builds, use: ./create-release.sh

set -e

CONFIGURATION="${1:-Release}"
ARCH="${2:-arm64}"

echo "=== Building VBDLIS Tools LOCALLY with Velopack ===" 
echo "Configuration: $CONFIGURATION"
echo "Architecture: $ARCH"
echo "Build Mode: LOCAL (auto-increment version)"
echo ""

# Check if Velopack is installed
echo "Checking for Velopack CLI..."
if ! command -v vpk &> /dev/null; then
    echo "Velopack CLI not found. Installing..."
    dotnet tool install --global vpk
    echo "Velopack CLI installed!"
else
    echo "Velopack CLI found!"
fi

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/src/Haihv.Vbdlis.Tools/Haihv.Vbdlis.Tools.Desktop"
PROJECT_FILE="$PROJECT_PATH/Haihv.Vbdlis.Tools.Desktop.csproj"
PUBLISH_PATH="$PROJECT_PATH/bin/publish/velopack"
OUTPUT_PATH="$SCRIPT_DIR/dist/velopack"
VERSION_LOG_FILE="$SCRIPT_DIR/build/version.json"

# Read or create version log
echo ""
echo "Reading version log..."
if [ ! -f "$VERSION_LOG_FILE" ]; then
    echo "Creating new version log"
    mkdir -p "$SCRIPT_DIR/build"
    cat > "$VERSION_LOG_FILE" << 'EOF'
{
  "majorMinor": "1.0",
  "currentVersion": "",
  "assemblyVersion": "",
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
else
    echo "Found existing version log"
fi

# Read Major.Minor from version log or .csproj
MAJOR_MINOR=$(grep -o '"majorMinor"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
if [ -z "$MAJOR_MINOR" ]; then
    echo "Reading Major.Minor from .csproj..."
    if grep -q '<Version>' "$PROJECT_FILE"; then
        EXISTING_VERSION=$(grep -o '<Version>[^<]*</Version>' "$PROJECT_FILE" | sed 's/<Version>\(.*\)<\/Version>/\1/')
        MAJOR_MINOR=$(echo "$EXISTING_VERSION" | cut -d'.' -f1-2)
    else
        MAJOR_MINOR="1.0"
    fi
    # Update version log with majorMinor
    TEMP_JSON=$(mktemp)
    jq --arg mm "$MAJOR_MINOR" '.majorMinor = $mm' "$VERSION_LOG_FILE" > "$TEMP_JSON"
    mv "$TEMP_JSON" "$VERSION_LOG_FILE"
fi
echo "Using Major.Minor: $MAJOR_MINOR"

# Calculate version - ALWAYS INCREMENT for local builds
echo ""
echo "Calculating new version for LOCAL build..."
TODAY_STRING=$(date +"%Y-%m-%d")
DATE_STRING=$(date +"%y%m%d")
YEAR_MONTH=$(date +"%y%m")
DAY_STRING=$(date +"%d")

# Read current build number and date
LAST_BUILD_DATE=$(grep -o '"lastBuildDate"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_LOG_FILE" | cut -d'"' -f4)
BUILD_NUMBER=$(grep -o '"buildNumber"[[:space:]]*:[[:space:]]*[0-9]*' "$VERSION_LOG_FILE" | grep -o '[0-9]*$')

# Always increment build number for local builds
if [ "$LAST_BUILD_DATE" = "$TODAY_STRING" ]; then
    BUILD_NUMBER=$((BUILD_NUMBER + 1))
    echo "Same day build detected. Incrementing to build #$BUILD_NUMBER"
else
    BUILD_NUMBER=1
    echo "New day detected. Starting with build #$BUILD_NUMBER"
fi

BUILD_NUM_STRING=$(printf "%02d" $BUILD_NUMBER)

# Create two different version formats
# 1. Assembly version (4-part): Major.Minor.YYMM.DDBB
DAY_BUILD="$DAY_STRING$BUILD_NUM_STRING"
ASSEMBLY_VERSION="$MAJOR_MINOR.$YEAR_MONTH.$DAY_BUILD"

# 2. Package version (3-part SemVer2): Major.Minor.YYMMDDBB
PATCH_NUMBER="$DATE_STRING$BUILD_NUM_STRING"
PACKAGE_VERSION="$MAJOR_MINOR.$PATCH_NUMBER"

echo ""
echo "=== VERSION CALCULATED ==="
echo "Assembly Version: $ASSEMBLY_VERSION (4-part for .NET)"
echo "Package Version:  $PACKAGE_VERSION (3-part SemVer2 for Velopack)"
echo "Build Number:     $BUILD_NUMBER"
echo "Date:             $TODAY_STRING"
echo ""

# Update version log
echo "Updating version log..."
TEMP_JSON=$(mktemp)

# Check if jq is available for better JSON handling
if command -v jq &> /dev/null; then
    jq --arg cv "$PACKAGE_VERSION" \
       --arg av "$ASSEMBLY_VERSION" \
       --arg lbd "$TODAY_STRING" \
       --argjson bn $BUILD_NUMBER \
       --arg mlb "$(date +"%Y-%m-%dT%H:%M:%S")" \
       --arg mv "$PACKAGE_VERSION" \
       '.currentVersion = $cv | .assemblyVersion = $av | .lastBuildDate = $lbd | .buildNumber = $bn | .platforms.macos.lastBuilt = $mlb | .platforms.macos.version = $mv' \
       "$VERSION_LOG_FILE" > "$TEMP_JSON"
    mv "$TEMP_JSON" "$VERSION_LOG_FILE"
else
    # Fallback to sed if jq not available
    WINDOWS_SECTION=$(grep -A 3 '"windows"' "$VERSION_LOG_FILE" | tail -n 3)
    cat > "$TEMP_JSON" << EOF
{
  "majorMinor": "$MAJOR_MINOR",
  "currentVersion": "$PACKAGE_VERSION",
  "assemblyVersion": "$ASSEMBLY_VERSION",
  "lastBuildDate": "$TODAY_STRING",
  "buildNumber": $BUILD_NUMBER,
  "platforms": {
    "windows": $WINDOWS_SECTION,
    "macos": {
      "lastBuilt": "$(date +"%Y-%m-%dT%H:%M:%S")",
      "version": "$PACKAGE_VERSION"
    }
  }
}
EOF
    mv "$TEMP_JSON" "$VERSION_LOG_FILE"
fi
echo "Version log updated!"

echo "Version log updated!"

# Update .csproj with assembly version
echo ""
echo "Updating .csproj with new version..."
if [ -f "$PROJECT_FILE" ]; then
    # Create backup
    cp "$PROJECT_FILE" "$PROJECT_FILE.bak"
    
    # Update or add version properties
    if grep -q '<AssemblyVersion>' "$PROJECT_FILE"; then
        sed -i '' "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|" "$PROJECT_FILE"
    else
        sed -i '' "s|<PropertyGroup>|<PropertyGroup>\n    <AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|" "$PROJECT_FILE"
    fi
    
    if grep -q '<FileVersion>' "$PROJECT_FILE"; then
        sed -i '' "s|<FileVersion>.*</FileVersion>|<FileVersion>$ASSEMBLY_VERSION</FileVersion>|" "$PROJECT_FILE"
    else
        sed -i '' "s|<PropertyGroup>|<PropertyGroup>\n    <FileVersion>$ASSEMBLY_VERSION</FileVersion>|" "$PROJECT_FILE"
    fi
    
    if grep -q '<Version>' "$PROJECT_FILE"; then
        sed -i '' "s|<Version>.*</Version>|<Version>$ASSEMBLY_VERSION</Version>|" "$PROJECT_FILE"
    else
        sed -i '' "s|<PropertyGroup>|<PropertyGroup>\n    <Version>$ASSEMBLY_VERSION</Version>|" "$PROJECT_FILE"
    fi
    
    # Add InformationalVersion for Velopack (3-part version)
    if grep -q '<InformationalVersion>' "$PROJECT_FILE"; then
        sed -i '' "s|<InformationalVersion>.*</InformationalVersion>|<InformationalVersion>$PACKAGE_VERSION</InformationalVersion>|" "$PROJECT_FILE"
    else
        # Add after Version tag
        sed -i '' "/<Version>.*<\/Version>/a\\
    <InformationalVersion>$PACKAGE_VERSION</InformationalVersion>
" "$PROJECT_FILE"
    fi
    
    rm -f "$PROJECT_FILE.bak"
    echo ".csproj updated with version $ASSEMBLY_VERSION"
fi

# Use packageVersion for Velopack
VERSION="$PACKAGE_VERSION"

# Clean previous builds
echo ""
echo "Cleaning previous builds..."
if [ -d "$PUBLISH_PATH" ]; then
    rm -rf "$PUBLISH_PATH"
fi
if [ -d "$OUTPUT_PATH" ]; then
    rm -rf "$OUTPUT_PATH"
fi
mkdir -p "$OUTPUT_PATH"

# Step 1: Publish application
echo ""
echo "Step 1: Publishing application..."
RUNTIME="osx-$ARCH"

dotnet publish "$PROJECT_FILE" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$PUBLISH_PATH" \
    /p:PublishSingleFile=false \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    /p:DebugType=embedded

if [ $? -ne 0 ]; then
    echo "Publish failed with exit code $?"
    exit 1
fi

echo "✅ Application published successfully!"

# Step 2: Create Velopack package
echo ""
echo "Step 2: Creating Velopack package..."

vpk pack \
    --packId "Haihv.Vbdlis.Tools.Desktop" \
    --packVersion "$VERSION" \
    --packDir "$PUBLISH_PATH" \
    --mainExe "Haihv.Vbdlis.Tools.Desktop" \
    --outputDir "$OUTPUT_PATH" \
    --packTitle "VBDLIS Tools" \
    --packAuthors "haitnmt" \
    --icon "$PROJECT_PATH/Assets/appicon.icns" \
    --runtime "$RUNTIME"

if [ $? -ne 0 ]; then
    echo "Velopack packaging failed with exit code $?"
    exit 1
fi

echo "✅ Velopack package created!"

# Step 3: Create DMG from portable package
echo ""
echo "Step 3: Creating DMG installer..."

# Find the portable ZIP created by Velopack
PORTABLE_ZIP=$(find "$OUTPUT_PATH" -type f -name "*-Portable.zip" | head -n 1)

if [ -n "$PORTABLE_ZIP" ]; then
    echo "Found portable package: $(basename "$PORTABLE_ZIP")"
    
    # Create temporary directory to extract portable app
    TEMP_EXTRACT="$OUTPUT_PATH/temp_extract"
    rm -rf "$TEMP_EXTRACT"
    mkdir -p "$TEMP_EXTRACT"
    
    # Extract portable ZIP
    echo "Extracting portable package..."
    unzip -q "$PORTABLE_ZIP" -d "$TEMP_EXTRACT"
    
    # Find the .app bundle in extracted files
    APP_BUNDLE=$(find "$TEMP_EXTRACT" -type d -name "*.app" -maxdepth 2 | head -n 1)
    
    if [ -n "$APP_BUNDLE" ]; then
        echo "Found app bundle: $(basename "$APP_BUNDLE")"
        
        DMG_NAME="VbdlisTools-$PACKAGE_VERSION-osx-$ARCH.dmg"
        DMG_PATH="$OUTPUT_PATH/$DMG_NAME"
        TEMP_DMG="$OUTPUT_PATH/temp.dmg"
        TEMP_DIR="$OUTPUT_PATH/dmg_temp"
        
        # Create temporary directory for DMG contents
        rm -rf "$TEMP_DIR"
        mkdir -p "$TEMP_DIR"
        
        # Copy app bundle to temp directory
        cp -R "$APP_BUNDLE" "$TEMP_DIR/"
        
        # Create Applications symlink
        ln -s /Applications "$TEMP_DIR/Applications"
        
        # Create comprehensive README
        cat > "$TEMP_DIR/README.txt" << 'READMEEOF'
VBDLIS Tools - macOS (Unsigned)
================================

⚠️ "App is damaged and can't be opened" ERROR?

This is NORMAL for unsigned apps. Choose one of these methods:

────────────────────────────────────────────────────────────
METHOD 1: Terminal Command (RECOMMENDED - Easiest)
────────────────────────────────────────────────────────────

1. Drag VBDLIS Tools.app to Applications folder
2. Open Terminal (Applications → Utilities → Terminal)
3. Copy and paste this command:

   xattr -cr "/Applications/VBDLIS Tools.app"

4. Press Enter
5. Now open VBDLIS Tools normally (double-click or Spotlight)

✅ Done! The app will open without any issues.

────────────────────────────────────────────────────────────
METHOD 2: Right-Click (Alternative)
────────────────────────────────────────────────────────────

1. Drag VBDLIS Tools.app to Applications folder
2. DON'T double-click the app yet
3. Right-click (or Control+Click) on VBDLIS Tools.app
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

────────────────────────────────────────────────────────────
SYSTEM REQUIREMENTS
────────────────────────────────────────────────────────────

• macOS 10.15 (Catalina) or later
• Apple Silicon (M1/M2/M3/M4) for arm64 build
• .NET 10.0 runtime (included in app)
• Internet connection (for auto-updates)

────────────────────────────────────────────────────────────
TROUBLESHOOTING
────────────────────────────────────────────────────────────

Q: Command not working?
A: Make sure you copied the FULL command including quotes

Q: Still see error after xattr command?
A: Try Method 2 (right-click → Open)

Q: App crashes on startup?
A: Check logs at ~/Library/Logs/VbdlisTools/

────────────────────────────────────────────────────────────
MORE INFORMATION
────────────────────────────────────────────────────────────

GitHub: https://github.com/haitnmt/Vbdlis-Tools
Issues: https://github.com/haitnmt/Vbdlis-Tools/issues

────────────────────────────────────────────────────────────

Enjoy using VBDLIS Tools! 🎉
READMEEOF
        
        # Create temporary DMG
        echo "Creating temporary DMG..."
        hdiutil create -volname "VBDLIS Tools" -srcfolder "$TEMP_DIR" -ov -format UDRW "$TEMP_DMG" > /dev/null 2>&1
        
        if [ $? -eq 0 ]; then
            # Convert to compressed DMG
            echo "Compressing DMG..."
            hdiutil convert "$TEMP_DMG" -format UDZO -o "$DMG_PATH" > /dev/null 2>&1
            rm -f "$TEMP_DMG"
            
            echo "✅ DMG created: $DMG_NAME"
            
            # Show DMG size
            DMG_SIZE=$(ls -lh "$DMG_PATH" | awk '{print $5}')
            echo "   Size: $DMG_SIZE"
        else
            echo "⚠️  Failed to create DMG"
        fi
        
        # Clean up temp directories
        rm -rf "$TEMP_DIR"
    else
        echo "⚠️  App bundle not found in portable package"
    fi
    
    # Clean up extraction directory
    rm -rf "$TEMP_EXTRACT"
else
    echo "⚠️  Portable ZIP not found"
fi

# Step 4: List portable ZIP info
echo ""
echo "Step 4: Portable package info..."
if [ -n "$PORTABLE_ZIP" ]; then
    PORTABLE_SIZE=$(ls -lh "$PORTABLE_ZIP" | awk '{print $5}')
    echo "✅ Portable ZIP: $(basename "$PORTABLE_ZIP") ($PORTABLE_SIZE)"
fi

# List generated files
echo ""
echo "=== BUILD COMPLETED ==="
echo ""
echo "Generated files:"
ls -lh "$OUTPUT_PATH" | tail -n +2 | awk '{print "  - " $9 " (" $5 ")"}'

echo ""
echo "✅ LOCAL BUILD SUCCESSFUL!"
echo ""
echo "Version built: $PACKAGE_VERSION"
echo "Architecture: $ARCH"
echo "Output directory: $OUTPUT_PATH"
echo ""
echo "📦 DISTRIBUTION FILES:"
echo "   DMG:      VbdlisTools-$PACKAGE_VERSION-osx-$ARCH.dmg"
echo "   Portable: Haihv.Vbdlis.Tools.Desktop-osx-Portable.zip"
echo ""
echo "🚀 RECOMMENDED FOR USERS:"
echo "   Share the DMG file - easiest to install!"
echo "   User just needs to run: xattr -cr \"/Applications/VBDLIS Tools.app\""
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "                    NEXT STEPS"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo "📝 This is a LOCAL build for testing."
echo ""
echo "To create a RELEASE on GitHub:"
echo "   ./create-release-macos.sh"
echo ""
echo "This will:"
echo "   1. Create git tag v$PACKAGE_VERSION"
echo "   2. Push to GitHub"
echo "   3. Trigger GitHub Actions to build Windows version"
echo "   4. You can manually upload macOS DMG with:"
echo "      gh release upload v$PACKAGE_VERSION $OUTPUT_PATH/VbdlisTools-$PACKAGE_VERSION-osx-$ARCH.dmg"
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo ""
