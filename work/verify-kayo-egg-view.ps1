$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
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
$previous = 0.0
foreach ($i in 0..120) {
    $t = $i / 120.0; $eased = 1 - [Math]::Pow(1-$t, 3)
    Assert-Egg ($eased -ge $previous -and $eased -ge 0 -and $eased -le 1) 'Diagonal slide overshoots or reverses'
    $previous = $eased
}
Assert-Egg ($effect.Contains('egg.rect.width / egg.rect.height') -and $effect.Contains('image.preserveAspect = true') -and $effect.Contains('image.raycastTarget = false')) 'Aspect or input preservation missing'
Assert-Egg (-not $effect.Contains('SetRoomLookPose') -and -not $effect.Contains('HideLookPresentation')) 'Background or dialogue unexpectedly modified'
foreach ($method in @('PrepareLine(DialogueLine line)', 'OnDisable()', 'CancelChapterTransition()')) {
    Assert-Egg ([regex]::IsMatch($source, [regex]::Escape($method) + '\s*\{\s*(?:moteruApproachElapsed = 0f;\s*)?StopKayoEggView\(\);')) "Cleanup missing in $method"
}
'PASS: egg exchange cue, existing standalone illustration, centered foreground, aspect preservation, portrait dimming/restoration and cleanup.'
