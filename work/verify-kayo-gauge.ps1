$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}
$chapter5 = @(Import-Csv (Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_5.csv'))
$chapter6 = @(Import-Csv (Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_6.csv'))
$ending = @(Import-Csv (Join-Path $projectRoot 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_End.csv'))
$source = Get-Content -LiteralPath 'C:/Users/yoooo/Downloads/drive-download-20261009T085548Z-1-001/大葉カヨ.txt' -Encoding UTF8
$expectedTexts = @($source[293], $source[294], $source[298], $source[295]) | ForEach-Object { $_.Trim().TrimStart('「').TrimEnd('」') }
$newLines = @($chapter5 | Select-Object -Last 4)
for ($i = 0; $i -lt 4; $i++) {
    Assert-True ($newLines[$i].text -ceq $expectedTexts[$i]) "Original scenario mismatch: $i"
}
Assert-True ($source[295] -ceq $source[299]) 'The shared rescue line must match both outcomes.'
$choice = $chapter5 | Where-Object lineId -eq chapter_kayo_5_025
Assert-True ($choice.choice1Text -ceq '成功' -and $choice.choice2Text -ceq '失敗' -and -not $choice.nextLineId) 'Temporary outcome choice missing.'
$asset = Get-Content (Join-Path $projectRoot 'Assets/Dialogue/Data/Kayo/Chapter_Kayo_5.asset') -Raw
foreach ($row in $newLines) {
    $match = [regex]::Match($asset, '(?ms)^  - lineId: ' + [regex]::Escape($row.lineId) + '\r?\n(?<body>.*?)(?=^  - lineId: |\z)')
    Assert-True $match.Success "Missing imported asset row $($row.lineId)"
    $body = $match.Groups['body'].Value
    $text = [regex]::Match($body, '(?m)^    text: (.*)$').Groups[1].Value.Trim() | ConvertFrom-Json
    Assert-True ($text -ceq $row.text) "CSV/asset text mismatch $($row.lineId)"
    Assert-True ($body -match ('(?m)^    nextLineId: ' + [regex]::Escape($row.nextLineId) + '\s*$')) "CSV/asset link mismatch $($row.lineId)"
}
foreach ($outcome in @('success','failure')) {
    $id = "chapter_kayo_5_${outcome}_001"
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    while ($id) {
        Assert-True ($seen.Add($id)) "Cycle in $outcome branch"
        $row = $chapter5 | Where-Object lineId -eq $id
        Assert-True ($null -ne $row) "Missing $id"
        $id = $row.nextLineId
        if (-not $id) { Assert-True ($row.lineId -ceq $chapter5[-1].lineId) 'Branch must exit at chapter end.' }
    }
    Assert-True ($seen.Count -eq $(if ($outcome -eq 'success') { 3 } else { 2 })) 'Wrong number of result lines.'
    Assert-True (-not ($seen.Contains("chapter_kayo_5_$(if ($outcome -eq 'success') {'failure'} else {'success'})_001"))) 'Opposite result leaked.'
}
Assert-True ($ending[-1].nextScenePath -match 'Title\.unity$') 'Ending must return to Title.'
Assert-True ($chapter6[55].lineId -ceq 'chapter_kayo_6_056' -and $chapter6[56].lineId -ceq 'chapter_kayo_6_057') 'Success-only boundary moved.'

# Execute the actual outcome controller and scenario methods with a small non-rendering
# Unity lifecycle harness. This checks routing, cloning and cache behavior, not Unity UI.
$scenarioCode = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Runtime/DialogueScenarioSO.cs') -Raw
$bridgeCode = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Search/KayoGaugeOutcomeController.cs') -Raw
$controllerCode = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
$methodStart = $controllerCode.IndexOf('    public bool ReplaceFollowingScenario(')
$methodEnd = $controllerCode.IndexOf('    public void StartEmbeddedDialogue()', $methodStart)
$replaceMethod = $controllerCode.Substring($methodStart, $methodEnd - $methodStart)
$testCode = @'
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
    public class Object {
        public string name;
        public static T Instantiate<T>(T original) where T : Object {
            var source = (DialogueScenarioSO)(object)original;
            var copy = new DialogueScenarioSO();
            copy.name = source.name;
            copy.SetDefaultBackground(source.DefaultBackground);
            var lines = new List<DialogueLine>();
            var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var line in source.Lines) lines.Add((DialogueLine)clone.Invoke(line, null));
            copy.ReplaceAll(lines);
            return (T)(object)copy;
        }
        public static void Destroy(Object target) { }
    }
    public class MonoBehaviour : Object { public bool enabled = true; }
    public class ScriptableObject : Object { }
    public class Sprite : Object { }
    public class SerializeField : Attribute { }
    public class Tooltip : Attribute { public Tooltip(string text) { } }
    public class TextArea : Attribute { public TextArea(int min, int max) { } }
    public class CreateAssetMenu : Attribute { public string menuName; public string fileName; }
    public static class Mathf { public static int Max(int a, int b) { return Math.Max(a,b); } }
    public static class Debug { public static void LogError(string text, Object context) { throw new Exception(text); } }
}
public class AffectionDelta { }
public class AffectionCondition { }
public class NovelDialogueController {
    public List<DialogueScenarioSO> followingScenarios = new List<DialogueScenarioSO>();
    public event Action<DialogueLine> LineStarted;
    public void Show(string id) { LineStarted?.Invoke(new DialogueLine { lineId = id }); }
'@
$testCode += $replaceMethod + "`n}`n" + ($scenarioCode -replace '(?m)^using .*;\s*$', '') + "`n" + ($bridgeCode -replace '(?m)^using .*;\s*$', '')
$testCode += @'
public static class KayoGaugeCheck {
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run() {
        var source = new DialogueScenarioSO { name = "Chapter_Kayo_6" };
        var lines = new List<DialogueLine>();
        for (int i=1; i<=72; i++) lines.Add(new DialogueLine {
            lineId = "chapter_kayo_6_" + i.ToString("000"),
            nextLineId = i < 72 ? "chapter_kayo_6_" + (i+1).ToString("000") : ""
        });
        source.ReplaceAll(lines);
        var ending = new DialogueScenarioSO();
        var controller = new NovelDialogueController();
        controller.followingScenarios.Add(source);
        controller.followingScenarios.Add(ending);
        var bridge = new KayoGaugeOutcomeController();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(KayoGaugeOutcomeController).GetField("dialogueController",flags).SetValue(bridge,controller);
        typeof(KayoGaugeOutcomeController).GetField("rescueAftermath",flags).SetValue(bridge,source);
        typeof(KayoGaugeOutcomeController).GetMethod("Awake",flags).Invoke(bridge,null);
        var failure = controller.followingScenarios[0];
        Check(failure != source && failure.Lines.Count == 56,"Default/skip must not unlock paper");
        Check(source.Lines.Count == 72 && source.Lines[55].nextLineId == "chapter_kayo_6_057","Source asset mutated");
        controller.Show("chapter_kayo_5_success_001");
        Check(controller.followingScenarios[0] == source,"Success must retain original extra conversation");
        controller.Show("chapter_kayo_5_failure_001");
        Check(controller.followingScenarios[0] == failure,"Failure must omit extra conversation");
        DialogueLine next;
        Check(!failure.TryGetNextLine("chapter_kayo_6_056",out next) && string.IsNullOrEmpty(failure.Lines[55].nextLineId),"Failure must finish chapter at common boundary");
        Check(!failure.TryGetLine("chapter_kayo_6_057",out next),"Success-only line remained in cache");
        Check(controller.followingScenarios[1] == ending,"Common ending lost");
        bridge.SetResult(true);
        Check(controller.followingScenarios[0] == source,"Future gauge result API failed");
        typeof(KayoGaugeOutcomeController).GetMethod("OnDestroy",flags).Invoke(bridge,null);
        controller.Show("chapter_kayo_5_failure_001");
        Check(controller.followingScenarios[0] == source,"Destroyed controller remained subscribed");
    }
}
'@
Add-Type -TypeDefinition $testCode
[KayoGaugeCheck]::Run()
Write-Output 'PASS: original dialogue, CSV/asset alignment, both rescue outcomes, success-only paper, common ending, source preservation, result API, event cleanup.'
