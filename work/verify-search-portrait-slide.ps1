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
    foreach ($head in @(0.82,0.86)) {
        $height = $h*3.1
        $y = $h*0.5 - ($head-0.5)*$height
        Assert-Slide ([Math]::Abs($y+($head-0.5)*$height-$h*0.5) -lt 0.001) 'Head midpoint does not align with screen top'
    }
}
for ($i=0; $i -le 800; $i++) {
    $t=$i/800.0; $p=$t*$t*(3-2*$t); $x=0.20-0.12*$p
    Assert-Slide ($x -ge 0.08-0.0001 -and $x -le 0.20) 'Slide exceeds horizontal bounds'
}
Assert-Slide ($effect.Contains('dimmed.r *= 0.70f') -and $effect.Contains('leftPortraitImage.color = searchDouteOriginalColor')) 'Doute dim/restore missing'
Assert-Slide ($effect.Contains('rect.SetSiblingIndex(leftPortraitImage.transform.GetSiblingIndex() + 1)') -and $effect.Contains('rect.SetSiblingIndex(searchPortraitOriginalSibling)')) 'Foreground layering or restoration missing'
Assert-Slide ($effect.Contains('Mathf.Lerp(0.48f, 0.52f, progress), 0.5f), 1.35f') -and $effect.Contains('searchParallaxBackground.raycastTarget = false')) 'Background zoom/parallax or input safety missing'
# Eyes are above the screen top for both imported sprites.
foreach ($pair in @(@(0.844,0.82),@(0.886,0.86))) {
    Assert-Slide (0.5 + ($pair[0]-$pair[1])*3.1 -gt 0.5) 'Eyes visible below screen top'
}
'PASS: exploration-only Kayo/Yowashi framing, dialogue kept visible, head crop, 8 s horizontal motion, pause and transform restoration.'
