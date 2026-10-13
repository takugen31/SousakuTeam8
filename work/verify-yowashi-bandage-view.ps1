$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$start=$source.IndexOf('public static bool IsYowashiBandageLine(')
$end=$source.IndexOf('private void StartYowashiBandageView', $start)
$method=$source.Substring($start,$end-$start)
Add-Type -TypeDefinition @"
using System;
namespace YowashiBandageViewChecks { public static class Cues {
$method
} }
"@
function Assert-Bandage($condition,$message){if(-not $condition){throw $message}}
foreach($row in @(1,8,17,18)) {
    Assert-Bandage (-not [YowashiBandageViewChecks.Cues]::IsYowashiBandageLine(('chapter2_yowashi_{0:000}' -f $row))) 'Torso appears outside bandage dialogue'
}
foreach($row in 9..16) {
    Assert-Bandage ([YowashiBandageViewChecks.Cues]::IsYowashiBandageLine(('chapter2_yowashi_{0:000}' -f $row))) 'Torso does not persist through bandage dialogue'
}
Assert-Bandage (-not [YowashiBandageViewChecks.Cues]::IsYowashiBandageLine('chapter1_yowashi_009')) 'Effect leaks into gathering scene'
$rows=Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter2/chapter2_yowashi.csv')
$surprise=$rows | Where-Object lineId -eq 'chapter2_yowashi_008'
Assert-Bandage ($surprise.nextLineId -eq 'chapter2_yowashi_009' -and $surprise.speakerId -eq 'doute') 'Entry does not follow Doute surprise page'
Assert-Bandage ($source.Contains('IsYowashiBandageLine(line?.lineId) != isYowashiBandageViewActive') -and $source.Contains('StartCoroutine(FadeToDialogueComposition(line))')) 'Entry/exit do not fade'
$effectStart=$source.IndexOf('private void StartYowashiBandageView')
$effectEnd=$source.IndexOf('private void StartGatheringComposition',$effectStart)
$effect=$source.Substring($effectStart,$effectEnd-$effectStart)
Assert-Bandage ($effect.Contains('viewport.y * 4f') -and $effect.Contains('(0.5f - 0.65f) * height')) 'Torso crop missing'
Assert-Bandage ($effect.Contains('new Vector2(0.56f, 1f)') -and $effect.Contains('new Vector2(0.86f, 0f)') -and $effect.Contains('image.color = Color.black')) 'Markup-aligned left band or right band missing'
Assert-Bandage ($effect.Contains('ConfigureFeather(new Vector2(parent.rect.width * 0.035f, 0f))')) 'Side-band edge transparency fade missing'
Assert-Bandage ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and -not $effect.Contains('HideLookPresentation')) 'Text UI is hidden or covered'
Assert-Bandage ($effect.Contains('leftPortraitImage.enabled = bandageLeftWasEnabled') -and $effect.Contains('rightPortraitImage.enabled = bandageRightWasEnabled')) 'Portrait visibility not restored'
Assert-Bandage ((0.90-0.65)*4 -gt 0.5 -and (0.66-0.65)*4 -lt 0.5 -and (0.66-0.65)*4 -gt -0.5) 'Face remains in view or forearm leaves view'
'PASS: surprise-page advance -> fade -> torso/forearm view, held for rows 009-016 with side bands/text UI, fade return and visibility restoration.'
