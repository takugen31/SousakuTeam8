$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$originalRoot = 'C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001'
function Normalize([string]$text) {
    return [regex]::Replace($text, '[\s「」"]', '').Replace('（','(').Replace('）',')')
}
function Scalar([string]$value) {
    $value = $value.Trim()
    if ($value.StartsWith('"')) { return [string]($value | ConvertFrom-Json) }
    return $value
}
$labels = @{ 'ドウテ'='doute'; 'カヨ'='kayo'; 'ヨワシ'='yowashi'; 'モテル'='moteru'; 'プレイヤー'='player'; '3人'='three_characters'; '三人'='three_characters'; '主人公'='player'; 'ヒロイン'='heroine' }
$sources = @{}
foreach ($file in Get-ChildItem $originalRoot -Filter '*.txt') {
    $group = if ($file.Name.Contains('プロ') -or $file.Name.Contains('プロ')) { 'common' } elseif ($file.Name.Contains('カヨ')) { 'kayo' } elseif ($file.Name.Contains('モテル')) { 'moteru' } else { 'choices' }
    $sources[$group] = @{ path=$file.FullName; lines=@(Get-Content $file.FullName -Encoding UTF8) }
}
$zip = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $originalRoot 'ヨワシシナリオ本文.docx'))
try {
    $reader = [System.IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
    try { [xml]$doc = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [System.Xml.XmlNamespaceManager]::new($doc.NameTable)
    $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $paragraphs = @($doc.SelectNodes('//w:body//w:p',$ns) | ForEach-Object { ($_.SelectNodes('.//w:t',$ns) | ForEach-Object InnerText) -join '' })
    $tracked = $doc.SelectNodes('//w:ins|//w:del',$ns).Count
    $sources['yowashi'] = @{ path=(Join-Path $originalRoot 'ヨワシシナリオ本文.docx'); lines=$paragraphs; tracked=$tracked }
} finally { $zip.Dispose() }
foreach ($key in @($sources.Keys)) {
    $source = $sources[$key]
    $source.compact = Normalize ($source.lines -join "`n")
    $records = [System.Collections.Generic.List[object]]::new()
    $speaker = ''; $labelAt = -1
    for ($i=0; $i -lt $source.lines.Count; $i++) {
        $line = $source.lines[$i].Trim()
        if ($labels.ContainsKey($line)) { $speaker=$labels[$line]; $labelAt=$i; continue }
        if ($line -match '^(?:[？?]+)?[（(](カヨ|ヨワシ|モテル)[）)]$') { $speaker=$labels[$Matches[1]]; $labelAt=$i; continue }
        if ($line -match '^[？?]+$') { $speaker=''; $labelAt=$i; continue }
        if ($line.StartsWith('「') -or $line.StartsWith('（') -or $line.StartsWith('(')) {
            $at=$i
            if ($line.StartsWith('「')) {
                while (($line.ToCharArray() | Where-Object {$_ -eq '「'}).Count -gt ($line.ToCharArray() | Where-Object {$_ -eq '」'}).Count -and $i+1 -lt $source.lines.Count) { $i++; $line += "`n" + $source.lines[$i].Trim() }
            }
            $records.Add([PSCustomObject]@{position=$at+1; speaker=$speaker; labelPosition=$labelAt+1; text=$line; normalized=(Normalize $line)})
        }
    }
    $source.records = $records
}
$assets = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'Assets/Dialogue/Data') -Recurse -Filter '*.asset') {
    $content = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '(?ms)^  - lineId: (?<id>[^\r\n]+)\r?\n(?<body>.*?)(?=^  - lineId:|\z)')) {
        $id=$match.Groups['id'].Value.Trim(); $body=$match.Groups['body'].Value
        $record = @{path=$file.FullName}
        foreach ($field in @('text','speakerId','nextLineId','nextScenePath')) {
            $raw=[regex]::Match($body, '(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value
            if ($raw.Trim().StartsWith('"') -and -not $raw.Trim().EndsWith('"')) {
                $raw=[regex]::Match($body, '(?s)    '+$field+': ("(?:\\.|[^"\\])*")').Groups[1].Value
                $raw=[regex]::Replace($raw, '\r?\n[ \t]+', ' ')
            }
            $record[$field]=Scalar $raw
        }
        $record.choices=@([regex]::Matches($body, '(?m)^    - text: ([^\r\n]*)\r?\n      nextLineId: ([^\r\n]*)') | ForEach-Object { @{text=(Scalar $_.Groups[1].Value); nextLineId=(Scalar $_.Groups[2].Value)} })
        if (-not $assets.ContainsKey($id)) { $assets[$id]=[System.Collections.Generic.List[object]]::new() }
        $assets[$id].Add($record)
    }
}
$counts=@{}; $unmatched=[System.Collections.Generic.List[object]]::new(); $speakerDifferences=[System.Collections.Generic.List[object]]::new(); $assetDifferences=[System.Collections.Generic.List[object]]::new(); $missingAssets=[System.Collections.Generic.List[object]]::new(); $choiceDifferences=[System.Collections.Generic.List[object]]::new()
$allRows=[System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem (Join-Path $root 'Assets/Dialogue/CSV') -Recurse -Filter '*.csv' | Where-Object Name -ne 'Characters.csv') {
    $rows = @(Import-Csv $file.FullName -Encoding UTF8)
    foreach ($row in $rows) {
        $allRows.Add($row)
        $group = if ($row.lineId.StartsWith('chapter_yowashi_')) {'yowashi'} elseif ($row.lineId.StartsWith('chapter_kayo_') -or $row.lineId.StartsWith('search_kayo_') -or $row.lineId.StartsWith('kayo_catch_')) {'kayo'} elseif ($row.lineId.StartsWith('chapter_moteru_')) {'moteru'} else {'common'}
        if (-not $counts.ContainsKey($group)) { $counts[$group]=@{rows=0; text=0; matched=0; empty=0} }
        $counts[$group].rows++
        $norm=Normalize $row.text
        if (-not $norm) { $counts[$group].empty++ } else {
            $counts[$group].text++
            if ($sources[$group].compact.Contains($norm)) {
                $counts[$group].matched++
                $exactRecords=@($sources[$group].records | Where-Object normalized -eq $norm)
                if ($row.speakerId -and $exactRecords.Count -gt 0 -and ($exactRecords | Where-Object { $_.speaker -eq $row.speakerId -or -not $_.speaker }).Count -eq 0) {
                    $speakerDifferences.Add(@{id=$row.lineId; actual=$row.speakerId; original=@($exactRecords | Select-Object position,speaker,labelPosition,text)})
                }
            } else { $unmatched.Add(@{id=$row.lineId; group=$group; text=$row.text; file=$file.FullName}) }
        }
        $csvChoices=@()
        foreach ($property in $row.PSObject.Properties | Where-Object Name -match '^choice[0-9]+Text$' | Sort-Object Name) {
            if (-not $property.Value) { continue }
            $nextProperty=$property.Name.Replace('Text','NextLineId')
            $csvChoices+=@{text=$property.Value; nextLineId=$row.$nextProperty}
            $choiceNorm=Normalize $property.Value
            if (-not $sources[$group].compact.Contains($choiceNorm) -and -not $sources.choices.compact.Contains($choiceNorm) -and -not $sources.common.compact.Contains($choiceNorm)) {
                $choiceDifferences.Add(@{id=$row.lineId; text=$property.Value; target=$row.$nextProperty})
            }
        }
        if (-not $assets.ContainsKey($row.lineId)) { $missingAssets.Add(@{id=$row.lineId; file=$file.FullName}); continue }
        $record=$assets[$row.lineId][0]
        foreach ($field in @('text','speakerId','nextLineId','nextScenePath')) {
            if (([string]$record[$field]).Replace("`r",'') -cne ([string]$row.$field).Replace("`r",'')) { $assetDifferences.Add(@{id=$row.lineId;field=$field;csv=$row.$field;asset=$record[$field];file=$record.path}) }
        }
        if ($csvChoices.Count -ne $record.choices.Count) { $assetDifferences.Add(@{id=$row.lineId;field='choicesCount';csv=$csvChoices.Count;asset=$record.choices.Count;file=$record.path}) }
        for($i=0;$i -lt [Math]::Min($csvChoices.Count,$record.choices.Count);$i++) {
            foreach($field in @('text','nextLineId')) { if($csvChoices[$i][$field] -cne $record.choices[$i][$field]) { $assetDifferences.Add(@{id=$row.lineId;field="choice$($i+1).$field";csv=$csvChoices[$i][$field];asset=$record.choices[$i][$field];file=$record.path}) } }
        }
    }
}
$allIds=[System.Collections.Generic.HashSet[string]]::new([string[]]@($allRows.lineId),[StringComparer]::Ordinal)
$brokenLinks=@($allRows | Where-Object { $_.nextLineId -and -not $allIds.Contains($_.nextLineId) } | Select-Object lineId,nextLineId)
$unrepresented=[System.Collections.Generic.List[object]]::new()
$gameText=Normalize (($allRows | ForEach-Object { $_.text; foreach($property in $_.PSObject.Properties | Where-Object Name -match '^choice[0-9]+Text$') { $property.Value } }) -join '')
foreach($group in @('common','kayo','yowashi','moteru')) {
    foreach($record in $sources[$group].records) {
        if($record.text.StartsWith('「') -and $record.normalized.Length -gt 3 -and -not $gameText.Contains($record.normalized)) { $unrepresented.Add(@{group=$group; position=$record.position; speaker=$record.speaker; text=$record.text}) }
    }
}
@{ counts=$counts; sourceFiles=@($sources.Values | ForEach-Object { @{path=$_.path; lines=$_.lines.Count; tracked=$_.tracked} }); unmatched=$unmatched; speakerDifferences=$speakerDifferences; assetDifferences=$assetDifferences; missingAssets=$missingAssets; choiceDifferences=$choiceDifferences; brokenLinks=$brokenLinks; unrepresentedOriginal=$unrepresented } | ConvertTo-Json -Depth 12 -Compress
