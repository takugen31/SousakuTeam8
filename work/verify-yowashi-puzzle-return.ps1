$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check($condition, $message) { if (-not $condition) { throw $message } }
$flow = Get-Content (Join-Path $root 'Assets/Scripts/Search/PuzzleGameFlowController.cs') -Raw -Encoding UTF8
$connectStart = $flow.IndexOf('private void ConnectCompletionEvent()')
$connectEnd = $flow.IndexOf('private void Start()', $connectStart)
$completeStart = $flow.IndexOf('private void OnPuzzleCompleted(')
$completeEnd = $flow.IndexOf('private IEnumerator ReturnToDiary()', $completeStart)
$methods = $flow.Substring($connectStart, $connectEnd-$connectStart) + $flow.Substring($completeStart, $completeEnd-$completeStart)
Add-Type -TypeDefinition @"
using System;
using System.Collections;
namespace PuzzleReturnChecks {
public class Request { public string ResumeLineId, ReturnSceneName; }
public class Puzzle {
    public bool IsComplete; public int MoveCount = 12; public bool interactable = true;
    public event Action<int> Completed;
    public void SetInteractable(bool value) { interactable = value; }
    public void Finish() { IsComplete = true; if (Completed != null) Completed(MoveCount); }
}
public static class Application { public static bool CanStreamedLevelBeLoaded(string scene) { return scene == "NovelScene_Yowashi" || scene == "NovelScene_Kayo"; } }
public static class Debug { public static void LogError(string text, object owner) { throw new Exception(text); } }
public class Bridge {
    public Puzzle puzzle = new Puzzle(); public Request activeRequest;
    public bool isReturning; public int returns;
    public void Connect() { ConnectCompletionEvent(); }
    public void Disconnect() { puzzle.Completed -= OnPuzzleCompleted; }
    private void StartCoroutine(IEnumerator coroutine) { while(coroutine.MoveNext()) {} }
    private IEnumerator ReturnToDiary() { returns++; yield break; }
$methods
} }
"@
foreach ($alreadyComplete in @($true, $false)) {
    $bridge = [PuzzleReturnChecks.Bridge]::new()
    $bridge.activeRequest = [PuzzleReturnChecks.Request]::new()
    $bridge.activeRequest.ReturnSceneName = 'NovelScene_Yowashi'
    $bridge.activeRequest.ResumeLineId = 'chapter_yowashi_diary_001'
    $bridge.puzzle.IsComplete = $alreadyComplete
    $bridge.Connect(); $bridge.Connect()
    if (-not $alreadyComplete) { $bridge.Disconnect(); $bridge.puzzle.Finish(); $bridge.Connect() }
    Check ($bridge.returns -eq 1 -and $bridge.isReturning -and -not $bridge.puzzle.interactable) 'Completion missed or duplicated after reconnect'
}
$standalone = [PuzzleReturnChecks.Bridge]::new(); $standalone.puzzle.IsComplete = $true; $standalone.Connect()
Check ($standalone.returns -eq 0) 'Standalone puzzle unexpectedly leaves scene'
$scene = Get-Content (Join-Path $root 'Assets/Scenes/GameMap/NovelScene_Yowashi.unity') -Raw
Check ($scene.Contains('puzzleEntryLineId: chapter_yowashi_1_035') -and $scene.Contains('diaryResumeLineId: chapter_yowashi_diary_001')) 'Yowashi route endpoints invalid'
Check ($flow.Contains('[SerializeField] private Request activeRequest') -and $flow.Contains('PlayerPrefs.SetString(PendingReturnKey') -and $flow.Contains('JsonUtility.FromJson<Request>')) 'Return context cannot recover after reload'
Check ($flow.IndexOf('NovelDialogueController.QueueResumeLine(activeRequest.ResumeLineId, true)') -lt $flow.IndexOf('SceneManager.LoadScene(activeRequest.ReturnSceneName')) 'Diary resume queued after scene loading'
'PASS: actual completion/reconnection logic handles normal completion, completion while disconnected, duplicate connections and standalone puzzle; Yowashi return/resume endpoints and reload-safe context verified.'
