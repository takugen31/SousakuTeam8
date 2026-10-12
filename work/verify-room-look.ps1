$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
function Assert-Room($condition, $message) { if (-not $condition) { throw $message } }
Assert-Room ($source.Contains('line.lineId == "prologue2_001"')) 'Waking cue is not restricted to the opening scene'
Assert-Room ($source.Contains('roomLookPresentation.alpha = 0f') -and $source.Contains('roomLookPresentation.blocksRaycasts = false')) 'Presentation is not hidden and input-disabled'
Assert-Room ($source.Contains('roomLookPresentation.alpha = roomLookOriginalAlpha') -and $source.Contains('CancelRoomLook();')) 'Presentation cleanup missing'
Assert-Room ($source.Contains('if (ArchiveManager.IsOpen) { yield return null; continue; }')) 'Esc pause missing'
Assert-Room ($source.Contains('isRoomLookPlaying || !isPlaying') -and $source.Contains('!ArchiveManager.IsOpen && !isRoomLookPlaying')) 'Advancing during the animation is possible'
Assert-Room ($source.Contains('currentLine == line') -and $source.Contains('new GameObject("AwakeningRoomLook", typeof(RectTransform), typeof(RectMask2D))')) 'Resume guard or clipping missing'
function Smooth($t) { $t = [Math]::Clamp($t, 0, 1); return $t*$t*(3-2*$t) }
# Sample the viewport coverage at 0.01 s intervals, including Free Aspect ratios.
foreach ($viewport in @(@(1672,941),@(1920,1080),@(1396,814),@(2560,1080),@(1080,1920))) {
    $w = $viewport[0]; $h = $viewport[1]
    for ($i = 0; $i -le 625; $i++) {
        $t = $i / 100.0
        $p = Smooth (($t - 3.6) / 2.65)
        $cx = 0.64 + (0.56-0.64)*$p
        $cy = 0.67 + (0.60-0.67)*$p
        $zoom = 1.5
        $height = [Math]::Max($h, $w/(1672.0/941))*$zoom
        $iw = $height*1672/941
        $ih = $height
        $mx = ($iw-$w)/2; $my = ($ih-$h)/2
        $x = [Math]::Clamp((0.5-$cx)*$iw, -$mx, $mx)
        $y = [Math]::Clamp((0.5-$cy)*$ih, -$my, $my)
        Assert-Room ($iw/2-[Math]::Abs($x) -ge $w/2-0.001 -and $ih/2-[Math]::Abs($y) -ge $h/2-0.001) 'Exposed edges during camera move'
    }
    Assert-Room ($ih -ge $h*1.5) 'Close-up zoom is lost before fade'
}
Assert-Room (-not $source.Contains('pullBack') -and $source.Contains('yield return ReturnLookToDialogue(line)')) 'Room view still zooms out instead of fading'
$returnBlock = $source.Substring($source.IndexOf('private IEnumerator ReturnLookToDialogue'), $source.IndexOf('private IEnumerator FadeLookReturn') - $source.IndexOf('private IEnumerator ReturnLookToDialogue'))
Assert-Room ($returnBlock.IndexOf('yield return FadeLookReturn(0f, 1f)') -lt $returnBlock.IndexOf('RestoreRoomLookPresentation()') -and $returnBlock.IndexOf('RestoreRoomLookPresentation()') -lt $returnBlock.IndexOf('yield return FadeLookReturn(1f, 0f)')) 'Normal view is restored before reaching black'
'PASS: waking cue, hidden presentation, cleanup, Esc pause, input guards, fade return, and 3130 viewport coverage samples.'
Assert-Room ($source.Contains('line.lineId == "chapter1_001" && line.speakerId == "moteru"')) 'Moteru first-appearance cue missing'
Assert-Room ($source.Contains('portrait.sprite = rightPortraitImage.sprite') -and $source.Contains('Mathf.Lerp(0.27f, 0.82f, progress)')) 'Moteru scan does not use the current portrait from above the cropped edge to face'
Assert-Room ($source.Contains('scanDuration = 9.5f, faceHold = 0.6f') -and $source.Contains('yield return FadeLookReturn(0f, 1f)')) 'Slow tilt, face hold, or return fade missing'
$previousY = 0.27
for ($i = 0; $i -le 950; $i++) {
    $p = Smooth ($i / 950.0)
    $centerY = 0.27 + 0.55 * $p
    Assert-Room ($centerY -ge $previousY) 'Scan reverses before reaching the face'
    $previousY = $centerY
}
# Sprite bottom stays 14.8% of viewport height below the screen initially; head stays visible.
Assert-Room ((0.5-0.27)*2.4-1.2 -lt -0.6) 'Cropped lower edge is visible at start'
Assert-Room ([Math]::Abs((0.5-0.82)*2.4+1.2-0.432) -lt 0.0001) 'Head is off screen at end'
Assert-Room (-not (Test-Path (Join-Path $root 'Assets/moteru-look-reference')) -and -not (Test-Path (Join-Path $root 'Assets/room-look-reference'))) 'Reference captures were imported into Unity'
'PASS: Moteru entrance cue, current sprite, lower-to-face trajectory, face hold and return fade; reference sheets remain outside Assets.'
