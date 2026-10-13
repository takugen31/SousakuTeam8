$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/FallingGame/Scripts/CatchMiniGameController.cs') -Raw
$start = $source.IndexOf('private void StartRound()')
$end = $source.IndexOf('public void HideGameOverPresentation()', $start)
$methods = $source.Substring($start, $end-$start)
Add-Type -TypeDefinition @"
using System;
namespace CatchResultVisibilityChecks {
public class GameObject { public bool active = true; public void SetActive(bool value) { active = value; } }
public class Text { public GameObject gameObject = new GameObject(); }
public class Dropper { public GameObject gameObject = new GameObject(); public int rounds; public void BeginRound() { rounds++; } }
public class Controller {
    public enum GamePhase { Playing, GameOver }
    public GamePhase phase;
    public float remainingTime, gameDuration = 100;
    public int score;
    public Dropper dropper = new Dropper();
    private Text startPromptText = new Text(), feedbackText = new Text();
    public bool cleared, result;
    public event Action<int> RoundEnded;
    private void RefreshHud() {}
    private void UpdateTimeText() {}
    private void ClearActiveItems() { cleared = true; }
    private void ShowGameOverPresentation() {
        if (dropper != null && dropper.gameObject.active) throw new Exception("Enemy visible when result shown");
        result = true;
    }
    public void Begin() { StartRound(); }
    public void Finish() { EndRound(); }
$methods
} }
"@
$controller = [CatchResultVisibilityChecks.Controller]::new()
foreach ($round in 1..3) {
    $controller.Begin()
    if (-not $controller.dropper.gameObject.active) { throw 'Enemy not restored for next round' }
    $controller.Finish()
    if ($controller.dropper.gameObject.active -or -not $controller.result -or -not $controller.cleared) { throw 'Result cleanup failed' }
}
$controller.dropper = $null
$controller.Begin(); $controller.Finish()
'PASS: actual round start/end hides enemy before results, restores it for another round, clears falling items, handles missing enemy safely.'
