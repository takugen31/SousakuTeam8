$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scene = Get-Content (Join-Path $root 'Assets/FallingGame/Scenes/falling_verYowashi.unity') -Raw
$dropper = [regex]::Match($scene, '(?s)--- !u!114 &904006940\r?\n.*?(?=\r?\n---|\z)').Value
$renderer = [regex]::Match($scene, '(?s)--- !u!212 &904006941\r?\n.*?(?=\r?\n---|\z)').Value
$result = [regex]::Match($scene, '(?s)--- !u!114 &1971000003\r?\n.*?(?=\r?\n---|\z)').Value
$chibi = '{fileID: 21300000, guid: 8a094f6363c048fbb522ffdedba97700, type: 3}'
if (-not $dropper.Contains($chibi) -or -not $renderer.Contains($chibi)) { throw 'Enemy renderer or animation still uses normal portrait' }
if (-not $renderer.Contains('m_Size: {x: 1.2142857, y: 1.7}')) { throw 'Chibi aspect ratio not preserved' }
if (-not $result.Contains('guid: ab77056009af31a44b33dde5dab2de7d')) { throw 'Result portrait unintentionally changed' }
$meta = Get-Content (Join-Path $root 'Assets/Art/Chibi/ヨワシ　デフォルメ.png.meta') -Raw
if (-not $meta.Contains('spriteMode: 1')) { throw 'Chibi sprite import invalid' }
'PASS: Yowashi enemy uses existing chibi for renderer and animation, 1500:2100 aspect retained, result portrait unchanged.'
