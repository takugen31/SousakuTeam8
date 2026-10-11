$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition,[string]$message) { if (-not $condition) { throw $message } }
$zip = [System.IO.Compression.ZipFile]::OpenRead('C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/ヨワシシナリオ本文.docx')
try {
    $reader=[System.IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
    try { [xml]$doc=$reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns=[System.Xml.XmlNamespaceManager]::new($doc.NameTable)
    $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $paragraphs=@($doc.SelectNodes('//w:body//w:p',$ns) | ForEach-Object { ($_.SelectNodes('.//w:t',$ns) | ForEach-Object InnerText) -join '' })
} finally { $zip.Dispose() }
$expected=@(
    @('Consultation','001','player',322,323),
    @('Consultation','003','player',326,327),
    @('Consultation','007','player',345,346),
    @('Consultation','009','player',349,350),
    @('Tennis','014','three_characters',280,281),
    @('Tennis','015','three_characters',280,282),
    @('Tennis','016','three_characters',280,283)
)
foreach($e in $expected) {
    $id='chapter_yowashi_'+$e[0].ToLower()+'_'+$e[1]
    $row=Import-Csv (Join-Path $root "Assets/Dialogue/CSV/Chapter_Yowashi_$($e[0]).csv") | Where-Object lineId -CEQ $id
    $sourceLabel=if($e[2] -eq 'player') {'プレイヤー'} else {'3人'}
    Assert-True ($paragraphs[$e[3]].Trim() -ceq $sourceLabel) "Source speaker mismatch $id"
    $sourceText=$paragraphs[$e[4]].Trim()
    $sourceText=$sourceText.Substring(1,$sourceText.Length-2)
    Assert-True ($row.speakerId -ceq $e[2] -and $row.expressionId -ceq 'normal' -and $row.text -ceq $sourceText) "Source/CSV mismatch $id"
}
$used=[System.Collections.Generic.HashSet[string]]::new()
foreach($name in @('NovelScene','NovelScene_Kayo','NovelScene_Yowashi')) {
    $scene=Get-Content (Join-Path $root "Assets/Scenes/GameMap/$name.unity") -Raw
    Assert-True ($scene.Contains('omitOuterDialogueQuotes: 1')) 'New quote presentation not enabled'
    $sequence=[regex]::Match($scene,'(?m)^  scenario: [^\r\n]*\r?\n  followingScenarios:\r?\n(?:  - [^\r\n]*\r?\n)+').Value
    foreach($m in [regex]::Matches($sequence,'guid: ([a-f0-9]+)')) { [void]$used.Add($m.Groups[1].Value) }
}
foreach($name in @('Chapter1_SearchScene','Chapter_Kayo_SearchScene')) {
    $scene=Get-Content (Join-Path $root "Assets/Scenes/GameMap/$name.unity") -Raw
    [void]$used.Add([regex]::Match($scene,'itemDialogueScenario: .*guid: ([a-f0-9]+)').Groups[1].Value)
}
$csvLookup=@{}
foreach($path in (& rg --files (Join-Path $root 'Assets/Dialogue/CSV') -g '*.csv')) {
    if($path -match '(?i)moteru') { continue }
    $rows=@(Import-Csv $path)
    if($rows.Count -and $rows[0].lineId) { $csvLookup[$rows[0].lineId]=@{Path=$path;Rows=$rows} }
}
$db=Get-Content (Join-Path $root 'Assets/Dialogue/Data/CharacterDatabaseatPrologue.asset') -Raw
$checked=0
foreach($meta in (& rg --files (Join-Path $root 'Assets/Dialogue/Data') -g '*.asset.meta')) {
    if($meta -match '(?i)moteru') { continue }
    $guid=[regex]::Match((Get-Content $meta -Raw),'(?m)^guid: (\w+)').Groups[1].Value
    if(-not $used.Contains($guid)) {continue}
    $asset=Get-Content $meta.Substring(0,$meta.Length-5) -Raw
    $firstId=[regex]::Match($asset,'(?m)^  - lineId: ([^\r\n]+)').Groups[1].Value
    $csv=$csvLookup[$firstId]
    Assert-True ($null -ne $csv) 'Missing editable source CSV'
    foreach($row in $csv.Rows) {
        $body=[regex]::Match($asset,'(?ms)^  - lineId: '+[regex]::Escape($row.lineId)+'\r?\n(?<body>.*?)(?=^  - lineId: |\z)').Groups['body'].Value
        foreach($field in @('speakerId','expressionId','nextLineId')) {
            $value=[regex]::Match($body,'(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value.Trim()
            Assert-True ($value -ceq [string]$row.$field) "CSV/asset mismatch: $($row.lineId) $field"
        }
        $textValue=[regex]::Match($body,'(?ms)^    text: (?<text>.*?)(?=^    [A-Za-z]\w*:|\z)').Groups['text'].Value.Trim()
        $textValue=$textValue -replace '\\\r?\n\s*','' -replace '\r?\n +',' '
        try { $text=$textValue | ConvertFrom-Json } catch { throw "Cannot parse wrapped YAML text at $($row.lineId): $textValue" }
        Assert-True ($text -ceq $row.text) "Source text changed: $($row.lineId)"
        if($row.speakerId) { Assert-True ($db.Contains('characterId: '+$row.speakerId)) "Unknown speaker $($row.lineId)" }
        $checked++
    }
}
$moteru=Get-Content (Join-Path $root 'Assets/Scenes/GameMap/NovelScene_Moteru.unity') -Raw
Assert-True (-not $moteru.Contains('omitOuterDialogueQuotes: 1')) 'Moteru must retain its existing presentation'
$controller=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$start=$controller.IndexOf('    public static string GetDialogueDisplayText(')
$end=$controller.IndexOf('    private void StartTyping(', $start)
$method=$controller.Substring($start,$end-$start)
$harness=@'
using System;
public class DialogueLine { public string text; public string speakerId; }
public static class QuotePresentation {
'@ + $method + @'
public static void Run() {
    var line=new DialogueLine { speakerId="doute", text="「外側だけ。内側の「引用」は残す。」" };
    string original=line.text;
    if(GetDialogueDisplayText(line,true)!="外側だけ。内側の「引用」は残す。")throw new Exception("Inner quotes changed");
    if(line.text!=original)throw new Exception("Source line modified");
    if(GetDialogueDisplayText(line,false)!=original)throw new Exception("Opt-out ignored");
    line.speakerId="";
    if(GetDialogueDisplayText(line,true)!=original)throw new Exception("Narrator quotes changed");
    line.speakerId="player";line.text="引用のない原文";
    if(GetDialogueDisplayText(line,true)!=line.text)throw new Exception("Plain text changed");
    line.text="『日記』";
    if(GetDialogueDisplayText(line,true)!=line.text)throw new Exception("Diary brackets changed");
}
}
'@
Add-Type -TypeDefinition $harness
[QuotePresentation]::Run()
Write-Output "PASS: $checked active common/Kayo/Yowashi/search dialogue rows have consistent CSV/asset speakers and unchanged text."
Write-Output 'PASS: Seven missing speakers match original Yowashi scenario; named dialogue omits only outer quotes at display time.'
Write-Output 'PASS: Narration, diary brackets, inner quotations and Moteru presentation are preserved.'
