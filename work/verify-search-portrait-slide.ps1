$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$search = Get-Content (Join-Path $root 'Assets/Scripts/Search/Chapter1SearchController.cs') -Raw
function Assert-Slide($condition, $message) { if (-not $condition) { throw $message } }
Assert-Slide ($search.Contains('itemDialogueController.ConfigureSearchPortraitSlide()')) 'Exploration does not enable the effect'
Assert-Slide ($source.Contains('(line.speakerId != "kayo" && line.speakerId != "yowashi")')) 'Effect not restricted to the two requested characters'
$start = $source.IndexOf('private void StartSearchPortraitSlide')
$end = $source.IndexOf('private bool TryStartConsultationTransition', $start)
$effect = $source.Substring($start, $end-$start)
Assert-Slide (-not $effect.Contains('HideLookPresentation') -and -not $effect.Contains('dialogueRoot.SetActive') -and -not $effect.Contains('roomLookPresentation.alpha')) 'Dialogue window hidden during slide'
Assert-Slide ($effect.Contains('ArchiveManager.IsOpen') -and $effect.Contains('searchPortraitSlideElapsed / 8f')) 'Pause or slow slide missing'
Assert-Slide ($effect.Contains('rect.anchorMin = searchPortraitOriginalPose.anchorMin') -and $effect.Contains('rect.localScale = searchPortraitOriginalPose.scale')) 'Normal portrait geometry not restored'
foreach ($h in @(941,1080,814)) {
    foreach ($head in @(0.854,0.912)) {
        $height = $h*2.5
        $y = $h*0.5 - ($head-0.5)*$height
        Assert-Slide ([Math]::Abs($y+($head-0.5)*$height-$h*0.5) -lt 0.001) 'Head midpoint does not align with screen top'
    }
}
for ($i=0; $i -le 800; $i++) {
    $t=$i/800.0; $p=$t*$t*(3-2*$t); $x=0.20-0.06*$p
    Assert-Slide ($x -ge 0.14-0.0001 -and $x -le 0.20) 'Slide exceeds subtle horizontal bounds'
}
'PASS: exploration-only Kayo/Yowashi framing, dialogue kept visible, head crop, 8 s horizontal motion, pause and transform restoration.'
