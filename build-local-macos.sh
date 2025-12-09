#!/bin/bash
# Local macOS build script (for dev machine only)
# Build on your Mac → Upload to GitHub Release manually
# Similar to build-local.ps1 for Windows

set -e

echo "=== Local macOS Build Script ==="
echo "This script is for building on your Mac and uploading manually"
echo "For GitHub Actions, use macos.sh instead"

# Same build logic as macos.sh but with optional self-signing
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/macos.sh"

# After build completes, optionally self-sign
echo ""
echo "=== Post-Build Options ==="
echo ""
echo "Your DMG is ready at: $DMG_PATH"
echo ""
echo "OPTIONS:"
echo "1. Upload as-is (users need xattr -cr fix)"
echo "2. Self-sign (only works on your Mac)"
echo "3. Sign with Developer ID (requires Apple Developer Program)"
echo ""
read -p "Choose option (1-3): " choice

case $choice in
    1)
        echo "✅ DMG ready for upload as-is"
        echo "Users will need to run: xattr -cr /Applications/VbdlisTools.app"
        ;;
    2)
        echo "⚠️  Self-signing (only works on your Mac)..."
        # This won't help end-users!
        security find-identity -v -p codesigning
        read -p "Enter certificate name (or press Enter to skip): " cert_name
        if [ -n "$cert_name" ]; then
            # Re-mount DMG to sign the app inside
            MOUNT_DIR=$(hdiutil attach "$DMG_PATH" | grep "/Volumes/" | sed 's/.*\/Volumes/\/Volumes/')
            codesign --deep --force --sign "$cert_name" "$MOUNT_DIR/VbdlisTools.app"
            hdiutil detach "$MOUNT_DIR"
            echo "⚠️  WARNING: Self-signed app only works on your Mac!"
            echo "End-users will still see 'damaged' error"
        fi
        ;;
    3)
        echo "🔐 Signing with Developer ID..."
        read -p "Enter Developer ID: " dev_id
        read -p "Enter Apple ID: " apple_id
        read -p "Enter Team ID: " team_id
        read -s -p "Enter App-Specific Password: " app_password
        echo ""
        
        if [ -n "$dev_id" ] && [ -n "$apple_id" ]; then
            # Re-mount DMG
            MOUNT_DIR=$(hdiutil attach "$DMG_PATH" | grep "/Volumes/" | sed 's/.*\/Volumes/\/Volumes/')
            
            # Sign with hardened runtime
            codesign --deep --force --verify --verbose \
                --sign "$dev_id" \
                --options runtime \
                --timestamp \
                "$MOUNT_DIR/VbdlisTools.app"
            
            hdiutil detach "$MOUNT_DIR"
            
            # Notarize
            echo "Submitting for notarization..."
            xcrun notarytool submit "$DMG_PATH" \
                --apple-id "$apple_id" \
                --password "$app_password" \
                --team-id "$team_id" \
                --wait
            
            # Staple ticket
            xcrun stapler staple "$DMG_PATH"
            
            echo "✅ App signed and notarized!"
            echo "Users can install without xattr fix"
        else
            echo "❌ Missing credentials, skipping"
        fi
        ;;
    *)
        echo "Invalid choice, DMG ready as-is"
        ;;
esac

echo ""
echo "=== Next Steps ==="
echo "1. Test the DMG on another Mac"
echo "2. Upload to GitHub Release manually"
echo "3. Or push tag to trigger automated workflow"
echo ""
echo "Manual upload command:"
echo "gh release upload v<VERSION> $DMG_PATH"
