param([string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot=Join-Path $env:TEMP ('HotelRoundValidation-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources","$validationRoot/Packages","$validationRoot/ProjectSettings" | Out-Null
foreach($source in @('Assets/Scripts/Multiplayer/TrapDefinition.cs','Assets/Scripts/Multiplayer/TrapManager.cs','Assets/Scripts/Multiplayer/PossessionSmoke.cs','Assets/Scripts/Multiplayer/GhostPossessionEffects.cs','Assets/Scripts/Multiplayer/GhostTentaclePresentation.cs','Assets/Scripts/Multiplayer/RoundIntroductionSettings.cs','Assets/Scripts/Multiplayer/GhostSelectionPresentation.cs','Assets/Scripts/Multiplayer/GhostSelectionWheel.cs','Assets/Scripts/Multiplayer/AtticReturnBlur.cs','Assets/Scripts/Multiplayer/HotelPalette.cs','Assets/Scripts/Multiplayer/Lobby/IntroductionPanelShape.cs','Assets/Scripts/Multiplayer/Lobby/IntroductionSkipCircle.cs','Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs','Assets/Scripts/DialogueSystem/StoryInput/StoryFunctions.Sequences.cs')) {
 Copy-Item -LiteralPath "$repoRoot/$source" -Destination "$validationRoot/Assets"
 Copy-Item -LiteralPath "$repoRoot/$source.meta" -Destination "$validationRoot/Assets"
}
foreach($source in @('Assets/Resources/PossessionSmoke.prefab','Assets/Dialogue/GhostPossessionSmoke.json','Assets/Art/cloud.png','Assets/Art/cardboardbox.png','Assets/Resources/AtticRoundCutscene.prefab','Assets/Resources/AtticReturnBlur.mat','Assets/Resources/AtticReturnBlur.shader','Assets/Art/attic.png','Assets/Art/ghost.png','Assets/Art/arrow.png','Assets/Art/wall.png','Assets/Dialogue/GhostRoundIntroduction.json')) {
 Copy-Item -LiteralPath "$repoRoot/$source" -Destination "$validationRoot/Assets/Resources"
 Copy-Item -LiteralPath "$repoRoot/$source.meta" -Destination "$validationRoot/Assets/Resources"
}
Copy-Item -LiteralPath "$PSScriptRoot/ValidateRoundIntroduction.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$PSScriptRoot/RoundValidationRunner.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Library/ScriptAssemblies/Ink-Libraries.dll" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings"
Copy-Item -LiteralPath "$repoRoot/Assets/TextMesh Pro/Fonts/LiberationSans.ttf" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Assets/TextMesh Pro/Fonts/LiberationSans.ttf.meta" -Destination "$validationRoot/Assets"
foreach($name in @('HotelPlayerMovement','GameSideScrollMotor','HotelFishSprite','GameSceneController','IHotelScene','GhostPlacementController','GhostPlacementWorld','GhostPlacementGrid','GameRoundGate','GhostTrapAreaPresentation','GameEditorRoleSwitch','IGhostTrap','GhostTrap','GhostTrapSupply')) {
 Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/$name.cs" -Destination "$validationRoot/Assets"
 Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/$name.cs.meta" -Destination "$validationRoot/Assets"
}
Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/Lobby/LobbyMovementMotor.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Tools/PlayerTests/SceneTestDependencies.cs" -Destination "$validationRoot/Assets"
$scene=Get-Content -LiteralPath "$repoRoot/Assets/Scenes/Game.unity" -Raw
$blocks=[regex]::Matches($scene,'(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)') | Where-Object { [long]$_.Groups[1].Value -ge 31000001 -and [long]$_.Groups[1].Value -le 31000028 } | ForEach-Object { $_.Value }
Set-Content -LiteralPath "$validationRoot/Assets/Resources/OriginalWheel.prefab" -Value ("%YAML 1.1`n%TAG !u! tag:unity3d.com,2011:`n"+(($blocks -join '') -replace 'm_Father: \{fileID: 32000022\}', 'm_Father: {fileID: 0}'))
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/Traps" -Destination "$validationRoot/Assets/Resources" -Recurse
foreach($art in @('chandeler-bg.png','chandler.png','cart.png','isle.png')) {
 if(Test-Path "$repoRoot/Assets/Art/$art") {
 Copy-Item -LiteralPath "$repoRoot/Assets/Art/$art" -Destination "$validationRoot/Assets/Resources"
 Copy-Item -LiteralPath "$repoRoot/Assets/Art/$art.meta" -Destination "$validationRoot/Assets/Resources"
 }
}
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/HotelMultiplayerActions.inputactions" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$PSScriptRoot/ValidateTraps.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$PSScriptRoot/ValidateTrapManager.cs" -Destination "$validationRoot/Assets"
$assetIndex=@{}
Get-ChildItem "$repoRoot/Assets" -Filter '*.meta' -Recurse -File | ForEach-Object {
 $match=[regex]::Match((Get-Content $_.FullName -Raw),'guid: (\w+)')
 if($match.Success){$assetIndex[$match.Groups[1].Value]=$_.FullName.Substring(0,$_.FullName.Length-5)}
}
$pending=New-Object 'System.Collections.Generic.Queue[string]'
Get-ChildItem "$repoRoot/Assets/Resources/Traps" -Filter '*.prefab' | ForEach-Object {$pending.Enqueue($_.FullName)}
$visited=@{}
while($pending.Count){
 $file=$pending.Dequeue()
 foreach($match in [regex]::Matches((Get-Content $file -Raw),'guid: (\w+)')) {
 $key=$match.Groups[1].Value
 if($visited.ContainsKey($key)){continue};$visited[$key]=$true
 if(!$assetIndex.ContainsKey($key)){continue}
 $dependency=$assetIndex[$key];$extension=[IO.Path]::GetExtension($dependency)
 if($extension -in @('.png','.mat','.shader','.ttf')) {
 Copy-Item -LiteralPath $dependency -Destination "$validationRoot/Assets/Resources"
 Copy-Item -LiteralPath ($dependency+'.meta') -Destination "$validationRoot/Assets/Resources"
 if($extension -eq '.mat'){$pending.Enqueue($dependency)}
 }
 }
}
$dependencies=@{}
foreach($module in @('audio','imageconversion','imgui','ui','uielements','physics')) { $dependencies['com.unity.modules.'+$module]='1.0.0' }
foreach($packageName in @('com.unity.inputsystem','com.unity.ugui','com.unity.2d.sprite')) {
 $packagePath=Get-ChildItem -LiteralPath "$repoRoot/Library/PackageCache" -Directory | Where-Object { $_.Name.StartsWith($packageName+'@') } | Select-Object -First 1
 if(!$packagePath){throw "Missing cache: $packageName"}
 $dependencies[$packageName]='file:'+$packagePath.FullName.Replace('\','/')
}
@{dependencies=$dependencies}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$validationRoot/Packages/manifest.json"
"%YAML 1.1`n%TAG !u! tag:unity3d.com,2011:`n--- !u!129 &1`nPlayerSettings:`n  activeInputHandler: 1"|Set-Content -LiteralPath "$validationRoot/ProjectSettings/ProjectSettings.asset"
$arguments=@('-batchmode','-projectPath',"`"$validationRoot`"",'-executeMethod','ValidateRoundIntroduction.Run','-logFile',"`"$validationRoot/validation.log`"")
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
Write-Output "Validation project: $validationRoot"
Write-Output "Unity process: $($process.Id)"
