$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$database = Get-Content (Join-Path $root 'Assets/Dialogue/Data/CharacterDatabaseatPrologue.asset') -Raw
$characters = @{ 'falling_verKayo' = 'kayo'; 'SampleScene' = 'kayo'; 'falling_verMoteru' = 'moteru'; 'falling_verYowashi' = 'yowashi' }
foreach ($scene in $characters.Keys) {
    $id = $characters[$scene]
    $character = [regex]::Match($database, "(?s)  - characterId: $id\r?\n.*?(?=\r?\n  - characterId:|\z)").Value
    $expected = [regex]::Match($character, 'portrait: (\{[^\r\n]+\})').Groups[1].Value
    $content = Get-Content (Join-Path $root "Assets/FallingGame/Scenes/$scene.unity") -Raw
    $portrait = [regex]::Match($content, '(?s)--- !u!114 &1971000003\r?\n.*?(?=\r?\n---|\z)').Value
    $actual = [regex]::Match($portrait, 'm_Sprite: (\{[^\r\n]+\})').Groups[1].Value
    if ([string]::IsNullOrEmpty($expected) -or $actual -ne $expected) { throw "$scene does not use $id's current novel portrait" }
    if (-not $portrait.Contains('m_PreserveAspect: 1')) { throw "$scene distorts portrait aspect" }
    if (-not $content.Contains('resultPortrait: {fileID: 1971000003}')) { throw "$scene result image reference disconnected" }
}
'PASS: all four catch result images match the current novel character portraits, with preserved aspect and intact result references.'
