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
$centers = @(0.34,0.26,0.18,0.10)
$heights = @(72,72,60,56)
for ($i = 0; $i -lt $centers.Count - 1; $i++) {
    $gap = ($centers[$i] - $centers[$i+1]) * 1080 - ($heights[$i] + $heights[$i+1])/2
    Assert-Title ($gap -ge 14) 'Title controls are crowded or overlapping'
}
Assert-Title ($title.Contains('Resources.Load<Texture2D>("TitleLogo")') -and $title.Contains('new Vector2(0.5f, 0.65f)') -and $title.Contains('logo.uvRect = new Rect(') -and $title.Contains('logo.raycastTarget = false;')) 'Supplied logo missing, misplaced or blocking controls'
Assert-Title ($title.Contains('new Vector2(0.76f, 0.045f)')) 'Audio control is not inset toward the center'
Assert-Title ($title.Contains('new Vector2(0.5f, 0.035f)') -and $title.Contains('Created by')) 'Credit is not centered below quit'
Assert-Title (-not $title.Contains('titleLetterMaterial') -and $title.Contains('ChoiceButtonHoverScale') -and -not $title.Contains('TitleMenuFocus')) 'Old title material or shared hover feedback issue'
Assert-Title ($title.Contains('new Vector2(430f, 72f)') -and $title.Contains('new Color(0.25f, 0.25f, 0.25f, 0.68f)')) 'Start emphasis or selection-style hover missing'
Assert-Title ((0.65 * 1080 - 520/2) -gt (0.34 * 1080 + 72/2 + 30)) 'Logo and actions are crowded'
'PASS: centered title hierarchy, compact actions, spacing, shade, fade layering and BGM wiring.'
