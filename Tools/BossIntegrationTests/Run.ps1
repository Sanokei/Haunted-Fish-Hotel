param([Parameter(Mandatory=$true)][string]$ValidationProject, [string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot=(Resolve-Path -LiteralPath $ValidationProject).Path
$tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
if(!$validationRoot.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) {throw 'Use an existing disposable full project under personal Temp.'}
if(!(Test-Path -LiteralPath (Join-Path $validationRoot 'ProjectSettings/ProjectVersion.txt'))) {throw 'A cached production project is required.'}
if(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {$_.CommandLine -like ('*'+$validationRoot+'*')}) {throw 'The validation copy is already open.'}
Copy-Item -Path (Join-Path $repoRoot 'Assets/Scripts/*') -Destination (Join-Path $validationRoot 'Assets/Scripts') -Recurse -Force
foreach($asset in @('Assets/Scenes/Game.unity','Assets/Scenes/Lobby.unity','Assets/Scenes/BossFight.unity','Assets/Resources/HotelNetworkPlayer.prefab','Assets/Resources/HotelMultiplayerActions.inputactions','ProjectSettings/EditorBuildSettings.asset')) {
 Copy-Item -LiteralPath (Join-Path $repoRoot $asset) -Destination (Join-Path $validationRoot $asset) -Force
 if(Test-Path -LiteralPath (Join-Path $repoRoot ($asset+'.meta'))) {Copy-Item -LiteralPath (Join-Path $repoRoot ($asset+'.meta')) -Destination (Join-Path $validationRoot ($asset+'.meta')) -Force}
}
foreach($folder in @('Assets/Art/BossFight','Assets/Resources/BossFight')) {
 $destination=Join-Path $validationRoot $folder
 New-Item -ItemType Directory -Path $destination -Force|Out-Null
 Copy-Item -Path (Join-Path $repoRoot ($folder+'/*')) -Destination $destination -Recurse -Force
 Copy-Item -LiteralPath (Join-Path $repoRoot ($folder+'.meta')) -Destination (Join-Path $validationRoot ($folder+'.meta')) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BossIntegrationRunner.cs') -Destination (Join-Path $validationRoot 'Assets/BossIntegrationRunner.cs') -Force
foreach($asset in @('Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs','Assets/Scripts/DialogueSystem/StoryInput/StoryFunctions.Sequences.cs','Assets/Dialogue/BossHappy.json','Assets/Dialogue/BossFrustrated.json','Assets/Dialogue/BossWin.json','Assets/Dialogue/BossLose.json')) {
 $sourceRoot=$repoRoot; $targetRoot=$validationRoot
 Copy-Item -LiteralPath (Join-Path $sourceRoot $asset) -Destination (Join-Path $targetRoot $asset) -Force
 if(Test-Path -LiteralPath (Join-Path $sourceRoot ($asset+'.meta'))) {Copy-Item -LiteralPath (Join-Path $sourceRoot ($asset+'.meta')) -Destination (Join-Path $targetRoot ($asset+'.meta')) -Force}
}
$logPath=Join-Path $validationRoot 'boss-integration.log'
$arguments=@('-batchmode','-job-worker-count','2','-projectPath',('"'+$validationRoot+'"'),'-executeMethod','BossIntegrationRunner.Run','--boss-integration','-logFile',('"'+$logPath+'"'))
$process=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -ArgumentList $arguments -PassThru
Write-Output ("Boss integration validation PID "+$process.Id+"; log "+$logPath)
while(!$process.WaitForExit(5000)) {}
if($process.ExitCode -ne 0) {throw ("Validation failed: "+$logPath)}
Write-Output 'Validation exited successfully; inspect BossIntegrationEvidence/result.txt and renders.'
