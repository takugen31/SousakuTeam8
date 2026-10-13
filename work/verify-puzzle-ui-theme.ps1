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
Check ($controller.Contains('vertex.color = color') -and $controller.Contains('color.a = 0f')) 'Panel edge transparency missing'
Add-Type -TypeDefinition (Get-Content (Join-Path $root 'Assets/PuzzleGame/Runtime/PuzzleBoardState.cs') -Raw)
$board = [SousakuTeam8.PuzzleGame.PuzzleBoardState]::new()
$board.ResetSolved()
Check ($board.IsComplete) 'Solved state broken'
Check ($board.TrySwapSlots(0, 8) -and -not $board.IsComplete -and $board.MoveCount -eq 1) 'Swap broken'
Check ($board.TrySwapSlots(0, 8) -and $board.IsComplete -and $board.MoveCount -eq 2) 'Completion broken'
'PASS: Japanese novel-style labels, original room/font references, theme-before-Awake, aspect-safe background, feathered black panels and unchanged swap/completion model.'
