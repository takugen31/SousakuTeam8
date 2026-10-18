$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rows = @(Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_1.csv'))
$first = $rows | Where-Object lineId -eq 'chapter_yowashi_1_006'
$second = $rows | Where-Object lineId -eq 'chapter_yowashi_1_006_b'
if ($first.nextLineId -ne $second.lineId -or $second.nextLineId -ne 'chapter_yowashi_1_007') { throw 'Split dialogue chain is broken' }
if ($first.text.Contains('恋人') -or -not $second.text.StartsWith('恋人を作らないと')) { throw 'Wrong semantic split point' }
if ($first.speakerId -ne 'yowashi' -or $second.speakerId -ne 'yowashi') { throw 'Speaker changed' }
$expected = 'ぴっちぴちの16歳！ていうか、細かいことを気にする男はモテないよ〜？恋人を作らないと出れない部屋なんだから、今からでもちょっとずつ意識しよっ、ね？'
if (($first.text + $second.text).Replace("`n", '').Replace("`r", '') -ne $expected) { throw 'Dialogue wording changed' }
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter_Yowashi_1.asset') -Raw
foreach ($row in @($first, $second)) {
    $block = [regex]::Match($asset, "(?s)  - lineId: $($row.lineId)\r?\n.*?(?=\r?\n  - lineId:|\z)").Value
    if (-not $block.Contains("nextLineId: $($row.nextLineId)") -or -not $block.Contains('speakerId: yowashi')) { throw 'Runtime asset differs from CSV' }
    $yamlText = [regex]::Match($block, 'text: "(.*)"').Groups[1].Value.Replace('\n', "`n")
    if ($yamlText -ne $row.text.Replace("`r", '')) { throw 'Runtime asset dialogue differs from CSV' }
    if ($block.Contains('fontSize')) { throw 'Font size changed for split page' }
}
if (($rows.lineId | Sort-Object -Unique).Count -ne $rows.Count) { throw 'Duplicate line ID' }
'PASS: explicit two-dialogue split before 恋人, unchanged wording/speaker, intact next links and matching runtime asset, no font-size changes.'
