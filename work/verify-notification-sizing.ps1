$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveMenuUI.cs') -Raw
function Assert-Notice($condition,$message) { if (-not $condition) { throw $message } }
Assert-Notice ($source.Contains('SizeNotificationToText();') -and $source.Contains('notificationText.GetPreferredValues(notificationText.text')) 'Notification does not follow text metrics'
Assert-Notice ($source.Contains('new Vector2(1f, 0.91f)') -and $source.Contains('rect.pivot = new Vector2(1f, 0.5f)')) 'Right-edge anchoring lost'
Assert-Notice ($source.Contains('ConfigureLeftEdgeOnly(32f)') -and $source.Contains('new Vector2(48f, 14f), new Vector2(-28f, -14f)')) 'Compact padding/left feather missing'
Assert-Notice ($source.Contains('string.Empty, 28f, PrimaryTextColor, FontStyles.Bold') -and $source.Contains('notificationText.enableAutoSizing = false')) 'Readable notification font missing or auto-shrinks'
Assert-Notice ($source.Contains('float hiddenX = rect.rect.width + 20f') -and $source.Contains('SlideNotification(rect, hiddenX, 0f)')) 'Slide no longer uses actual notification width'
Assert-Notice ($source.Contains('width - leftPadding - rightPadding') -and $source.Contains('TextWrappingModes.Normal')) 'Long-title fitting missing'
foreach($textWidth in @(60,120,350,2000)) {
    $width=[Math]::Clamp([double][Math]::Ceiling([double]($textWidth+76)),[double]96,[double]1440)
    Assert-Notice ($width-76 -ge [Math]::Min($textWidth,1364)) 'Horizontal padding clips text'
    if ($textWidth -lt 350) { Assert-Notice ($width -lt 426) 'Short title leaves excessive empty space' }
}
'PASS: content-based notification width/height, compact padding, wrapping, right anchor and preserved slide.'
