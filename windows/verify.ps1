param([string]$OutputDirectory='dist',[switch]$SkipBuild,[switch]$SkipRuntimes,[switch]$SelfTestOnly)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if (!$SkipBuild) { & ./build.ps1 -OutputDirectory $OutputDirectory -SkipRuntimes:$SkipRuntimes }
$outputRoot=(Resolve-Path -LiteralPath $OutputDirectory).Path
$executable=Join-Path $outputRoot 'Yike.exe'
function Run-Verification([string[]]$Arguments) {
    $process=Start-Process -FilePath $executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        if (!$process.WaitForExit(30000)) { $process.Kill();throw "Verification timed out: $($Arguments -join ' ')" }
        if ($process.ExitCode -ne 0) { throw "Verification failed: $($Arguments -join ' ')" }
    } finally { $process.Dispose() }
}
Run-Verification @('--self-test')
if (!(Get-Content (Join-Path $outputRoot 'test-results.txt') -Raw).Contains('ALL TESTS PASSED')) { throw 'Regression tests failed.' }
if ($SelfTestOnly) { Get-Content (Join-Path $outputRoot 'test-results.txt'); return }
foreach ($check in @(@{Argument='--theme-test';Report='theme-test-results.txt'},@{Argument='--zoom-test';Report='zoom-test-results.txt'},@{Argument='--ui-audit-test';Report='ui-audit-results.txt'},@{Argument='--functional-ui-test';Report='functional-ui-results.txt'})) {
    Run-Verification @('--render-preview',$check.Argument)
    if (!(Get-Content (Join-Path $outputRoot $check.Report) -Raw).StartsWith('PASS:')) { throw "Verification failed: $($check.Report)" }
}
$previews=@(
    @{File='preview.png';Arguments=@('--render-preview')},
    @{File='preview-dark.png';Arguments=@('--render-preview','--dark')},
    @{File='selection-light.png';Arguments=@('--render-preview','--selection-preview')},
    @{File='selection-dark.png';Arguments=@('--render-preview','--selection-preview','--dark')},
    @{File='selection-light-error.png';Arguments=@('--render-preview','--selection-preview','--error')},
    @{File='selection-light-loading.png';Arguments=@('--render-preview','--selection-preview','--loading')},
    @{File='settings-light.png';Arguments=@('--render-preview','--settings-preview')},
    @{File='settings-dark.png';Arguments=@('--render-preview','--settings-preview','--dark')},
    @{File='deepl-dialog.png';Arguments=@('--render-preview','--engine-preview','--dark')},
    @{File='deepl-help.png';Arguments=@('--render-preview','--deepl-help-preview','--dark')},
    @{File='history-preview.png';Arguments=@('--render-preview','--history-preview','--dark')},
    @{File='about-updates.png';Arguments=@('--render-preview','--about-preview','--dark')}
)
foreach ($preview in $previews) {
    $file=Join-Path $outputRoot $preview.File
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    Run-Verification $preview.Arguments
    if (!(Test-Path -LiteralPath $file) -or (Get-Item -LiteralPath $file).Length -eq 0) { throw "Preview missing: $file" }
}
Get-Content (Join-Path $outputRoot 'ui-audit-results.txt')
Write-Output 'PASS: regression, theme, zoom, UI audit, and twelve previews.'
