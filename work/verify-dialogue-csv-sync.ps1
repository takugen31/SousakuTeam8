param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $root ('Temp/DialogueCsvSyncChecks-' + [guid]::NewGuid().ToString('N'))
$editorFolder = Join-Path $testProject 'Assets/Editor'
New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testProject 'Packages') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testProject 'ProjectSettings') -Force | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $testProject 'Packages/manifest.json'), '{"dependencies":{}}', $encoding)
Copy-Item -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $testProject 'ProjectSettings/ProjectVersion.txt')
$editorSource = Join-Path $root 'Assets/Scripts/Dialogue/Editor'
foreach ($name in @('CsvParser.cs', 'DialogueCsvImportService.cs', 'DialogueCsvImporterWindow.cs',
        'DialogueCsvSyncRegistry.cs', 'DialogueCsvSyncProcessor.cs', 'DialogueCsvSyncWindow.cs')) {
    Copy-Item -LiteralPath (Join-Path $editorSource $name) -Destination $editorFolder
}
foreach ($name in @('DialogueScenarioSO.cs', 'CharacterDatabaseSO.cs', 'DialogueRuntimeCsv.cs')) {
    Copy-Item -LiteralPath (Join-Path $root "Assets/Scripts/Dialogue/Runtime/$name") -Destination (Join-Path $testProject 'Assets')
}
foreach ($name in @('AffectionDelta.cs', 'AffectionCondition.cs')) {
    Copy-Item -LiteralPath (Join-Path $root "Assets/Scripts/Affection/$name") -Destination (Join-Path $testProject 'Assets')
}
# AffectionCondition's runtime manager is unrelated to editor CSV syncing.
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/AffectionManager.cs'),
    'public class AffectionManager { public int GetAffection(string id) { return 0; } }', $encoding)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DialogueCsvSyncIntegrationChecks.cs') -Destination $editorFolder
$logPath = Join-Path $testProject 'sync-checks.log'
Write-Output "Isolated Unity project: $testProject"
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $testProject + '"'),
    '-executeMethod', 'DialogueCsvSyncIntegrationChecks.Run', '-logFile', ('"' + $logPath + '"'))
$deadline = [DateTime]::UtcNow.AddMinutes(6)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) {
        $process.Kill()
        throw "Unity checks timed out. Inspect: $logPath"
    }
}
$output = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
$output -split '\r?\n' | Where-Object { $_ -match 'PASS:|FAIL:|error CS|^CSV' } | Write-Output
if ($process.ExitCode -ne 0 -or $output -notmatch 'PASS: Dialogue CSV sync integration') {
    throw "Unity checks failed (exit $($process.ExitCode)). Inspect: $logPath"
}
