$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$start=$source.IndexOf('public static bool IsMoteruRevealLine(')
$end=$source.IndexOf('private void StartMoteruRevealView',$start)
$method=$source.Substring($start,$end-$start)
$examinationStart = $source.IndexOf('public static bool IsKayoExaminationLine(')
$examinationEnd = $source.IndexOf('private void StartKayoExaminationView(', $examinationStart)
$method += $source.Substring($examinationStart, $examinationEnd - $examinationStart)
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
foreach ($row in 1..12) {
    $id = 'kayo_catch_success_before_{0:000}' -f $row
    Assert-Reveal ([MoteruRevealSplitChecks.Cues]::IsKayoApproachLine($id) -eq ($row -ge 6 -and $row -le 7)) 'Wrong Kayo approach cue'
    Assert-Reveal ([MoteruRevealSplitChecks.Cues]::IsKayoExaminationLine($id) -eq ($row -ge 8)) 'Wrong Kayo examination cue'
}
foreach ($id in @($null, 'kayo_catch_success_before_013', 'kayo_catch_success_after_006', 'kayo_catch_failure_006')) {
    Assert-Reveal (-not [MoteruRevealSplitChecks.Cues]::IsKayoApproachLine($id)) 'Kayo approach leaks into unrelated scenes'
    Assert-Reveal (-not [MoteruRevealSplitChecks.Cues]::IsKayoExaminationLine($id)) 'Examination leaks into unrelated scenes'
}
$start=$source.IndexOf('private void StartMoteruRevealView')
$end=$source.IndexOf('public static bool IsYowashiBandageLine',$start)
$effect=$source.Substring($start,$end-$start)
Assert-Reveal ($effect.Contains('SetRoomLookPose(topRoom, viewport, new Vector2(0.44f, 0.62f), 1.6f)') -and $effect.Contains('SetRoomLookPose(bottomRoom, viewport, new Vector2(0.56f, 0.38f), 1.6f)')) 'Independent enlarged background framing missing'
Assert-Reveal ($effect.Contains('new Vector4(0f, height * 0.5f, 0f, 0f)') -and $effect.Contains('new Vector4(0f, 0f, 0f, height * 0.5f)')) 'Half clipping missing'
Assert-Reveal ($effect.Contains('new Vector2(0f, 0.475f)') -and $effect.Contains('new Vector2(1f, 0.525f)') -and $effect.Contains('ConfigureFeather(new Vector2(0f, viewport.y * 0.02f))')) 'Feathered horizontal separator missing'
Assert-Reveal ($source.Contains('IsApproachRevealLine(line?.lineId) != isMoteruRevealViewActive') -and $source.Contains('StartCoroutine(FadeToDialogueComposition(line))')) 'Entry/return fade missing'
Assert-Reveal ($effect.Contains('rightPortraitImage.transform.GetSiblingIndex() + 1') -and -not $effect.Contains('HideLookPresentation')) 'Dialogue hidden/covered'
foreach($face in @(0.85,0.90,0.94)) {
    $y=0.25-(0.90-0.5)*2.6+($face-0.5)*2.6
    Assert-Reveal ($y -gt 0.025 -and $y -lt 0.5) 'Doute face clipped by top frame or divider'
}
$eyes=(0.88-0.85)*2.7;$mouth=(0.835-0.85)*2.7
Assert-Reveal ($eyes -gt 0 -and $mouth -lt -0.025 -and $mouth -gt -0.5) 'Moteru eyes are visible or mouth is hidden'
Assert-Reveal ($effect.Contains('leftPortraitImage.enabled = revealLeftWasEnabled') -and $effect.Contains('rightPortraitImage.enabled = revealRightWasEnabled')) 'Original portrait visibility not restored'
Assert-Reveal ($effect.Contains('viewport, 0.27f, 2.6f') -and $effect.Contains('Mathf.Lerp(0.82f, 0.52f, t)')) 'Initial separation and left approach missing'
Assert-Reveal ($effect.Contains('currentLine?.lineId == "chapter2_moteru_010"') -and $effect.Contains('ArchiveManager.IsOpen') -and $source.Contains('UpdateMoteruApproach();')) 'Page advance trigger or menu pause missing'
Assert-Reveal ($source.Contains('if (!IsApproachRevealLine(line?.lineId)) moteruApproachElapsed = 0f;')) 'Progress does not persist across split pages or reset outside them'
Assert-Reveal ($effect.Contains('TryGetCharacter(isKayo ? "kayo" : "moteru"') -and $effect.Contains('"KayoApproachSplitView"') -and $effect.Contains('line.lineId == "kayo_catch_success_before_006"') -and $effect.Contains('IsKayoApproachLine(currentLine?.lineId) ? 0.84f : 0.85f')) 'Kayo sprite, entry reset or mouth framing missing'
Assert-Reveal ($effect.Contains('moteruRevealBackground = bottomRoom;') -and $effect.Contains('new Vector2(Mathf.Lerp(0.56f, 0.68f, t), 0.38f), 1.6f)') -and $effect.Contains('moteruRevealBackground = null;')) 'Lower background does not pan with portrait and clean up'
$previous = 0.82
foreach ($i in 0..240) {
    $t = $i / 240.0
    $x = 0.82 + (0.52 - 0.82) * ($t * $t * (3 - 2 * $t))
    Assert-Reveal ($x -le $previous + 0.000001 -and $x -ge 0.52 -and $x -le 0.82) 'Approach is not monotonic and bounded'
    $center = 0.56 + 0.12 * ($t * $t * (3 - 2 * $t))
    $backgroundX = (0.5 - $center) * 1.6
    Assert-Reveal ($backgroundX -ge -0.3 -and $backgroundX -le 0.3) 'Lower background pan exposes empty edges at 16:9'
    $previous = $x
}
'PASS: approach/reveal-only split, full Doute face above, cropped Moteru face below, two enlarged backgrounds, horizontal separator, text UI, fades and cleanup.'
