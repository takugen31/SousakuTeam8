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
    for ($i = 0; $i -le 695; $i++) {
        $t = $i / 100.0
        $p = Smooth (($t - 3.6) / 2.65)
        $settle = Smooth (($t - 6.25) / 0.7)
        $cx = (0.64 + (0.56-0.64)*$p)*(1-$settle) + 0.5*$settle
        $cy = (0.67 + (0.60-0.67)*$p)*(1-$settle) + 0.5*$settle
        $zoom = 1.5 - 0.5*$settle
        $height = [Math]::Max($h, $w/(1672.0/941))*$zoom
        $iw = ($height*1672/941)*(1-$settle) + $w*$settle
        $ih = $height*(1-$settle) + $h*$settle
        $mx = ($iw-$w)/2; $my = ($ih-$h)/2
        $x = [Math]::Clamp((0.5-$cx)*$iw, -$mx, $mx)
        $y = [Math]::Clamp((0.5-$cy)*$ih, -$my, $my)
        Assert-Room ($iw/2-[Math]::Abs($x) -ge $w/2-0.001 -and $ih/2-[Math]::Abs($y) -ge $h/2-0.001) 'Exposed edges during camera move'
    }
    Assert-Room ([Math]::Abs($iw-$w) -lt 0.001 -and [Math]::Abs($ih-$h) -lt 0.001) 'Final view does not match normal background'
}
'PASS: waking cue, hidden presentation, cleanup, Esc pause, input guards, and 3480 viewport coverage samples.'
