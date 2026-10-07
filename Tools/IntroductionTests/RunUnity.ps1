param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot = Join-Path $env:TEMP ('HotelIntroductionValidation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
foreach ($source in @(
    'Assets/Scripts/Multiplayer/Lobby/LobbyManager.cs',
    'Assets/Scripts/Multiplayer/Lobby/IntroductionSkipCircle.cs',
    'Assets/Scripts/Multiplayer/Lobby/IntroductionPanelShape.cs',
    'Assets/Scripts/Multiplayer/HotelPalette.cs',
    'Assets/Scripts/Multiplayer/Lobby/LobbyIntroductionPresentation.cs',
    'Assets/Scripts/Multiplayer/Lobby/LobbyContracts.cs',
    'Assets/Scripts/DialogueSystem/StoryInput/StoryFunctions.Sequences.cs',
    'Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs'
)) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $source) -Destination "$validationRoot/Assets"
}
Copy-Item -LiteralPath "$PSScriptRoot/ValidateIntroduction.cs" -Destination "$validationRoot/Assets/IntroductionValidationRunner.cs"
Copy-Item -LiteralPath "$repoRoot/Library/ScriptAssemblies/Ink-Libraries.dll" -Destination "$validationRoot/Assets"
Copy-Item -LiteralPath "$repoRoot/Assets/Dialogue/LobbyIntroduction.json" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/LobbyIntroductionUI.prefab" -Destination "$validationRoot/Assets/Resources"
foreach ($source in @('Assets/Scripts/DialogueSystem/StoryInput/StoryUI.cs.meta',
    'Assets/Scripts/Multiplayer/Lobby/IntroductionPanelShape.cs.meta',
    'Assets/Scripts/Multiplayer/Lobby/IntroductionSkipCircle.cs.meta')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $source) -Destination "$validationRoot/Assets"
}
Copy-Item -LiteralPath "$repoRoot/Assets/TextMesh Pro" -Destination "$validationRoot/Assets" -Recurse
Copy-Item -LiteralPath "$repoRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings"
$dependencies = @{}
foreach ($module in @('audio', 'imageconversion', 'imgui', 'ui', 'uielements', 'physics')) {
    $dependencies['com.unity.modules.' + $module] = '1.0.0'
}
foreach ($packageName in @('com.unity.inputsystem', 'com.unity.ugui')) {
    $packagePath = Get-ChildItem -LiteralPath "$repoRoot/Library/PackageCache" -Directory |
        Where-Object { $_.Name.StartsWith($packageName + '@') } | Select-Object -First 1
    if (!$packagePath) { throw "Missing local package cache: $packageName" }
    $dependencies[$packageName] = 'file:' + $packagePath.FullName.Replace('\', '/')
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$validationRoot/Packages/manifest.json"
@'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  activeInputHandler: 1
'@ | Set-Content -LiteralPath "$validationRoot/ProjectSettings/ProjectSettings.asset"
$arguments = @('-batchmode', '-projectPath', "`"$validationRoot`"", '-executeMethod', 'ValidateIntroduction.Run',
    '-logFile', "`"$validationRoot/validation.log`"")
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
Write-Output "Validation project: $validationRoot"
Write-Output "Unity process: $($process.Id)"
