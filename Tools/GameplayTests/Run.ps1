param(
    [Parameter(Mandatory=$true)][string]$ValidationProject,
    [string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot=(Resolve-Path -LiteralPath $ValidationProject).Path
$tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
if(!$validationRoot.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) {throw 'Use an existing disposable full project under personal Temp.'}
if(!(Test-Path -LiteralPath (Join-Path $validationRoot 'ProjectSettings/ProjectVersion.txt'))) {throw 'A cached production project is required.'}
if(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {$_.CommandLine -like ('*'+$validationRoot+'*')}) {throw 'The validation copy is already open in an Editor.'}
Copy-Item -Path (Join-Path $repoRoot 'Assets/Scripts/*') -Destination (Join-Path $validationRoot 'Assets/Scripts') -Recurse -Force
$assets=@('Assets/Scenes/Game.unity','Assets/Scenes/Lobby.unity','Assets/Resources/HotelNetworkPlayer.prefab','Assets/Resources/HotelMultiplayerActions.inputactions','Assets/Resources/Traps/ShoppingCartTrap.prefab','Assets/Resources/Traps/ClickTrap.prefab','Assets/Resources/Traps/FallingChandelierTrap.prefab','Assets/Resources/GhostEmergence.prefab','Assets/Dialogue/GhostEmergence.json','Assets/Dialogue/GrandmafishInspect.json','Assets/Art/player_ghost.png')
foreach($asset in $assets) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $asset) -Destination (Join-Path $validationRoot $asset) -Force
    if(Test-Path -LiteralPath (Join-Path $repoRoot ($asset+'.meta'))) {Copy-Item -LiteralPath (Join-Path $repoRoot ($asset+'.meta')) -Destination (Join-Path $validationRoot ($asset+'.meta')) -Force}
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GameplayUpdateRunner.cs') -Destination (Join-Path $validationRoot 'Assets/GameplayUpdateRunner.cs') -Force
$logPath=Join-Path $validationRoot 'gameplay-update.log'
$arguments=@('-batchmode','-job-worker-count','2','-projectPath',('"'+$validationRoot+'"'),'-executeMethod','GameplayUpdateRunner.Run','--gameplay-update','-logFile',('"'+$logPath+'"'))
$process=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -ArgumentList $arguments -PassThru
Write-Output ("Gameplay validation PID "+$process.Id+"; log "+$logPath)
while(!$process.WaitForExit(5000)) {}
if($process.ExitCode -ne 0) {throw ("Validation failed: "+$logPath)}
Write-Output 'Validation exited successfully; inspect GameplayUpdateEvidence/result.txt and rendered frames.'
