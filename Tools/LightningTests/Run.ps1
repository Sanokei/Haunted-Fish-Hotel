param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validationRoot = Join-Path $env:TEMP ('HotelLightningValidation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$validationRoot/Assets/Resources", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
foreach ($path in @('Scripts/LightningEffectManager.cs', 'Art/Window.png', 'Art/Window.png.meta')) {
    Copy-Item -LiteralPath "$repoRoot/Assets/$path" -Destination "$validationRoot/Assets"
}
Copy-Item -LiteralPath "$repoRoot/Assets/Resources/WindowLightning.shader" -Destination "$validationRoot/Assets/Resources"
Copy-Item -LiteralPath "$PSScriptRoot/ValidateLightning.cs" -Destination "$validationRoot/Assets/LightningValidationRunner.cs"
Copy-Item -LiteralPath "$repoRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings"
$dependencies = @{
    'com.unity.modules.physics' = '1.0.0'
    'com.unity.modules.imageconversion' = '1.0.0'
    'com.unity.modules.imgui' = '1.0.0'
    'com.unity.modules.ui' = '1.0.0'
    'com.unity.modules.uielements' = '1.0.0'
}
foreach ($packageName in @('com.unity.ugui', 'com.unity.2d.sprite')) {
    $packagePath = Get-ChildItem -LiteralPath "$repoRoot/Library/PackageCache" -Directory |
        Where-Object { $_.Name.StartsWith($packageName + '@') } | Select-Object -First 1
    if (!$packagePath) { throw "Missing local package cache: $packageName" }
    $dependencies[$packageName] = 'file:' + $packagePath.FullName.Replace('\', '/')
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$validationRoot/Packages/manifest.json"
$arguments = @('-batchmode', '-projectPath', "`"$validationRoot`"", '-executeMethod', 'ValidateLightning.Run',
    '-logFile', "`"$validationRoot/validation.log`"")
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
Write-Output "Validation project: $validationRoot"
Write-Output "Unity process: $($process.Id)"
