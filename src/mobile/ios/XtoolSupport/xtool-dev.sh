#!/usr/bin/env bash
# Build/run XerahSMobile with xtool. Arguments go to `xtool dev` (default: run).
#   XtoolSupport/xtool-dev.sh              # build, sign, install and launch on the connected device
#   XtoolSupport/xtool-dev.sh build --ipa  # build an .ipa without installing it
set -euo pipefail

ios_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ios_dir"

python3 -I XtoolSupport/generate-info-plists.py
exec xtool dev "$@"
