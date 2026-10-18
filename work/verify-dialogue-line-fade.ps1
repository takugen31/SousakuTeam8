$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'Assets/Scripts/Dialogue/Runtime'
$controller = Get-Content (Join-Path $runtime 'NovelDialogueControllerSO.cs') -Raw -Encoding UTF8
$start = $controller.IndexOf('    private bool TryStartLineFade(')
$end = $controller.IndexOf('    private void ShowLineImmediately(', $start)
$methods = $controller.Substring($start, $end - $start)
# Execute the production CSV/model and transition coroutine against in-memory Unity doubles.
# This tests ordering and parsing; it does not verify Unity rendering or input dispatch.
$source = @'
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public static class Mathf { public static int Max(int a,int b) { return Math.Max(a,b); } }
 public class SerializeField : Attribute {}
 public class TooltipAttribute : Attribute { public TooltipAttribute(string s) {} }
 public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a,int b) {} }
 public class CreateAssetMenuAttribute : Attribute { public string menuName; public string fileName; }
 public enum HideFlags { HideAndDontSave }
 public class ScriptableObject { public string name; public HideFlags hideFlags; public static T CreateInstance<T>() where T:new() { return new T(); } }
 public class Sprite {}
 public class TextAsset { public string name="test"; public string text; public TextAsset(string t) { text=t; } }
 public class WaitForSecondsRealtime { public float seconds; public WaitForSecondsRealtime(float s) { seconds=s; } }
 public static class Debug { public static void LogWarning(string s, object o) {} }
}
public class AffectionDelta {}
public class AffectionCondition {}
public class Image { public Image transform { get { return this; } } public Image gameObject { get { return this; } } public bool active; public void SetActive(bool b) { active=b; } public void SetAsLastSibling() {} }
public class Body { public string text="old"; public int maxVisibleCharacters; }
public class FadeHarness {
 bool isChapterTransitioning, restoringCheckpointLine, omitOuterDialogueQuotes;
 float autoAdvanceAt;
 float chapterFadeOutDuration=1f, chapterFadeHoldDuration=1.7f, chapterFadeInDuration=1f;
 IEnumerator chapterTransitionCoroutine;
 Image chapterTransitionOverlay=new Image();
 Body bodyText=new Body();
 HashSet<string> shownConsultationLineIds=new HashSet<string>();
 List<string> events=new List<string>();
 int applied;
 Image EnsureChapterTransitionOverlay() { return chapterTransitionOverlay; }
 void StopTypingAndRevealText() {}
 void HideChoices() {}
 IEnumerator StartCoroutine(IEnumerator e) { return e; }
 IEnumerator FadeChapterTransition(float a,float b,float d) { events.Add($"fade:{a}:{b}:{d}"); yield return null; }
 void PrepareLine(DialogueLine l) { applied++; events.Add("prepare"); }
 string GetDialogueDisplayText(DialogueLine l,bool omit) { return l.text; }
 void StartTyping(string s) { Check(!isChapterTransitioning,"typing while locked"); events.Add("type:"+s); }
 public static void Check(bool b,string message) { if(!b) throw new Exception(message); }
 void Drain(IEnumerator e) { while(e.MoveNext()) { Check(isChapterTransitioning,"transition unlocked early"); if(e.Current is IEnumerator nested) Drain(nested); if(e.Current is WaitForSecondsRealtime wait) events.Add("hold:"+wait.seconds); } }
 public static void TestTransition() {
  var h=new FadeHarness(); var l=new DialogueLine {lineId="target",text="next",fadeBeforeLine=true,consultationTitle="title"};
  Check(h.TryStartLineFade(l),"not started"); Check(!h.TryStartLineFade(l),"nested transition");
  h.Drain(h.chapterTransitionCoroutine);
  Check(string.Join(",",h.events)=="fade:0:1:1,prepare,hold:1.7,fade:1:0:1,type:next","wrong default order");
  Check(h.applied==1 && !h.isChapterTransitioning && !h.chapterTransitionOverlay.active && h.chapterTransitionCoroutine==null,"cleanup/apply once");
  Check(h.shownConsultationLineIds.Contains("target"),"consultation duplicated");
  h.events.Clear(); l.lineFadeOutDuration=0; l.lineFadeHoldDuration=0; l.lineFadeInDuration=0.25f;
  Check(h.TryStartLineFade(l),"second start"); h.Drain(h.chapterTransitionCoroutine);
  Check(string.Join(",",h.events)=="fade:0:1:0,prepare,fade:1:0:0.25,type:next","zero/custom durations");
  l.fadeBeforeLine=false; Check(!h.TryStartLineFade(l),"disabled cue");
  l.fadeBeforeLine=true; h.restoringCheckpointLine=true; Check(!h.TryStartLineFade(l),"restore replays cue");
  h.restoringCheckpointLine=false; h.chapterTransitionOverlay=null; Check(!h.TryStartLineFade(l),"missing canvas fallback");
 }
 // Production methods inserted below.
'@
$source += "`n" + $methods + "`n}`n"
$source += @'
public static class Program {
 public static void Main() {
  System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.InvariantCulture;
  var old=DialogueRuntimeCsv.CreateScenario(new TextAsset("lineId,text\na,hello\n"));
  FadeHarness.Check(!old.Lines[0].fadeBeforeLine && old.Lines[0].lineFadeOutDuration==-1,"legacy default");
  var csv="lineId,text,fadeBeforeLine,lineFadeOutDuration,lineFadeHoldDuration,lineFadeInDuration\na,hello,true,0.5,0,1.25\n";
  var line=DialogueRuntimeCsv.CreateScenario(new TextAsset(csv)).Lines[0];
  FadeHarness.Check(line.fadeBeforeLine && line.lineFadeOutDuration==0.5f && line.lineFadeHoldDuration==0 && line.lineFadeInDuration==1.25f,"CSV settings");
  var settings=new Dictionary<string,string> { ["fadeBeforeLine"]="yes", ["lineFadeOutDuration"]="-1" };
  var shared=new DialogueLine {lineId="shared"}; DialogueRuntimeCsv.ApplyLineFadeSettings(shared,k=>settings.ContainsKey(k)?settings[k]:"");
  FadeHarness.Check(shared.fadeBeforeLine && shared.lineFadeOutDuration==-1 && shared.lineFadeInDuration==-1,"shared importer/defaults");
  foreach(var bad in new[]{"NaN","Infinity","-0.5","-2","abc"}) {
   settings["lineFadeOutDuration"]=bad; bool rejected=false;
   try { DialogueRuntimeCsv.ApplyLineFadeSettings(shared,k=>settings.ContainsKey(k)?settings[k]:""); } catch(FormatException) { rejected=true; }
   FadeHarness.Check(rejected,"accepted bad duration: "+bad);
  }
  settings.Clear(); settings["fadeBeforeLine"]="typo";
  bool invalid=false; try { DialogueRuntimeCsv.ApplyLineFadeSettings(shared,k=>settings.ContainsKey(k)?settings[k]:""); } catch(FormatException) { invalid=true; }
  FadeHarness.Check(invalid,"accepted bad boolean");
  FadeHarness.TestTransition();
  Console.WriteLine("PASS: legacy/new CSV, shared import, invalid values, fade/prepare/hold/reveal/type order, input lock, defaults/zero/custom timing, nested/restore guards, missing canvas, cleanup.");
 }
}
'@
$temp = Join-Path $root ('Temp/LineFadeChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
$testSource = Join-Path $temp 'Checks.cs'
[IO.File]::WriteAllText($testSource, $source, [Text.UTF8Encoding]::new($false))
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$exe = Join-Path $temp 'Checks.exe'
& $compiler /nologo /langversion:latest /nowarn:0414 /target:exe "/out:$exe" $testSource (Join-Path $runtime 'DialogueScenarioSO.cs') (Join-Path $runtime 'DialogueRuntimeCsv.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fade check compilation failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Fade checks failed' }
