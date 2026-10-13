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
$begin=$source.IndexOf('private void RestoreNamesIntroducedBefore(')
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
$endLine.lineId='chapter2_end_003'; $endLine.speakerId='kayo'
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
'PASS: actual history reconstruction restores Kayo after introduction, including skip/resume, without revealing current/future introductions.'
'PASS: actual name resolver keeps Doute known with empty/reset cache, honors initial/revealed names, and keeps other characters unknown before introductions.'
