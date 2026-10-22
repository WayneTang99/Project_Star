#!/usr/bin/env bash
# 安装一次性 P0 程序并运行诊断；不创建玩家数据，不覆盖已有发布。
set -euo pipefail
if [ "$#" -ne 3 ]; then
    printf 'Usage: %s <archive> <p0-release-id> <sha256>\n' "$0" >&2
    exit 2
fi
archive=$(realpath -e "$1")
release_id=$2
expected_hash=$3
case "$archive" in "$HOME/project-star-p0/"*) ;; *) printf 'Archive must be in ~/project-star-p0\n' >&2; exit 2 ;; esac
case "$release_id" in p0-*) ;; *) printf 'Invalid release prefix\n' >&2; exit 2 ;; esac
case "$release_id" in *[!a-zA-Z0-9._-]*) printf 'Invalid release name\n' >&2; exit 2 ;; esac
if [[ ! "$expected_hash" =~ ^[a-fA-F0-9]{64}$ ]]; then printf 'Invalid SHA256\n' >&2; exit 2; fi
printf '%s  %s\n' "$expected_hash" "$archive" | sha256sum -c -
tar -tzf "$archive" | awk '/^\// || /(^|\/)\.\.(\/|$)/ {bad=1} END {exit bad}'
release="/opt/project-star/releases/$release_id"
logs=/var/log/project-star/p0
if [ -e "$release" ]; then printf 'Release already exists: %s\n' "$release" >&2; exit 1; fi
sudo -n install -d -o root -g root -m 755 "$release"
sudo -n tar --no-same-owner -xzf "$archive" -C "$release"
sudo -n chmod -R u=rwX,go=rX "$release"
sudo -n chmod 755 "$release/project-star-p0.x86_64"
sudo -n install -d -o "$(id -un)" -g "$(id -gn)" -m 750 "$logs"
if [ -w "$release" ]; then printf 'Runtime user can write release directory\n' >&2; exit 1; fi
python3 - "$release" <<'PY'
import hashlib, json, pathlib, sys
root = pathlib.Path(sys.argv[1]).resolve()
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8-sig'))
for item in manifest['files']:
    path = (root / item['path']).resolve()
    if root not in path.parents:
        raise SystemExit('Manifest path outside release')
    data = path.read_bytes()
    if len(data) != item['bytes'] or hashlib.sha256(data).hexdigest().upper() != item['sha256'].upper():
        raise SystemExit('Manifest mismatch: ' + item['path'])
print('Release manifest: OK')
PY
cd /tmp
timeout 180 "$release/project-star-p0.x86_64" --headless -- \
    --report="$logs/$release_id.json" > "$logs/$release_id.log" 2>&1
if grep -Eq '^(ERROR:|SCRIPT ERROR:|SERVER_BASELINE_FAILED=)' "$logs/$release_id.log"; then
    printf 'Godot reported an error: %s\n' "$logs/$release_id.log" >&2
    exit 1
fi
test -s "$logs/$release_id.json"
printf 'P0 release passed: %s\nReport: %s\n' "$release" "$logs/$release_id.json"
