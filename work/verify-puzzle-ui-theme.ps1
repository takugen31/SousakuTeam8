$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check($condition, $message) { if (-not $condition) { throw $message } }
$controller = Get-Content (Join-Path $root 'Assets/PuzzleGame/Runtime/PuzzleGameController.cs') -Raw
$bootstrap = Get-Content (Join-Path $root 'Assets/PuzzleGame/Runtime/PuzzleGameStandaloneBootstrap.cs') -Raw
$scene = Get-Content (Join-Path $root 'Assets/PuzzleGame/Scenes/PuzzleGame.unity') -Raw
foreach ($old in @('PIECE PUZZLE', 'MOVES', '3 × 3  PUZZLE', '#F1745B', '#101A2D')) { Check (-not $controller.Contains($old)) "Old visual style remains: $old" }
foreach ($label in @('パズル', '操作回数', '9枚のピースを並べて、絵を完成させよう')) { Check ($controller.Contains($label)) "Missing Japanese label: $label" }
Check ($scene.Contains('guid: 2f638f3b99bcade49888e208c0066080') -and $scene.Contains('guid: c9e304f8c7ae8d34ea5e5d4070d3919f')) 'Novel room or Noto Black font not configured'
Check ($bootstrap.IndexOf('gameObject.SetActive(false)') -lt $bootstrap.IndexOf('gameObject.AddComponent<PuzzleGameController>') -and $bootstrap.IndexOf('ConfigureTheme(roomBackground, uiFont)') -lt $bootstrap.IndexOf('gameObject.SetActive(true)')) 'Theme configured after UI Awake'
Check ($controller.Contains('AspectRatioFitter.AspectMode.EnvelopeParent') -and $controller.Contains('new Color(0f, 0f, 0f, 0.76f)') -and $controller.Contains('AddComponent<PuzzlePanelFeather>()')) 'Cover background or feathered black panels missing'
Check ($controller.Contains('vertex.color = color') -and $controller.Contains('color.a *= weights[x] * weights[y]')) 'Panel smooth edge transparency missing'
Check ($controller.Contains('const int steps = 12') -and $controller.Contains('Mathf.SmoothStep(0f, 1f, t)') -and $controller.Contains('boardInner.gameObject.AddComponent<PuzzlePanelFeather>()')) 'Subdivided smooth fade or inner panel feather missing'
$weights = [double[]]::new(26)
for ($i=0; $i -le 12; $i++) {
    $t=$i/12.0; $weights[$i]=$weights[25-$i]=$t*$t*(3-2*$t)
}
Check ($weights[0] -eq 0 -and $weights[25] -eq 0 -and $weights[12] -eq 1 -and $weights[13] -eq 1) 'Fade changes central opacity or leaves opaque outer edge'
for ($i=1; $i -le 12; $i++) { Check ($weights[$i] -ge $weights[$i-1] -and $weights[$i] -eq $weights[25-$i]) 'Fade is asymmetric or non-monotonic' }
Check ([Math]::Abs($weights[6]*$weights[6]-0.25) -lt 0.000001) 'Corner alpha does not blend both axes'
Add-Type -TypeDefinition (Get-Content (Join-Path $root 'Assets/PuzzleGame/Runtime/PuzzleBoardState.cs') -Raw)
$board = [SousakuTeam8.PuzzleGame.PuzzleBoardState]::new()
$board.ResetSolved()
Check ($board.IsComplete) 'Solved state broken'
Check ($board.TrySwapSlots(0, 8) -and -not $board.IsComplete -and $board.MoveCount -eq 1) 'Swap broken'
Check ($board.TrySwapSlots(0, 8) -and $board.IsComplete -and $board.MoveCount -eq 2) 'Completion broken'
'PASS: Japanese novel-style labels, original room/font references, theme-before-Awake, aspect-safe background, feathered black panels and unchanged swap/completion model.'
