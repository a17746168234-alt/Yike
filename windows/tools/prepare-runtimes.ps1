param([Parameter(Mandatory=$true)][string]$FromDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = (Resolve-Path -LiteralPath $FromDirectory).Path
$assetsRoot = Join-Path $projectRoot 'assets'
$names = @('speech-runtime','whisper-runtime')
$required = @(
    'speech-runtime\python.exe',
    'speech-runtime\LICENSE.txt',
    'speech-runtime\Lib\site-packages\edge_tts\__init__.py',
    'whisper-runtime\ggml-base-q5_1.bin',
    'whisper-runtime\Release\whisper-stream.exe',
    'whisper-runtime\Release\whisper.dll',
    'whisper-runtime\Release\SDL2.dll'
)
foreach ($relative in $required) {
    $path = Join-Path $sourceRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) { throw "源目录缺少运行库文件：$relative" }
}
foreach ($name in $names) {
    if (Test-Path -LiteralPath (Join-Path $assetsRoot $name)) { throw "assets/$name 已存在，请保留现有目录或先自行备份。导入不会覆盖它。" }
}
$staging = Join-Path $assetsRoot ('.runtime-import-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    foreach ($name in $names) {
        Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination (Join-Path $staging $name) -Recurse -Force
    }
    foreach ($name in $names) {
        $target = [IO.Path]::GetFullPath((Join-Path $assetsRoot $name))
        if ((Split-Path -Parent $target) -ne $assetsRoot -or (Test-Path -LiteralPath $target)) { throw '运行库目标目录校验失败。' }
        Move-Item -LiteralPath (Join-Path $staging $name) -Destination $target
    }
    Write-Host '已导入运行库；这些目录已加入 .gitignore。'
} finally {
    if (Test-Path -LiteralPath $staging) {
        $resolved = (Resolve-Path -LiteralPath $staging).Path
        if ((Split-Path -Parent $resolved) -ne $assetsRoot -or (Split-Path -Leaf $resolved) -notmatch '^\.runtime-import-[0-9a-f]{32}$') { throw '拒绝清理未通过校验的临时目录。' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}