#!/bin/bash

# Script to test the native macOS notifier sidecar
# Usage: ./test-notifier.sh [message]

MESSAGE="${1:-Hello from the independent test script!}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
SOURCE_DIR="$REPO_ROOT/src/Extensions/MacOS"
BINARY_PATH="$REPO_ROOT/bin/TenSecondTom.Extensions.MacOS.app/Contents/MacOS/notifier"

# Ensure the binary exists
if [ ! -f "$BINARY_PATH" ]; then
    echo "Notifier binary not found at $BINARY_PATH"
    echo "Compiling..."
    (cd "$REPO_ROOT" && make extensions)
fi

# Create a test payload
# We use a unique ID to track it
ID=$(uuidgen)
JSON_PAYLOAD=$(cat <<EOF
{
  "id": "$ID",
  "title": "Test Notification",
  "message": "$MESSAGE",
  "actions": [
    { "id": "action_1", "label": "Primary Action" },
    { "id": "action_2", "label": "Secondary Action" }
  ]
}
EOF
)

echo "Sending notification..."
echo "Payload: $JSON_PAYLOAD"
echo "----------------------------------------"
echo "Check your Notification Center."
echo "Click a button to see the output below (or Ctrl+C to exit if you don't click)."

# Run the notifier
"$BINARY_PATH" "$JSON_PAYLOAD"
