$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/FallingGame/Scripts/CatchMiniGameController.cs') -Raw
$start = $source.IndexOf('private bool TryInstantClearForTesting()')
$end = $source.IndexOf('private void ApplyRenderingQuality()', $start)
$method = $source.Substring($start, $end-$start)
Add-Type -CompilerOptions '/define:UNITY_EDITOR' -TypeDefinition @"
using System;
namespace UnityEngine.InputSystem {
public class Key { public bool isPressed, wasPressedThisFrame; }
public class Keyboard {
    public static Keyboard current = new Keyboard();
    public Key ctrlKey = new Key(), shiftKey = new Key(), f10Key = new Key();
} }
namespace CatchInstantClearChecks {
public static class Time { public static float timeScale = 1f; }
public static class Mathf { public static int Max(int a, int b) { return Math.Max(a,b); } }
public class GameObject { public bool active = true; public void SetActive(bool value) { active = value; } }
public class Text { public GameObject gameObject = new GameObject(); }
public class Controller {
    public enum GamePhase { AwaitingInput, Countdown, Playing, GameOver }
    public GamePhase phase; public int score, endings, hudUpdates;
    public Text startPromptText = new Text(), jumpUnlockText = new Text(), sweepUnlockText = new Text();
    private void RefreshHud() { hudUpdates++; }
    private void EndRound() { endings++; phase = GamePhase.GameOver; }
    public bool Trigger() { return TryInstantClearForTesting(); }
$method
} }
"@
function Check($condition, $message) { if (-not $condition) { throw $message } }
$keyboard = [UnityEngine.InputSystem.Keyboard]::current
foreach ($phase in @('AwaitingInput', 'Countdown', 'Playing')) {
    $game=[CatchInstantClearChecks.Controller]::new()
    $game.phase=[CatchInstantClearChecks.Controller+GamePhase]::$phase
    $keyboard.ctrlKey.isPressed=$true; $keyboard.shiftKey.isPressed=$true; $keyboard.f10Key.wasPressedThisFrame=$true
    Check ($game.Trigger() -and $game.score -ge 10000 -and $game.endings -eq 1) 'Shortcut does not end successfully'
    Check (-not $game.startPromptText.gameObject.active -and -not $game.jumpUnlockText.gameObject.active -and -not $game.sweepUnlockText.gameObject.active) 'Game UI remains over results'
    Check (-not $game.Trigger() -and $game.endings -eq 1) 'Shortcut repeats result event'
}
foreach ($missing in @('ctrlKey', 'shiftKey', 'f10Key')) {
    $keyboard.ctrlKey.isPressed=$true; $keyboard.shiftKey.isPressed=$true; $keyboard.f10Key.wasPressedThisFrame=$true
    if ($missing -eq 'f10Key') { $keyboard.f10Key.wasPressedThisFrame=$false } else { $keyboard.$missing.isPressed=$false }
    Check (-not ([CatchInstantClearChecks.Controller]::new()).Trigger()) 'Incomplete chord triggers clear'
}
$keyboard.ctrlKey.isPressed=$true; $keyboard.shiftKey.isPressed=$true; $keyboard.f10Key.wasPressedThisFrame=$true
[CatchInstantClearChecks.Time]::timeScale=0
Check (-not ([CatchInstantClearChecks.Controller]::new()).Trigger()) 'Shortcut fires while menu pauses game'
[CatchInstantClearChecks.Time]::timeScale=1
$game=[CatchInstantClearChecks.Controller]::new(); $game.score=15000; $game.Trigger() | Out-Null
Check ($game.score -eq 15000) 'Shortcut reduces existing high score'
$helpStart=$source.IndexOf('public string CurrentOperationHelp')
$helpEnd=$source.IndexOf('private void Awake()', $helpStart)
Check (-not $source.Substring($helpStart, $helpEnd-$helpStart).Contains('F10')) 'Hidden shortcut disclosed in operation menu'
Check ($method.Contains('#if UNITY_EDITOR || DEVELOPMENT_BUILD')) 'Testing shortcut enabled in release builds'
'PASS: actual Ctrl+Shift+F10 handler clears waiting/countdown/playing with success score, normal result path, no duplicate events, paused-menu guard, hidden operation help and editor/development-only gate.'
