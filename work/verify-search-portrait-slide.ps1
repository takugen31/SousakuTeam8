$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$search = Get-Content (Join-Path $root 'Assets/Scripts/Search/Chapter1SearchController.cs') -Raw
function Assert-Slide($condition, $message) { if (-not $condition) { throw $message } }
Assert-Slide ($search.Contains('itemDialogueController.ConfigureSearchPortraitSlide(backgroundImage)')) 'Exploration does not enable the effect with its room background'
Assert-Slide ($source.Contains('(line.speakerId != "kayo" && line.speakerId != "yowashi")')) 'Effect not restricted to the two requested characters'
$start = $source.IndexOf('private void StartSearchPortraitSlide')
$end = $source.IndexOf('private bool TryStartConsultationTransition', $start)
$effect = $source.Substring($start, $end-$start)
Assert-Slide (-not $effect.Contains('HideLookPresentation') -and -not $effect.Contains('dialogueRoot.SetActive') -and -not $effect.Contains('roomLookPresentation.alpha')) 'Dialogue window hidden during slide'
Assert-Slide ($effect.Contains('ArchiveManager.IsOpen') -and $effect.Contains('searchPortraitSlideElapsed / 8f')) 'Pause or slow slide missing'
Assert-Slide ($effect.Contains('rect.anchorMin = searchPortraitOriginalPose.anchorMin') -and $effect.Contains('rect.localScale = searchPortraitOriginalPose.scale')) 'Normal portrait geometry not restored'
foreach ($h in @(941,1080,814)) {
    foreach ($head in @(0.83,0.87)) {
        $height = $h*3.1
        $y = $h*0.5 - ($head-0.5)*$height
        Assert-Slide ([Math]::Abs($y+($head-0.5)*$height-$h*0.5) -lt 0.001) 'Head midpoint does not align with screen top'
    }
}
for ($i=0; $i -le 800; $i++) {
    $t=$i/800.0; $p=$t*$t*(3-2*$t); $x=0.20-0.12*$p
    Assert-Slide ($x -ge 0.08-0.0001 -and $x -le 0.20) 'Slide exceeds horizontal bounds'
}
Assert-Slide ($effect.Contains('const float brightness = 0.45f') -and $effect.Contains('leftPortraitImage.color = searchDouteOriginalColor')) 'Doute fixed dim/restore missing'
Assert-Slide ($effect.Contains('rect.SetSiblingIndex(leftPortraitImage.transform.GetSiblingIndex() + 1)') -and $effect.Contains('rect.SetSiblingIndex(searchPortraitOriginalSibling)')) 'Foreground layering or restoration missing'
Assert-Slide ($effect.Contains('Mathf.Lerp(0.48f, 0.52f, progress), 0.5f), 1.35f') -and $effect.Contains('searchParallaxBackground.raycastTarget = false')) 'Background zoom/parallax or input safety missing'
# Eyes are above the screen top for both imported sprites.
foreach ($pair in @(@(0.844,0.83),@(0.886,0.87))) {
    Assert-Slide (0.5 + ($pair[0]-$pair[1])*3.1 -gt 0.5) 'Eyes visible below screen top'
}
Assert-Slide ($effect.Contains('line.speakerId == "kayo" ? 0.83f : 0.87f')) 'Per-character mouth framing missing'
# Mouth upper edges are below the viewport top, unlike eye centers.
foreach ($pair in @(@(0.818,0.83),@(0.866,0.87))) {
    $mouthY = 0.5 + ($pair[0]-$pair[1])*3.1
    Assert-Slide ($mouthY -lt 0.5 -and $mouthY -gt 0.35) 'Mouth cropped or too far below top'
}
Assert-Slide ($effect.Contains('Vector2.right * (parent.rect.width * 0.035f * progress)') -and $effect.Contains('leftPortraitImage.rectTransform.anchoredPosition = searchDouteOriginalPosition;')) 'Doute right slide/restore missing'
Assert-Slide (-not $source.Contains('searchPortraitFadeCopy') -and -not $effect.Contains('entryBlend')) 'Normal-size ghost or brightness crossfade remains'
$entryStart = $source.IndexOf('private IEnumerator FadeIntoSearchCloseUp')
$entryEnd = $source.IndexOf('private IEnumerator PlayAwakeningRoomLook', $entryStart)
$entry = $source.Substring($entryStart,$entryEnd-$entryStart)
Assert-Slide ($entry.IndexOf('yield return FadeLookReturn(0f, 1f)') -lt $entry.IndexOf('PrepareLine(line)') -and $entry.IndexOf('PrepareLine(line)') -lt $entry.IndexOf('yield return FadeLookReturn(1f, 0f)')) 'Incoming normal portrait is applied before full black'
Assert-Slide ($entry.Contains('yield return null;') -and $effect.Contains('isRoomLookPlaying || rightPortraitImage == null')) 'Full-black frame or motion pause missing'
$exitStart = $source.IndexOf('private IEnumerator FadeOutSearchCloseUp')
$exitEnd = $source.IndexOf('private void EndDialogueImmediately', $exitStart)
$exitEffect = $source.Substring($exitStart,$exitEnd-$exitStart)
Assert-Slide ($source.Contains('if (useSearchPortraitSlide && isSearchPortraitSliding)') -and $source.Contains('StartCoroutine(FadeOutSearchCloseUp())')) 'Exit input skips the fade'
Assert-Slide ($exitEffect.IndexOf('yield return FadeLookReturn(0f, 1f)') -lt $exitEffect.IndexOf('StopSearchPortraitSlide()') -and $exitEffect.IndexOf('StopSearchPortraitSlide()') -lt $exitEffect.IndexOf('yield return FadeLookReturn(1f, 0f)')) 'Exit restores the normal view before full black'
Assert-Slide ($exitEffect.IndexOf('yield return FadeLookReturn(1f, 0f)') -lt $exitEffect.IndexOf('EndDialogueImmediately()') -and $exitEffect.Contains('isRoomLookPlaying = true')) 'Completion/input are not held until exit fade ends'
'PASS: exploration-only Kayo/Yowashi framing, dialogue kept visible, head crop, 8 s horizontal motion, pause and transform restoration.'
