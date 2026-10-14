$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content "$root/Assets/Scripts/GameProgress.cs" -Raw
$start = $source.IndexOf('public static class EndingCatalog')
$end = $source.IndexOf('/// <summary>', $start + 10)
Add-Type -TypeDefinition $source.Substring($start, $end - $start)
$endpoints = @(Get-ChildItem "$root/Assets/Dialogue/CSV" -Recurse -Filter *.csv | ForEach-Object {
    Import-Csv $_.FullName | Where-Object { $_.nextScenePath -match '/Title\.unity$' }
})
$numbers = @{}
foreach ($row in $endpoints) {
    [int]$number = 0
    [string]$title = ''
    if (-not [EndingCatalog]::TryGetEnding($row.lineId, [ref]$number, [ref]$title)) { throw "Unmapped end: $($row.lineId)" }
    if ($number -le 0 -or $numbers.ContainsKey($number) -or -not $title.EndsWith('（仮）')) { throw 'Invalid/duplicate number or missing provisional title' }
    $numbers[$number] = $row.lineId
}
if ($endpoints.Count -ne 5) { throw 'Endpoint count changed; update ending catalog' }
foreach ($id in @($null, '', 'chapter2_kayo_001', 'chapter_yowashi_rescue_037', 'chapter_yowashi_rescue_041')) {
    [int]$number=0; [string]$title=''
    if ([EndingCatalog]::TryGetEnding($id, [ref]$number, [ref]$title)) { throw "Premature ending: $id" }
}
$controller = Get-Content "$root/Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs" -Raw
$advanceStart = $controller.IndexOf('public void Advance()')
$advanceEnd = $controller.IndexOf('private bool TryResolveNextLine(', $advanceStart)
$advance = $controller.Substring($advanceStart, $advanceEnd - $advanceStart)
if ($advance.IndexOf('if (isTyping)') -gt $advance.IndexOf('TryShowEndingCard(currentLine)') -or
    $advance.IndexOf('TryShowEndingCard(currentLine)') -gt $advance.IndexOf('LoadDialogueScene(currentLine.nextScenePath, currentLine)')) {
    throw 'Ending must follow final text completion and precede title transition'
}
if (-not $controller.Contains('caption.text = $"END No.{number:00}') -or -not $controller.Contains('label.text = "タイトルへ";')) { throw 'Confirmation UI missing' }
'PASS: all five connected endpoints have distinct fixed numbers and provisional names; intermediate dialogue excluded; final-line/typewriter and title-return ordering verified.'
