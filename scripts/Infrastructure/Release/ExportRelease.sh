#!/usr/bin/env bash
# 在 CI 临时目录分别导出程序；不读取服务器凭据、玩家存档或数据库。
set -euo pipefail
if [ "$#" -ne 4 ]; then
    printf 'Usage: %s <server|client> <godot> <templates> <output>\n' "$0" >&2
    exit 2
fi
target=$1
godot=$(realpath -e "$2")
templates=$(realpath -e "$3")
output=$(realpath -m "$4")
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
case "$target" in server|client) ;; *) printf 'Unknown target\n' >&2; exit 2 ;; esac
case "$output/" in "$root/"*) printf 'Output must be outside the checkout\n' >&2; exit 2 ;; esac
source="$output/$target-source"
package="$output/$target"
if [ -e "$source" ] || [ -e "$package" ]; then printf 'Build directories already exist\n' >&2; exit 1; fi
mkdir -p "$source" "$package"
rsync -a "$root/scripts/" "$source/scripts/"
cp "$root/Project_Star.csproj" "$root/Project_Star.sln" "$root/project.godot" "$source/"
if [ "$target" = client ]; then
    rsync -a "$root/art/" "$source/art/"
    rsync -a "$root/audio/" "$source/audio/"
    cp "$root/Main.tscn" "$root/Playtest.tscn" "$root/icon.svg" "$source/"
    platform='Windows Desktop'
    template="$templates/windows_release_x86_64.exe"
    filter=all_resources
    files='PackedStringArray()'
    dedicated=false
    features=''
    executable="$package/ProjectStar.exe"
else
    platform=Linux
    template="$templates/linux_release.x86_64"
    filter=scenes
    files='PackedStringArray("res://scripts/Presentation/Verification/ServerBaseline.tscn")'
    dedicated=true
    features=dedicated_server
    executable="$package/project-star-server.x86_64"
    # 当前仅发布 P0 诊断入口；正式常驻服务器实现后需在这里更换场景。
    python3 - "$source/project.godot" <<'PY'
import pathlib, re, sys
p = pathlib.Path(sys.argv[1])
text = p.read_text(encoding='utf-8-sig')
text = re.sub(r'(?m)^run/main_scene=.*$', 'run/main_scene="res://scripts/Presentation/Verification/ServerBaseline.tscn"', text)
text = re.sub(r'(?m)^config/icon=.*\n?', '', text)
p.write_text(text, encoding='utf-8')
PY
fi
test -f "$template"
cat > "$source/export_presets.cfg" <<EOF
[preset.0]
name="Release"
platform="$platform"
runnable=true
dedicated_server=$dedicated
custom_features="$features"
export_filter="$filter"
export_files=$files
include_filter=""
exclude_filter="ref/*,docs/*,output/*,.git/*,.github/*,*.pem,*.key"
script_export_mode=2
[preset.0.options]
custom_template/release="$template"
binary_format/architecture="x86_64"
binary_format/embed_pck=false
dotnet/include_scripts_content=false
dotnet/embed_build_outputs=false
application/modify_resources=false
codesign/enable=false
EOF
dotnet build "$source/Project_Star.csproj" --nologo > "$output/$target-build.log" 2>&1
run_godot() {
    local log=$1
    shift
    if ! "$godot" "$@" > "$log" 2>&1; then cat "$log" >&2; return 1; fi
    if grep -Eq '(ERROR:|SCRIPT ERROR:)' "$log"; then cat "$log" >&2; return 1; fi
}
run_godot "$output/$target-import.log" --headless --editor --path "$source" --import
run_godot "$output/$target-export.log" --headless --path "$source" --export-release Release "$executable"
test -s "$executable"
test -s "${executable%.*}.pck"
if [ "$target" = server ]; then
    chmod +x "$executable"
    printf '%s\n' 'P0 diagnostic server only. This is not a persistent PvP service.' \
        'Run: ./project-star-server.x86_64 --headless -- --report=/absolute/path/outside/program/report.json' \
        'Keep player data outside the program directory.' > "$package/README.txt"
    # 除检查构建退出码，还运行最终 Linux 包，诊断结果写在程序目录之外。
    timeout 180 "$executable" --headless -- --report="$output/server-smoke.json" > "$output/server-smoke.log" 2>&1
    if grep -Eq '(ERROR:|SCRIPT ERROR:|SERVER_BASELINE_FAILED=)' "$output/server-smoke.log"; then
        cat "$output/server-smoke.log" >&2
        exit 1
    fi
    tar -czf "$output/ProjectStar-linux-server-x86_64.tar.gz" -C "$package" .
else
    (cd "$package" && zip -qr "$output/ProjectStar-windows-client-x86_64.zip" .)
fi
printf 'Exported %s package in %s\n' "$target" "$output"
