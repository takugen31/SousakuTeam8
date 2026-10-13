$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$start=$source.IndexOf('public static bool IsMoteruRevealLine(')
$end=$source.IndexOf('private void StartMoteruRevealView',$start)
$method=$source.Substring($start,$end-$start)
Add-Type -TypeDefinition @"
using System;
namespace MoteruRevealSplitChecks { public static class Cues {
$method
} }
"@
function Assert-Reveal($condition,$message){if(-not $condition){throw $message}}
foreach($row in @(1,8,19,27)) {
    Assert-Reveal (-not [MoteruRevealSplitChecks.Cues]::IsMoteruRevealLine(('chapter2_moteru_{0:000}' -f $row))) 'Split appears outside approach/reveal scene'
}
foreach($row in 9..18) {
    Assert-Reveal ([MoteruRevealSplitChecks.Cues]::IsMoteruRevealLine(('chapter2_moteru_{0:000}' -f $row))) 'Split disappears during approach/reveal'
}
Assert-Reveal (-not [MoteruRevealSplitChecks.Cues]::IsMoteruRevealLine('chapter_moteru_1_009')) 'Effect leaks into Moteru route'
$start=$source.IndexOf('private void StartMoteruRevealView')
$end=$source.IndexOf('public static bool IsYowashiBandageLine',$start)
$effect=$source.Substring($start,$end-$start)
Assert-Reveal ($effect.Contains('SetRoomLookPose(topRoom, viewport, new Vector2(0.5f, 0.54f), 1.6f)') -and $effect.Contains('SetRoomLookPose(bottomRoom, viewport, new Vector2(0.5f, 0.46f), 1.6f)')) 'Both halves lack enlarged backgrounds'
Assert-Reveal ($effect.Contains('new Vector4(0f, height * 0.5f, 0f, 0f)') -and $effect.Contains('new Vector4(0f, 0f, 0f, height * 0.5f)')) 'Half clipping missing'
Assert-Reveal ($effect.Contains('new Vector2(0f, 0.475f)') -and $effect.Contains('new Vector2(1f, 0.525f)') -and $effect.Contains('ConfigureFeather(new Vector2(0f, viewport.y * 0.02f))')) 'Feathered horizontal separator missing'
Assert-Reveal ($source.Contains('IsMoteruRevealLine(line?.lineId) != isMoteruRevealViewActive') -and $source.Contains('StartCoroutine(FadeToDialogueComposition(line))')) 'Entry/return fade missing'
Assert-Reveal ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and -not $effect.Contains('HideLookPresentation')) 'Dialogue hidden/covered'
foreach($face in @(0.85,0.90,0.94)) {
    $y=0.25-(0.90-0.5)*2.6+($face-0.5)*2.6
    Assert-Reveal ($y -gt 0.025 -and $y -lt 0.5) 'Doute face clipped by top frame or divider'
}
$eyes=(0.88-0.85)*2.7;$mouth=(0.835-0.85)*2.7
Assert-Reveal ($eyes -gt 0 -and $mouth -lt -0.025 -and $mouth -gt -0.5) 'Moteru eyes are visible or mouth is hidden'
Assert-Reveal ($effect.Contains('leftPortraitImage.enabled = revealLeftWasEnabled') -and $effect.Contains('rightPortraitImage.enabled = revealRightWasEnabled')) 'Original portrait visibility not restored'
'PASS: approach/reveal-only split, full Doute face above, cropped Moteru face below, two enlarged backgrounds, horizontal separator, text UI, fades and cleanup.'
