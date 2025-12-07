#!/bin/bash
# Script to build macOS x64 (Intel) application
# Requires: .NET 10.0 SDK

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Get configuration and version from arguments, or use defaults
CONFIGURATION="${1:-Release}"
VERSION="${2:-1.0.0}"

echo "=== Building VBDLIS Tools for macOS x64 (Intel) ==="

# Call main build script with x64 architecture
"$SCRIPT_DIR/build-macos.sh" "$CONFIGURATION" "$VERSION" x64
