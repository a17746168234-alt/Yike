$ErrorActionPreference = 'Stop'
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Yike')).TrimEnd([IO.Path]::DirectorySeparatorChar)
$installRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSCommandPath)).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not [string]::Equals($installRoot,$expectedRoot,[StringComparison]::OrdinalIgnoreCase)) {
    throw '卸载脚本不在 Yike 的标准安装目录中，已拒绝删除文件。'
}

function Stop-YikeProcess([string]$Name,[string]$ExpectedPath) {
    foreach ($running in @(Get-Process -Name $Name -ErrorAction SilentlyContinue)) {
        try {
            $runningPath = $running.MainModule.FileName
            if (-not [string]::Equals($runningPath,$ExpectedPath,[StringComparison]::OrdinalIgnoreCase)) { continue }
            $running.Kill()
            if (-not $running.WaitForExit(5000)) { throw "无法停止 $Name，请手动结束后重试。" }
        } catch [System.ComponentModel.Win32Exception] {
            continue
        } finally { $running.Dispose() }
    }
}

Stop-YikeProcess 'Yike' (Join-Path $installRoot 'Yike.exe')
Stop-YikeProcess 'whisper-stream' (Join-Path $installRoot 'whisper-runtime\Release\whisper-stream.exe')

$startMenuRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Yike'
$desktopRoot = [Environment]::GetFolderPath('Desktop')
$startMenuLink = Join-Path $startMenuRoot 'Yike.lnk'
Remove-Item -LiteralPath $startMenuLink -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $desktopRoot 'Yike.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $startMenuRoot -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Yike' -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Force -Recurse
}

Write-Host 'Yike 已卸载。'
