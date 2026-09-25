#!/usr/bin/env bash
# Single Source of Truth Bootstrap Script
# Target: Ubuntu 24.04 / 26.04 LTS
# Purpose: MinGW-w64 cross compilation, Wine test runtime, vcpkg, and CMake toolchain.

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
VCPKG_DIR="${VCPKG_ROOT:-${HOME}/vcpkg}"
TOOLCHAIN_FILE="${REPO_ROOT}/toolchain-x86_64-w64-mingw32.cmake"

log() { printf '[bootstrap] %s\n' "$*"; }
die() { printf '[bootstrap][ERROR] %s\n' "$*" >&2; exit 1; }

[[ "$(id -u)" -ne 0 ]] || die 'Run as a normal user with sudo access; do not run as root.'
command -v sudo >/dev/null 2>&1 || die 'sudo is required.'
sudo -v

log '1/7 Enabling i386 architecture support'
sudo dpkg --add-architecture i386

log '2/7 Updating package lists'
sudo apt-get update

log '3/7 Installing build, cross-compile, Wine and packaging prerequisites'
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y \
  build-essential cmake ninja-build gcc-mingw-w64 g++-mingw-w64 \
  gfortran-mingw-w64 p7zip-full zip unzip tar curl git pkg-config \
  wine wine64 wine32

log '4/7 Selecting POSIX MinGW thread model where alternatives exist'
declare -A alternatives=(
  [x86_64-w64-mingw32-gcc]=/usr/bin/x86_64-w64-mingw32-gcc-posix
  [x86_64-w64-mingw32-g++]=/usr/bin/x86_64-w64-mingw32-g++-posix
  [i686-w64-mingw32-gcc]=/usr/bin/i686-w64-mingw32-gcc-posix
  [i686-w64-mingw32-g++]=/usr/bin/i686-w64-mingw32-g++-posix
)
for tool in "${!alternatives[@]}"; do
  target="${alternatives[$tool]}"
  [[ -x "$target" ]] && sudo update-alternatives --set "$tool" "$target" || true
done

log '5/7 Installing vcpkg'
if [[ ! -d "${VCPKG_DIR}/.git" ]]; then
  git clone https://github.com/microsoft/vcpkg.git "$VCPKG_DIR"
fi
if [[ ! -x "${VCPKG_DIR}/vcpkg" ]]; then
  "${VCPKG_DIR}/bootstrap-vcpkg.sh" -disableMetrics
fi
export VCPKG_ROOT="$VCPKG_DIR"
export PATH="${VCPKG_ROOT}:${PATH}"

if [[ -f "${HOME}/.bashrc" ]] && ! grep -Fq 'export VCPKG_ROOT=' "${HOME}/.bashrc"; then
  printf '\n# vcpkg\nexport VCPKG_ROOT=%q\nexport PATH="$VCPKG_ROOT:$PATH"\n' "$VCPKG_DIR" >> "${HOME}/.bashrc"
fi

log '6/7 Writing CMake MinGW toolchain'
cat > "$TOOLCHAIN_FILE" <<'EOF'
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

set(CMAKE_C_COMPILER x86_64-w64-mingw32-gcc-posix)
set(CMAKE_CXX_COMPILER x86_64-w64-mingw32-g++-posix)
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)

set(CMAKE_EXE_LINKER_FLAGS_INIT "-static -static-libgcc -static-libstdc++")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "-static -static-libgcc -static-libstdc++")
set(CMAKE_MODULE_LINKER_FLAGS_INIT "-static -static-libgcc -static-libstdc++")

find_program(WINE_EXECUTABLE NAMES wine64 wine REQUIRED)
set(CMAKE_CROSSCOMPILING_EMULATOR "${WINE_EXECUTABLE}")

set(CMAKE_FIND_ROOT_PATH /usr/x86_64-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
EOF

log '7/7 Initializing headless Wine environment'
export WINEDLLOVERRIDES='mscoree,gecko='
export WINEDEBUG='-all'
export WINEPREFIX="${WINEPREFIX:-${HOME}/.wine-camfrog}"
wineboot --init >/dev/null 2>&1 || die 'Wine initialization failed.'

log 'Validating toolchain'
command -v cmake >/dev/null
command -v ninja >/dev/null
command -v x86_64-w64-mingw32-gcc-posix >/dev/null
command -v x86_64-w64-mingw32-g++-posix >/dev/null
command -v wine64 >/dev/null || command -v wine >/dev/null
[[ -x "${VCPKG_DIR}/vcpkg" ]] || die 'vcpkg bootstrap did not produce an executable.'

cat <<EOF

======================================================================
Bootstrap complete

Native CMake:
  cmake -S . -B build-mingw -G Ninja \
    -DCMAKE_TOOLCHAIN_FILE="${TOOLCHAIN_FILE}" \
    -DCMAKE_BUILD_TYPE=Release
  cmake --build build-mingw

vcpkg:
  cmake -S . -B build-mingw -G Ninja \
    -DCMAKE_TOOLCHAIN_FILE="${VCPKG_DIR}/scripts/buildsystems/vcpkg.cmake" \
    -DVCPKG_TARGET_TRIPLET=x64-mingw-static \
    -DCMAKE_BUILD_TYPE=Release

Wine:
  export WINEPREFIX="${WINEPREFIX}"
  wine64 path/to/app.exe

Important:
  This repository is a Windows .NET/WPF application. MinGW/CMake is for
  native helper components and cross-platform native tests only; it cannot
  replace the supported .NET WPF production build.

  Supported production build:
    .\build-release.ps1
======================================================================
EOF
