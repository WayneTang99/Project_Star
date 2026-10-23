#!/usr/bin/env bash
# 从隔离源码导出无图形规则进程；输出必须在源码之外，不读取玩家数据。
set -euo pipefail
[[ $# == 4 ]] || { printf 'Usage: %s SOURCE GODOT LINUX_TEMPLATE OUTPUT\n' "$0" >&2; exit 2; }
source_root=$(realpath "$1")
godot=$(realpath "$2")
template=$(realpath "$3")
output=$(realpath -m "$4")
case "$output/" in "$source_root/"*) printf 'Output must be outside source\n' >&2; exit 2;; esac
[[ ! -e "$output" ]] || { printf 'Use a new output directory\n' >&2; exit 2; }
mkdir -p "$output/source" "$output/rules"
rsync -a "$source_root/scripts/" "$output/source/scripts/"
cp "$source_root/Project_Star.csproj" "$source_root/Project_Star.sln" "$output/source/"
cat > "$output/source/project.godot" <<'PROJECT'
config_version=5
[application]
config/name="Project Star Rules"
run/main_scene="res://scripts/Presentation/Server/ServerRules.tscn"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")
[dotnet]
project/assembly_name="Project_Star"
[rendering]
renderer/rendering_method="gl_compatibility"
PROJECT
cat > "$output/source/export_presets.cfg" <<PRESET
[preset.0]
name="Rules Linux"
platform="Linux"
runnable=true
dedicated_server=true
custom_features="dedicated_server"
export_filter="scenes"
export_files=PackedStringArray("res://scripts/Presentation/Server/ServerRules.tscn")
include_filter=""
exclude_filter="ref/*,docs/*,output/*,.git/*,*.pem,*.key"
script_export_mode=2
[preset.0.options]
custom_template/release="$template"
binary_format/architecture="x86_64"
binary_format/embed_pck=false
dotnet/include_scripts_content=false
dotnet/embed_build_outputs=false
PRESET
dotnet build "$output/source/Project_Star.csproj" --nologo > "$output/build.log" 2>&1
"$godot" --headless --editor --path "$output/source" --import > "$output/import.log" 2>&1
"$godot" --headless --path "$output/source" --export-release 'Rules Linux' "$output/rules/project-star-rules.x86_64" > "$output/export.log" 2>&1
if grep -Eq '(ERROR:|SCRIPT ERROR:)' "$output/import.log" "$output/export.log"; then
  printf 'Godot import or export reported errors; inspect build logs.\n' >&2
  exit 1
fi
assembly="$output/rules/data_Project_Star_linuxbsd_x86_64/Project_Star.dll"
sha256sum "$assembly" | cut -d ' ' -f1 > "$output/rules/rules-version.txt"
chmod +x "$output/rules/project-star-rules.x86_64"
timeout 60 "$output/rules/project-star-rules.x86_64" --headless --quiet -- --verify-save > "$output/save-verification.log" 2>&1
grep -q '^SAVE_VERIFICATION_PASSED$' "$output/save-verification.log"
printf 'Rules exported and save verification passed: %s\n' "$output/rules"
