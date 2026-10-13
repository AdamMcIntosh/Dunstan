#!/bin/sh
# Starts Dustan from its install folder (<prefix>/lib/dustan) so it can find the bundled bridge.
here=$(dirname "$(readlink -f "$0")")
exec "$here/../lib/dustan/Dustan" "$@"
