$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
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
foreach($row in @(1,8,15,16,17,18)) {
    Assert-Bandage (-not [YowashiBandageViewChecks.Cues]::IsYowashiBandageLine(('chapter2_yowashi_{0:000}' -f $row))) 'Torso appears outside bandage dialogue'
}
foreach($row in 9..14) {
    Assert-Bandage ([YowashiBandageViewChecks.Cues]::IsYowashiBandageLine(('chapter2_yowashi_{0:000}' -f $row))) 'Torso does not persist through bandage dialogue'
}
Assert-Bandage (-not [YowashiBandageViewChecks.Cues]::IsYowashiBandageLine('chapter1_yowashi_009')) 'Effect leaks into gathering scene'
$rows=Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter2/chapter2_yowashi.csv')
$surprise=$rows | Where-Object lineId -eq 'chapter2_yowashi_008'
Assert-Bandage ($surprise.nextLineId -eq 'chapter2_yowashi_009' -and $surprise.speakerId -eq 'doute') 'Entry does not follow Doute surprise page'
$beforeQuestion = $rows | Where-Object lineId -eq 'chapter2_yowashi_014'
$question = $rows | Where-Object lineId -eq 'chapter2_yowashi_015'
Assert-Bandage ($beforeQuestion.nextLineId -eq $question.lineId -and $question.speakerId -eq 'doute' -and $question.text.Contains('この腕はいったい')) 'Return does not precede Doute bandage question'
Assert-Bandage ($source.Contains('IsYowashiBandageLine(line?.lineId) != isYowashiBandageViewActive') -and $source.Contains('StartCoroutine(FadeToDialogueComposition(line))')) 'Entry/exit do not fade'
$effectStart=$source.IndexOf('private void StartYowashiBandageView')
$effectEnd=$source.IndexOf('private void StartGatheringComposition',$effectStart)
$effect=$source.Substring($effectStart,$effectEnd-$effectStart)
Assert-Bandage ($effect.Contains('viewport.y * 4f') -and $effect.Contains('(0.5f - 0.65f) * height')) 'Torso crop missing'
Assert-Bandage ($effect.Contains('const float bandWidth = 0.14f / 3f') -and $effect.Contains('leftInner - bandWidth, 0f') -and $effect.Contains('rightInner + bandWidth, 1f') -and $effect.Contains('image.color = Color.black')) 'Equal one-third-width separators missing'
Assert-Bandage ($effect.Contains('ConfigureFeather(new Vector2(parent.rect.width * 0.01f, 0f))')) 'Narrow side-band edge transparency fade missing'
Assert-Bandage ($effect.Contains('SetRoomLookPose(normalRoom, viewport, new Vector2(0.5f, 0.5f), 1f)')) 'Normal-size outer background missing'
Assert-Bandage ($effect.Contains('CreateRoomLookImage(crop, "BandageZoomedRoom")') -and $effect.Contains('CreateRoomLookImage(crop, "YowashiTorso")') -and $effect.Contains('cropObject.GetComponent<RectMask2D>().padding')) 'Zoomed room or portrait leaks outside separators'
$width = 0.14 / 3
$leftInner = 0.56; $rightInner = 0.86
$leftCut = $leftInner - $width / 2; $rightCut = $rightInner + $width / 2
Assert-Bandage ($leftCut -gt ($leftInner - $width + 0.01) -and $leftCut -lt ($leftInner - 0.01)) 'Left clipping seam outside opaque band center'
Assert-Bandage ($rightCut -gt ($rightInner + 0.01) -and $rightCut -lt ($rightInner + $width - 0.01)) 'Right clipping seam outside opaque band center'
Assert-Bandage ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and -not $effect.Contains('HideLookPresentation')) 'Text UI is hidden or covered'
Assert-Bandage ($effect.Contains('leftPortraitImage.enabled = bandageLeftWasEnabled') -and $effect.Contains('rightPortraitImage.enabled = bandageRightWasEnabled')) 'Portrait visibility not restored'
Assert-Bandage ((0.90-0.65)*4 -gt 0.5 -and (0.66-0.65)*4 -lt 0.5 -and (0.66-0.65)*4 -gt -0.5) 'Face remains in view or forearm leaves view'
'PASS: surprise-page advance -> fade -> torso/forearm view, rows 009-014, narrow separators/normal outer background, fade return before Doute question (015), visibility restoration.'
