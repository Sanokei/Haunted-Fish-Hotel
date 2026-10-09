param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot = Join-Path $env:TEMP ('HotelPlayerValidation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null

foreach ($name in @('HotelPlayerMovement', 'GameSideScrollMotor', 'HotelFishSprite', 'GameSceneController','IHotelPlayerCommands','GamePlayerCommands', 'IHotelScene', 'GhostSelectionPresentation', 'GhostSelectionWheel', 'GhostPlacementController', 'GhostPlacementWorld', 'GhostPlacementGrid', 'GameRoundGate', 'GhostTrapAreaPresentation', 'GameEditorRoleSwitch', 'HotelPalette', 'IGhostTrap', 'GhostTrap', 'GhostTrapSupply','TrapDefinition','TrapManager', 'RoundIntroductionSettings', 'AtticReturnBlur')) {
    Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/$name.cs" -Destination "$validationRoot/Assets"
    Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/$name.cs.meta" -Destination "$validationRoot/Assets"
}
foreach($source in @('Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs','Assets/Scripts/DialogueSystem/StoryInput/StoryFunctions.Sequences.cs','Assets/Scripts/Multiplayer/Lobby/IntroductionPanelShape.cs','Assets/Scripts/Multiplayer/Lobby/IntroductionSkipCircle.cs')) {
 Copy-Item -LiteralPath "$repoRoot/$source" -Destination "$validationRoot/Assets"
 Copy-Item -LiteralPath "$repoRoot/$source.meta" -Destination "$validationRoot/Assets"
}
Copy-Item -LiteralPath "$repoRoot/Library/ScriptAssemblies/Ink-Libraries.dll" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Assets/Scripts/Multiplayer/Lobby/LobbyMovementMotor.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/GhostPlacementGrid.shader" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$PSScriptRoot/ValidatePlayer.cs" -Destination "$validationRoot/Assets/PlayerValidationRunner.cs"
Copy-Item -LiteralPath "$PSScriptRoot/SceneTestDependencies.cs" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Assets/Art/HotelFish" -Destination "$validationRoot/Assets" -Recurse
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/HotelMultiplayerActions.inputactions" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/HotelMultiplayerActions.inputactions.meta" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$repoRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings"

# Test the authored movement and visual hierarchy without booting a lobby or a network connection.
$prefabText = Get-Content -LiteralPath "$repoRoot/Assets/Resources/HotelNetworkPlayer.prefab" -Raw
foreach ($fileId in @(21000003, 21000005)) {
    $prefabText = $prefabText -replace "(?m)^  - component: \{fileID: $fileId\}\r?\n", ''
    $prefabText = $prefabText -replace "(?ms)^--- !u!114 &$fileId\r?\n.*?(?=^--- !u!|\z)", ''
}
# Preview with the built-in sprite material; the source prefab keeps its URP material.
$prefabText = $prefabText -replace '\{fileID: 2100000, guid: 9dfc825aed78fcd4ba02077103263b40, type: 2\}', '{fileID: 0}'
Set-Content -LiteralPath "$validationRoot/Assets/HotelNetworkPlayer.prefab" -Value $prefabText

$dependencies = @{
    'com.unity.modules.audio' = '1.0.0'
    'com.unity.modules.physics' = '1.0.0'
    'com.unity.modules.imageconversion' = '1.0.0'
    'com.unity.modules.imgui' = '1.0.0'
    'com.unity.modules.ui' = '1.0.0'
    'com.unity.modules.uielements' = '1.0.0'
}
foreach ($packageName in @('com.unity.inputsystem', 'com.unity.2d.sprite', 'com.unity.ugui')) {
    $packagePath = Get-ChildItem -LiteralPath "$repoRoot/Library/PackageCache" -Directory |
        Where-Object { $_.Name.StartsWith($packageName + '@') } | Select-Object -First 1
    if (!$packagePath) { throw "Missing local package cache: $packageName" }
    $dependencies[$packageName] = 'file:' + $packagePath.FullName.Replace('\', '/')
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath "$validationRoot/Packages/manifest.json"
@'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  activeInputHandler: 1
'@ | Set-Content -LiteralPath "$validationRoot/ProjectSettings/ProjectSettings.asset"

$arguments = @('-batchmode', '-projectPath', "`"$validationRoot`"", '-executeMethod', 'ValidatePlayer.Run',
    '-logFile', "`"$validationRoot/validation.log`"")
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
Write-Output "Validation project: $validationRoot"
Write-Output "Unity process: $($process.Id)"
