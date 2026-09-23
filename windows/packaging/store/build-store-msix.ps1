param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$PackageName,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [string]$Version = '2.1.0.0',
    [string]$SourceDirectory = '',
    [string]$OutputDirectory = '',
    [string]$MakeAppxPath = '',
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $projectRoot 'release\Yike'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'release\store'
}
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if ($Version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') {
    throw 'MSIX 版本必须是四段数字，例如 1.0.0.0。'
}
foreach ($part in $Version.Split('.')) {
    if ([int]$part -gt 65535) { throw 'MSIX 每段版本号必须在 0 到 65535 之间。' }
}
if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory 'Yike.exe'))) {
    throw "发布目录缺少 Yike.exe：$SourceDirectory"
}

function Resolve-MakeAppx([string]$ExplicitPath) {
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolved = Resolve-Path -LiteralPath $ExplicitPath -ErrorAction Stop
        return $resolved.Path
    }
    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @(
        (Join-Path $projectRoot 'tools\windows-sdk-build-tools'),
        'C:\Program Files (x86)\Windows Kits\10\bin',
        'C:\Program Files\Windows Kits\10\bin'
    )
    $candidates = foreach ($root in $roots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Filter makeappx.exe -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '[\\/]x64[\\/]makeappx\.exe$' }
        }
    }
    $selected = $candidates | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $selected) {
        throw '找不到 MakeAppx.exe。请安装 Windows SDK Build Tools，或通过 -MakeAppxPath 指定路径。'
    }
    return $selected.FullName
}

function Save-StoreAsset([Drawing.Image]$Source, [string]$Path, [int]$Width, [int]$Height) {
    $bitmap = New-Object Drawing.Bitmap $Width, $Height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
        $scale = [Math]::Min($Width / $Source.Width, $Height / $Source.Height)
        $drawWidth = [int][Math]::Round($Source.Width * $scale)
        $drawHeight = [int][Math]::Round($Source.Height * $scale)
        $x = [int](($Width - $drawWidth) / 2)
        $y = [int](($Height - $drawHeight) / 2)
        $graphics.DrawImage($Source, $x, $y, $drawWidth, $drawHeight)
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$makeAppx = Resolve-MakeAppx $MakeAppxPath
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ('Yike-msix-' + [Guid]::NewGuid().ToString('N'))
$assetsRoot = Join-Path $stagingRoot 'Assets'
New-Item -ItemType Directory -Path $assetsRoot -Force | Out-Null

try {
    $payloadFiles = @(
        'Yike.exe',
        'MainWindow.xaml',
        'SelectionWindow.xaml',
        'ocr.ps1',
        'speech-online.py',
        'speech-local.ps1',
        'app.png',
        'app.ico',
        'update-feed.json'
    )
    foreach ($relativePath in $payloadFiles) {
        $sourcePath = Join-Path $SourceDirectory $relativePath
        if (-not (Test-Path -LiteralPath $sourcePath)) { throw "发布目录缺少必要文件：$relativePath" }
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $stagingRoot $relativePath) -Force
    }
    foreach ($runtimeName in @('speech-runtime', 'whisper-runtime')) {
        $runtimePath = Join-Path $SourceDirectory $runtimeName
        if (-not (Test-Path -LiteralPath $runtimePath)) { throw "发布目录缺少必要运行库：$runtimeName" }
        Copy-Item -LiteralPath $runtimePath -Destination (Join-Path $stagingRoot $runtimeName) -Recurse -Force
    }

    $manifestTemplate = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.template.xml') -Raw
    $manifest = $manifestTemplate.Replace('__PACKAGE_VERSION__', $Version).
        Replace('__PACKAGE_NAME__', [Security.SecurityElement]::Escape($PackageName)).
        Replace('__PACKAGE_PUBLISHER__', [Security.SecurityElement]::Escape($Publisher)).
        Replace('__PUBLISHER_DISPLAY_NAME__', [Security.SecurityElement]::Escape($PublisherDisplayName))
    [IO.File]::WriteAllText(
        (Join-Path $stagingRoot 'AppxManifest.xml'),
        $manifest,
        (New-Object Text.UTF8Encoding($false)))

    Add-Type -AssemblyName System.Drawing
    $sourceLogoPath = Join-Path $SourceDirectory 'app.png'
    if (-not (Test-Path -LiteralPath $sourceLogoPath)) { throw "发布目录缺少 app.png：$sourceLogoPath" }
    $sourceLogo = [Drawing.Image]::FromFile($sourceLogoPath)
    try {
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'StoreLogo.png') 50 50
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'Square44x44Logo.png') 44 44
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'Square71x71Logo.png') 71 71
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'Square150x150Logo.png') 150 150
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'Wide310x150Logo.png') 310 150
        Save-StoreAsset $sourceLogo (Join-Path $assetsRoot 'Square310x310Logo.png') 310 310
    }
    finally { $sourceLogo.Dispose() }

    $packageName = 'Yike_{0}_x64.msix' -f $Version
    $packagePath = Join-Path $OutputDirectory $packageName
    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
    & $makeAppx pack /d $stagingRoot /p $packagePath /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packagePath)) {
        throw 'MakeAppx 未能生成 MSIX 包。'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $names = @($archive.Entries | ForEach-Object FullName)
        foreach ($required in @('AppxManifest.xml', 'AppxBlockMap.xml', 'Yike.exe')) {
            if ($names -notcontains $required) { throw "MSIX 缺少必要文件：$required" }
        }
    }
    finally { $archive.Dispose() }

    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
    Write-Host "已生成 Microsoft Store 上传包：$packagePath"
    Write-Host "版本：$Version"
    Write-Host "SHA256：$hash"
    Write-Host '注意：此包用于上传 Partner Center，由 Microsoft Store 审核后重新签名；不要直接侧载未签名包。'
}
finally {
    if (-not $KeepStaging -and (Test-Path -LiteralPath $stagingRoot)) {
        $resolved = (Resolve-Path -LiteralPath $stagingRoot).Path
        $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ((Split-Path -Parent $resolved) -ne $tempParent -or (Split-Path -Leaf $resolved) -notmatch '^Yike-msix-[0-9a-f]{32}$') {
            throw "拒绝清理未经校验的暂存目录：$resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    }
}
