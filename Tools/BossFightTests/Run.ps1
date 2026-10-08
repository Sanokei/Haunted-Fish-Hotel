param([Parameter(Mandatory=$true)][string]$ValidationProject)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$copy=(Resolve-Path -LiteralPath $ValidationProject).Path
$temp=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
if(!$copy.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an existing isolated production copy under personal Temp.'}
if(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {$_.CommandLine -like ('*'+$copy+'*')}){throw 'Editor already uses this copy.'}
foreach($kind in @('Scripts','Art','Resources')){
 $dest=Join-Path $copy ('Assets/'+$kind+'/BossFight')
 New-Item -ItemType Directory -Force $dest | Out-Null
 Copy-Item -Path (Join-Path $repo ('Assets/'+$kind+'/BossFight/*')) -Destination $dest -Recurse -Force
 Copy-Item -LiteralPath (Join-Path $repo ('Assets/'+$kind+'/BossFight.meta')) -Destination (Join-Path $copy ('Assets/'+$kind+'/BossFight.meta')) -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Scenes/BossFight.unity'),(Join-Path $repo 'Assets/Scenes/BossFight.unity.meta') -Destination (Join-Path $copy 'Assets/Scenes') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BossArenaValidation.cs') -Destination (Join-Path $copy 'Assets/BossArenaValidation.cs') -Force
foreach($asset in @('Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs','Assets/Scripts/DialogueSystem/StoryInput/StoryFunctions.Sequences.cs','Assets/Dialogue/BossHappy.json','Assets/Dialogue/BossFrustrated.json','Assets/Dialogue/BossWin.json','Assets/Dialogue/BossLose.json')) {
 $sourceRoot=$repo; $targetRoot=$copy
 Copy-Item -LiteralPath (Join-Path $sourceRoot $asset) -Destination (Join-Path $targetRoot $asset) -Force
 if(Test-Path -LiteralPath (Join-Path $sourceRoot ($asset+'.meta'))) {Copy-Item -LiteralPath (Join-Path $sourceRoot ($asset+'.meta')) -Destination (Join-Path $targetRoot ($asset+'.meta')) -Force}
}
$log=Join-Path $copy 'boss-arena.log'
$proc=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList @('-batchmode','-job-worker-count','2','-projectPath',('"'+$copy+'"'),'-executeMethod','BossArenaValidation.Run','-logFile',('"'+$log+'"')) -PassThru
Write-Output ('BossFight validation PID '+$proc.Id)
while(!$proc.WaitForExit(5000)){}
if($proc.ExitCode -ne 0){throw ('BossFight validation failed; inspect '+$log)}
Write-Output ('Inspect '+$log+' and BossFightEvidence before reporting checks.')
