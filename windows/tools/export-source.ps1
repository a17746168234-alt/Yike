param([string]$OutputFile='', [switch]$ForExistingRepository)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = if ($ForExistingRepository) { 'release\Yike-Windows-追加包.zip' } else { 'release\Yike-GitHub.zip' }
}
$files = @(
    '.gitignore','.gitattributes','.editorconfig',
    'README.md','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md',
    'build.ps1','verify.ps1',
    'assets\app.png','assets\app.ico','assets\update-feed.json',
    'installer\Setup.cs','installer\uninstall.ps1','installer\build-installer.ps1',
    'packaging\store\AppxManifest.template.xml','packaging\store\build-store-msix.ps1',
    'tests\RegressionTests.cs',
    'tests\SpeechPlaybackTests.cs','tests\OnlineSpeechTests.py',
    'tests\UpdateTests.cs',
    'tools\prepare-runtimes.ps1','tools\export-source.ps1',
    'docs\代码结构.md','docs\PRIVACY.md','docs\DEPENDENCIES.md','docs\GITHUB.md','docs\STORE.md',
    'docs\EXISTING_REPOSITORY.md','packaging\github\windows-ci-monorepo.yml',
    'docs\images\main-light.png','docs\images\main-dark.png'
)
foreach ($folder in @('src','.github','third_party\licenses')) {
    $folderRoot = Join-Path $projectRoot $folder
    $allItems = @(Get-Item -LiteralPath $folderRoot) + @(Get-ChildItem -LiteralPath $folderRoot -Recurse -Force)
    foreach ($item in $allItems) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "拒绝导出目录链接：$($item.FullName)" }
        if (-not $item.PSIsContainer) {
            $allowed = if ($folder -eq 'src') { @('.cs','.xaml','.ps1','.py','.manifest') } elseif ($folder -eq '.github') { @('.yml','.yaml','.md') } else { @('.txt') }
            if ($item.Extension -notin $allowed) { throw "发现清单外文件，请先确认是否应公开：$($item.FullName)" }
            $files += $item.FullName.Substring($projectRoot.Length + 1)
        }
    }
}
$files = @($files | Sort-Object -Unique)
foreach ($relative in $files) {
    $path = Join-Path $projectRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "公开源码文件缺失：$relative" }
}
$target = if ([IO.Path]::IsPathRooted($OutputFile)) { [IO.Path]::GetFullPath($OutputFile) } else { [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputFile)) }
if (-not $target.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($target) -ne '.zip') { throw '源码 ZIP 输出必须位于项目目录内。' }
New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($target,[IO.FileMode]::Create)
try {
    $archive = New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach ($relative in $files) {
            $entryName = $relative.Replace('\','/')
            if ($ForExistingRepository) { $entryName = 'windows/' + $entryName }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $projectRoot $relative),$entryName,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        if ($ForExistingRepository) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $projectRoot 'packaging\github\windows-ci-monorepo.yml'),'.github/workflows/yike-windows-ci.yml',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $projectRoot '.github\ISSUE_TEMPLATE\bug_report.md'),'.github/ISSUE_TEMPLATE/windows_bug_report.md',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $projectRoot '.github\ISSUE_TEMPLATE\feature_request.md'),'.github/ISSUE_TEMPLATE/windows_feature_request.md',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
Write-Host "已导出：$target"
Write-Host "SHA256：$((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash)"
