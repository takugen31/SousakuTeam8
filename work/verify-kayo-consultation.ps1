$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

# Run the existing importer's choice parsing code, rather than replacing its rules.
$importer = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Editor/DialogueCsvImporterWindow.cs') -Raw
$parseStart = $importer.IndexOf('    private static List<DialogueChoice> ParseChoices(')
$parseEnd = $importer.IndexOf('    private static List<string> ParseArchiveUnlockIds', $parseStart)
$indexStart = $importer.IndexOf('    private static bool TryGetChoiceColumnIndex(')
$indexEnd = $importer.IndexOf('    private static AffectionDelta ParseAffectionDelta(', $indexStart)
$parseMethod = $importer.Substring($parseStart, $parseEnd - $parseStart)
$indexMethod = $importer.Substring($indexStart, $indexEnd - $indexStart)
$parserSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Editor/CsvParser.cs') -Raw
$testSource = "using System; using System.Collections.Generic; using System.Text;`n" + @'
public sealed class DialogueChoice { public string text; public string nextLineId; }
public static class KayoImporterChoiceCheck {
    public static List<DialogueChoice> Parse(CsvRecord record) { return ParseChoices(record); }
'@ + "`n" + $parseMethod + $indexMethod + "`n}`n" + ($parserSource -replace '(?m)^using .*;\s*$', '')
Add-Type -TypeDefinition $testSource

$csvPath = Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_3.csv'
$records = [CsvParser]::ParseRecords((Get-Content -LiteralPath $csvPath -Raw -Encoding UTF8))
$rows = @(Import-Csv -LiteralPath $csvPath -Encoding UTF8)
$asset = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Dialogue/Data/Kayo/Chapter_Kayo_3.asset') -Raw -Encoding UTF8
$source = Get-Content -LiteralPath 'C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/選択肢案.txt' -Raw -Encoding UTF8
$section = $source.Split('【カヨの相談パートの選択肢】')[1].Split('【モテルの相談パートの選択肢】')[0]
$labels = @([regex]::Matches($section, '(?m)^\s*『([^』]+)』\s*$') | ForEach-Object { $_.Groups[1].Value })
$questions = @(
    @('chapter_kayo_3_034', 'カヨのドジ', 'chapter_kayo_3_035'),
    @('chapter_kayo_3_037', 'カヨの家族', 'chapter_kayo_3_038'),
    @('chapter_kayo_3_040', '大葉カヨ', 'chapter_kayo_3_041')
)
foreach ($question in $questions) {
    $record = $records | Where-Object { $_.Get('lineId') -eq $question[0] }
    $choices = [KayoImporterChoiceCheck]::Parse($record)
    Assert-True ($choices.Count -eq 6) 'Importer did not read all six choices.'
    Assert-True ([string]::IsNullOrEmpty($record.Get('nextLineId'))) 'Choice line has an automatic next-line link.'
    $assetPattern = '(?s)  - lineId: ' + $question[0] + '\r?\n.*?(?=  - lineId: |\z)'
    $assetBlock = [regex]::Match($asset, $assetPattern).Value
    $assetChoices = [regex]::Matches($assetBlock, '    - text: (.+)\r?\n      nextLineId: (\S+)')
    Assert-True ($assetChoices.Count -eq 6) 'Serialized asset does not have six choices.'
    for ($index = 0; $index -lt 6; $index++) {
        $choice = $choices[$index]
        $expectedNext = if ($labels[$index] -ceq $question[1]) { $question[2] } else { $question[0] }
        Assert-True ($choice.text -ceq $labels[$index]) 'Evidence label differs from the supplied source.'
        Assert-True ($choice.nextLineId -ceq $expectedNext) 'Correct/incorrect choice target differs.'
        $assetLabel = $assetChoices[$index].Groups[1].Value.Trim() | ConvertFrom-Json
        Assert-True ($assetLabel -ceq $choice.text) 'CSV and asset choice labels differ.'
        Assert-True ($assetChoices[$index].Groups[2].Value -ceq $choice.nextLineId) 'CSV and asset choice targets differ.'
        Assert-True ($rows.lineId -contains $choice.nextLineId) 'Choice points to a missing dialogue line.'
    }
    Assert-True ($assetBlock -match 'affectionChanges: \[\]' -and $assetBlock -match 'archiveUnlockIds: \[\]') 'Retry has side effects.'
}
Write-Output 'PASS: Existing CSV importer reads all six original evidence labels for all three questions.'
Write-Output 'PASS: Three correct choices advance; fifteen incorrect choices retry the same question without penalties.'

$branchRow = $rows | Where-Object lineId -eq 'chapter_kayo_3_011'
Assert-True ($branchRow.choice1NextLineId -eq 'chapter_kayo_3_012') 'Bad-ending branch changed.'
Assert-True ($branchRow.choice2NextLineId -eq 'chapter_kayo_3_033') 'Rest branch changed.'
$badEnd = $rows | Where-Object lineId -eq 'chapter_kayo_3_032'
Assert-True ($badEnd.nextScenePath -eq 'Assets/Scenes/GameMap/Title.unity') 'Bad-ending transition changed.'
Write-Output 'PASS: Existing story choice and bad-ending transition are unchanged.'
