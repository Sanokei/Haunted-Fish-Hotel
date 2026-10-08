param(
    [Parameter(Mandatory=$true)][string]$ValidationProject,
    [string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe',
    [int]$TimeoutSeconds=300,
    [switch]$PrepareOnly,
    [switch]$StartOnly
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$validationRoot=(Resolve-Path -LiteralPath $ValidationProject).Path
$tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
if(!$validationRoot.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an existing disposable full validation project under personal Temp.'
}
foreach($required in @('ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Assets/Resources','Library')) {
    if(!(Test-Path -LiteralPath (Join-Path $validationRoot $required))) {
        throw ('The validation copy must contain the full production project and cached packages: '+$required)
    }
}
if(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.IndexOf($validationRoot,[StringComparison]::OrdinalIgnoreCase) -ge 0
}) {
    throw 'An Editor already owns this validation copy; wait for it to exit before syncing or launching.'
}
Copy-Item -Path (Join-Path $repoRoot 'Assets/Scripts/*') -Destination (Join-Path $validationRoot 'Assets/Scripts') -Recurse -Force
foreach($asset in @('Assets/Resources/MultiplayerDialoguePanel.prefab','Assets/Resources/MultiplayerDialogueOption.prefab','Assets/Resources/MultiplayerInputPanel.prefab')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $asset) -Destination (Join-Path $validationRoot $asset) -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot ($asset+'.meta')) -Destination (Join-Path $validationRoot ($asset+'.meta')) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NativeDialogueValidation.cs') -Destination (Join-Path $validationRoot 'Assets/NativeDialogueValidation.cs') -Force
if($PrepareOnly) {
    Write-Output ('Prepared native dialogue sources in '+$validationRoot+'; Unity was not launched.')
    return
}
if(!(Test-Path -LiteralPath $UnityEditor)) {throw ('Unity Editor was not found: '+$UnityEditor)}
$logPath=Join-Path $validationRoot 'native-dialogue-validation.log'
$arguments=@('-batchmode','-job-worker-count','2','-projectPath',('"'+$validationRoot+'"'),'-executeMethod','NativeDialogueValidation.Run','--hotel-dialogue-validation','-logFile',('"'+$logPath+'"'))
$process=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -ArgumentList $arguments -PassThru
Write-Output ('Native dialogue validation PID '+$process.Id+'; log '+$logPath)
if($StartOnly) {return}
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$process.WaitForExit(5000)) {
    if([DateTime]::UtcNow -ge $deadline) {
        $process.Kill($true)
        throw ('Owned native dialogue validation timed out; inspect '+$logPath)
    }
}
if($process.ExitCode -ne 0) {throw ('Native dialogue validation failed; inspect '+$logPath)}
$resultPath=Join-Path $validationRoot 'DialogueValidationEvidence/result.txt'
if(!(Test-Path -LiteralPath $resultPath) -or (Get-Content -LiteralPath $resultPath -Raw) -notmatch '^PASS ') {
    throw ('Unity exited without a passing native dialogue result; inspect '+$logPath)
}
Get-Content -LiteralPath $resultPath
