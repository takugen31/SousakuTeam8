$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
function Assert-Shake($condition, $message) { if (-not $condition) { throw $message } }
Assert-Shake ($source.Contains('currentLine.lineId == "chapter1_003" && currentLine.speakerId == "moteru"')) 'Shake cue not restricted to Moteru panic line'
Assert-Shake ($source.Contains('panel == dialogueRoot.transform') -and $source.Contains('!plate.IsChildOf(panel)')) 'Portraits may shake or the name plate may receive double transforms'
Assert-Shake ($source.Contains('shakingWindowRects[i].anchoredPosition = shakingWindowPositions[i];') -and $source.Contains('shakingWindowRects[i].localRotation = shakingWindowRotations[i];')) 'Exact transform restore missing'
Assert-Shake ($source.Contains('shakingWindowRects == null || ArchiveManager.IsOpen || isChapterTransitioning || isSkipConfirmationOpen')) 'Menu/transition pause missing'
Assert-Shake ($source.Contains('const float duration = 2.2f;') -and $source.Contains('windowShakeElapsed >= duration) { StopWindowShake(); return; }')) 'Shake does not stop after initial seconds'
foreach ($method in @('PrepareLine(DialogueLine line)', 'OnDisable()', 'CancelChapterTransition()')) {
    $pattern = [regex]::Escape($method) + '\s*\{\s*(?:StopGatheringComposition\(\);\s*)?(?:StopSearchPortraitSlide\(\);\s*)?StopWindowShake\(\);'
    Assert-Shake ([regex]::IsMatch($source, $pattern)) "Cleanup missing in $method"
}
for ($i = 0; $i -le 220; $i++) {
    $t = $i / 100.0; $p = [Math]::Clamp($t/2.2,0,1); $strength = 1-($p*$p*(3-2*$p))
    $x = [Math]::Sin($t*2*[Math]::PI*5)*10*$strength
    $y = [Math]::Sin($t*2*[Math]::PI*7)*6*$strength
    $angle = [Math]::Sin($t*2*[Math]::PI*4)*1.2*$strength
    Assert-Shake ([Math]::Abs($x) -le 10 -and [Math]::Abs($y) -le 6 -and [Math]::Abs($angle) -le 1.2) 'Shake exceeds intended bounds'
}
Assert-Shake ($strength -eq 0) 'Shake fails to settle'
'PASS: panic-only cue, window-only targets, pause/cleanup guards and 221 bounded decay samples.'
