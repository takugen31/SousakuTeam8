$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csv = Get-Content (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_1.csv') -Raw
$rows = @(Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_1.csv'))
$before = $rows | Where-Object lineId -eq 'chapter_yowashi_1_008'
$after = $rows | Where-Object lineId -eq 'chapter_yowashi_1_010'
if ($before.nextLineId -ne $after.lineId -or $after.archiveUnlockIds -ne 'chapter_yowashi.bandage') { throw 'Dialogue or unlock chain broken' }
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter_Yowashi_1.asset') -Raw
foreach ($text in @($csv, $asset)) {
    if ($text.Contains('chapter_yowashi_1_009') -or $text.Contains('ヨワシの腕の包帯　を手に入れた！')) { throw 'Acquisition dialogue remains' }
}
$block = [regex]::Match($asset, '(?s)  - lineId: chapter_yowashi_1_010\r?\n.*?(?=\r?\n  - lineId:|\z)').Value
if (-not $block.Contains('- chapter_yowashi.bandage')) { throw 'Runtime archive unlock missing' }
$manager = Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveManager.cs') -Raw
if (-not $manager.Contains('menu.ShowUnlockNotification(entry.Title)')) { throw 'Right-top notification missing' }
'PASS: acquisition dialogue removed, 008 advances directly to 010, archive unlock and shared notification preserved.'
