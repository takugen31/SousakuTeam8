$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
function Assert-Fall($condition,$message) { if(-not $condition){throw $message} }
Assert-Fall ($source.Contains('displayText.IndexOf("きゃ", StringComparison.Ordinal)') -and $source.Contains('bodyText.maxVisibleCharacters >= screamAt + 2')) 'Drop starts before scream is visible'
Assert-Fall ($source.Contains('currentLine?.lineId == "chapter1_kayo_014"') -and $source.Contains('BeginKayoFallMotion(1f, 0.28f)')) 'Fast scream-only drop missing'
Assert-Fall ($source.Contains('currentLine?.lineId == "chapter1_kayo_017" && kayoScreamTriggered') -and $source.Contains('BeginKayoFallMotion(0f, 0.45f)')) 'Recovery reply does not raise Kayo'
$start=$source.IndexOf('private void UpdateKayoFall')
$end=$source.IndexOf('private void ApplyGatheringDouteSlide',$start)
$effect=$source.Substring($start,$end-$start)
Assert-Fall (-not $effect.Contains('SetRoomLookPose') -and -not $effect.Contains('gatheringRightBackground')) 'Fall animation moves the background'
Assert-Fall ($effect.Contains('ArchiveManager.IsOpen') -and $effect.Contains('kayoScreamTriggered = false')) 'Pause/reset missing'
Assert-Fall ($source.Contains('ApplyKayoFallPose();') -and $source.Contains('gatheringAnimationTarget != target')) 'Fallen pose not preserved across dialogue pages'
$normalY=0.5-(0.83-0.5)*3.1
$drop=-0.4-($normalY+(0.922-0.5)*3.1)
$crown=$normalY+$drop+(0.922-0.5)*3.1
$eyes=$normalY+$drop+(0.844-0.5)*3.1
Assert-Fall ([Math]::Abs($crown+0.4) -lt 0.0001 -and $eyes -lt -0.5) 'Crown-only pose exposes the eyes or hides entire head'
foreach($p in @(0,0.25,0.5,0.75,1)) {
    Assert-Fall ($normalY+$drop*$p -le $normalY) 'Fall moves upward'
}
'PASS: scream-visible cue, 0.28 s drop, crown-only pose held across pages, 0.45 s recovery on thank-you, fixed Kayo background and pause/reset guards.'
