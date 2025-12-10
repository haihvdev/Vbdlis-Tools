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

# Step 3: Create ZIP archive of the installer
echo ""
echo "Step 3: Creating ZIP archive of installer..."

# Find the installer file (e.g., VbdlisTools-1.0.25121023-osx-arm64.pkg or similar)
INSTALLER_FILE=$(find "$OUTPUT_PATH" -type f -name "*.pkg" -o -name "*Setup*" | head -n 1)

if [ -n "$INSTALLER_FILE" ]; then
    INSTALLER_NAME=$(basename "$INSTALLER_FILE")
    ZIP_NAME="${INSTALLER_NAME%.*}.zip"
    ZIP_PATH="$OUTPUT_PATH/$ZIP_NAME"
    
    # Create README for installer ZIP
    README_PATH="$OUTPUT_PATH/README-INSTALLER.txt"
    cat > "$README_PATH" << EOF
VBDLIS Tools - Installer Package (macOS)
Version: $PACKAGE_VERSION
Architecture: $ARCH
==========================================

CONTENTS:
- $INSTALLER_NAME (Velopack Installer)

INSTALLATION:
1. Extract this ZIP file
2. Run $INSTALLER_NAME
3. Follow the installation wizard

FEATURES:
- Full installer with auto-update support
- Automatic Velopack updates from GitHub Releases
- Native Apple Silicon support

SYSTEM REQUIREMENTS:
- macOS 10.15 (Catalina) or later
- Apple Silicon (M1/M2/M3/M4) for arm64 build
- .NET 10.0 (included)

For more info: https://github.com/haitnmt/Vbdlis-Tools
EOF
    
    # Create ZIP with installer and README
    echo "Creating ZIP: $ZIP_NAME..."
    cd "$OUTPUT_PATH"
    zip -q "$ZIP_NAME" "$INSTALLER_NAME" "README-INSTALLER.txt"
    cd "$SCRIPT_DIR"
    
    # Remove the README after zipping
    rm -f "$README_PATH"
    
    echo "✅ Installer ZIP created: $ZIP_NAME"
else
    echo "⚠️  Installer file not found, skipping ZIP creation"
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
echo "Output directory: $OUTPUT_PATH"
echo ""
echo "📝 NOTE: This is a LOCAL build for testing."
echo "   To create a RELEASE, use: ./create-release.sh"
echo ""
