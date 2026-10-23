# 开发维护验证：生成一次性邀请码，仅经进程环境传给客户端；日志不保存凭据或存档。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [string]$SshTarget = 'ubuntu@118.89.176.115',
    [string]$IdentityFile = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.ssh/project_star_waynetang.pem')
)
$ErrorActionPreference = 'Stop'
$onlineRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$onlineLogs = Join-Path $onlineRoot 'output/online-client'
New-Item -ItemType Directory -Force $onlineLogs | Out-Null
$onlineAdmin = "sudo -n -u project-star bash -c 'set -a; source /etc/project-star/api.env; set +a; exec /opt/project-star/api-current/host/ProjectStar.Server create-invite 1 1'"
$onlineInvite = (& ssh -i (Resolve-Path -LiteralPath $IdentityFile).Path -o IdentitiesOnly=yes -o BatchMode=yes `
    -o StrictHostKeyChecking=yes -o ConnectTimeout=10 $SshTarget $onlineAdmin).Trim()
if ($LASTEXITCODE -ne 0 -or $onlineInvite.Length -ne 43) { throw 'Unable to prepare one-use test invitation.' }
$onlineStart = [Diagnostics.ProcessStartInfo]::new()
$onlineStart.FileName = (Resolve-Path -LiteralPath $GodotPath).Path
$onlineStart.WorkingDirectory = $onlineRoot
$onlineStart.UseShellExecute = $false
$onlineStart.CreateNoWindow = $true
$onlineStart.RedirectStandardOutput = $true
$onlineStart.RedirectStandardError = $true
foreach ($onlineArg in @('--headless','--path',$onlineRoot,'res://Verification.tscn','--','--verify-online-cloud')) { $onlineStart.ArgumentList.Add($onlineArg) }
$onlineStart.Environment['PROJECT_STAR_TEST_INVITATION'] = $onlineInvite
$onlineProcess = [Diagnostics.Process]::new()
$onlineProcess.StartInfo = $onlineStart
$onlineStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
try {
    [void]$onlineProcess.Start()
    $onlineInvite = $null
    $onlineStart.Environment.Remove('PROJECT_STAR_TEST_INVITATION') | Out-Null
    $onlineOut = $onlineProcess.StandardOutput.ReadToEndAsync()
    $onlineError = $onlineProcess.StandardError.ReadToEndAsync()
    if (-not $onlineProcess.WaitForExit(90000)) { $onlineProcess.Kill($true); $onlineProcess.WaitForExit(); throw 'Client verification exceeded 90 seconds.' }
    $onlineOutputPath = Join-Path $onlineLogs "cloud-$onlineStamp.log"
    [IO.File]::WriteAllText($onlineOutputPath, $onlineOut.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $onlineLogs "cloud-$onlineStamp.errors.log"), $onlineError.GetAwaiter().GetResult())
    Get-Content -LiteralPath $onlineOutputPath
    Write-Host "Cloud verification log: $onlineOutputPath"
    exit $onlineProcess.ExitCode
}
finally { if ($onlineProcess.Id -and -not $onlineProcess.HasExited) { $onlineProcess.Kill($true) }; $onlineProcess.Dispose() }
