param(
    [string]$UnityEditor = 'D:/Unity/Unity Editer/6000.3.6f1/Editor/Unity.exe',
    [string]$ReuseProject
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testProject = if ($ReuseProject) { $ReuseProject } else { Join-Path $root ('Temp/DialogueSceneProgressChecks-' + [guid]::NewGuid().ToString('N')) }
$testProject = [IO.Path]::GetFullPath($testProject)
if (-not $testProject.StartsWith((Join-Path $root 'Temp/DialogueSceneProgressChecks-'))) { throw 'Use a workspace-local isolated project.' }
$utf8 = [Text.UTF8Encoding]::new($false)
foreach ($folder in @('Assets/Editor','Assets/Resources/StoryMusic','Assets/Scenes','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $testProject $folder) -Force | Out-Null
}
$dependencies = @{}
function Add-LocalPackage([string]$package) {
    if ($dependencies.ContainsKey($package)) { return }
    if ($package.StartsWith('com.unity.modules.')) { $dependencies[$package]='1.0.0'; return }
    $cache = Get-ChildItem -LiteralPath (Join-Path $root 'Library/PackageCache') -Directory | Where-Object Name -Like ($package+'@*') | Select-Object -First 1
    if (-not $cache) { throw "Missing local Unity package: $package" }
    $dependencies[$package] = 'file:' + $cache.FullName.Replace('\','/')
    $packageInfo = Get-Content -LiteralPath (Join-Path $cache.FullName 'package.json') -Raw | ConvertFrom-Json
    foreach ($dependency in $packageInfo.dependencies.PSObject.Properties) { Add-LocalPackage $dependency.Name }
}
foreach ($package in @('com.unity.ugui','com.unity.inputsystem','com.unity.render-pipelines.universal')) { Add-LocalPackage $package }
foreach ($module in @('audio','imageconversion','physics','physics2d','jsonserialize','ui','animation','imgui','uielements','video','director','unitywebrequest')) {
    $dependencies['com.unity.modules.'+$module] = '1.0.0'
}
[IO.File]::WriteAllText((Join-Path $testProject 'Packages/manifest.json'), (@{dependencies=$dependencies} | ConvertTo-Json), $utf8)
Copy-Item -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $testProject 'ProjectSettings')
foreach ($folder in @('Assets/Scripts','Assets/FallingGame/Scripts','Assets/PuzzleGame/Runtime','Assets/TextMesh Pro/Resources','Assets/TextMesh Pro/Shaders','Assets/Dialogue/Data')) {
    $destination = Join-Path $testProject $folder
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root $folder) | Copy-Item -Destination $destination -Recurse -Force
}
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/Resources/StoryMusic/MusicPlan.json'), '{"dialogueScenes":[],"sceneCues":[],"scenarios":[]}', $utf8)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DialogueSceneProgressChecks.cs') -Destination (Join-Path $testProject 'Assets')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DialogueScriptBindingChecks.cs') -Destination (Join-Path $testProject 'Assets/Editor')
New-Item -ItemType Directory -Path (Join-Path $testProject 'Assets/Scenes/GameMap') -Force | Out-Null
foreach ($scene in @('NovelScene','NovelScene_Kayo','NovelScene_Moteru','NovelScene_Yowashi')) {
    foreach ($suffix in @('.unity','.unity.meta')) {
        Copy-Item -LiteralPath (Join-Path $root ('Assets/Scenes/GameMap/'+$scene+$suffix)) -Destination (Join-Path $testProject 'Assets/Scenes/GameMap')
    }
}
$bootstrap = @'
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DialogueSceneProgressBootstrap {
 public static void Run() {
  if (!Application.dataPath.Contains("DialogueSceneProgressChecks-")) throw new Exception("Use an isolated project.");
  PlayerSettings.companyName="DialogueProgressChecks"; PlayerSettings.productName=System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Application.dataPath));
  DialogueScriptBindingChecks.Run();
  var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>();
  foreach(string name in new[]{"NovelScene_Moteru","ProgressOther","NovelScene"}) {
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   string path="Assets/Scenes/"+name+".unity"; EditorSceneManager.SaveScene(scene,path); scenes.Add(new EditorBuildSettingsScene(path,true));
  }
  EditorBuildSettings.scenes=scenes.ToArray();
  SessionState.SetBool("DialogueSceneProgressChecks",true);
  EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
 }
}
'@
[IO.File]::WriteAllText((Join-Path $testProject 'Assets/Editor/DialogueSceneProgressBootstrap.cs'), $bootstrap, $utf8)
$logPath = Join-Path $testProject 'progress-checks.log'
Write-Output "Isolated Unity project: $testProject"
$process = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','-nographics','-projectPath',('"'+$testProject+'"'),'-executeMethod','DialogueSceneProgressBootstrap.Run','-logFile',('"'+$logPath+'"'))
$deadline = [DateTime]::UtcNow.AddMinutes(6)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity checks timed out: $logPath" }
}
$output = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
$output -split '\r?\n' | Where-Object { $_ -match '^PASS:|^FAIL:|error CS|^Exception:|Exception:' } | Write-Output
if ($process.ExitCode -ne 0 -or -not $output.Contains('PASS: dialogue scene progress integration completed.')) {
    throw "Unity checks failed (exit $($process.ExitCode)): $logPath"
}
