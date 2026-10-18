param(
    [string]$ScenarioPath = 'C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/大葉カヨ.txt'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$csvPath = Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_Diary.csv'
$assetPath = Join-Path $projectRoot 'Assets/Dialogue/Data/Kayo/Chapter_Kayo_Diary.asset'
$rows = @(Import-Csv -LiteralPath $csvPath -Encoding UTF8)
$assetLines = @(Get-Content -LiteralPath $assetPath -Encoding UTF8)
$assetTexts = @($assetLines | Where-Object { $_ -match '^    text: ' } | ForEach-Object {
    $_.Substring(10) | ConvertFrom-Json
})
Assert-True ($rows.Count -eq 15 -and $assetTexts.Count -eq 15) 'Diary line count differs.'

$sourceLines = @(Get-Content -LiteralPath $ScenarioPath -Encoding UTF8)
$intro = $sourceLines[137]
$expectedTexts = @($intro.Substring(1, $intro.Length - 2)) + @($sourceLines[139..152])
for ($index = 0; $index -lt $rows.Count; $index++) {
    Assert-True ($rows[$index].text -ceq $expectedTexts[$index]) "CSV differs from the original at line $index."
    Assert-True ($assetTexts[$index] -ceq $expectedTexts[$index]) "Asset differs from the original at line $index."
    $expectedId = 'chapter_kayo_diary_{0:D3}' -f ($index + 1)
    Assert-True ($rows[$index].lineId -ceq $expectedId) 'Diary line ID differs.'
    $expectedNext = if ($index + 1 -lt $rows.Count) { $rows[$index + 1].lineId } else { '' }
    Assert-True ($rows[$index].nextLineId -ceq $expectedNext) 'Diary next-line link differs.'
}
Write-Output 'PASS: All 15 diary lines match the supplied scenario exactly; CSV and asset links agree.'

$searchScene = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scenes/GameMap/Chapter_Kayo_SearchScene.unity') -Raw
$novelScene = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scenes/GameMap/NovelScene_Kayo.unity') -Raw
$puzzleScene = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/PuzzleGame/Scenes/PuzzleGame.unity') -Raw
Assert-True ($searchScene -match 'puzzleSceneName: PuzzleGame') 'Search does not point to PuzzleGame.'
Assert-True ($searchScene -match 'completionResumeLineId: chapter_kayo_diary_001') 'Search resumes after the diary.'
Assert-True ($puzzleScene -match 'guid: 7ac902dc5bae4d0ca9d6df46b8df9c67') 'Puzzle bridge is not attached.'
Assert-True ($puzzleScene -match 'component: \{fileID: 1584477324\}') 'Puzzle bridge component reference is missing.'
$chapter2Index = $novelScene.IndexOf('guid: 4b835d0e93d76b84f82420dcbc8d8e74')
$diaryIndex = $novelScene.IndexOf('guid: 850f54e9b9e34b1dad6ee0325e54f12b')
$chapter3Index = $novelScene.IndexOf('guid: 6553fee95283e8b4a9d7e6289eb039fd')
Assert-True ($chapter2Index -ge 0 -and $chapter2Index -lt $diaryIndex -and $diaryIndex -lt $chapter3Index) 'Kayo scenario order differs.'
$buildSettings = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Raw
foreach ($scene in @('Assets/PuzzleGame/Scenes/PuzzleGame.unity', 'Assets/Scenes/GameMap/NovelScene_Kayo.unity')) {
    Assert-True ($buildSettings -match ('- enabled: 1\s+path: ' + [regex]::Escape($scene))) "Scene is not enabled: $scene"
}
Write-Output 'PASS: Search -> PuzzleGame -> diary -> existing Chapter 3 references are connected.'

# Exercise the existing puzzle model against the compiled Unity project assembly.
$assemblyPath = Join-Path $projectRoot 'Temp/bin/Debug/SousakuTeam8.PuzzleGame.dll'
$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$boardType = $assembly.GetType('SousakuTeam8.PuzzleGame.PuzzleBoardState', $true)
for ($seed = 0; $seed -lt 128; $seed++) {
    $board = [Activator]::CreateInstance($boardType)
    $board.Shuffle([int]$seed)
    Assert-True (-not $board.IsComplete) "Puzzle starts completed for seed $seed."
    # Any slot can be exchanged with any other slot; every shuffle can be solved.
    for ($slot = 0; $slot -lt 9; $slot++) {
        $pieceSlot = $board.FindSlotForPiece([int]$slot)
        if ($pieceSlot -ne $slot) {
            Assert-True ($board.TrySwapSlots([int]$slot, [int]$pieceSlot)) 'A valid exchange was rejected.'
        }
    }
    Assert-True ($board.IsComplete) "Puzzle cannot be completed for seed $seed."
}
Write-Output 'PASS: 128 shuffled puzzles start unfinished and can all be completed.'
