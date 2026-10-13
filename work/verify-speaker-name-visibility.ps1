$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$begin=$source.IndexOf('public static string GetSpeakerDisplayName(')
$end=$source.IndexOf('private void ApplyProtagonistFacing', $begin)
$method=$source.Substring($begin,$end-$begin)
Add-Type -TypeDefinition @"
using System;
namespace SpeakerNameVisibilityChecks {
public class CharacterData { public string characterId; public string displayName; public bool nameKnownInitially; }
public static class Names {
$method
}
}
"@
function Check-Name($id,$display,$initial,$revealed,$expected) {
    $character=[SpeakerNameVisibilityChecks.CharacterData]::new()
    $character.characterId=$id;$character.displayName=$display;$character.nameKnownInitially=$initial
    $actual=[SpeakerNameVisibilityChecks.Names]::GetSpeakerDisplayName($character,$revealed,'???')
    if($actual -ne $expected) { throw "Incorrect speaker label for $id : $actual" }
}
Check-Name 'doute' 'ドウテ' $false $false 'ドウテ'
Check-Name 'doute' 'ドウテ' $true $false 'ドウテ'
foreach($pair in @(@('kayo','カヨ'),@('yowashi','ヨワシ'),@('moteru','モテル'))) {
    Check-Name $pair[0] $pair[1] $false $false '???'
    Check-Name $pair[0] $pair[1] $false $true $pair[1]
}
Check-Name 'player' 'プレイヤー' $true $false 'プレイヤー'
if(-not $source.Contains('knownCharacterIds.Add("doute")') -or -not $source.Contains('revealedCharacterIds.Contains(line.speakerId)')) { throw 'Reset/revealed-state guard missing' }
$begin=$source.IndexOf('public static bool AreCommonIntroductionsComplete(')
$end=$source.IndexOf('private void RevealCurrentSpeakerName()', $begin)
$restore=$source.Substring($begin,$end-$begin)
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
namespace NameHistoryChecks {
public class DialogueLine { public string lineId, speakerId; public bool revealSpeakerName; }
public class DialogueScenarioSO { public List<DialogueLine> Lines = new List<DialogueLine>(); }
public class Controller {
public DialogueScenarioSO currentScenario, scenario;
public int currentScenarioIndex;
public List<DialogueScenarioSO> followingScenarios = new List<DialogueScenarioSO>();
public HashSet<string> knownCharacterIds = new HashSet<string>();
public HashSet<string> revealedCharacterIds = new HashSet<string>();
public void Restore(DialogueLine line) { RestoreNamesIntroducedBefore(line); }
$restore
} }
"@
$intro=[NameHistoryChecks.DialogueScenarioSO]::new()
$introLine=[NameHistoryChecks.DialogueLine]::new()
$introLine.lineId='chapter2_kayo_001'; $introLine.speakerId='kayo'; $introLine.revealSpeakerName=$true
$intro.Lines.Add($introLine)
$endScenario=[NameHistoryChecks.DialogueScenarioSO]::new()
$endLine=[NameHistoryChecks.DialogueLine]::new()
$endLine.lineId='chapter2_kayo_002'; $endLine.speakerId='kayo'
$endScenario.Lines.Add($endLine)
$controller=[NameHistoryChecks.Controller]::new()
$controller.scenario=$intro; $controller.currentScenario=$intro; $controller.currentScenarioIndex=0
$controller.Restore($introLine)
if($controller.knownCharacterIds.Contains('kayo')) { throw 'Current introduction reveals name prematurely' }
$controller.currentScenario=$endScenario; $controller.currentScenarioIndex=1
$controller.followingScenarios.Add($endScenario)
$controller.Restore($endLine)
if(-not $controller.knownCharacterIds.Contains('kayo') -or -not $controller.revealedCharacterIds.Contains('kayo')) { throw 'Skipped/resumed introduction fails to restore Kayo name' }
if($controller.knownCharacterIds.Contains('yowashi')) { throw 'Future name revealed without introduction' }
foreach ($id in @('chapter_yowashi_1_019', 'chapter_yowashi_beforecatch_010', 'chapter_yowashi_catchsuccess_001', 'chapter_kayo_3_008', 'chapter2_end_003', 'chapter3_001')) {
    if (-not [NameHistoryChecks.Controller]::AreCommonIntroductionsComplete($id)) { throw "Route prerequisites missing at $id" }
}
if (-not $source.Contains('AreCommonIntroductionsComplete(line.lineId),')) { throw 'Speaker render still relies only on transient name cache' }
foreach ($row in 1..19) {
    $id = 'chapter_yowashi_beforecatch_{0:000}' -f $row
    $knownFromContext = [NameHistoryChecks.Controller]::AreCommonIntroductionsComplete($id)
    Check-Name 'yowashi' 'ヨワシ' $false $knownFromContext 'ヨワシ'
}
$csv = Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_BeforeCatch.csv')
$greeting = $csv | Where-Object lineId -eq 'chapter_yowashi_beforecatch_001'
if ($greeting.speakerId -ne 'yowashi' -or $greeting.text -ne 'おはようドウテ卿！いい朝だねぇ') { throw 'Greeting speaker incorrect' }
$beforeCatchAsset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter_Yowashi_BeforeCatch.asset') -Raw
if (-not [regex]::IsMatch($beforeCatchAsset, 'lineId: chapter_yowashi_beforecatch_001\s+speakerId: yowashi')) { throw 'Runtime greeting speaker incorrect' }
$problem = $csv | Where-Object lineId -eq 'chapter_yowashi_beforecatch_007'
if ($problem.speakerId -ne 'yowashi' -or -not $problem.text.Contains('この日記は知らんぞ')) { throw 'Problem dialogue cue differs' }
foreach ($id in @('chapter1_yowashi_001', 'chapter2_yowashi_001', 'chapter2_kayo_001', 'prologue2_001')) {
    if ([NameHistoryChecks.Controller]::AreCommonIntroductionsComplete($id)) { throw "Names revealed early at $id" }
}
$route=[NameHistoryChecks.Controller]::new()
$route.currentScenario=[NameHistoryChecks.DialogueScenarioSO]::new()
$line=[NameHistoryChecks.DialogueLine]::new(); $line.lineId='chapter_yowashi_1_019'; $line.speakerId='yowashi'
$route.currentScenario.Lines.Add($line)
$route.Restore($line)
foreach ($id in @('kayo', 'yowashi', 'moteru')) {
    if (-not $route.knownCharacterIds.Contains($id) -or -not $route.revealedCharacterIds.Contains($id)) { throw 'Route entered with unknown previously introduced character' }
}
'PASS: actual history reconstruction restores Kayo after introduction, including skip/resume, without revealing current/future introductions.'
'PASS: actual name resolver keeps Doute known with empty/reset cache, honors initial/revealed names, and keeps other characters unknown before introductions.'
