$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveMenuUI.cs') -Raw
function Assert-Notice($condition,$message) { if (-not $condition) { throw $message } }
Assert-Notice ($source.Contains('SizeNotificationToText();') -and $source.Contains('notificationText.GetPreferredValues(notificationText.text')) 'Notification does not follow text metrics'
Assert-Notice ($source.Contains('new Vector2(1f, 0.91f)') -and $source.Contains('rect.pivot = new Vector2(1f, 0.5f)')) 'Right-edge anchoring lost'
Assert-Notice ($source.Contains('ConfigureLeftEdgeOnly(24f)') -and $source.Contains('new Vector2(36f, 10f), new Vector2(-20f, -10f)')) 'Compact padding/left feather missing'
Assert-Notice ($source.Contains('float hiddenX = rect.rect.width + 20f') -and $source.Contains('SlideNotification(rect, hiddenX, 0f)')) 'Slide no longer uses actual notification width'
Assert-Notice ($source.Contains('width - leftPadding - rightPadding') -and $source.Contains('TextWrappingModes.Normal')) 'Long-title fitting missing'
foreach($textWidth in @(60,120,350,2000)) {
    $width=[Math]::Clamp([double][Math]::Ceiling([double]($textWidth+56)),[double]96,[double]1440)
    Assert-Notice ($width-56 -ge [Math]::Min($textWidth,1384)) 'Horizontal padding clips text'
    if ($textWidth -lt 350) { Assert-Notice ($width -lt 406) 'Short title leaves excessive empty space' }
}
'PASS: content-based notification width/height, compact padding, wrapping, right anchor and preserved slide.'
