#!/usr/bin/env bash
# Packages a framework-dependent linux-x64 `dotnet publish` output (passed as $1)
# into installers/linux/SmartCommander-x86_64.AppImage. Requires .NET 10 runtime
# installed on the machine that later runs the AppImage (framework-dependent build).
set -euo pipefail

PUBLISH_DIR="${1:?Usage: build-appimage.sh <publish-dir>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPDIR="${SCRIPT_DIR}/AppDir"
APPIMAGETOOL="${SCRIPT_DIR}/appimagetool.AppImage"

rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin" "${APPDIR}/usr/share/applications" "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

cp -r "${PUBLISH_DIR}/." "${APPDIR}/usr/bin/"
cp "${SCRIPT_DIR}/smartcommander.desktop" "${APPDIR}/usr/share/applications/"
cp "${SCRIPT_DIR}/smartcommander.desktop" "${APPDIR}/"
cp "${SCRIPT_DIR}/smartcommander.png" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/"
cp "${SCRIPT_DIR}/smartcommander.png" "${APPDIR}/"

cat > "${APPDIR}/AppRun" <<'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export LD_LIBRARY_PATH="${HERE}/usr/bin:${LD_LIBRARY_PATH:-}"
exec "${HERE}/usr/bin/SmartCommander" "$@"
EOF
chmod +x "${APPDIR}/AppRun" "${APPDIR}/usr/bin/SmartCommander"

if [ ! -f "${APPIMAGETOOL}" ]; then
    curl -L -o "${APPIMAGETOOL}" \
        "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x "${APPIMAGETOOL}"
fi

ARCH=x86_64 "${APPIMAGETOOL}" "${APPDIR}" "${SCRIPT_DIR}/SmartCommander-x86_64.AppImage"
