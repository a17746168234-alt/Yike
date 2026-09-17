param([string]$JobPath)
$ErrorActionPreference='Stop'
try {
 $job=Get-Content -LiteralPath $JobPath -Raw -Encoding UTF8 | ConvertFrom-Json
 Add-Type -AssemblyName System.Runtime.WindowsRuntime
 $null=[Windows.Media.SpeechSynthesis.SpeechSynthesizer,Windows.Media.SpeechSynthesis,ContentType=WindowsRuntime]
 $null=[Windows.Storage.Streams.DataReader,Windows.Storage.Streams,ContentType=WindowsRuntime]
 $asTask=([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
 function Await($op,$type){$task=$asTask.MakeGenericMethod($type).Invoke($null,@($op));$task.Wait();$task.Result}
 $lang=switch($job.language){'EN-US'{'en'}'JA'{'ja'}'KO'{'ko'}default{'zh'}}
 $voice=[Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object {$_.Language.StartsWith($lang) -and $_.Gender.ToString() -eq $job.gender} | Select-Object -First 1
 if(-not $voice){throw 'No matching local voice; use online natural speech.'}
 $synth=New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
 $synth.Voice=$voice
 $synth.Options.SpeakingRate=1.0+$job.rate/100.0
 $audio=Await ($synth.SynthesizeTextToStreamAsync($job.text)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
 $reader=New-Object Windows.Storage.Streams.DataReader($audio)
 $null=Await ($reader.LoadAsync([uint32]$audio.Size)) ([uint32])
 $bytes=New-Object byte[] ([int]$audio.Size);$reader.ReadBytes($bytes)
 [IO.File]::WriteAllBytes($job.output,$bytes)
 $reader.Dispose();$audio.Dispose();$synth.Dispose()
} catch { [Console]::Error.WriteLine($_.Exception.Message);exit 1 }
