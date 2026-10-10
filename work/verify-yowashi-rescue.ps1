$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
$zip = [System.IO.Compression.ZipFile]::OpenRead('C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/ヨワシシナリオ本文.docx')
try {
    $reader = [System.IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
    try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $paragraphs = @($document.SelectNodes('//w:body//w:p',$ns) | ForEach-Object {
        ($_.SelectNodes('.//w:t',$ns) | ForEach-Object { $_.InnerText }) -join ''
    })
} finally { $zip.Dispose() }
# Original paragraph locations also enforce completeness/order, not just substring matching.
$expected = @'
{"Tennis":[{"lineId":"chapter_yowashi_tennis_001","indices":[255]},{"lineId":"chapter_yowashi_tennis_002","indices":[256]},{"lineId":"chapter_yowashi_tennis_003","indices":[258]},{"lineId":"chapter_yowashi_tennis_004","indices":[260]},{"lineId":"chapter_yowashi_tennis_005","indices":[262,263]},{"lineId":"chapter_yowashi_tennis_006","indices":[265]},{"lineId":"chapter_yowashi_tennis_007","indices":[267]},{"lineId":"chapter_yowashi_tennis_008","indices":[269]},{"lineId":"chapter_yowashi_tennis_009","indices":[271]},{"lineId":"chapter_yowashi_tennis_010","indices":[273]},{"lineId":"chapter_yowashi_tennis_011","indices":[275]},{"lineId":"chapter_yowashi_tennis_012","indices":[277]},{"lineId":"chapter_yowashi_tennis_013","indices":[279]},{"lineId":"chapter_yowashi_tennis_014","indices":[281]},{"lineId":"chapter_yowashi_tennis_015","indices":[282]},{"lineId":"chapter_yowashi_tennis_016","indices":[283]},{"lineId":"chapter_yowashi_tennis_017","indices":[285,286]},{"lineId":"chapter_yowashi_tennis_018","indices":[288]},{"lineId":"chapter_yowashi_tennis_019","indices":[290]},{"lineId":"chapter_yowashi_tennis_020","indices":[292]},{"lineId":"chapter_yowashi_tennis_021","indices":[295]},{"lineId":"chapter_yowashi_tennis_022","indices":[298]},{"lineId":"chapter_yowashi_tennis_023","indices":[300]},{"lineId":"chapter_yowashi_tennis_024","indices":[302]},{"lineId":"chapter_yowashi_tennis_025","indices":[304]},{"lineId":"chapter_yowashi_tennis_026","indices":[306]},{"lineId":"chapter_yowashi_tennis_027","indices":[308]},{"lineId":"chapter_yowashi_tennis_028","indices":[310]},{"lineId":"chapter_yowashi_tennis_029","indices":[312]},{"lineId":"chapter_yowashi_tennis_030","indices":[313]},{"lineId":"chapter_yowashi_tennis_031","indices":[315]},{"lineId":"chapter_yowashi_tennis_032","indices":[317]},{"lineId":"chapter_yowashi_tennis_033","indices":[319]}],"Consultation":[{"lineId":"chapter_yowashi_consultation_001","indices":[323]},{"lineId":"chapter_yowashi_consultation_002","indices":[325]},{"lineId":"chapter_yowashi_consultation_003","indices":[327]},{"lineId":"chapter_yowashi_consultation_004","indices":[329]},{"lineId":"chapter_yowashi_consultation_005","indices":[336,337]},{"lineId":"chapter_yowashi_consultation_006","indices":[344]},{"lineId":"chapter_yowashi_consultation_007","indices":[346]},{"lineId":"chapter_yowashi_consultation_008","indices":[348]},{"lineId":"chapter_yowashi_consultation_009","indices":[350]}],"Rescue":[{"lineId":"chapter_yowashi_rescue_001","indices":[354]},{"lineId":"chapter_yowashi_rescue_002","indices":[356]},{"lineId":"chapter_yowashi_rescue_003","indices":[358]},{"lineId":"chapter_yowashi_rescue_004","indices":[360]},{"lineId":"chapter_yowashi_rescue_005","indices":[361]},{"lineId":"chapter_yowashi_rescue_006","indices":[362]},{"lineId":"chapter_yowashi_rescue_007","indices":[364]},{"lineId":"chapter_yowashi_rescue_008","indices":[366]},{"lineId":"chapter_yowashi_rescue_009","indices":[368]},{"lineId":"chapter_yowashi_rescue_010","indices":[370]},{"lineId":"chapter_yowashi_rescue_011","indices":[371,372]},{"lineId":"chapter_yowashi_rescue_012","indices":[376]},{"lineId":"chapter_yowashi_rescue_013","indices":[380,381]},{"lineId":"chapter_yowashi_rescue_014","indices":[382]},{"lineId":"chapter_yowashi_rescue_015","indices":[388]},{"lineId":"chapter_yowashi_rescue_016","indices":[390]},{"lineId":"chapter_yowashi_rescue_017","indices":[392]},{"lineId":"chapter_yowashi_rescue_018","indices":[394]},{"lineId":"chapter_yowashi_rescue_019","indices":[396]},{"lineId":"chapter_yowashi_rescue_020","indices":[398]},{"lineId":"chapter_yowashi_rescue_021","indices":[400]},{"lineId":"chapter_yowashi_rescue_022","indices":[402]},{"lineId":"chapter_yowashi_rescue_023","indices":[404]},{"lineId":"chapter_yowashi_rescue_024","indices":[406,407]},{"lineId":"chapter_yowashi_rescue_025","indices":[409]},{"lineId":"chapter_yowashi_rescue_026","indices":[411,412]},{"lineId":"chapter_yowashi_rescue_027","indices":[413]},{"lineId":"chapter_yowashi_rescue_028","indices":[415]},{"lineId":"chapter_yowashi_rescue_029","indices":[417]},{"lineId":"chapter_yowashi_rescue_030","indices":[418]},{"lineId":"chapter_yowashi_rescue_031","indices":[420]},{"lineId":"chapter_yowashi_rescue_032","indices":[422]},{"lineId":"chapter_yowashi_rescue_033","indices":[424]},{"lineId":"chapter_yowashi_rescue_034","indices":[426]},{"lineId":"chapter_yowashi_rescue_035","indices":[427]},{"lineId":"chapter_yowashi_rescue_036","indices":[429]},{"lineId":"chapter_yowashi_rescue_037","indices":[431]},{"lineId":"chapter_yowashi_rescue_038","indices":[433,434]},{"lineId":"chapter_yowashi_rescue_039","indices":[440]},{"lineId":"chapter_yowashi_rescue_040","indices":[442]},{"lineId":"chapter_yowashi_rescue_041","indices":[444]},{"lineId":"chapter_yowashi_rescue_042","indices":[446]}]}
'@ | ConvertFrom-Json -AsHashtable

# Use the real editor importer and CSV parser for all choice columns.
$importer = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Editor/DialogueCsvImporterWindow.cs') -Raw
$parseStart = $importer.IndexOf('    private static List<DialogueChoice> ParseChoices(')
$parseEnd = $importer.IndexOf('    private static List<string> ParseArchiveUnlockIds', $parseStart)
$indexStart = $importer.IndexOf('    private static bool TryGetChoiceColumnIndex(')
$indexEnd = $importer.IndexOf('    private static AffectionDelta ParseAffectionDelta(', $indexStart)
$parser = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Editor/CsvParser.cs') -Raw
$testSource = "using System; using System.Collections.Generic; using System.Text;`n" +
    'public sealed class DialogueChoice { public string text; public string nextLineId; } public static class YowashiChoiceParser { public static List<DialogueChoice> Parse(CsvRecord r) { return ParseChoices(r); }' +
    $importer.Substring($parseStart,$parseEnd-$parseStart) +
    $importer.Substring($indexStart,$indexEnd-$indexStart) + "}`n" +
    ($parser -replace '(?m)^using .*;\s*$','')
Add-Type -TypeDefinition $testSource
$chapters = @{}
$choiceMap = @{}
$total = 0
foreach ($name in @('Tennis','Consultation','Rescue')) {
    $csv = Get-Content (Join-Path $projectRoot "Assets/Dialogue/CSV/Chapter_Yowashi_$name.csv") -Raw -Encoding UTF8
    $rows = @(ConvertFrom-Csv $csv)
    $records = [CsvParser]::ParseRecords($csv)
    $asset = Get-Content (Join-Path $projectRoot "Assets/Dialogue/Data/Chapter_Yowashi_$name.asset") -Raw -Encoding UTF8
    $chapters[$name] = $rows
    Assert-True ($rows.Count -eq $expected[$name].Count) "Wrong chapter count $name"
    for ($i=0; $i -lt $rows.Count; $i++) {
        $row = $rows[$i]
        $original = (($expected[$name][$i].indices | ForEach-Object { $paragraphs[$_] }) -join "`n").Trim()
        if (($original.StartsWith('「') -and $original.EndsWith('」')) -or ($original.StartsWith('『') -and $original.EndsWith('』'))) {
            $original = $original.Substring(1,$original.Length-2)
        }
        Assert-True ($row.lineId -ceq $expected[$name][$i].lineId -and $row.text -ceq $original) "Source transcription/order mismatch $($row.lineId)"
        $body = [regex]::Match($asset,'(?ms)^  - lineId: '+[regex]::Escape($row.lineId)+'\r?\n(?<body>.*?)(?=^  - lineId: |\z)').Groups['body'].Value
        Assert-True ($body.Length -gt 0) "Missing asset line $($row.lineId)"
        $text = [regex]::Match($body,'(?m)^    text: (.*)$').Groups[1].Value.Trim() | ConvertFrom-Json
        Assert-True ($text -ceq $row.text) "CSV/asset text mismatch $($row.lineId)"
        foreach ($field in @('speakerId','expressionId','nextLineId','nextScenePath')) {
            $value = [regex]::Match($body,'(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value.Trim()
            Assert-True ($value -ceq $row.$field) "CSV/asset field mismatch $field $($row.lineId)"
        }
        $choices = [YowashiChoiceParser]::Parse($records[$i])
        $choiceMap[$row.lineId] = @($choices)
        $assetChoices = [regex]::Matches($body,'    - text: (.+)\r?\n      nextLineId: (\S+)')
        Assert-True ($choices.Count -eq $assetChoices.Count) 'CSV/asset choice count mismatch'
        for ($j=0; $j -lt $choices.Count; $j++) {
            $label = $assetChoices[$j].Groups[1].Value.Trim() | ConvertFrom-Json
            Assert-True ($label -ceq $choices[$j].text -and $assetChoices[$j].Groups[2].Value -ceq $choices[$j].nextLineId) 'CSV/asset choice mismatch'
            Assert-True ($rows.lineId -ccontains $choices[$j].nextLineId) 'Choice destination must exist in the same chapter'
        }
        Assert-True (-not ($choices.Count -and ($row.nextLineId -or $row.nextScenePath))) 'Choice cannot have automatic transition'
        Assert-True (-not ($row.nextLineId -and $row.nextScenePath)) 'Scene transition cannot have next line'
        if ($row.nextLineId) { Assert-True ($rows.lineId -ccontains $row.nextLineId) 'Dangling next line' }
        Assert-True ($body.Contains('affectionChanges: []') -and $body.Contains('archiveUnlockIds: []')) 'Unexpected score/evidence penalty'
        $total++
    }
}
Assert-True ($total -eq 84) 'Unexpected source line count'
$questions = @(
    @('chapter_yowashi_consultation_004',0,'chapter_yowashi_consultation_005'),
    @('chapter_yowashi_consultation_005',1,'chapter_yowashi_consultation_006')
)
foreach ($q in $questions) {
    $choices = $choiceMap[$q[0]]
    Assert-True ($choices.Count -eq 3) 'Each question must have three original choices'
    for ($i=0; $i -lt 3; $i++) {
        $target = if ($i -eq $q[1]) { $q[2] } else { $q[0] }
        Assert-True ($choices[$i].nextLineId -ceq $target) 'Wrong answer must retry; correct must advance'
        $sourceIndex = if ($q[0].EndsWith('004')) { 332+$i } else { 340+$i }
        $originalLabel = $paragraphs[$sourceIndex].Trim() -replace '\s*\(正解\)$',''
        Assert-True ($choices[$i].text -ceq $originalLabel) 'Invented evidence label'
    }
}
$gauge = $choiceMap['chapter_yowashi_rescue_014']
Assert-True ($gauge.Count -eq 2 -and $gauge[0].text -ceq '成功' -and $gauge[1].text -ceq '失敗') 'Temporary result choices changed'
foreach ($outcome in 0,1) {
    $rows = $chapters.Rescue
    $current = $rows[0]
    $visited = [System.Collections.Generic.HashSet[string]]::new()
    while (-not $current.nextScenePath) {
        Assert-True ($visited.Add($current.lineId)) 'Unexpected endless route'
        $options = $choiceMap[$current.lineId]
        $nextId = if ($options.Count) { $options[$outcome].nextLineId } else { $current.nextLineId }
        Assert-True (-not [string]::IsNullOrEmpty($nextId)) 'Unexpected early ending'
        $current = $rows | Where-Object lineId -CEQ $nextId
        Assert-True ($null -ne $current) 'Missing route destination'
    }
    Assert-True ($current.nextScenePath -ceq 'Assets/Scenes/GameMap/Title.unity') 'Ending must return to Title'
    $expectedLast = if ($outcome -eq 0) { 'chapter_yowashi_rescue_038' } else { 'chapter_yowashi_rescue_042' }
    Assert-True ($current.lineId -ceq $expectedLast) 'Wrong ending reached'
    if ($outcome -eq 0) {
        Assert-True (-not $visited.Contains('chapter_yowashi_rescue_039')) 'Success leaked into failure'
    } else {
        Assert-True (-not $visited.Contains('chapter_yowashi_rescue_015')) 'Failure leaked into success'
    }
}
$novel = Get-Content (Join-Path $projectRoot 'Assets/Scenes/GameMap/NovelScene_Yowashi.unity') -Raw
$lastIndex = $novel.IndexOf('guid: 80fefbecf55c419cb3fcbddc688ffb14')
foreach ($guid in @('a592ec6a731644879bf62a97688e824a','af0a183b132a498fac8e85252db919b0','320a3c5a5d9b4b77810653dca6e3615e')) {
    $index = $novel.IndexOf('guid: '+$guid)
    Assert-True ($index -gt $lastIndex) 'Continuation chapter order incorrect'
    $lastIndex = $index
}
$characters = Get-Content (Join-Path $projectRoot 'Assets/Dialogue/Data/CharacterDatabaseatPrologue.asset') -Raw
foreach ($row in $chapters.Values | ForEach-Object { $_ } | Where-Object speakerId) {
    Assert-True ($characters.Contains('characterId: '+$row.speakerId)) 'Unknown character'
}
Assert-True ($novel.Contains('namePlate: {fileID: 2100000100}') -and $novel.Contains('m_AnchoredPosition: {x: 0, y: 80}')) 'Shared UI/choice positioning lost'
Write-Output 'PASS: 84 dialogue rows match source paragraph wording/order and serialized assets.'
Write-Output 'PASS: Existing CSV importer reads all original evidence choices; four wrong answers retry without side effects.'
Write-Output 'PASS: Temporary success/failure paths reach their separate original endings and Title.'
Write-Output 'PASS: Medical-book continuation, character references and shared dialogue UI are valid.'
