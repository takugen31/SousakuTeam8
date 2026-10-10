$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
$zip = [System.IO.Compression.ZipFile]::OpenRead('C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/ヨワシシナリオ本文.docx')
try {
    $reader = [System.IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
    try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $source = @($document.SelectNodes('//w:body//w:p',$ns) | ForEach-Object {
        ($_.SelectNodes('.//w:t',$ns) | ForEach-Object { $_.InnerText }) -join ''
    }) -join "`n"
} finally { $zip.Dispose() }
$chapters = @{}
$all = @()
foreach ($name in @('BeforeCatch','CatchFailure','CatchSuccess','MedicalBook')) {
    $rows = @(Import-Csv (Join-Path $projectRoot "Assets/Dialogue/CSV/Chapter_Yowashi_$name.csv") -Encoding UTF8)
    $chapters[$name] = $rows
    $all += $rows
    $asset = Get-Content (Join-Path $projectRoot "Assets/Dialogue/Data/Chapter_Yowashi_$name.asset") -Raw -Encoding UTF8
    foreach ($row in $rows) {
        Assert-True ($source.Contains($row.text)) "Text not found verbatim in source: $($row.lineId)"
        $match = [regex]::Match($asset,'(?ms)^  - lineId: '+[regex]::Escape($row.lineId)+'\r?\n(?<body>.*?)(?=^  - lineId: |\z)')
        Assert-True $match.Success "Missing imported asset line $($row.lineId)"
        $body = $match.Groups['body'].Value
        $text = [regex]::Match($body,'(?m)^    text: (.*)$').Groups[1].Value.Trim() | ConvertFrom-Json
        Assert-True ($text -ceq $row.text) "CSV/asset text mismatch $($row.lineId)"
        foreach ($field in @('nextLineId','nextScenePath','speakerId')) {
            $value = [regex]::Match($body,'(?m)^    '+$field+':([^\r\n]*)').Groups[1].Value.Trim()
            Assert-True ($value -ceq $row.$field) "CSV/asset $field mismatch $($row.lineId)"
        }
        if ($row.archiveUnlockIds) { Assert-True ($body.Contains('    - '+$row.archiveUnlockIds)) 'Archive import mismatch' }
        if ($row.nextLineId) {
            Assert-True ($rows.lineId -ccontains $row.nextLineId) 'Next line must remain within chapter (CSV importer contract).'
        }
        Assert-True (-not ($row.nextScenePath -and $row.nextLineId)) 'Scene and next line must not coexist.'
    }
}
Assert-True ($all.Count -eq 57) 'Unexpected dialogue count'
Assert-True ($chapters.BeforeCatch[-1].nextScenePath -ceq 'Assets/FallingGame/Scenes/falling_verYowashi.unity') 'Missing game transition'
Assert-True ($chapters.CatchFailure[-1].nextScenePath -ceq 'Assets/Scenes/GameMap/Title.unity') 'Failure must reach Title'
Assert-True (-not $chapters.CatchSuccess[-1].nextScenePath -and -not $chapters.MedicalBook[-1].nextScenePath) 'Success must continue/stop without false ending'
Assert-True (@($chapters.CatchFailure | Where-Object archiveUnlockIds).Count -eq 0) 'Failure must not gain success evidence'
Assert-True (@($chapters.CatchSuccess | Where-Object archiveUnlockIds).Count -eq 2) 'Success evidence missing'
Assert-True ($chapters.MedicalBook[-1].archiveUnlockIds -ceq 'chapter_yowashi.medical_knowledge') 'Medical knowledge missing'
$novel = Get-Content (Join-Path $projectRoot 'Assets/Scenes/GameMap/NovelScene_Yowashi.unity') -Raw
$orderedGuids = @('d6f53c2278014a839a4d38961e7f4372','c3a54410a71e44e7a3593740a1365eed','f85aecf746b74f9a8b291b164e18c5b0','d8c43b29dff94652b0e318e0b7c07cbd','80fefbecf55c419cb3fcbddc688ffb14')
$lastIndex = -1
foreach ($guid in $orderedGuids) {
    $index = $novel.IndexOf('guid: '+$guid)
    Assert-True ($index -gt $lastIndex) 'Wrong chapter order; success could enter failure.'
    $lastIndex = $index
}
Assert-True ($novel.Contains('m_Name: SpeakerPlate') -and $novel.Contains('namePlate: {fileID: 2100000100}')) 'Shared dialogue UI lost'
$game = Get-Content (Join-Path $projectRoot 'Assets/FallingGame/Scenes/falling_verYowashi.unity') -Raw
Assert-True ($game.StartsWith('%YAML 1.1') -and $game -notmatch 'tokens truncated|Warning: truncated') 'Scene read/export was truncated.'
Assert-True ([regex]::Matches($game,'(?m)^  m_Name: Pooled Item \d+\s*$').Count -eq 64) 'All 64 pooled items must be present.'
Assert-True (-not $game.Contains('KayoCatchGameFlowController') -and -not $game.Contains('returnSceneName: NovelScene_Kayo')) 'Kayo result flow remains'
Assert-True ($game.Contains('returnSceneName: NovelScene_Yowashi') -and $game.Contains('successScoreExclusive: 3000')) 'Wrong return/threshold'
Assert-True ($game.Contains('gameDuration: 120')) 'Game must match original two-minute reference'
Assert-True ($game.Contains('startPromptMessage: "じゃあ早速始めよう！！"') -and $game.Contains('resultDialogue: ""')) 'Wrong character dialogue remains'
$fileIds = @([regex]::Matches($game,'(?m)^--- !u!\d+ &(\d+)') | ForEach-Object { $_.Groups[1].Value })
Assert-True (($fileIds | Sort-Object -Unique).Count -eq $fileIds.Count) 'Duplicate game scene file ID'
foreach ($match in [regex]::Matches($game,'\{fileID: (\d+)\}')) {
    if ($match.Groups[1].Value -ne '0') { Assert-True ($fileIds -contains $match.Groups[1].Value) 'Dangling local game reference' }
}
$build = Get-Content (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset') -Raw
Assert-True ($build -match 'enabled: 1\s+path: Assets/FallingGame/Scenes/falling_verYowashi.unity') 'Game not enabled in build'
$archive = Get-Content (Join-Path $projectRoot 'Assets/Resources/Archive/ArchiveDatabase.asset') -Raw
foreach ($row in $all | Where-Object archiveUnlockIds) { Assert-True ($archive.Contains('  - id: '+$row.archiveUnlockIds)) 'Missing archive definition' }

# Execute the actual result flow with a non-rendering lifecycle harness.
$bridge = Get-Content (Join-Path $projectRoot 'Assets/FallingGame/Scripts/YowashiCatchGameFlowController.cs') -Raw
$harness = @'
#pragma warning disable 0649
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine {
    public class SerializeField : Attribute { }
    public class Tooltip : Attribute { public Tooltip(string text) { } }
    public class Min : Attribute { public Min(float value) { } }
    public class WaitForSecondsRealtime { public float duration; public WaitForSecondsRealtime(float seconds) { duration=seconds; } }
    public class MonoBehaviour {
        public bool enabled=true;
        public T GetComponent<T>() { return default(T); }
        public void StartCoroutine(IEnumerator coroutine) { while(coroutine.MoveNext()) { } }
    }
    public static class Application { public static bool valid=true; public static bool CanStreamedLevelBeLoaded(string name) { return valid; } }
    public static class Debug { public static int errors; public static void LogError(string text, object target) { errors++; } }
}
namespace UnityEngine.SceneManagement {
    public enum LoadSceneMode { Single }
    public static class SceneManager { public static int loads; public static string loaded; public static void LoadScene(string name,LoadSceneMode mode) { loads++; loaded=name; } }
}
public static class ArchiveManager { public static void Close() { } }
public static class NovelDialogueController {
    public static string resume; public static bool fade;
    public static void QueueResumeLine(string id,bool showFade) { resume=id; fade=showFade; }
}
namespace Sousakusai8.MiniGame {
    public class CatchMiniGameController {
        public event Action<int> RoundEnded;
        public void End(int score) { RoundEnded?.Invoke(score); }
    }
}
'@
$harness += "`n" + ($bridge -replace '(?m)^using .*;\s*$', '')
$harness += @'
public static class YowashiCatchCheck {
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    public static void Run() {
        var type=typeof(Sousakusai8.MiniGame.YowashiCatchGameFlowController);
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        Check(!Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(2999,3000),"Below threshold succeeded");
        Check(!Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(3000,3000),"Equal threshold succeeded");
        Check(Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(3001,3000),"Above threshold failed");
        Check(Sousakusai8.MiniGame.YowashiCatchGameFlowController.IsSuccess(101,100),"Editable threshold ignored");
        foreach(int score in new[]{3000,3001}) {
            var game=new Sousakusai8.MiniGame.CatchMiniGameController();
            var flow=new Sousakusai8.MiniGame.YowashiCatchGameFlowController();
            type.GetField("miniGame",flags).SetValue(flow,game);
            type.GetMethod("Awake",flags).Invoke(flow,null);
            int before=SceneManager.loads;
            game.End(score);
            Check(SceneManager.loads==before+1 && SceneManager.loaded=="NovelScene_Yowashi" && NovelDialogueController.fade,"Game return failed");
            Check(NovelDialogueController.resume==(score>3000?"chapter_yowashi_catchsuccess_001":"chapter_yowashi_catchfailure_001"),"Wrong result branch");
            game.End(score);
            Check(SceneManager.loads==before+1,"Duplicate return");
            type.GetMethod("OnDestroy",flags).Invoke(flow,null);
        }
        var invalidGame=new Sousakusai8.MiniGame.CatchMiniGameController();
        var invalidFlow=new Sousakusai8.MiniGame.YowashiCatchGameFlowController();
        type.GetField("miniGame",flags).SetValue(invalidFlow,invalidGame);
        type.GetMethod("Awake",flags).Invoke(invalidFlow,null);
        Application.valid=false;
        int previous=SceneManager.loads;
        invalidGame.End(10000);
        Check(SceneManager.loads==previous && Debug.errors==1,"Invalid target was loaded");
        type.GetMethod("OnDestroy",flags).Invoke(invalidFlow,null);
        Application.valid=true;
        invalidGame.End(10000);
        Check(SceneManager.loads==previous,"Destroyed flow remained subscribed");
    }
}
'@
Add-Type -TypeDefinition $harness
[YowashiCatchCheck]::Run()
Write-Output 'PASS: 57 original lines, CSV/asset links, game build entry/hierarchy, success-only evidence, shared UI, result routing, 3000-point boundary, invalid target and event cleanup.'
