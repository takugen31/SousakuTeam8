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
Assert-Egg ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and $effect.Contains('rect.anchoredPosition = Vector2.zero') -and $effect.Contains('root.rect.height * 0.42f')) 'Foreground centered large illustration missing'
Assert-Egg ($effect.Contains('egg.rect.width / egg.rect.height') -and $effect.Contains('image.preserveAspect = true') -and $effect.Contains('image.raycastTarget = false')) 'Aspect or input preservation missing'
Assert-Egg (-not $effect.Contains('SetRoomLookPose') -and -not $effect.Contains('HideLookPresentation')) 'Background or dialogue unexpectedly modified'
foreach ($method in @('PrepareLine(DialogueLine line)', 'OnDisable()', 'CancelChapterTransition()')) {
    Assert-Egg ([regex]::IsMatch($source, [regex]::Escape($method) + '\s*\{\s*StopKayoEggView\(\);')) "Cleanup missing in $method"
}
'PASS: egg exchange cue, existing standalone illustration, centered foreground, aspect preservation, portrait dimming/restoration and cleanup.'
