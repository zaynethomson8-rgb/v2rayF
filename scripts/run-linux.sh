#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
chmod +x "$DIR/cores/xray" "$DIR/v2rayF" "$DIR/v2rayF.Desktop" "$0" 2>/dev/null || true

if [[ -x "$DIR/v2rayF" ]]; then
  exec "$DIR/v2rayF" "$@"
fi

if [[ -x "$DIR/v2rayF.Desktop" ]]; then
  exec "$DIR/v2rayF.Desktop" "$@"
fi

echo "v2rayF launcher not found in $DIR (expected v2rayF or v2rayF.Desktop)." >&2
exit 1
