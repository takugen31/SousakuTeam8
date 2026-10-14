$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$planPath = Join-Path $root 'Assets/Resources/StoryMusic/MusicPlan.json'
$json = Get-Content -LiteralPath $planPath -Raw
$plan = $json | ConvertFrom-Json
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/StoryMusicController.cs') -Raw

# Compile the actual cue-resolution code with plain data stubs, independent of Unity.
$models = $source.Substring($source.IndexOf('    [Serializable] public sealed class Cue'))
$models = $models.Substring(0, $models.IndexOf('    private static StoryMusicController instance;'))
$resolver = $source.Substring($source.IndexOf('    public static string ResolveTrack('))
$resolver = $resolver.Substring(0, $resolver.IndexOf('    private void OnSceneLoaded('))
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
namespace StoryMusicCueVerification {
public class DialogueLine { public string lineId; }
public class DialogueScenarioSO { public string name; public List<DialogueLine> Lines = new List<DialogueLine>(); }
public class Resolver {
$models
$resolver
}
}
"@

$typedPlan = [StoryMusicCueVerification.Resolver+MusicPlan]::new()
$typedPlan.scenarios = @($plan.scenarios | ForEach-Object {
    $segment = [StoryMusicCueVerification.Resolver+ScenarioCues]::new()
    $segment.scenario = $_.scenario
    $segment.cues = @($_.cues | ForEach-Object {
        $cue = [StoryMusicCueVerification.Resolver+Cue]::new()
        $cue.startLineId = $_.startLineId
        $cue.track = $_.track
        $cue
    })
    $segment
})
$scenarios = @{}
$cueCount = 0
foreach ($segment in $plan.scenarios) {
    $asset = Get-ChildItem (Join-Path $root 'Assets/Dialogue/Data') -Recurse -Filter ($segment.scenario + '.asset')
    $scenario = [StoryMusicCueVerification.DialogueScenarioSO]::new()
    $scenario.name = $segment.scenario
    if ($asset) {
        $ids = @(Select-String -LiteralPath $asset.FullName -Pattern '^  - lineId: (.+)$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    } else {
        $csv = Get-ChildItem (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Kayo') -Filter ($segment.scenario + '.csv')
        if (-not $csv) { throw "Missing scenario: $($segment.scenario)" }
        $ids = @((Import-Csv -LiteralPath $csv.FullName).lineId)
    }
    foreach ($id in $ids) {
        $row = [StoryMusicCueVerification.DialogueLine]::new()
        $row.lineId = $id
        $scenario.Lines.Add($row)
    }
    $scenarios[$scenario.name] = $scenario
    foreach ($cue in $segment.cues) {
        if ($cue.startLineId -and $ids -notcontains $cue.startLineId) { throw "Missing cue: $($cue.startLineId)" }
        if ($cue.track -ne 'stop' -and -not (Test-Path (Join-Path $root ('Assets/Resources/StoryMusic/Tracks/' + $cue.track + '.mp3')))) { throw "Missing track: $($cue.track)" }
        $id = if ($cue.startLineId) { $cue.startLineId } else { $ids[0] }
        $resolved = [StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $scenario, $id)
        if ($resolved -ne $cue.track) { throw "Incorrect cue $id : $resolved" }
        $cueCount++
    }
    foreach ($id in $ids) {
        if (-not [StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $scenario, $id)) { throw "Uncovered row: $id" }
    }
}
$checks = @(
    @('Chapter_Kayo_Diary', 'chapter_kayo_diary_003', 'Nostalgic'),
    @('Chapter_Kayo_Diary', 'chapter_kayo_diary_009', 'Melancholy'),
    @('Chapter_Kayo_Diary', 'chapter_kayo_diary_015', 'Horror'),
    @('Chapter_Kayo_3', 'chapter_kayo_3_032', 'Horror'),
    @('Chapter_Kayo_3', 'chapter_kayo_3_033', 'Melancholy'),
    @('Chapter_Kayo_4', 'chapter_kayo_4_011', 'Panic'),
    @('Chapter_Kayo_5', 'chapter_kayo_5_success_001', 'Panic'),
    @('Chapter_Kayo_5', 'chapter_kayo_5_failure_001', 'Panic'),
    @('Chapter_Kayo_6', 'chapter_kayo_6_040', 'Nostalgic'),
    @('Chapter3', 'chapter3_009', 'Quiet'),
    @('Chapter3', 'chapter3_010', 'Mystery'),
    @('Prologue1', 'prologue1_005', 'stop'),
    @('Chapter_Yowashi_1', 'chapter_yowashi_1_028', 'Quiet'),
    @('Chapter_Yowashi_1', 'chapter_yowashi_1_035', 'Mystery'),
    @('Chapter_Yowashi_Diary', 'chapter_yowashi_diary_005', 'Melancholy'),
    @('Chapter_Yowashi_Diary', 'chapter_yowashi_diary_013', 'Quiet'),
    @('Chapter_Yowashi_Diary', 'chapter_yowashi_diary_015', 'Mystery'),
    @('Chapter_Yowashi_BeforeCatch', 'chapter_yowashi_beforecatch_005', 'Mystery'),
    @('Chapter_Yowashi_BeforeCatch', 'chapter_yowashi_beforecatch_019', 'Pop'),
    @('Chapter_Yowashi_CatchSuccess', 'chapter_yowashi_catchsuccess_001', 'Pop'),
    @('Chapter_Yowashi_CatchSuccess', 'chapter_yowashi_catchsuccess_012', 'Quiet'),
    @('Chapter_Yowashi_CatchSuccess', 'chapter_yowashi_catchsuccess_015', 'Mystery'),
    @('Chapter_Yowashi_CatchFailure', 'chapter_yowashi_catchfailure_001', 'Quiet'),
    @('Chapter_Yowashi_CatchFailure', 'chapter_yowashi_catchfailure_009', 'Melancholy'),
    @('Chapter_Yowashi_MedicalBook', 'chapter_yowashi_medicalbook_006', 'Mystery'),
    @('Chapter_Yowashi_Tennis', 'chapter_yowashi_tennis_021', 'Pop'),
    @('Chapter_Yowashi_Tennis', 'chapter_yowashi_tennis_024', 'Quiet'),
    @('Chapter_Yowashi_Tennis', 'chapter_yowashi_tennis_027', 'Panic'),
    @('Chapter_Yowashi_Consultation', 'chapter_yowashi_consultation_004', 'Mystery'),
    @('Chapter_Yowashi_Rescue', 'chapter_yowashi_rescue_014', 'Panic'),
    @('Chapter_Yowashi_Rescue', 'chapter_yowashi_rescue_018', 'Nostalgic'),
    @('Chapter_Yowashi_Rescue', 'chapter_yowashi_rescue_035', 'Nostalgic'),
    @('Chapter_Yowashi_Rescue', 'chapter_yowashi_rescue_039', 'Melancholy'),
    @('Chapter_Yowashi_Rescue', 'chapter_yowashi_rescue_042', 'Melancholy')
)
foreach ($check in $checks) {
    $actual = [StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $scenarios[$check[0]], $check[1])
    if ($actual -ne $check[2]) { throw "Branch/resume mismatch: $($check[1]) = $actual" }
}
$failureScenario = [StoryMusicCueVerification.DialogueScenarioSO]::new()
$failureScenario.name = 'Chapter_Kayo_6 (Gauge Failure)'
$failureScenario.Lines = $scenarios['Chapter_Kayo_6'].Lines
if ([StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $failureScenario, 'chapter_kayo_6_035') -ne 'Nostalgic') { throw 'Restored gauge failure lost its music' }
if ($plan.dialogueScenes -contains 'NovelScene_Moteru' -or $plan.dialogueScenes -contains 'falling_verMoteru') { throw 'Unapproved route music enabled' }
if ($plan.dialogueScenes -notcontains 'NovelScene_Yowashi' -or $plan.dialogueScenes -notcontains 'falling_verYowashi') { throw 'Yowashi music disabled' }
$yowashiScenarios = @(Get-ChildItem (Join-Path $root 'Assets/Dialogue/Data') -Filter 'Chapter_Yowashi_*.asset')
foreach ($scenario in $yowashiScenarios) {
    if (-not $scenarios.ContainsKey($scenario.BaseName)) { throw "Yowashi scenario missing: $($scenario.BaseName)" }
}
$unmapped = [StoryMusicCueVerification.DialogueScenarioSO]::new()
$unmapped.name = 'Chapter_Moteru_1'
if ([StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $unmapped, 'unknown')) { throw 'Unmapped route received music' }
if (-not [StoryMusicCueVerification.Resolver]::ShouldKeepPuzzleMusic('PuzzleGame', 'NovelScene_Yowashi')) { throw 'Yowashi puzzle music lost' }
if (-not [StoryMusicCueVerification.Resolver]::ShouldKeepPuzzleMusic('PuzzleGame', 'Chapter_Kayo_SearchScene')) { throw 'Kayo puzzle music lost' }
foreach ($origin in @('NovelScene_Moteru', 'Title', 'PuzzleGame', '')) {
    if ([StoryMusicCueVerification.Resolver]::ShouldKeepPuzzleMusic('PuzzleGame', $origin)) { throw "Unapproved puzzle origin: $origin" }
}
if ([StoryMusicCueVerification.Resolver]::ShouldKeepPuzzleMusic('Title', 'NovelScene_Yowashi')) { throw 'Puzzle rule leaked into Title' }
foreach ($cue in $plan.sceneCues) {
    if (-not (Test-Path (Join-Path $root ('Assets/Resources/StoryMusic/Tracks/' + $cue.track + '.mp3')))) { throw 'Missing scene track' }
}
"PASS: $($scenarios.Count) scenarios, $cueCount cues, $($checks.Count) branch/resume checks."
$resumeTrack = [StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $scenarios['Chapter1'], 'chapter1_006')
if ($resumeTrack -ne 'Pop') { throw 'Search return keeps old music until advancing first conversation' }
if (-not $source.Contains('SceneManager.sceneLoaded += OnSceneLoaded') -or -not $source.Contains('dialogue.TryGetSceneEntryMusic(out DialogueScenarioSO scenario, out DialogueLine line)')) { throw 'Scene-load music resolution missing' }
$dialogueSource = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$previewStart = $dialogueSource.IndexOf('public bool TryGetSceneEntryMusic')
$previewEnd = $dialogueSource.IndexOf('private bool TryStartPendingResumeLine', $previewStart)
$preview = $dialogueSource.Substring($previewStart,$previewEnd-$previewStart)
if ($preview.Contains('pendingResumeLineId =') -or -not $preview.Contains('candidate.TryGetLine(pendingResumeLineId') -or -not $preview.Contains('scenario.TryGetFirstLine(out entryLine)')) { throw 'Scene cue preview consumes resume or misses fallback' }
$showStart = $dialogueSource.IndexOf('private void ShowLineImmediately')
$showEnd = $dialogueSource.IndexOf('private IEnumerator FadeToDialogueComposition', $showStart)
$show = $dialogueSource.Substring($showStart,$showEnd-$showStart)
if ($show.IndexOf('StoryMusicController.ApplyDialogue') -gt $show.IndexOf('StartCoroutine(FadeToDialogueComposition')) { throw 'Music waits for the entrance animation' }
if (-not $source.Contains('track == requestedTrack')) { throw 'Scene-load and row cues restart the same clip' }
'PASS: Search return switches to Pop on arrival; sceneLoaded resolves resume/first-row music without consuming resume, before intro fades, and same-track deduplication remains.'
