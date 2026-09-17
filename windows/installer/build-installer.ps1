param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\release'),
    [string]$SourceExe = '',
    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$releaseRoot = Join-Path $projectRoot 'release\Yike'
$fallbackExe = Join-Path $releaseRoot 'Yike.exe'
if ([string]::IsNullOrWhiteSpace($SourceExe)) {
    $SourceExe = $fallbackExe
}
if (-not (Test-Path -LiteralPath $SourceExe)) {
    throw '找不到 Yike.exe。'
}
$SourceExe = (Resolve-Path -LiteralPath $SourceExe).Path
$releaseRoot = Split-Path -Parent $SourceExe
$applicationVersion = [version][Diagnostics.FileVersionInfo]::GetVersionInfo($SourceExe).FileVersion
$bundleVersion = [version](Get-Content -LiteralPath (Join-Path $releaseRoot 'update-feed.json') -Raw | ConvertFrom-Json).Version
if ($applicationVersion -ne $bundleVersion) { throw '程序版本与安装包更新信息不一致，已停止打包。请更新 AssemblyInfo.cs 和 assets/update-feed.json 后重新构建。' }

$OutputDirectory = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('Yike-installer-' + [guid]::NewGuid().ToString('N'))
$payloadRoot = Join-Path $tempRoot 'payload'
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

try {
    Copy-Item -LiteralPath $SourceExe -Destination (Join-Path $payloadRoot 'Yike.exe') -Force
    foreach ($name in @('MainWindow.xaml','SelectionWindow.xaml','ocr.ps1','speech-online.py','speech-local.ps1','app.png','app.ico','update-feed.json')) {
        $source = Join-Path $releaseRoot $name
        if (-not (Test-Path -LiteralPath $source)) { throw "发布目录缺少文件：$source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $payloadRoot $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $releaseRoot 'app.ico') -Destination (Join-Path $payloadRoot 'app-rounded.ico') -Force

    $speechRuntime = Join-Path $releaseRoot 'speech-runtime'
    if (-not (Test-Path -LiteralPath $speechRuntime)) {
        $speechRuntime = Join-Path $projectRoot 'assets\speech-runtime'
    }
    if (-not (Test-Path -LiteralPath $speechRuntime)) { throw '找不到 speech-runtime。' }
    Copy-Item -LiteralPath $speechRuntime -Destination (Join-Path $payloadRoot 'speech-runtime') -Recurse -Force
    $whisperRuntime = Join-Path $releaseRoot 'whisper-runtime'
    if (-not (Test-Path -LiteralPath $whisperRuntime)) {
        $whisperRuntime = Join-Path $projectRoot 'assets\whisper-runtime'
    }
    if (-not (Test-Path -LiteralPath $whisperRuntime)) { throw '找不到 whisper-runtime。' }
    Copy-Item -LiteralPath $whisperRuntime -Destination (Join-Path $payloadRoot 'whisper-runtime') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $payloadRoot 'uninstall.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $payloadRoot -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'third_party\licenses') -Destination (Join-Path $payloadRoot 'licenses') -Recurse -Force

    $requiredPayload = @(
        'Yike.exe','MainWindow.xaml','SelectionWindow.xaml','ocr.ps1','speech-online.py','speech-local.ps1',
        'app.png','app.ico','app-rounded.ico','update-feed.json','uninstall.ps1','speech-runtime\python.exe',
        'whisper-runtime\ggml-base-q5_1.bin','whisper-runtime\Release\whisper-stream.exe',
        'whisper-runtime\Release\whisper.dll','whisper-runtime\Release\SDL2.dll'
    )
    foreach ($relative in $requiredPayload) {
        $requiredPath = Join-Path $payloadRoot $relative
        if (-not (Test-Path -LiteralPath $requiredPath) -or (Get-Item -LiteralPath $requiredPath).Length -eq 0) {
            throw "安装包缺少必要文件：$relative"
        }
    }

    $payloadZip = Join-Path $tempRoot 'payload.zip'
    Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal

    $targetName = Join-Path $OutputDirectory 'Yike-Setup.exe'
    $tempTargetName = Join-Path $tempRoot 'Yike-Setup.exe'
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw "找不到 C# 编译器：$compiler" }
    $setupSource = Join-Path $PSScriptRoot 'Setup.cs'
    $icon = Join-Path $releaseRoot 'app.ico'
    $arguments = @(
        '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
        "/out:$tempTargetName",
        "/resource:$payloadZip,payload.zip",
        '/r:System.dll', '/r:System.Core.dll', '/r:System.Windows.Forms.dll',
        '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
        '/r:Microsoft.CSharp.dll', $setupSource
    )
    if (Test-Path -LiteralPath $icon) { $arguments += "/win32icon:$icon" }
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempTargetName)) {
        throw '安装程序编译失败。'
    }

    Copy-Item -LiteralPath $tempTargetName -Destination $targetName -Force
    $hash = (Get-FileHash -LiteralPath $targetName -Algorithm SHA256).Hash
    Write-Host "已生成：$targetName"
    Write-Host "SHA256：$hash"
}
finally {
    if (-not $KeepTemp -and (Test-Path -LiteralPath $tempRoot)) {
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
        $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ((Split-Path -Parent $resolvedTemp) -ne $expectedParent -or (Split-Path -Leaf $resolvedTemp) -notmatch '^Yike-installer-[0-9a-f]{32}$') {
            throw '安装器临时目录校验失败，已保留文件。'
        }
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
