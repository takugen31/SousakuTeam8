$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check($condition, $message) { if (-not $condition) { throw $message } }
function Read-YowashiScenario($csvName, $assetName) {
    $rows = @(Import-Csv (Join-Path $root "Assets/Dialogue/CSV/Yowashi/$csvName.csv"))
    $asset = [System.IO.File]::ReadAllText((Join-Path $root "Assets/Dialogue/Data/Yowashi/$assetName.asset"))
    $blocks = [regex]::Matches($asset, '(?ms)^  - lineId: ([^\r\n]+)\r?\n.*?(?=^  - lineId:|\z)')
    Check ($rows.Count -eq $blocks.Count) 'CSV/runtime line counts differ'
    Check (($rows.lineId | Sort-Object -Unique).Count -eq $rows.Count) 'Duplicate dialogue line ID'
    $map = @{}
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $row = $rows[$i]; $block = $blocks[$i].Value
        Check ($row.lineId -eq $blocks[$i].Groups[1].Value) 'CSV/runtime line order differs'
        $encoded = [regex]::Match($block, '(?m)^    text:([^\r\n]*)').Groups[1].Value.Trim()
        $text = if ($encoded.StartsWith('"')) { ConvertFrom-Json $encoded } else { $encoded }
        Check ($text.Replace("`r", '') -ceq ([string]$row.text).Replace("`r", '')) "CSV/runtime text mismatch: $($row.lineId)"
        foreach ($field in @('speakerId', 'expressionId', 'nextLineId')) {
            $value = [regex]::Match($block, '(?m)^    ' + $field + ':([^\r\n]*)').Groups[1].Value.Trim()
            Check ($value -ceq ([string]$row.$field)) "CSV/runtime $field mismatch: $($row.lineId)"
        }
        $choices = [regex]::Matches($block, '(?m)^    - text: ("[^\r\n]*")\r?\n      nextLineId: ([^\r\n]+)')
        $expectedChoiceCount = 0
        foreach ($number in 1..3) {
            $choiceText = [string]$row.("choice${number}Text")
            if (-not $choiceText) { continue }
            Check ($expectedChoiceCount -lt $choices.Count) 'Missing runtime choice'
            $choice = $choices[$expectedChoiceCount++]
            Check ((ConvertFrom-Json $choice.Groups[1].Value) -ceq $choiceText) 'Choice text differs'
            Check ($choice.Groups[2].Value.Trim() -ceq $row.("choice${number}NextLineId")) 'Choice target differs'
        }
        Check ($expectedChoiceCount -eq $choices.Count) 'Unexpected runtime choice'
        $map[$row.lineId] = $row
    }
    foreach ($row in $rows) {
        foreach ($field in @('nextLineId', 'choice1NextLineId', 'choice2NextLineId', 'choice3NextLineId')) {
            $target = [string]$row.$field
            if ($target) { Check ($map.ContainsKey($target)) "Missing dialogue destination: $target" }
        }
    }
    return ,$map
}
$tennis = Read-YowashiScenario 'chapter_yowashi_tennis' 'Chapter_Yowashi_Tennis'
Check ($tennis['chapter_yowashi_tennis_009'].text -ceq 'うん、『テニスの女王様』って漫画面白いじゃん？前からやってみたくてさ〜') 'Manga title revision missing'
Check ($tennis['chapter_yowashi_tennis_021'].nextLineId -eq 'chapter_yowashi_tennis_021_b') 'Existing narration page split lost'
Check ($tennis['chapter_yowashi_tennis_021_b'].nextLineId -eq 'chapter_yowashi_tennis_021_time') 'Time passage in wrong position'
Check ($tennis['chapter_yowashi_tennis_021_time'].text -ceq '(10分後)' -and $tennis['chapter_yowashi_tennis_021_time'].nextLineId -eq 'chapter_yowashi_tennis_022') 'Time passage text/continuation incorrect'
$consultation = Read-YowashiScenario 'chapter_yowashi_consultation' 'Chapter_Yowashi_Consultation'
foreach ($question in @('004', '005')) {
    $id = 'chapter_yowashi_consultation_' + $question
    $wrong = $consultation[$id + '_wrong']
    Check ($wrong.speakerId -eq 'player' -and $wrong.text -ceq '(いや、それは多分関係ないな)' -and $wrong.nextLineId -eq $id) 'Wrong-answer response/return incorrect'
    $wrongChoices = if ($question -eq '004') { @(2, 3) } else { @(1, 3) }
    foreach ($number in $wrongChoices) {
        Check ($consultation[$id].("choice${number}NextLineId") -eq $wrong.lineId) 'Wrong choice bypasses feedback'
    }
}
Check ($consultation['chapter_yowashi_consultation_004'].choice1NextLineId -eq 'chapter_yowashi_consultation_005') 'First correct answer changed'
Check ($consultation['chapter_yowashi_consultation_005'].choice2NextLineId -eq 'chapter_yowashi_consultation_006') 'Second correct answer changed'
Check (@($consultation.Values | Where-Object { -not $_.nextLineId -and -not $_.choice1Text }).Count -eq 1 -and -not $consultation['chapter_yowashi_consultation_009'].nextLineId) 'Consultation termination changed'
'PASS: four revisions, prior page split, both wrong-answer retry paths, correct-answer progression, and complete CSV/runtime parity.'
