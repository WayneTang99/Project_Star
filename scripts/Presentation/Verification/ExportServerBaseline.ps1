# 在隔离目录导出 P0 Linux 程序；不修改正式导出配置，不包含玩家数据。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [Parameter(Mandatory = $true)][string]$LinuxTemplatePath,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$enginePath = (Resolve-Path -LiteralPath $GodotPath).Path
$templatePath = (Resolve-Path -LiteralPath $LinuxTemplatePath).Path.Replace('\', '/')
$runDirectory = Join-Path $projectRoot ('output/pvp-p0/export-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
if (Test-Path -LiteralPath $runDirectory) { throw "导出目录已存在：$runDirectory" }
$isolatedSource = Join-Path $runDirectory 'source'
$releaseDirectory = Join-Path $runDirectory 'release'
New-Item -ItemType Directory -Path $isolatedSource, $releaseDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts') -Destination $isolatedSource -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'Project_Star.csproj'), (Join-Path $projectRoot 'Project_Star.sln') -Destination $isolatedSource
$projectConfig = @'
config_version=5
[application]
config/name="Project Star P0"
run/main_scene="res://scripts/Presentation/Verification/ServerBaseline.tscn"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")
[dotnet]
project/assembly_name="Project_Star"
[rendering]
renderer/rendering_method="gl_compatibility"
'@
[IO.File]::WriteAllText((Join-Path $isolatedSource 'project.godot'), $projectConfig)
$presetConfig = @"
[preset.0]
name="P0 Linux"
platform="Linux"
runnable=true
dedicated_server=true
custom_features="dedicated_server"
export_filter="scenes"
export_files=PackedStringArray("res://scripts/Presentation/Verification/ServerBaseline.tscn")
include_filter=""
exclude_filter=""
script_export_mode=2
[preset.0.options]
custom_template/release="$templatePath"
binary_format/architecture="x86_64"
binary_format/embed_pck=false
dotnet/include_scripts_content=false
dotnet/embed_build_outputs=false
"@
[IO.File]::WriteAllText((Join-Path $isolatedSource 'export_presets.cfg'), $presetConfig)
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $enginePath
$start.WorkingDirectory = $isolatedSource
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('--headless', '--path', $isolatedSource, '--export-release', 'P0 Linux',
    (Join-Path $releaseDirectory 'project-star-p0.x86_64'))) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        throw "导出超时：$runDirectory"
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $runDirectory 'export.log'), $output + $errors)
    if ($process.ExitCode -ne 0 -or ($output + $errors) -match '(?m)^(ERROR:|SCRIPT ERROR:)') {
        throw "导出失败：$runDirectory/export.log"
    }
    foreach ($required in @('project-star-p0.x86_64', 'project-star-p0.pck', 'data_Project_Star_linuxbsd_x86_64/Project_Star.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $releaseDirectory $required))) { throw "缺少发布文件：$required" }
    }
    $manifest = Get-ChildItem -LiteralPath $releaseDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = [IO.Path]::GetRelativePath($releaseDirectory, $_.FullName).Replace('\', '/');
            bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    @{ kind = 'p0-diagnostic-release'; platform = 'linux-x64'; files = @($manifest) } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $releaseDirectory 'manifest.json') -Encoding utf8NoBOM
    $archive = Join-Path $runDirectory 'project-star-p0-linux-x64.tar.gz'
    & tar -czf $archive -C $releaseDirectory .
    if ($LASTEXITCODE -ne 0) { throw '打包失败' }
    Write-Output "P0 发布包：$archive"
}
finally {
    $process.Dispose()
}
