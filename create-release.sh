#!/bin/bash
# Script to create a new release tag and trigger GitHub Actions

set -e

echo "=== VBDLIS Tools - Create Release ==="

# Read current version from version.json
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/build/version.json"

if [ ! -f "$VERSION_FILE" ]; then
    echo "❌ version.json not found!"
    exit 1
fi

CURRENT_VERSION=$(grep -o '"currentVersion"[[:space:]]*:[[:space:]]*"[^"]*"' "$VERSION_FILE" | cut -d'"' -f4)

echo ""
echo "📦 Current version: $CURRENT_VERSION"
echo ""

# Ask for confirmation or new version
read -p "Use this version for release? (y/n/custom): " CHOICE

if [ "$CHOICE" = "n" ]; then
    echo "Aborted."
    exit 0
elif [ "$CHOICE" = "custom" ]; then
    read -p "Enter custom version (e.g., 1.0.25120906): " CUSTOM_VERSION
    VERSION="$CUSTOM_VERSION"
else
    VERSION="$CURRENT_VERSION"
fi

TAG_NAME="v$VERSION"

echo ""
echo "🏷️  Creating release tag: $TAG_NAME"
echo ""

# Check if tag already exists
if git rev-parse "$TAG_NAME" >/dev/null 2>&1; then
    echo "⚠️  Tag $TAG_NAME already exists!"
    read -p "Delete and recreate? (y/n): " DELETE_CHOICE
    if [ "$DELETE_CHOICE" = "y" ]; then
        git tag -d "$TAG_NAME"
        git push origin ":refs/tags/$TAG_NAME" 2>/dev/null || true
        echo "✅ Old tag deleted"
    else
        echo "Aborted."
        exit 0
    fi
fi

# Get release notes
echo ""
echo "📝 Enter release notes (press Ctrl+D when done):"
echo "---"
RELEASE_NOTES=$(cat)

# Commit any uncommitted changes
if [ -n "$(git status --porcelain)" ]; then
    echo ""
    echo "📋 Uncommitted changes detected."
    read -p "Commit changes? (y/n): " COMMIT_CHOICE
    if [ "$COMMIT_CHOICE" = "y" ]; then
        read -p "Commit message: " COMMIT_MSG
        git add .
        git commit -m "$COMMIT_MSG"
        echo "✅ Changes committed"
    fi
fi

# Push commits
echo ""
echo "⬆️  Pushing commits to origin..."
git push origin $(git rev-parse --abbrev-ref HEAD)

# Create annotated tag
echo ""
echo "🏷️  Creating tag $TAG_NAME..."
if [ -n "$RELEASE_NOTES" ]; then
    git tag -a "$TAG_NAME" -m "$RELEASE_NOTES"
else
    git tag -a "$TAG_NAME" -m "Release $VERSION"
fi

# Push tag
echo ""
echo "⬆️  Pushing tag to origin..."
git push origin "$TAG_NAME"

echo ""
echo "✅ Release tag created successfully!"
echo ""
echo "📺 GitHub Actions will now:"
echo "   1. Build Windows (Velopack)"
echo "   2. Build macOS arm64 (Apple Silicon)"
echo "   3. Build macOS x64 (Intel)"
echo "   4. Create GitHub Release with all artifacts"
echo ""
echo "🔗 Check progress at:"
echo "   https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/actions"
echo ""
echo "⏱️  Build will take approximately 10-15 minutes"
echo ""
echo "🎉 Done!"
