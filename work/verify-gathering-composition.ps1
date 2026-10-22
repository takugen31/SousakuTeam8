$ErrorActionPreference = 'Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
function Assert-Gather($condition,$message) { if (-not $condition) { throw $message } }
$start=$source.IndexOf('private void StartGatheringComposition')
$end=$source.IndexOf('private void StartSearchPortraitSlide',$start)
$effect=$source.Substring($start,$end-$start)
Assert-Gather ($effect.Contains('StartsWith("chapter1_kayo_", StringComparison.Ordinal)') -and $effect.Contains('StartsWith("chapter1_yowashi_", StringComparison.Ordinal)') -and -not $effect.Contains('chapter2_')) 'Gathering scope incorrect'
Assert-Gather ($effect.Contains('SetGatheringPortrait(leftPortraitImage, viewport, 0.23f, 2.7f, Mathf.Lerp(startY, -0.91f, progress)') -and $effect.Contains('SetGatheringPortrait(rightPortraitImage, viewport, 0.76f, 3.1f')) 'Portrait framing missing'
Assert-Gather ($effect.Contains('new Vector2(0.5f, 0.48f), 1.35f') -and $effect.Contains('Mathf.Lerp(0.48f, 0.53f, progress)') -and $effect.Contains('gatheringDivider.color = Color.black') -and $effect.Contains('ConfigureFeather(new Vector2(viewport.x * 0.08f, 0f))')) 'Independent camera heights, opaque center or wider edge feather missing'
Assert-Gather ($effect.Contains('CreateRoomLookImage(gatheringLeftFrame, "GatheringLeftRoom")') -and $effect.Contains('CreateRoomLookImage(gatheringRightFrame, "GatheringRightRoom")')) 'Background halves are not independently clipped'
Assert-Gather ($effect.Contains('Mathf.Lerp(0.48f, 0.53f, progress)') -and $effect.Contains('SetRoomLookPose(gatheringRightBackground, viewport, new Vector2(0.5f, 0.48f), 1.35f)')) 'Right background is not above Doute background after slide'
Assert-Gather ((0.5-0.48)*1.35 -gt (0.5-0.53)*1.35) 'Vertical background offset direction is reversed'
Assert-Gather ((0.53-0.48)*1.35 -gt 0.06 -and (0.53-0.48)*1.35 -lt 0.08) 'Background height difference is not subtle'
Assert-Gather ($effect.Contains('width * 0.5f') -and $effect.Contains('typeof(RectMask2D)')) 'Portrait halves can overlap'
Assert-Gather ($effect.Contains('SetSiblingIndex(gatheringRightFrame.GetSiblingIndex() + 1)') -and $effect.Contains('gatheringDivider.raycastTarget = false')) 'Divider layering/input safety incorrect'
Assert-Gather (-not $effect.Contains('HideLookPresentation') -and $effect.Contains('RestorePortraitPose')) 'Dialogue hidden or original layout not restored'
# Doute's hair top and face are inside the viewport; target eyes are above it and mouths below it.
Assert-Gather (-0.91+(1-0.5)*2.7 -lt 0.5 -and -0.91+(0.9-0.5)*2.7 -gt -0.5) 'Doute face/head out of frame'
foreach($pair in @(@(0.844,0.818,0.83),@(0.886,0.866,0.87))) {
    Assert-Gather (0.5+($pair[0]-$pair[2])*3.1 -gt 0.5 -and 0.5+($pair[1]-$pair[2])*3.1 -lt 0.5) 'Target eye/mouth crop incorrect'
}
'PASS: gathering-only split composition, visible Doute face, cropped target eyes with mouth visible, zoomed background, center feathered divider, masks and cleanup.'
Assert-Gather ($effect.Contains('gatheringAnimationTarget != target') -and $effect.Contains('gatheringDouteSlideElapsed / 5.5f') -and $effect.Contains('ArchiveManager.IsOpen || isChapterTransitioning')) 'Slide restart guard, slow duration or pause missing'
$startY=0.5-(0.90-0.5)*2.7
Assert-Gather ([Math]::Abs($startY+(0.90-0.5)*2.7-0.5) -lt 0.0001) 'Initial Doute head midpoint not at screen top'
$previousY=$startY
for($i=0;$i -le 550;$i++) {
    $t=$i/550.0;$p=$t*$t*(3-2*$t)
    $y=$startY+(-0.91-$startY)*$p
    $bg=(0.5-(0.48+0.05*$p))*1.35
    Assert-Gather ($y -le $previousY+0.0001) 'Doute moves upward instead of down'
    $previousY=$y
}
Assert-Gather ([Math]::Abs($y+0.91) -lt 0.0001 -and [Math]::Abs($bg-(0.5-0.53)*1.35) -lt 0.0001) 'Original Doute/background final framing changed'
'PASS: Doute starts head-cropped, slides downward over 5.5 s with left background, and retains progress across dialogue pages.'
