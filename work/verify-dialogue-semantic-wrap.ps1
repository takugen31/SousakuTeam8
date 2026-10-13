$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$start = $source.IndexOf('public static string WrapDialogueAtMeaningfulBreaks(')
$end = $source.IndexOf('private void StartWindowShake()', $start)
$methods = $source.Substring($start, $end-$start)
Add-Type -TypeDefinition @"
using System;
public static class DialogueWrapChecks {
$methods
public static string Wrap(string text, float width) {
    return WrapDialogueAtMeaningfulBreaks(text, width, value => value.Length);
}
}
"@
function Check($condition, $message) { if (-not $condition) { throw $message } }
$example = 'カヨちゃんは自分なりにドジを気にしてるみたいですけど、治すことの難しさもあるし、俺はどう応援するのがいいんでしょう？'
$formatted = [DialogueWrapChecks]::Wrap($example, 28)
Check ($formatted.Contains("ですけど、`n")) 'Clause end not preferred'
Check ($formatted.Replace("`n", '') -eq $example) 'Dialogue content changed'
$explicit = "短いセリフ。`n次のセリフ。"
Check ([DialogueWrapChecks]::Wrap($explicit, 28) -eq $explicit) 'Authored newline lost'
$rich = '<color=red>長い文章を装飾するタグ</color>'
Check ([DialogueWrapChecks]::Wrap($rich, 8) -eq $rich) 'Rich text corrupted'
Check ([DialogueWrapChecks]::Wrap('', 28) -eq '') 'Empty choice-only dialogue changed'
Check ([DialogueWrapChecks]::Wrap($example, 0) -eq $example) 'Zero-width layout corrupts content'
$count = 0; $changed = 0
foreach ($file in Get-ChildItem (Join-Path $root 'Assets/Dialogue/CSV') -Recurse -Filter *.csv) {
    foreach ($row in Import-Csv $file.FullName) {
        if ([string]::IsNullOrEmpty($row.text)) { continue }
        foreach ($width in @(26, 32, 40)) {
            $wrapped = [DialogueWrapChecks]::Wrap($row.text, $width)
            Check ($wrapped.Replace("`n", '').Replace("`r", '') -eq $row.text.Replace("`n", '').Replace("`r", '')) "Content changed: $($row.lineId)"
            Check (-not [regex]::IsMatch($wrapped, '\n[、。，．？！!?）］｝」』】ぁぃぅぇぉっゃゅょァィゥェォッャュョー…‥]')) "Bad line start: $($row.lineId)"
            if ($wrapped -ne $row.text) { $changed++ }
            $count++
        }
    }
}
"PASS: actual formatter checked $count dialogue/width combinations ($changed adjusted), authored breaks and all content preserved, punctuation-safe boundaries."
"Example:`n$formatted"
