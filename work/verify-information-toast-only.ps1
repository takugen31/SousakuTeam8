$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$controller = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
$start = $controller.IndexOf('public static string GetInformationNoticeTitle(')
$end = $controller.IndexOf('public static string WrapDialogueAtMeaningfulBreaks(', $start)
$method = $controller.Substring($start, $end-$start)
Add-Type -TypeDefinition @"
using System;
namespace InformationToastChecks {
public class DialogueLine { public string speakerId, text; }
public static class Notices {
$method
public static string Title(string speaker, string text) { return GetInformationNoticeTitle(new DialogueLine { speakerId=speaker, text=text }); }
} }
"@
function Check($condition, $message) { if (-not $condition) { throw $message } }
$expected = @{
    'chapter_yowashi_medicalbook_014' = '医学書の知識'
    'chapter_yowashi_catchsuccess_014' = 'ヨワシは息切れしやすい'
    'chapter_kayo_3_007' = '大葉カヨの家族'
}
$matched = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'Assets/Dialogue/CSV') -Recurse -Filter *.csv) {
    foreach ($row in Import-Csv $file.FullName) {
        $title = [InformationToastChecks.Notices]::Title($row.speakerId, $row.text)
        if ($title) { $matched[$row.lineId] = $title }
    }
}
Check ($matched.Count -eq $expected.Count) 'Ordinary dialogue falsely detected or notice missed'
foreach ($id in $expected.Keys) { Check ($matched[$id] -eq $expected[$id]) "Title mismatch: $id" }
Check ([InformationToastChecks.Notices]::Title('doute', '（ひとまず情報は手に入れたしコレは後で破っておこう）') -eq $null) 'Spoken dialogue removed'
Check ([InformationToastChecks.Notices]::Title('', '［誰かの日記］を手に入れた') -eq '誰かの日記') 'Legacy item notice unsupported'
Check ([InformationToastChecks.Notices]::Title('', '[情報] 新しい手がかり を手に入れた！') -eq '新しい手がかり') 'ASCII information marker unsupported'
Check ($controller.Contains('!currentLine.HasChoices && !currentLine.HasSceneTransition') -and $controller.Contains('ArchiveManager.UnlockInformationTitle(title)')) 'Choice/scene transition safety or title acquisition missing'
$runtimeCsv = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/DialogueRuntimeCsv.cs') -Raw
Check ($runtimeCsv.Contains('Get(row, columns, "archiveUnlockIds").Split') -and $runtimeCsv.Contains('line.archiveUnlockIds.Add(id)')) 'Embedded CSV events drop archive IDs'
$consultation = Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_Consultation.csv')
Check (($consultation | Where-Object lineId -eq 'chapter_yowashi_consultation_004').choice1Text.StartsWith('［情報］')) 'Information-selection choices removed'
$database = Get-Content (Join-Path $root 'Assets/Resources/Archive/ArchiveDatabase.asset') -Raw
Check ($database.Contains('id: chapter_kayo.family') -and $database.Contains('title: 大葉カヨの家族')) 'Family information cannot be acquired'
'PASS: all remaining acquisition notices route to toasts, exact titles, ordinary dialogue and information choices preserved, runtime CSV archive IDs supported.'
