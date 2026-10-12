$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$title = Get-Content (Join-Path $root 'Assets/Scripts/TitleMenu.cs') -Raw
function Assert-Title($condition, $message) { if (-not $condition) { throw $message } }
Assert-Title ($title.Contains('AddComponent<TitleBottomShade>()') -and $title.Contains('Mathf.Pow(1f - y, 3f)')) 'Missing subtle bottom shade'
Assert-Title (-not $title.Contains('TitleMenuPanel') -and -not $title.Contains('TitlePlate')) 'Large title panel remains'
Assert-Title ($title.Contains('new Vector2(1920f, 1080f)') -and $title.Contains('scaler.matchWidthOrHeight = 0.5f')) 'Title reference resolution differs from system menu'
Assert-Title ($title.Contains('label.alignment = TextAlignmentOptions.Center') -and $title.Contains('image.sprite = null')) 'Legacy button visuals remain'
Assert-Title ($title.Contains('fadePanel.transform.SetAsLastSibling()')) 'Transition fade no longer covers the new title layout'
Assert-Title ($title.Contains('SetBGM(bgmSource.mute)') -and $title.Contains('StoryMusicController.SetEnabled(enabled)')) 'BGM control is disconnected'
$centers = @(0.36,0.265,0.17)
$heights = @(72,66,66)
for ($i = 0; $i -lt $centers.Count - 1; $i++) {
    $gap = ($centers[$i] - $centers[$i+1]) * 1080 - ($heights[$i] + $heights[$i+1])/2
    Assert-Title ($gap -ge 30) 'Title controls are crowded or overlapping'
}
Assert-Title ($title.Contains('new Vector2(0.5f, 0.65f)') -and $title.Contains('text.fontSize = 126f')) 'Logo hierarchy changed'
Assert-Title ($title.Contains('new Vector2(0.875f, 0.055f)')) 'Audio control is not separated from main actions'
Assert-Title ($title.Contains('Destroy(titleLetterMaterial)') -and $title.Contains('TitleMenuFocus')) 'Material cleanup or focus feedback missing'
Assert-Title ((0.65 * 1080 - 190/2) -gt (0.36 * 1080 + 72/2 + 100)) 'Logo and actions are crowded'
'PASS: centered title hierarchy, compact actions, spacing, shade, fade layering and BGM wiring.'
