# 将云端回环API转发到本机；保持此终端运行，Ctrl+C关闭，不打开公网API端口。
[CmdletBinding()]
param(
    [string]$SshTarget = 'ubuntu@118.89.176.115',
    [ValidateRange(1, 65535)][int]$SshPort = 22,
    [ValidateRange(1, 65535)][int]$LocalPort = 15080,
    [string]$IdentityFile = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.ssh/project_star_waynetang.pem')
)
$ErrorActionPreference = 'Stop'
$tunnelIdentity = (Resolve-Path -LiteralPath $IdentityFile).Path
& ssh -i $tunnelIdentity -p $SshPort -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes `
    -o ConnectTimeout=10 -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -N `
    -L "127.0.0.1:${LocalPort}:127.0.0.1:5080" $SshTarget
exit $LASTEXITCODE
