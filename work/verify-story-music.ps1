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
    @('Prologue1', 'prologue1_005', 'stop')
)
foreach ($check in $checks) {
    $actual = [StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $scenarios[$check[0]], $check[1])
    if ($actual -ne $check[2]) { throw "Branch/resume mismatch: $($check[1]) = $actual" }
}
if ($plan.dialogueScenes -contains 'NovelScene_Moteru' -or $plan.dialogueScenes -contains 'NovelScene_Yowashi') { throw 'Unapproved route music enabled' }
$unmapped = [StoryMusicCueVerification.DialogueScenarioSO]::new()
$unmapped.name = 'Chapter_Yowashi_1'
if ([StoryMusicCueVerification.Resolver]::ResolveTrack($typedPlan, $unmapped, 'unknown')) { throw 'Unmapped route received music' }
foreach ($cue in $plan.sceneCues) {
    if (-not (Test-Path (Join-Path $root ('Assets/Resources/StoryMusic/Tracks/' + $cue.track + '.mp3')))) { throw 'Missing scene track' }
}
"PASS: $($scenarios.Count) scenarios, $cueCount cues, $($checks.Count) branch/resume checks."
