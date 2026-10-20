$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Search/Chapter1SearchController.cs') -Raw
$start = $source.IndexOf('"manga",')
$end = $source.IndexOf('"facing_wall",', $start)
$definition = $source.Substring($start, $end-$start)
$matches = [regex]::Matches($definition, 'new Vector2\(([0-9.]+)f, ([0-9.]+)f\)')
if ($matches.Count -ne 8) { throw 'Manga polygon not configured with six outline points' }
$points = @($matches | Select-Object -Skip 2 | ForEach-Object { ,@([double]$_.Groups[1].Value, [double]$_.Groups[2].Value) })
function Inside([double]$x, [double]$y) {
    $inside = $false
    for ($i=0; $i -lt $points.Count; $i++) {
        $a=$points[$i]; $b=$points[($i+1)%$points.Count]
        if (($a[1] -gt $y) -ne ($b[1] -gt $y)) {
            $intersection=($b[0]-$a[0])*($y-$a[1])/($b[1]-$a[1])+$a[0]
            if ($x -lt $intersection) { $inside = -not $inside }
        }
    }
    return $inside
}
if (-not (Inside 0.705 0.35) -or -not (Inside 0.71 0.37)) { throw 'Manga cover/page stack is not clickable' }
foreach ($point in @(@(0.65,0.35), @(0.76,0.35), @(0.70,0.40), @(0.70,0.29))) {
    if (Inside $point[0] $point[1]) { throw 'Surrounding bed remains clickable as manga' }
}
if ($definition.Contains('false))')) { throw 'Manga uses rectangular raycast instead of polygon' }
if (-not $source.Contains('item.UsePolygonRaycast') -or -not $source.Contains('polygon.SetPoints(item.HotspotPolygon)')) { throw 'Outline/raycast polygon not connected' }
'PASS: manga uses six-point cover/stack outline, cover/pages clickable, surrounding bed excluded, shared polygon outline and raycast path.'
