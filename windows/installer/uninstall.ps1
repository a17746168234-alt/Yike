param(
    [switch]$Cleanup,
    [string]$Root = '',
    [int]$ParentId = 0
)

$ErrorActionPreference = 'Stop'

function Resolve-YikeRoot([string]$Candidate) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) { throw '没有找到 Yike 的安装目录。' }
    $resolved = [IO.Path]::GetFullPath($Candidate).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
    $volumeRoot = [IO.Path]::GetPathRoot($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
    if ([string]::Equals($resolved,$volumeRoot,[StringComparison]::OrdinalIgnoreCase)) { throw '拒绝把磁盘根目录作为卸载目标。' }
    foreach ($required in @('Yike.exe','MainWindow.xaml','uninstall.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $resolved $required) -PathType Leaf)) { throw "安装目录校验失败：缺少 $required。" }
    }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $resolved 'Yike.exe'))
    if ($info.ProductName -ne 'Yike for Windows') { throw '安装目录校验失败：目标不是 Yike。' }
    return $resolved
}

function Test-PathInside([string]$Path,[string]$RootPath) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $resolvedPath = [IO.Path]::GetFullPath($Path)
        $prefix = $RootPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        return $resolvedPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)
    } catch { return $false }
}

function Stop-YikeProcesses([string]$InstallRoot) {
    # Speech and OCR helpers are assigned to Yike's Windows job object and exit
    # with the app. Restrict probing to our named native executables so unrelated
    # Python processes cannot delay or block uninstall.
    foreach ($processName in @('Yike','whisper-stream','whisper-cli')) {
        foreach ($running in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
            try {
                if (Test-PathInside $running.MainModule.FileName $InstallRoot) {
                    $running.Kill()
                    if (-not $running.WaitForExit(5000)) { throw "无法停止 $($running.ProcessName)，请手动结束后重试。" }
                }
            } catch [System.ComponentModel.Win32Exception] {
            } catch [System.InvalidOperationException] {
            } finally {
                $running.Dispose()
            }
        }
    }
}

if ($Cleanup) {
    $cleanupRoot = Resolve-YikeRoot $Root
    if ($ParentId -gt 0) {
        try { Wait-Process -Id $ParentId -Timeout 5 -ErrorAction SilentlyContinue } catch {}
    }
    Remove-Item -LiteralPath $cleanupRoot -Force -Recurse
    try { Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue } catch {}
    exit 0
}

$installRoot = Resolve-YikeRoot (Split-Path -Parent $PSCommandPath)
Stop-YikeProcesses $installRoot

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Yike'
$ownsRegistration = $false
if (Test-Path -LiteralPath $uninstallKey) {
    $registeredRoot = (Get-ItemProperty -LiteralPath $uninstallKey -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
    $ownsRegistration = -not [string]::IsNullOrWhiteSpace($registeredRoot) -and [string]::Equals([IO.Path]::GetFullPath($registeredRoot).TrimEnd('\'),$installRoot,[StringComparison]::OrdinalIgnoreCase)
}

$startMenuRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Yike'
$startMenuLink = Join-Path $startMenuRoot 'Yike.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Yike.lnk'
if ($ownsRegistration) {
    Remove-Item -LiteralPath $startMenuLink -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $desktopLink -Force -ErrorAction SilentlyContinue
    if ((Test-Path -LiteralPath $startMenuRoot -PathType Container) -and @(Get-ChildItem -LiteralPath $startMenuRoot -Force).Count -eq 0) {
        Remove-Item -LiteralPath $startMenuRoot -Force
    }
    Remove-Item -LiteralPath $uninstallKey -Force
}

$installParent = Split-Path -Parent $installRoot
$installLeaf = Split-Path -Leaf $installRoot
$quarantine = Join-Path $installParent ($installLeaf + '.uninstall-' + [guid]::NewGuid().ToString('N'))
$expectedPrefix = [IO.Path]::GetFullPath($installParent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedQuarantine = [IO.Path]::GetFullPath($quarantine)
if (-not $resolvedQuarantine.StartsWith($expectedPrefix,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedQuarantine) -notmatch ('^' + [regex]::Escape($installLeaf) + '\.uninstall-[0-9a-f]{32}$')) {
    throw '卸载临时目录校验失败。'
}
$temporaryScript = Join-Path ([IO.Path]::GetTempPath()) ('Yike-uninstall-' + [guid]::NewGuid().ToString('N') + '.ps1')
Copy-Item -LiteralPath $PSCommandPath -Destination $temporaryScript -Force
[IO.Directory]::Move($installRoot,$resolvedQuarantine)
$arguments = @(
    '-NoProfile','-NonInteractive','-WindowStyle','Hidden','-ExecutionPolicy','Bypass',
    '-File',('"' + $temporaryScript + '"'),'-Cleanup','-Root',('"' + $resolvedQuarantine + '"'),'-ParentId',([string]$PID)
)
Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden | Out-Null
