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
'PASS: actual name resolver keeps Doute known with empty/reset cache, honors initial/revealed names, and keeps other characters unknown before introductions.'
