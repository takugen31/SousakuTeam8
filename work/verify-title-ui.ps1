$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$title = Get-Content (Join-Path $root 'Assets/Scripts/TitleMenu.cs') -Raw
function Assert-Title($condition, $message) { if (-not $condition) { throw $message } }
Assert-Title ($title.Contains('ConfigureRightMenu()')) 'Missing feathered right-hand panel'
Assert-Title ($title.Contains('new Vector2(1920f, 1080f)') -and $title.Contains('scaler.matchWidthOrHeight = 0.5f')) 'Title reference resolution differs from system menu'
Assert-Title ($title.Contains('label.alignment = TextAlignmentOptions.Center') -and $title.Contains('image.sprite = null')) 'Legacy button visuals remain'
Assert-Title ($title.Contains('fadePanel.transform.SetAsLastSibling()')) 'Transition fade no longer covers the new title layout'
Assert-Title ($title.Contains('SetBGM(bgmSource.mute)') -and $title.Contains('StoryMusicController.SetEnabled(enabled)')) 'BGM control is disconnected'
$panelWidth = 1920 * (1 - 0.64)
$buttonCenter = $panelWidth * 0.55
Assert-Title (($buttonCenter - 460/2) -ge 110 -and ($buttonCenter + 460/2) -lt $panelWidth) 'Buttons overlap feathered edge or screen edge'
$centers = @(0.62,0.46,0.30,0.14)
$heights = @(96,96,96,64)
for ($i = 0; $i -lt $centers.Count - 1; $i++) {
    $gap = ($centers[$i] - $centers[$i+1]) * 1080 - ($heights[$i] + $heights[$i+1])/2
    Assert-Title ($gap -ge 40) 'Title controls are crowded or overlapping'
}
$titleRight = 1920 * 0.315 + 1030/2
Assert-Title ($titleRight -lt 1920 * 0.64) 'Title plate overlaps the menu'
'PASS: shared title styling, centered labels, reference layout spacing, fade layering and BGM wiring.'
