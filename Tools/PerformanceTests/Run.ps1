param(
    [Parameter(Mandatory=$true)][string]$ValidationProject,
    [ValidateSet('Host','Direct','Lifecycle','Lobby')][string]$Mode='Host',
    [string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot=(Resolve-Path -LiteralPath $ValidationProject).Path
$tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
if(!$validationRoot.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) {throw 'Use a disposable validation copy under the personal Temp directory.'}
if(!(Test-Path -LiteralPath (Join-Path $validationRoot 'ProjectSettings/ProjectVersion.txt'))) {throw 'The validation copy must already contain the production project and cached packages.'}
if(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {$_.CommandLine -like ('*'+$validationRoot+'*')}) {throw 'An Editor is already using this validation copy; inspect it before launching another.'}
Copy-Item -Path (Join-Path $repoRoot 'Assets/Scripts/*') -Destination (Join-Path $validationRoot 'Assets/Scripts') -Recurse -Force
foreach($asset in @('Assets/Scenes/Game.unity','Assets/Scenes/Lobby.unity','Assets/Resources/Traps/FallingChandelierTrap.prefab')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $asset) -Destination (Join-Path $validationRoot $asset) -Force
}
foreach($source in @('Tools/PerformanceTests/AuditGuardrails.cs','Tools/PerformanceTests/AuditLifecycleRunner.cs','Tools/PerformanceTests/ActualGameSceneValidation.cs','Tools/RoundIntroductionTests/ActualGameFlowRunner.cs','Tools/RoundIntroductionTests/ActualLobbyFlowRunner.cs','Tools/IntroductionTests/LobbyLoadingValidationRunner.cs')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $source) -Destination (Join-Path $validationRoot 'Assets') -Force
}
$method=switch($Mode) {'Host' {'ActualGameSceneValidation.RunLobby'} 'Direct' {'ActualGameSceneValidation.Run'} 'Lifecycle' {'AuditLifecycleRunner.Run'} 'Lobby' {'ValidateLobbyLoading.Run'}}
$logPath=Join-Path $validationRoot ('audit-'+$Mode.ToLowerInvariant()+'.log')
$editorArgs=@('-batchmode','-job-worker-count','2','-projectPath',('"'+$validationRoot+'"'),'-executeMethod',$method,'-logFile',('"'+$logPath+'"'))
if($Mode -eq 'Host') {$editorArgs+=@('--actual-game-validation','--actual-lobby-validation')}
if($Mode -eq 'Direct') {$editorArgs+='--actual-game-validation'}
if($Mode -eq 'Lifecycle') {$editorArgs+='--audit-lifecycle'}
$process=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -ArgumentList $editorArgs -PassThru
Write-Output ("Started isolated "+$Mode+" validation PID "+$process.Id+"; log "+$logPath)
while(!$process.WaitForExit(5000)) { }
if($process.ExitCode -ne 0) {throw ("Unity validation failed with exit code "+$process.ExitCode+"; inspect "+$logPath)}
Write-Output 'Unity validation exited successfully. Inspect the checks and result file before reporting coverage.'
