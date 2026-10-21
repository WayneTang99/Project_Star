# 按验证入口生成的清单顺序运行独立批次，超时或失败后继续收集其余结果。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [string[]]$Batch,
    [switch]$List,
    [switch]$SkipBuild,
    [ValidateRange(10, 3600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$godotExecutable = (Resolve-Path -LiteralPath $GodotPath).Path
$runDirectory = Join-Path $projectRoot ('output/verification/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

function Invoke-VerificationProcess([string]$Argument, [string]$LogName) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $godotExecutable
    $start.WorkingDirectory = $projectRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($value in @('--headless', '--path', $projectRoot, 'res://Main.tscn', '--', $Argument)) { $start.ArgumentList.Add($value) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $timedOut = $false
    $started = $false
    $outputFile = $null
    $errorFile = $null
    try {
        [void]$process.Start()
        $started = $true
        $outputFile = [IO.FileStream]::new((Join-Path $runDirectory "$LogName.log"), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite, 1, $true)
        $errorFile = [IO.FileStream]::new((Join-Path $runDirectory "$LogName.errors.log"), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite, 1, $true)
        $stdout = $process.StandardOutput.BaseStream.CopyToAsync($outputFile)
        $stderr = $process.StandardError.BaseStream.CopyToAsync($errorFile)
        @{ batch = $LogName; pid = $process.Id; timeoutSeconds = $TimeoutSeconds } | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'current.json')
        while (!$process.WaitForExit(250)) {
            if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $timedOut = $true
                $process.Kill($true)
                $process.WaitForExit()
                break
            }
        }
        $stdout.GetAwaiter().GetResult()
        $stderr.GetAwaiter().GetResult()
        $outputFile.Dispose(); $outputFile = $null
        $errorFile.Dispose(); $errorFile = $null
        $output = [IO.File]::ReadAllText((Join-Path $runDirectory "$LogName.log"))
        $errors = [IO.File]::ReadAllText((Join-Path $runDirectory "$LogName.errors.log"))
        return [pscustomobject]@{ output = $output; errors = $errors; exitCode = $process.ExitCode; timedOut = $timedOut; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 2) }
    }
    finally {
        if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        if ($outputFile) { $outputFile.Dispose() }
        if ($errorFile) { $errorFile.Dispose() }
        $process.Dispose()
    }
}

Push-Location $projectRoot
try {
    if (!$SkipBuild) {
        dotnet build Project_Star.csproj --nologo *> (Join-Path $runDirectory 'build.log')
        if ($LASTEXITCODE -ne 0) { throw "构建失败，见 $runDirectory/build.log" }
    }
    $discovery = Invoke-VerificationProcess '--verify-list' 'list'
    if ($discovery.exitCode -ne 0 -or $discovery.timedOut) { throw "无法读取验证清单，见 $runDirectory/list.errors.log" }
    $line = $discovery.output -split '\r?\n' | Where-Object { $_.StartsWith('VERIFY_BATCHES_JSON=') } | Select-Object -First 1
    if (!$line) { throw '验证入口未返回批次清单。' }
    $batches = @($line.Substring('VERIFY_BATCHES_JSON='.Length) | ConvertFrom-Json)
    if ($List) { $batches | Format-Table key, name, count -AutoSize; exit 0 }
    foreach ($key in $Batch) { if ($key -notin $batches.key) { throw "未知批次：$key。请先使用 -List。" } }
    if ($Batch) { $batches = @($batches | Where-Object { $_.key -in $Batch }) }
    $summary = [Collections.Generic.List[object]]::new()
    foreach ($item in $batches) {
        Write-Host "RUN $($item.key) $($item.name) ($($item.count)项)"
        $result = Invoke-VerificationProcess "--verify-batch=$($item.key)" $item.key
        $hasEngineError = $result.errors -match '(?m)^ERROR:|Fatal error|Unhandled exception'
        $status = if ($result.timedOut) { 'timeout' } elseif ($result.exitCode -ne 0 -or $hasEngineError) { 'failed' } else { 'passed' }
        $failedChecks = @([regex]::Matches($result.output, '(?m)^✗ (.+)\r?$') | ForEach-Object { $_.Groups[1].Value.TrimEnd() })
        $summary.Add([pscustomobject]@{ key = $item.key; name = $item.name; count = $item.count; status = $status; exitCode = $result.exitCode; seconds = $result.seconds; engineErrors = [bool]$hasEngineError; failedChecks = $failedChecks })
        $summary.ToArray() | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $runDirectory 'summary.json')
        Write-Host "$status $($item.key) $($result.seconds)s"
    }
    $failed = @($summary | Where-Object status -ne 'passed')
    Write-Host "完成：$($summary.Count - $failed.Count)/$($summary.Count)批通过。日志：$runDirectory"
    if ($failed.Count) { $failed | Format-Table key, name, status -AutoSize; exit 1 }
    exit 0
}
finally { Pop-Location }
