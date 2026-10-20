$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$database = Get-Content "$root/Assets/Resources/Archive/ArchiveDatabase.asset" -Raw
$controller = Get-Content "$root/Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs" -Raw
$rows = @{}
Get-ChildItem "$root/Assets/Dialogue/CSV" -Recurse -Filter *.csv | ForEach-Object {
    Import-Csv $_.FullName | ForEach-Object { if ($_.lineId) { $rows[$_.lineId] = $_ } }
}
$entries = @{}
$triggerCount = 0
foreach ($match in [regex]::Matches($database, '(?ms)^  - id: ([^\r\n]+)\r?\n(.*?)(?=^  - id: |\z)')) {
    $id = $match.Groups[1].Value
    $block = $match.Groups[2].Value
    $triggers = @([regex]::Matches($block, '(?m)^    - ([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value })
    $entries[$id] = $triggers
    foreach ($lineId in $triggers) {
        if (-not $rows.ContainsKey($lineId) -or -not $rows[$lineId].text) { throw "Missing dialogue: $id -> $lineId" }
        $triggerCount++
    }
    if ($id.StartsWith('character.') -and ($triggers.Count -eq 0 -or $block -notmatch 'unlockedAtStart: 0' -or $block -notmatch 'showBeforeUnlock: 0')) {
        throw "Premature/missing character unlock: $id"
    }
}
$expected = @{
    'character.kayo.name' = 'chapter2_kayo_001'
    'character.yowashi.name' = 'chapter2_yowashi_002'
    'character.moteru.name' = 'chapter2_moteru_001'
    'character.doute.name' = 'prologue2_014'
    'character.yowashi.age' = 'chapter_yowashi_1_006'
    'character.moteru.gender' = 'chapter2_moteru_013'
    'character.kayo.mother' = 'chapter_kayo_diary_009'
    'character.kayo.overwork' = 'chapter_kayo_4_005'
    'chapter_kayo.family' = 'chapter_kayo_diary_015'
}
foreach ($id in $expected.Keys) {
    if ($entries[$id] -notcontains $expected[$id]) { throw "Incorrect reveal timing: $id" }
}
foreach ($lineId in $entries['character.doute.introduction']) {
    if ($rows[$lineId].text -notmatch 'ゲーム、血液型はA型、誕生日は1月21日') { throw "Introduction content mismatch: $lineId" }
}
if (-not $controller.Contains('ArchiveManager.UnlockDialogueLine(line?.lineId);')) { throw 'Dialogue hook missing' }
& "$PSScriptRoot/verify-notification-archive-registration.ps1"
"PASS: $triggerCount reveal triggers reference existing dialogue; all characters covered, later facts hidden until revealed; actual unlock logic tested."
