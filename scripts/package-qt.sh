#!/usr/bin/env bash
# Builds a self-contained Dustan release archive for the machine it runs on.
#
#   scripts/package-qt.sh <version> [extra CMake arguments...]
#
# Output: artifacts/Dustan-<version>-<rid>.tar.gz (Linux) or .zip (Windows, macOS).
# The archive bundles the Qt runtime and the .NET bridge. libmpv comes from the system on Linux,
# from -DMPV_RUNTIME on Windows, and is copied into the app bundle on macOS. Run Windows builds
# from Git Bash inside an MSVC developer environment.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
version="${1:?usage: package-qt.sh <version> [cmake args...]}"
shift

case "$(uname -s)" in
    Linux) os=linux ;;
    Darwin) os=osx ;;
    MINGW* | MSYS* | CYGWIN*) os=win ;;
    *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
esac
case "$(uname -m)" in
    arm64 | aarch64) arch=arm64 ;;
    *) arch=x64 ;;
esac
rid="$os-$arch"
build="$root/Dustan.Qt/build/package-$rid"
stage="$root/artifacts/stage-$rid"
rm -rf "${stage:?}"
mkdir -p "$root/artifacts"

cmake -S "$root/Dustan.Qt" -B "$build" -G Ninja -DCMAKE_BUILD_TYPE=Release \
    -DDUSTAN_DEPLOY_QT=ON -DDUSTAN_VERSION="$version" -DDUSTAN_RID="$rid" "$@"
cmake --build "$build"

name="Dustan-$version-$rid"
case "$os" in
    linux)
        cmake --install "$build" --prefix "$stage/Dustan"
        tar -C "$stage" -czf "$root/artifacts/$name.tar.gz" Dustan
        echo "artifacts/$name.tar.gz" ;;
    win)
        cmake --install "$build" --prefix "$stage/Dustan"
        (cd "$stage" && cmake -E tar cf "$root/artifacts/$name.zip" --format=zip Dustan)
        echo "artifacts/$name.zip" ;;
    osx)
        cmake --install "$build" --prefix "$stage"
        ditto -c -k --keepParent "$stage/Dustan.app" "$root/artifacts/$name.zip"
        echo "artifacts/$name.zip" ;;
esac
