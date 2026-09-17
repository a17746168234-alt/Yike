param([string]$ImagePath,[string]$OutputPath,[string]$Language='auto')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null=[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null=[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null=[Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]
$asTask=([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($Operation,$Type) { $task=$asTask.MakeGenericMethod($Type).Invoke($null,@($Operation));$task.Wait();$task.Result }
try {
 $file=Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
 $stream=Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
 $decoder=Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
 $transform=New-Object Windows.Graphics.Imaging.BitmapTransform
 $scale=[Math]::Min(1.0,[Windows.Media.Ocr.OcrEngine]::MaxImageDimension/[double][Math]::Max($decoder.PixelWidth,$decoder.PixelHeight))
 $transform.ScaledWidth=[uint32]($decoder.PixelWidth*$scale);$transform.ScaledHeight=[uint32]($decoder.PixelHeight*$scale)
 $bitmap=Await ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,[Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied,$transform,[Windows.Graphics.Imaging.ExifOrientationMode]::IgnoreExifOrientation,[Windows.Graphics.Imaging.ColorManagementMode]::DoNotColorManage)) ([Windows.Graphics.Imaging.SoftwareBitmap])
 $engines=@()
 if($Language -eq 'auto') { foreach($lang in [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages) { if($lang.LanguageTag -match '^(en|zh|ja|ko)') {$engines+= [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang)} } }
 else { $tag=switch($Language){'ZH-HANS'{'zh-Hans-CN'}'EN-US'{'en-US'}'JA'{'ja'}'KO'{'ko'}}; $lang=New-Object Windows.Globalization.Language($tag);$engines+= [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang) }
 $engines=@($engines | Where-Object { $null -ne $_ })
 if($engines.Count -eq 0) {throw '没有对应的 Windows OCR 语言包，请在 Windows 设置 → 时间和语言 → 语言和区域中安装语言及其 OCR 功能。'}
 $candidates=@()
 foreach($engine in $engines) {
  $result=Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
  foreach($line in $result.Lines) {
   $left=[double]::MaxValue;$top=[double]::MaxValue;$right=0;$bottom=0
   foreach($word in $line.Words) {$r=$word.BoundingRect;$left=[Math]::Min($left,$r.X);$top=[Math]::Min($top,$r.Y);$right=[Math]::Max($right,$r.X+$r.Width);$bottom=[Math]::Max($bottom,$r.Y+$r.Height)}
   if($line.Words.Count -gt 0) {
    $cleanText=[Regex]::Replace($line.Text,'(?<=[\u3400-\u9FFF\u3040-\u30FF\uAC00-\uD7AF])\s+(?=[\u3400-\u9FFF\u3040-\u30FF\uAC00-\uD7AF])','')
    if(-not [string]::IsNullOrWhiteSpace($cleanText)) {$candidates+= @{Text=$cleanText.Trim();Translated='';X=$left/$scale;Y=$top/$scale;Width=($right-$left)/$scale;Height=($bottom-$top)/$scale;FontSize=($bottom-$top)/$scale*0.8;Color='#202020';HighContrast=$false}}
   }
  }
 }
 # Auto detection runs every installed CJK/English OCR engine. Merge their
 # non-overlapping lines and keep the most informative text for duplicate boxes.
 $regions=@()
 foreach($candidate in ($candidates | Sort-Object @{Expression={[double]$_.Y}},@{Expression={[double]$_.X}})) {
  $duplicate=$null
  foreach($existing in $regions) {
   $ix=[Math]::Max(0,[Math]::Min($candidate.X+$candidate.Width,$existing.X+$existing.Width)-[Math]::Max($candidate.X,$existing.X))
   $iy=[Math]::Max(0,[Math]::Min($candidate.Y+$candidate.Height,$existing.Y+$existing.Height)-[Math]::Max($candidate.Y,$existing.Y))
   $intersection=$ix*$iy;$smaller=[Math]::Min($candidate.Width*$candidate.Height,$existing.Width*$existing.Height)
   if($smaller -gt 0 -and $intersection/$smaller -ge 0.60) {$duplicate=$existing;break}
  }
  if($null -eq $duplicate) {$regions+=$candidate}
  elseif(($candidate.Text -replace '\s','').Length -gt ($duplicate.Text -replace '\s','').Length) {$duplicate.Text=$candidate.Text}
 }
 @{Width=$decoder.PixelWidth;Height=$decoder.PixelHeight;Regions=$regions} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
 $bitmap.Dispose();$stream.Dispose()
} catch { @{Error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding UTF8;exit 1 }
