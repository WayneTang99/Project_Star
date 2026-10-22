# 运行独立 P0 入口并保留结果；适用于已配置 Godot .NET 的本机或云端。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [switch]$SkipBuild,
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$enginePath = (Resolve-Path -LiteralPath $GodotPath).Path
$reportDirectory = Join-Path $projectRoot ('output/pvp-p0/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
if (!$SkipBuild) {
    dotnet build (Join-Path $projectRoot 'Project_Star.csproj') --nologo *> (Join-Path $reportDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "构建失败：$reportDirectory/build.log" }
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $enginePath
$start.WorkingDirectory = $projectRoot
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('--headless', '--path', $projectRoot,
    'res://scripts/Presentation/Verification/ServerBaseline.tscn', '--',
    ('--report=' + (Join-Path $reportDirectory 'report.json')))) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        throw "P0 运行超时：$reportDirectory"
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $reportDirectory 'run.log'), $output + $errors)
    if ($process.ExitCode -ne 0 -or $errors -match '(?m)^(ERROR:|SCRIPT ERROR:)' -or $output -notmatch 'SERVER_BASELINE_JSON=') {
        throw "P0 验证失败：$reportDirectory/run.log"
    }
    Write-Output "P0 验证通过：$reportDirectory/report.json"
}
finally {
    $process.Dispose()
}
