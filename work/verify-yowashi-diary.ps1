$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
$sourcePath = 'C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/ヨワシシナリオ本文.docx'
$zip = [System.IO.Compression.ZipFile]::OpenRead($sourcePath)
try {
    $reader = [System.IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
    try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $paragraphs = @($document.SelectNodes('//w:body//w:p',$ns) | ForEach-Object {
        ($_.SelectNodes('.//w:t',$ns) | ForEach-Object { $_.InnerText }) -join ''
    })
} finally { $zip.Dispose() }
$source = $paragraphs -join "`n"
$all = @()
foreach ($name in @('Chapter_Yowashi_1','Chapter_Yowashi_Diary')) {
    $rows = @(Import-Csv (Join-Path $projectRoot "Assets/Dialogue/CSV/$name.csv") -Encoding UTF8)
    $all += $rows
    $asset = Get-Content (Join-Path $projectRoot "Assets/Dialogue/Data/$name.asset") -Raw -Encoding UTF8
    foreach ($row in $rows) {
        Assert-True ($source.Contains($row.text)) "Text not found verbatim in scenario: $($row.lineId)"
        $match = [regex]::Match($asset,'(?ms)^  - lineId: '+[regex]::Escape($row.lineId)+'\r?\n(?<body>.*?)(?=^  - lineId: |\z)')
        Assert-True $match.Success "Missing asset line $($row.lineId)"
        $body = $match.Groups['body'].Value
        $text = [regex]::Match($body,'(?m)^    text: (.*)$').Groups[1].Value.Trim() | ConvertFrom-Json
        Assert-True ($text -ceq $row.text) "CSV/asset text mismatch $($row.lineId)"
        foreach ($field in @('nextLineId','nextScenePath','speakerId')) {
            $value = [regex]::Match($body,'(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value.Trim()
            Assert-True ($value -ceq $row.$field) "CSV/asset $field mismatch $($row.lineId)"
        }
        if ($row.archiveUnlockIds) { Assert-True ($body.Contains('    - '+$row.archiveUnlockIds)) 'Archive import mismatch' }
    }
}
Assert-True ($all.Count -eq 51) 'Unexpected line count'
$ids = @($all | ForEach-Object lineId)
Assert-True (($ids | Sort-Object -Unique).Count -eq $ids.Count) 'Duplicate line ID'
foreach ($row in $all) { if ($row.nextLineId) { Assert-True ($ids -ccontains $row.nextLineId) "Missing next line $($row.nextLineId)" } }
$transition = @($all | Where-Object nextScenePath)
Assert-True ($transition.Count -eq 1 -and $transition[0].lineId -ceq 'chapter_yowashi_1_035' -and -not $transition[0].nextLineId) 'Wrong puzzle entry'
Assert-True ($transition[0].nextScenePath -ceq 'Assets/PuzzleGame/Scenes/PuzzleGame.unity') 'Wrong puzzle scene'
$scene = Get-Content (Join-Path $projectRoot 'Assets/Scenes/GameMap/NovelScene_Yowashi.unity') -Raw
Assert-True ($scene.Contains('scenario: {fileID: 11400000, guid: 826dba538b03427a814f588cb5de2b8b')) 'Wrong starting chapter'
Assert-True ($scene.Contains('guid: d6f53c2278014a839a4d38961e7f4372')) 'Diary missing from sequence'
Assert-True (-not $scene.Contains('guid: cf8d3c86502709540a8bcadb9944ddf1')) 'Prologue replay remains'
$archive = Get-Content (Join-Path $projectRoot 'Assets/Resources/Archive/ArchiveDatabase.asset') -Raw
foreach ($row in $all | Where-Object archiveUnlockIds) { Assert-True ($archive.Contains('  - id: '+$row.archiveUnlockIds)) 'Missing archive item' }
$controller = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
Assert-True ($controller.Contains('SceneTransitionStarting?.Invoke(transitionLine);')) 'Transition event missing'
$bridgeCode = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Search/YowashiPuzzleEntryController.cs') -Raw
$harness = @'
#pragma warning disable 0649
using System;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
    public class SerializeField : Attribute { }
    public class Tooltip : Attribute { public Tooltip(string text) { } }
    public class Texture2D { }
    public class Scene { public string name = "NovelScene_Yowashi"; }
    public class GameObject { public Scene scene = new Scene(); }
    public class MonoBehaviour { public bool enabled = true; public GameObject gameObject = new GameObject(); public T GetComponent<T>() { return default(T); } }
    public static class Application { public static bool valid = true; public static bool CanStreamedLevelBeLoaded(string name) { return valid; } }
    public static class Debug { public static void LogError(string text, object context) { throw new Exception(text); } }
}
public class DialogueLine { public string lineId; public string nextLineId; public string nextScenePath; }
public class NovelDialogueController {
    public event Action<DialogueLine> SceneTransitionStarting;
    public void Transition(DialogueLine line) { SceneTransitionStarting?.Invoke(line); }
}
public static class KayoPuzzleGameFlowController {
    public static int calls; public static string target, returnScene, resume; public static Texture2D image;
    public static void Queue(string a,string b,string c,Texture2D d) { calls++; target=a; returnScene=b; resume=c; image=d; }
}
'@
$harness += "`n" + ($bridgeCode -replace '(?m)^using .*;\s*$', '')
$harness += @'
public static class YowashiPuzzleCheck {
    static void Check(bool ok,string message) { if (!ok) throw new Exception(message); }
    public static void Run() {
        var controller = new NovelDialogueController();
        var bridge = new YowashiPuzzleEntryController();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(YowashiPuzzleEntryController);
        type.GetField("dialogueController",flags).SetValue(bridge,controller);
        var image = new Texture2D();
        type.GetField("diaryPuzzleImage",flags).SetValue(bridge,image);
        type.GetMethod("Awake",flags).Invoke(bridge,null);
        controller.Transition(null);
        controller.Transition(new DialogueLine { nextLineId="unrelated", nextScenePath="Title" });
        Check(KayoPuzzleGameFlowController.calls==0,"Unrelated transition queued puzzle");
        var line = new DialogueLine { lineId="chapter_yowashi_1_035",nextScenePath="Assets/PuzzleGame/Scenes/PuzzleGame.unity" };
        controller.Transition(line);
        Check(KayoPuzzleGameFlowController.calls==1 && KayoPuzzleGameFlowController.returnScene=="NovelScene_Yowashi" && KayoPuzzleGameFlowController.target=="PuzzleGame" && KayoPuzzleGameFlowController.resume=="chapter_yowashi_diary_001" && KayoPuzzleGameFlowController.image==image,"Incorrect request");
        controller.Transition(line);
        Check(KayoPuzzleGameFlowController.calls==2,"Repeated/skip transition failed");
        Application.valid=false;
        bool rejected=false;
        try { controller.Transition(line); } catch (InvalidOperationException) { rejected=true; }
        Check(rejected && KayoPuzzleGameFlowController.calls==2,"Invalid return accepted");
        Application.valid=true;
        type.GetMethod("OnDestroy",flags).Invoke(bridge,null);
        controller.Transition(line);
        Check(KayoPuzzleGameFlowController.calls==2,"Event subscription leaked");
    }
}
'@
Add-Type -TypeDefinition $harness
[YowashiPuzzleCheck]::Run()
Write-Output 'PASS: all 51 lines match original source; CSV/asset alignment, scene/resume links, archive IDs, queued puzzle return, unrelated/invalid transitions and cleanup.'
