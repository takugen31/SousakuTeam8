param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$kayo = Get-Content -LiteralPath (Join-Path $root 'Assets/FallingGame/Scenes/falling_verKayo.unity') -Raw
$moteru = Get-Content -LiteralPath (Join-Path $root 'Assets/FallingGame/Scenes/falling_verMoteru.unity') -Raw
foreach ($field in @('resultDisplayDuration', 'fadeDuration')) {
    $kayoValue = [regex]::Match($kayo, "(?m)^  ${field}: ([0-9.]+)").Groups[1].Value
    $moteruValue = [regex]::Match($moteru, "(?m)^  ${field}: ([0-9.]+)").Groups[1].Value
    if (-not $kayoValue -or $kayoValue -ne $moteruValue) { throw "Kayo/Moteru timing mismatch: $field" }
}
$resumePath = Join-Path $root 'Assets/Dialogue/Data/Moteru/Chapter_Moteru_2.asset'
$resume = Get-Content -LiteralPath $resumePath -Raw
$resumeMeta = Get-Content -LiteralPath ($resumePath + '.meta') -Raw
$resumeGuid = [regex]::Match($resumeMeta, '(?m)^guid: ([a-f0-9]{32})').Groups[1].Value
if (-not $moteru.Contains("resumeScenario: {fileID: 11400000, guid: $resumeGuid, type: 2}")) { throw 'Resume asset not connected' }
$novel = Get-Content -LiteralPath (Join-Path $root 'Assets/Scenes/GameMap/NovelScene_Moteru.unity') -Raw
if (-not $novel.Contains("guid: $resumeGuid, type: 2}")) { throw 'Resume scenario missing from novel sequence' }
if ($resume -notmatch '(?m)^  - lineId: chapter_moteru_2_001\r?$') { throw 'Resume first line changed; update the integration expectation' }
$gameSource = Get-Content -LiteralPath (Join-Path $root 'Assets/FallingGame/Scripts/CatchMiniGameController.cs') -Raw
if ($gameSource.IndexOf('ShowGameOverPresentation();') -gt $gameSource.IndexOf('RoundEnded?.Invoke(score);')) { throw 'Result signal precedes score display' }
Write-Output 'PASS: real scene timings match Kayo; resume asset, first line, novel sequence, and score/event ordering are connected.'

$testProject = Join-Path $root ('Temp/MoteruCatchReturnChecks-' + [guid]::NewGuid().ToString('N'))
foreach ($folder in @('Assets/Editor', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $testProject $folder) -Force | Out-Null
}
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $testProject 'Packages/manifest.json'), '{"dependencies":{"com.unity.ugui":"2.0.0"}}', $encoding)
Copy-Item -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $testProject 'ProjectSettings')
foreach ($file in @('CatchGameFade.cs','MoteruCatchGameFlowController.cs')) {
    Copy-Item -LiteralPath (Join-Path $root "Assets/FallingGame/Scripts/$file") -Destination (Join-Path $testProject 'Assets')
}
Copy-Item -LiteralPath (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/DialogueScenarioSO.cs') -Destination (Join-Path $testProject 'Assets')
$checksSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MoteruCatchReturnChecks.cs'))
$bootstrapStart = $checksSource.IndexOf('[InitializeOnLoad]')
if ($bootstrapStart -lt 0) { throw 'Test bootstrap not found' }
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/MoteruCatchReturnChecks.cs'), $checksSource.Substring(0, $bootstrapStart), $encoding)
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/Editor/MoteruCatchReturnBootstrap.cs'),
    "using System; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;`n" + $checksSource.Substring($bootstrapStart), $encoding)
$gameDouble = @'
using System;
using UnityEngine;
namespace Sousakusai8.MiniGame {
 public sealed class CatchMiniGameController : MonoBehaviour {
  public event Action<int> RoundEnded;
  public bool PresentationVisible;
  public void Finish(int score) { PresentationVisible=true; RoundEnded?.Invoke(score); }
 }
}
'@
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/CatchMiniGameController.cs'), $gameDouble, $encoding)
$boundaryDoubles = @'
using System;
public static class DialogueProgressTransitions {
 public static string ResumeLine; public static bool FadeIn;
 public static bool ReturnToScene(string scene,string line,bool fadeIn=true) {
  ResumeLine=line; FadeIn=fadeIn; UnityEngine.SceneManagement.SceneManager.LoadScene(scene,UnityEngine.SceneManagement.LoadSceneMode.Single); return true;
 }
}
public static class ArchiveManager { public static bool Closed; public static void Close() { Closed=true; } }
[Serializable] public sealed class AffectionDelta {}
[Serializable] public sealed class AffectionCondition {}
'@
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/DialogueResumeProbe.cs'), $boundaryDoubles, $encoding)
$logPath = Join-Path $testProject 'return-checks.log'
Write-Output "Isolated Unity project: $testProject"
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"'+$testProject+'"'),
    '-executeMethod', 'MoteruCatchReturnBootstrap.Run', '-logFile', ('"'+$logPath+'"'))
$deadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity checks timed out: $logPath" }
}
$output = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
$output -split '\r?\n' | Where-Object { $_ -match '^PASS:|^FAIL:|error CS' } | Write-Output
if ($process.ExitCode -ne 0 -or $output -notmatch 'PASS: Moteru catch return integration completed') {
    throw "Unity checks failed (exit $($process.ExitCode)): $logPath"
}
