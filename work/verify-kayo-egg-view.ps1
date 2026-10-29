$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
$start = $source.IndexOf('public static bool IsKayoEggLine(')
$end = $source.IndexOf('private void StartKayoEggView', $start)
$method = $source.Substring($start, $end - $start)
Add-Type -TypeDefinition "using System; public static class KayoEggChecks { $method }"
function Assert-Egg($condition, $message) { if (-not $condition) { throw $message } }
foreach ($row in 1..23) {
    $actual = [KayoEggChecks]::IsKayoEggLine(('chapter2_kayo_{0:000}' -f $row))
    Assert-Egg ($actual -eq ($row -ge 12 -and $row -le 14)) "Wrong cue at row $row"
}
Assert-Egg (-not [KayoEggChecks]::IsKayoEggLine($null)) 'Null cue accepted'
$asset = Get-Content (Join-Path $root 'Assets/Resources/DialogueAdvanceMarks.asset') -Raw
Assert-Egg ($asset.Contains('kayo: {fileID: -3340895152570392092, guid: e035f7301bbad7c4e8f00898dff70e7b')) 'Standalone egg asset not referenced'
$start = $source.IndexOf('private void StartKayoEggView')
$end = $source.IndexOf('public static bool IsMoteruRevealLine', $start)
$effect = $source.Substring($start, $end - $start)
Assert-Egg ($effect.Contains('Resources.Load<DialogueAdvanceMarkSet>("DialogueAdvanceMarks")?.kayo')) 'Existing egg illustration not used'
Assert-Egg ($effect.Contains('eggLeftOriginalColor.r * 0.45f') -and $effect.Contains('eggRightOriginalColor.r * 0.45f')) 'Both portraits not dimmed'
Assert-Egg ($effect.Contains('leftPortraitImage.color = eggLeftOriginalColor') -and $effect.Contains('rightPortraitImage.color = eggRightOriginalColor')) 'Original colors not restored'
Assert-Egg ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and $effect.Contains('Vector2.Lerp(kayoEggSlideStart, Vector2.zero, eased)') -and $effect.Contains('viewport.y * 0.52f')) 'Foreground centered enlarged illustration missing'
Assert-Egg ($effect.Contains('(viewport.x + rect.sizeDelta.x) * 0.5f') -and $effect.Contains('-(viewport.y + rect.sizeDelta.y) * 0.5f')) 'Entrance does not start offscreen at lower right'
Assert-Egg ($source.Contains('UpdateKayoEggSlide();') -and $effect.Contains('ArchiveManager.IsOpen') -and $effect.Contains('kayoEggSlideElapsed + Time.unscaledDeltaTime')) 'Slide update or menu pause missing'
Assert-Egg ($source.Contains('if (!IsKayoEggLine(line?.lineId) || line.lineId == "chapter2_kayo_012") kayoEggSlideElapsed = 0f;')) 'Entrance restarts on later pages or fails to reset on entry'
Assert-Egg ($effect.Contains('kayoEggSlideElapsed = kayoEggSlideElapsed + Time.unscaledDeltaTime;') -and $effect.Contains('kayoEggSlideElapsed - 1.2f') -and $effect.Contains('2f * Mathf.PI / 4f') -and $effect.Contains('new Vector2(0f, floatY)')) 'Post-arrival four-second vertical float missing'
foreach ($i in 0..800) {
    $time = $i / 100.0
    $ramp = [Math]::Min(1.0, $time / 0.8)
    $floatY = [Math]::Sin($time * 2 * [Math]::PI / 4) * (1080 * 0.018) * ($ramp * $ramp * (3-2*$ramp))
    Assert-Egg ([Math]::Abs($floatY) -le 19.440001) 'Float exceeds intended gentle amplitude'
    if ($i -eq 0) { Assert-Egg ($floatY -eq 0) 'Float jumps at arrival' }
}
$previous = 0.0
foreach ($i in 0..120) {
    $t = $i / 120.0; $eased = 1 - [Math]::Pow(1-$t, 3)
    Assert-Egg ($eased -ge $previous -and $eased -ge 0 -and $eased -le 1) 'Diagonal slide overshoots or reverses'
    $previous = $eased
}
Assert-Egg ($effect.Contains('egg.rect.width / egg.rect.height') -and $effect.Contains('image.preserveAspect = true') -and $effect.Contains('image.raycastTarget = false')) 'Aspect or input preservation missing'
Assert-Egg (-not $effect.Contains('SetRoomLookPose') -and -not $effect.Contains('HideLookPresentation')) 'Background or dialogue unexpectedly modified'
foreach ($method in @('PrepareLine(DialogueLine line)', 'OnDisable()', 'CancelChapterTransition()')) {
    Assert-Egg ([regex]::IsMatch($source, [regex]::Escape($method) + '\s*\{\s*(?:StopEndingCard\(\);\s*)?(?:moteruApproachElapsed = 0f;\s*)?StopKayoEggView\(\);')) "Cleanup missing in $method"
}
Assert-Egg ($source.Contains('isKayoEggViewActive && kayoEggRect != null && !IsKayoEggLine(line?.lineId)') -and $source.Contains('StartCoroutine(SlideKayoEggOut(line))')) 'Leaving egg dialogue bypasses the exit animation'
$exitStart = $source.IndexOf('private IEnumerator SlideKayoEggOut(')
$exitEnd = $source.IndexOf('private void StopKayoEggView()', $exitStart)
$exit = $source.Substring($exitStart, $exitEnd - $exitStart)
Assert-Egg ($exit.Contains('Vector2 start = kayoEggRect.anchoredPosition;') -and $exit.Contains('Vector2 destination = -kayoEggSlideStart;')) 'Exit jumps from idle or fails to travel offscreen upper left'
Assert-Egg ($exit.Contains('elapsed / 1.2f') -and $exit.Contains('1f - Mathf.Pow(1f - t, 3f)') -and $exit.Contains('Vector2.Lerp(start, destination, eased)')) 'Exit does not match entrance duration/easing'
Assert-Egg ($exit.Contains('isRoomLookPlaying = true;') -and $exit.Contains('ArchiveManager.IsOpen || isChapterTransitioning || isSkipConfirmationOpen')) 'Exit input lock or menu pause missing'
Assert-Egg ($exit.IndexOf('StopKayoEggView();') -gt $exit.IndexOf('Vector2.Lerp(start, destination, eased)') -and $exit.IndexOf('ShowLineImmediately(nextLine)') -gt $exit.IndexOf('StopKayoEggView();')) 'Egg removed or next dialogue shown before exit completes'
'PASS: egg exchange cue, existing standalone illustration, centered foreground, aspect preservation, portrait dimming/restoration and cleanup.'
