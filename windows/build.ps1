param([string]$OutputDirectory='dist', [switch]$SkipRuntimes)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows x64 和 .NET Framework 4.8 的 C# 编译器。' }
if (-not $SkipRuntimes) {
    foreach ($name in @('speech-runtime','whisper-runtime')) {
        if (-not (Test-Path -LiteralPath "assets\$name")) {
            throw "缺少 assets\$name。请参阅 docs/DEPENDENCIES.md，或使用 -SkipRuntimes 构建核心应用。"
        }
    }
}
$refs = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','Microsoft.CSharp.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll','System.Speech.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$outputRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory)) }
New-Item -ItemType Directory -Force $outputRoot | Out-Null
$sources = Get-ChildItem src,tests -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /nowarn:0219,0649 /win32manifest:src/app.manifest /win32icon:assets/app.ico "/out:$outputRoot\Yike.exe" @refs @sources
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item src/Presentation/Views/*.xaml $outputRoot -Force
Copy-Item src/Scripts/speech-online.py $outputRoot -Force
foreach ($powerShellScript in @('ocr.ps1','speech-local.ps1')) {
    $scriptSource = Join-Path $PSScriptRoot "src\Scripts\$powerShellScript"
    $scriptDestination = Join-Path $outputRoot $powerShellScript
    $scriptText = [IO.File]::ReadAllText($scriptSource,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($scriptDestination,$scriptText,[Text.UTF8Encoding]::new($true))
}
Copy-Item assets/app.png,assets/app.ico,assets/update-feed.json $outputRoot -Force

function Copy-RuntimeDirectory([string]$Name) {
    $sourceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "assets\$Name")).Path
    $destination = Join-Path $outputRoot $Name
    if ([string]::Equals($sourceRoot,[IO.Path]::GetFullPath($destination),[StringComparison]::OrdinalIgnoreCase)) { throw '运行库源目录不能同时作为输出目录。' }
    if (Test-Path -LiteralPath $destination) {
        $resolved = (Resolve-Path -LiteralPath $destination).Path
        if ((Split-Path -Parent $resolved) -ne $outputRoot -or (Split-Path -Leaf $resolved) -ne $Name) { throw "拒绝清理未通过校验的目录：$resolved" }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    Copy-Item -LiteralPath $sourceRoot -Destination $destination -Recurse -Force
    if ($Name -eq 'whisper-runtime') {
        $unusedModel = Join-Path $destination 'ggml-base-q5_1.bin'
        if (Test-Path -LiteralPath $unusedModel -PathType Leaf) { Remove-Item -LiteralPath $unusedModel -Force }
    } elseif ($Name -eq 'speech-runtime') {
        $destinationPrefix = [IO.Path]::GetFullPath($destination).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $packageOnly = @(Get-ChildItem -LiteralPath $destination -Directory -Recurse -Force | Where-Object { $_.Name -eq '__pycache__' -or $_.Name.EndsWith('.dist-info',[StringComparison]::OrdinalIgnoreCase) })
        foreach ($folder in $packageOnly) {
            $full = [IO.Path]::GetFullPath($folder.FullName)
            if (-not $full.StartsWith($destinationPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw "拒绝清理输出目录之外的文件：$full" }
            Remove-Item -LiteralPath $full -Recurse -Force
        }
    }
}
if (-not $SkipRuntimes) {
    Copy-RuntimeDirectory 'speech-runtime'
    Copy-RuntimeDirectory 'whisper-runtime'
}
Write-Host "已生成 $outputRoot\Yike.exe"
