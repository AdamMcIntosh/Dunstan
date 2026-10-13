#!/usr/bin/env bash
# Builds the Qt Quick app (Dustan.Qt) and its .NET bridge in Release.
#
#   scripts/build-qt.sh                      # build into Dustan.Qt/build/release
#   scripts/build-qt.sh --install ~/.local   # also install (Linux: ~/.local/bin/dustan)
#
# Requires: CMake 3.21+, Ninja, a C++20 compiler, Qt 6.5+ (Quick, QuickControls2, OpenGL),
# libmpv, and the .NET 8 SDK. See docs/QT-APP.md for per-platform packages.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
build="${BUILD_DIR:-$root/Dustan.Qt/build/release}"
prefix=""
if [[ "${1:-}" == "--install" ]]; then
    prefix="${2:?usage: build-qt.sh --install <prefix>}"
    shift 2
fi

cmake -S "$root/Dustan.Qt" -B "$build" -G Ninja -DCMAKE_BUILD_TYPE=Release "$@"
cmake --build "$build"

if [[ -n "$prefix" ]]; then
    cmake --install "$build" --prefix "$prefix"
    echo "Installed. Launch with: $prefix/bin/dustan"
else
    echo "Built. Launch with: $build/Dustan"
fi
