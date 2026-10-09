$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
$build = Get-Content (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Raw
$enabledPaths = @([regex]::Matches($build,'enabled: 1\s+path: ([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Trim() })
$assetByGuid = @{}
foreach ($metaPath in (& rg --files (Join-Path $projectRoot 'Assets/Dialogue/Data') -g '*.asset.meta')) {
    $meta = Get-Content -LiteralPath $metaPath -Raw
    $guid = [regex]::Match($meta,'(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    $assetByGuid[$guid] = $metaPath.Substring(0,$metaPath.Length-5)
}
$csvFiles = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo') -Filter *.csv) +
    @(Get-ChildItem (Join-Path $projectRoot 'Assets/Dialogue/CSV') -Filter 'Chapter_Yowashi_*.csv')
$csvByName = @{}
foreach ($file in $csvFiles) { $csvByName[$file.BaseName] = $file.FullName }
$routeLineCount = 0
$sceneTransitionCount = 0
foreach ($route in @('Kayo','Yowashi')) {
    $scenePath = "Assets/Scenes/GameMap/NovelScene_$route.unity"
    $scene = Get-Content (Join-Path $projectRoot $scenePath) -Raw
    Assert-True ($enabledPaths -ccontains $scenePath) "Disabled route scene $route"
    $sequence = [regex]::Match($scene,'(?m)^  scenario: [^\r\n]*\r?\n  followingScenarios:\r?\n(?:  - [^\r\n]*\r?\n)+').Value
    Assert-True (-not [string]::IsNullOrEmpty($sequence)) "No scenario sequence for $route"
    $seenIds = [System.Collections.Generic.HashSet[string]]::new()
    $chapterCount = 0
    foreach ($match in [regex]::Matches($sequence,'guid: ([a-f0-9]+)')) {
        $guid = $match.Groups[1].Value
        Assert-True ($assetByGuid.ContainsKey($guid)) "Missing scenario asset $guid"
        $assetPath = $assetByGuid[$guid]
        $name = [System.IO.Path]::GetFileNameWithoutExtension($assetPath)
        Assert-True ($name.StartsWith("Chapter_$route")) "Shared prologue replay or wrong route chapter: $name"
        Assert-True ($csvByName.ContainsKey($name)) "Missing editable CSV for $name"
        $rows = @(Import-Csv -LiteralPath $csvByName[$name] -Encoding UTF8)
        $asset = Get-Content -LiteralPath $assetPath -Raw -Encoding UTF8
        $assetIds = @([regex]::Matches($asset,'(?m)^  - lineId: (.*)$') | ForEach-Object { $_.Groups[1].Value.Trim() })
        Assert-True (($assetIds -join '|') -ceq ($rows.lineId -join '|')) "CSV/asset order mismatch $name"
        foreach ($row in $rows) {
            Assert-True ($seenIds.Add($row.lineId)) "Duplicate resume line across route: $($row.lineId)"
            $body = [regex]::Match($asset,'(?ms)^  - lineId: '+[regex]::Escape($row.lineId)+'\r?\n(?<body>.*?)(?=^  - lineId: |\z)').Groups['body'].Value
            $textValue = [regex]::Match($body,'(?m)^    text: (.*)$').Groups[1].Value.Trim()
            $text = if ($textValue.StartsWith('"')) { $textValue | ConvertFrom-Json } else { $textValue }
            Assert-True ($text -ceq $row.text) "CSV/asset dialogue mismatch $($row.lineId)"
            foreach ($field in @('speakerId','expressionId','nextLineId','nextScenePath')) {
                $value = [regex]::Match($body,'(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value.Trim()
                Assert-True ($value -ceq [string]$row.$field) "CSV/asset field mismatch $field $($row.lineId)"
            }
            if ($row.nextLineId) {
                Assert-True ($rows.lineId -ccontains $row.nextLineId) "Missing next line $($row.lineId)"
            }
            if ($row.nextScenePath) {
                Assert-True (Test-Path -LiteralPath (Join-Path $projectRoot $row.nextScenePath)) "Stale scene path $($row.nextScenePath)"
                Assert-True ($enabledPaths -ccontains $row.nextScenePath) "Disabled scene transition $($row.nextScenePath)"
                Assert-True (-not $row.nextLineId) 'Scene transition conflicts with next line'
                $sceneTransitionCount++
            }
            foreach ($property in $row.PSObject.Properties | Where-Object Name -match '^choice\d+NextLineId$') {
                if ($property.Value) {
                    Assert-True ($rows.lineId -ccontains $property.Value) "Missing choice destination $($property.Value)"
                    Assert-True (-not $row.nextLineId -and -not $row.nextScenePath) 'Choice conflicts with automatic transition'
                }
            }
            $routeLineCount++
        }
        $chapterCount++
    }
    Write-Output "PASS: $route active sequence has $chapterCount route-only chapters with editable CSVs and unique resume IDs."
}
# Also cover result chapters selected dynamically by the existing Kayo game bridge.
foreach ($file in $csvFiles) {
    foreach ($row in (Import-Csv $file.FullName -Encoding UTF8)) {
        if ($row.nextScenePath) {
            Assert-True (Test-Path -LiteralPath (Join-Path $projectRoot $row.nextScenePath)) "Missing dynamic-result target $($row.lineId)"
            Assert-True ($enabledPaths -ccontains $row.nextScenePath) "Disabled dynamic-result target $($row.lineId)"
        }
    }
}
$kayoEnd = Import-Csv (Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_End.csv') -Encoding UTF8
Assert-True ($kayoEnd[-1].nextScenePath -ceq 'Assets/Scenes/GameMap/Title.unity') 'Kayo end must use the real Title asset path'
Write-Output "PASS: $routeLineCount active dialogue rows match CSV/asset data; $sceneTransitionCount scene transitions reference existing enabled scenes."
Write-Output 'PASS: Dynamic game-result CSV destinations and Kayo end-to-Title path are valid.'
