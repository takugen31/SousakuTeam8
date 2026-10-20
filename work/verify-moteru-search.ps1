param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe',
    [ValidateSet('Search','Diary')][string]$Suite = 'Search',
    [switch]$CompletionFixture
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $root ('Temp/MoteruSearchChecks-' + [guid]::NewGuid().ToString('N'))
foreach ($folder in @('Assets/Editor','Assets/Dialogue/CSV/Moteru','Assets/Dialogue/Data/Moteru',
    'Assets/Scenes/GameMap','Assets/PuzzleGame/Scenes','Assets/Fonts/Noto_Sans_JP','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $testProject $folder) -Force | Out-Null
}
$encoding = [Text.UTF8Encoding]::new($false)
$manifest = '{"dependencies":{"com.unity.ugui":"2.0.0","com.unity.inputsystem":"1.18.0","com.unity.render-pipelines.universal":"17.3.0","com.unity.modules.audio":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.physics2d":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.animation":"1.0.0"}}'
[IO.File]::WriteAllText((Join-Path $testProject 'Packages/manifest.json'), $manifest, $encoding)
Copy-Item -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $testProject 'ProjectSettings')
foreach ($folder in @('Assets/Scripts','Assets/FallingGame/Scripts','Assets/PuzzleGame/Runtime',
    'Assets/Art','Assets/Resources','Assets/TextMesh Pro/Resources')) {
    $destination = Join-Path $testProject $folder
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $destination -Recurse
}
foreach ($file in @('Assets/Dialogue/CSV/Characters.csv',
    'Assets/Dialogue/CSV/Moteru/chapter_moteru_search.csv',
    'Assets/Dialogue/CSV/Moteru/chapter_moteru_diary.csv',
    'Assets/Dialogue/Data/CharacterDatabase.asset',
    'Assets/Dialogue/Data/Moteru/Chapter_Moteru_Search.asset',
    'Assets/Dialogue/Data/Moteru/MoteruSearchProfile.asset',
    'Assets/Scenes/GameMap/Chapter_Moteru_SearchScene.unity',
    'Assets/Scenes/GameMap/NovelScene_Moteru.unity',
    'Assets/PuzzleGame/Scenes/PuzzleGame.unity',
    'Assets/Fonts/Noto_Sans_JP/NotoSansJP-Black SDF.asset',
    'Assets/Fonts/Noto_Sans_JP/NotoSansJP-Black.ttf')) {
    foreach ($suffix in @('', '.meta')) {
        Copy-Item -LiteralPath (Join-Path $root ($file + $suffix)) -Destination (Join-Path $testProject ($file + $suffix))
    }
}
Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Dialogue/Data/Moteru') -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $testProject ('Assets/Dialogue/Data/Moteru/' + $_.Name))
}
$checkName = if ($Suite -eq 'Diary') { 'MoteruDiary' } else { 'MoteruSearch' }
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot ($checkName + 'Checks.cs')))
$split = $source.IndexOf('[InitializeOnLoad]')
[IO.File]::WriteAllText((Join-Path $testProject ('Assets/' + $checkName + 'Checks.cs')), $source.Substring(0,$split), $encoding)
[IO.File]::WriteAllText((Join-Path $testProject ('Assets/Editor/' + $checkName + 'Bootstrap.cs')),
    "using System; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;`n" + $source.Substring($split), $encoding)
$logPath = Join-Path $testProject 'search-checks.log'
Write-Output "Isolated Unity project: $testProject"
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','-nographics','-projectPath',('"'+$testProject+'"'),
    '-executeMethod',($checkName + 'Bootstrap.' + $(if ($CompletionFixture -and $Suite -eq 'Search') { 'RunWithCompletionFixture' } else { 'Run' })),
    '-logFile',('"'+$logPath+'"'))
$deadline = [DateTime]::UtcNow.AddMinutes(6)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity checks timed out: $logPath" }
}
$output = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
$output -split '\r?\n' | Where-Object { $_ -match '^PASS:|^FAIL:|error CS|^Exception:' } | Write-Output
$successMarker = if ($Suite -eq 'Diary') { 'PASS: Moteru diary integration completed' } else { 'PASS: Moteru search integration completed' }
if ($process.ExitCode -ne 0 -or -not $output.Contains($successMarker)) {
    throw "Unity checks failed (exit $($process.ExitCode)): $logPath"
}
