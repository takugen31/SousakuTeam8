$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$saveSource = Get-Content (Join-Path $root 'Assets/Scripts/GameSaveService.cs') -Raw
$progressSource = Get-Content (Join-Path $root 'Assets/Scripts/GameProgress.cs') -Raw
$saveSource = [regex]::Replace($saveSource, '(?m)^using .*;\r?\n', '')
$progressSource = [regex]::Replace($progressSource, '(?m)^using .*;\r?\n', '')
# Compile production storage/validation/restore code against isolated in-memory Unity doubles.
# This never reads or modifies the real game's PlayerPrefs.
$stubs = @'
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEditor {
 public sealed class InitializeOnEnterPlayModeAttribute : Attribute {}
 public enum EnterPlayModeOptions { None }
}
namespace UnityEngine {
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} }
 public struct Vector2 { public float x,y; }
 public struct Vector3 { public float x,y,z; }
 public struct Rect { public float x,y,width,height; }
 public enum FindObjectsSortMode { None }
 public static class Object {
  public static Dictionary<Type,object> Objects = new Dictionary<Type,object>();
  public static T FindFirstObjectByType<T>() where T:class { return Objects.TryGetValue(typeof(T),out object o) ? (T)o : null; }
  public static T[] FindObjectsByType<T>(FindObjectsSortMode s) where T:class { T o=FindFirstObjectByType<T>(); return o == null ? Array.Empty<T>() : new[]{o}; }
 }
 public static class PlayerPrefs {
  public static Dictionary<string,string> Values = new Dictionary<string,string>();
  public static bool FailSave;
  public static bool HasKey(string k) => Values.ContainsKey(k);
  public static string GetString(string k,string fallback="") => Values.TryGetValue(k,out string v) ? v : fallback;
  public static void SetString(string k,string v) { Values[k]=v; }
  public static void DeleteKey(string k) { Values.Remove(k); }
  public static void Save() { if(FailSave) throw new Exception("simulated storage failure"); }
 }
 public static class JsonUtility {
  private static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
  public static string ToJson(object o) => System.Text.Json.JsonSerializer.Serialize(o,o.GetType(),Options);
  public static T FromJson<T>(string s) => System.Text.Json.JsonSerializer.Deserialize<T>(s,Options);
 }
 public static class Debug { public static void LogException(Exception e) {} }
 public static class Application { public static bool ValidScene=true; public static bool CanStreamedLevelBeLoaded(string s) => ValidScene; }
 public static class Time { public static float timeScale=1; }
}
namespace UnityEngine.SceneManagement {
 public struct Scene { public string name; }
 public enum LoadSceneMode { Single }
 public static class SceneManager {
  public static string Current="NovelScene"; public static bool FailLoad;
  public static Scene GetActiveScene() => new Scene { name=Current };
  public static void LoadScene(string s,LoadSceneMode mode) { if(FailLoad) throw new Exception("simulated scene failure"); Current=s; }
 }
}
public class FakeGameObject { public Scene scene => SceneManager.GetActiveScene(); }
public class NovelDialogueController {
 public FakeGameObject gameObject = new FakeGameObject(); public static string[] Names = new[]{"doute","kayo"}; public bool Ready=true;
 public bool CaptureCheckpoint(GameSaveData d) { if(!Ready) return false; d.kind="dialogue"; d.scenario="Chapter_Kayo_6"; d.lineId="chapter_kayo_6_035"; d.label="カヨとの会話"; d.text="test"; d.textFullyDisplayed=true; d.images=new[]{new GameSaveImage(),new GameSaveImage(),new GameSaveImage()}; return true; }
 public static string[] CaptureRevealedNames() => Names;
 public static void RestoreRevealedNames(string[] n) { Names=n; }
 public static void ResetStoryState() { Names=Array.Empty<string>(); }
}
public class Chapter1SearchController { public bool CaptureCheckpoint(GameSaveData d) { d.kind="search"; d.searchMode="modal"; d.itemId="door"; d.room=1; return true; } }
public class SearchSceneController { public bool CaptureCheckpoint(GameSaveData d) { d.kind="search"; d.searchMode="room"; d.room=0; d.paperVisible=true; return true; } }
public class SelectionPartController { public bool CaptureCheckpoint(GameSaveData d) { d.kind="selection"; d.introDismissed=true; return true; } }
public class KayoGaugeOutcomeController { public bool CurrentResult=true; }
public static class PuzzleGameFlowController { public static void ResetPendingRequest() {} }
public class AffectionEntry { public string characterId; public int value; }
public class AffectionManager {
 public static AffectionManager Instance=new AffectionManager(); public int Value=25;
 public void Save() { PlayerPrefs.SetString("AffectionState",JsonUtility.ToJson(new Payload{entries=new[]{new AffectionEntry{characterId="kayo",value=Value}}})); }
 public void Load() { Value=JsonUtility.FromJson<Payload>(PlayerPrefs.GetString("AffectionState")).entries[0].value; }
 public void ResetAll() { Value=0; Save(); }
 public class Payload { public AffectionEntry[] entries; }
}
public class ArchiveManager {
 public static ArchiveManager Instance=new ArchiveManager(); public int Reloads;
 public static void Close() {}
 public void ReloadProgress() { Reloads++; }
 public void ResetProgress() { PlayerPrefs.SetString("ArchiveState.v1","{\"unlockedIds\":[],\"readIds\":[]}"); }
 public void OpenArchive() {}
}
'@
Add-Type -TypeDefinition ($stubs + "`n" + $saveSource + "`n" + $progressSource) -CompilerOptions '/define:UNITY_EDITOR' -WarningAction SilentlyContinue
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$objects = [UnityEngine.Object]::Objects
$dialogue = [NovelDialogueController]::new()
$objects[[NovelDialogueController]] = $dialogue
$objects[[KayoGaugeOutcomeController]] = [KayoGaugeOutcomeController]::new()
[UnityEngine.PlayerPrefs]::SetString('ArchiveState.v1','{"unlockedIds":["chapter1.nameplate"],"readIds":["chapter1.nameplate"]}')
[UnityEngine.PlayerPrefs]::SetString('Chapter1Search.Acquired.v1','{"acquiredItemIds":["door"]}')
[UnityEngine.PlayerPrefs]::SetString('KayoSearch.Acquired.v1','{"acquiredItemIds":["bookshelf"]}')
$message = ''
for ($slot=0; $slot -lt 6; $slot++) {
    Assert ([GameSaveService]::Save($slot,[ref]$message)) "Save slot $slot failed: $message"
    $data = [GameSaveService]::Read($slot,[ref]$message)
    Assert ($data.lineId -eq 'chapter_kayo_6_035' -and $data.kayoGaugeSucceeded -and $data.revealedNames.Length -eq 2) 'Dialogue snapshot round-trip failed'
}
$objects[[KayoGaugeOutcomeController]].CurrentResult=$false
Assert ([GameSaveService]::Save(4,[ref]$message)) 'Failure outcome save failed'
Assert (-not [GameSaveService]::Read(4,[ref]$message).kayoGaugeSucceeded) 'Failure outcome became success'
$objects[[KayoGaugeOutcomeController]].CurrentResult=$true
Assert (-not [GameSaveService]::Save(6,[ref]$message)) 'Invalid slot accepted'
$old = [UnityEngine.PlayerPrefs]::GetString('GameSave.Slot.v1.0')
[UnityEngine.PlayerPrefs]::FailSave = $true
Assert (-not [GameSaveService]::Save(0,[ref]$message)) 'Failed storage reported success'
[UnityEngine.PlayerPrefs]::FailSave = $false
Assert ([UnityEngine.PlayerPrefs]::GetString('GameSave.Slot.v1.0') -eq $old) 'Failed overwrite destroyed old data'
[AffectionManager]::Instance.Value = 99
[UnityEngine.PlayerPrefs]::SetString('Chapter1Search.Acquired.v1','{"acquiredItemIds":["door","wall"]}')
Assert ([GameSaveService]::Load(0,[ref]$message)) 'Load failed'
Assert ([AffectionManager]::Instance.Value -eq 25) 'Affection not restored'
Assert ([UnityEngine.PlayerPrefs]::GetString('Chapter1Search.Acquired.v1') -eq '{"acquiredItemIds":["door"]}') 'Future acquisition leaked into loaded state'
Assert ($null -eq [GameSaveService]::Consume('SelectionPart','selection')) 'Wrong controller consumed checkpoint'
Assert ($null -ne [GameSaveService]::Consume('NovelScene','dialogue')) 'Resume missing'
Assert ($null -eq [GameSaveService]::Consume('NovelScene','dialogue')) 'Checkpoint replayed twice'
[GameProgress]::ResetAll()
Assert ($null -ne [GameSaveService]::Read(0,[ref]$message)) 'New game deleted manual slot'
foreach ($scene in @('falling_verKayo','falling_verYowashi','PuzzleGame','NovelScene_Moteru')) {
    [UnityEngine.SceneManagement.SceneManager]::Current=$scene
    Assert (-not [GameSaveService]::Save(0,[ref]$message)) "Unsupported scene saved: $scene"
}
[UnityEngine.SceneManagement.SceneManager]::Current='NovelScene'
$dialogue.Ready=$false
Assert (-not [GameSaveService]::Save(0,[ref]$message)) 'Transition scene saved'
$dialogue.Ready=$true
[UnityEngine.Application]::ValidScene=$false
Assert (-not [GameSaveService]::Load(0,[ref]$message)) 'Missing scene accepted'
[UnityEngine.Application]::ValidScene=$true
[AffectionManager]::Instance.Value=77
[UnityEngine.SceneManagement.SceneManager]::FailLoad=$true
Assert (-not [GameSaveService]::Load(0,[ref]$message)) 'Failed load reported success'
Assert ([AffectionManager]::Instance.Value -eq 77) 'Failed load did not roll back progress'
[UnityEngine.SceneManagement.SceneManager]::FailLoad=$false
$data=[GameSaveService]::Read(0,[ref]$message)
$data.version=2
Assert (-not [GameSaveService]::IsValid($data)) 'Unsupported version accepted'
$data.version=1
$data.progress[0].value='not json'
Assert (-not [GameSaveService]::IsValid($data)) 'Corrupt progress accepted'
[UnityEngine.PlayerPrefs]::SetString('GameSave.Slot.v1.5','bad json')
Assert ($null -eq [GameSaveService]::Read(5,[ref]$message)) 'Corrupt slot accepted'
$objects.Clear()
[UnityEngine.SceneManagement.SceneManager]::Current='SelectionPart'
$objects[[SelectionPartController]]=[SelectionPartController]::new()
Assert ([GameSaveService]::Save(1,[ref]$message)) 'Selection save failed'
Assert ([GameSaveService]::Read(1,[ref]$message).introDismissed) 'Selection intro flag lost'
$objects.Clear()
[UnityEngine.SceneManagement.SceneManager]::Current='Chapter1_SearchScene'
$objects[[Chapter1SearchController]]=[Chapter1SearchController]::new()
Assert ([GameSaveService]::Save(2,[ref]$message)) 'Search modal save failed'
Assert ([GameSaveService]::Read(2,[ref]$message).itemId -eq 'door') 'Modal item lost'
$objects.Clear()
[UnityEngine.SceneManagement.SceneManager]::Current='Chapter_Kayo_SearchScene'
$objects[[SearchSceneController]]=[SearchSceneController]::new()
Assert ([GameSaveService]::Save(3,[ref]$message)) 'Kayo search save failed'
Assert ([GameSaveService]::Read(3,[ref]$message).paperVisible) 'Paper background lost'
$reset=[GameSaveService].GetMethod('ClearSlotsOnPlay',[Reflection.BindingFlags]'NonPublic,Static')
$reset.Invoke($null,@([UnityEditor.EnterPlayModeOptions]::None)) | Out-Null
for ($slot=0; $slot -lt 6; $slot++) { Assert (-not [UnityEngine.PlayerPrefs]::HasKey("GameSave.Slot.v1.$slot")) 'Play-mode reset failed' }
$novel = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
Assert ($novel.Contains('if (!restoringCheckpointLine)')) 'Resume can reapply affection/archive effects'
Assert ($novel.Contains('gauge.SetResult(data.kayoGaugeSucceeded)')) 'Gauge outcome restoration missing'
foreach ($script in @('Chapter1SearchController','SearchSceneController')) {
    $search = Get-Content (Join-Path $root "Assets/Scripts/Search/$script.cs") -Raw
    Assert ($search.IndexOf('GameSaveService.Consume') -lt $search.IndexOf('bool hasCompletedSearch')) 'Search completion fires before restoring modal/dialogue'
}
'PASS: 6 slots, JSON round-trips, progress rollback, corruption handling, one-shot resume, selection/search checkpoints, editor reset, no duplicate effects.'
$saveUI = Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveMenuUI.SaveLoad.cs') -Raw
Assert (-not $saveUI.Contains('保存する枠を選んでください') -and -not $saveUI.Contains('読み込む枠を選んでください')) 'Unwanted slot instructions remain'
Assert ($saveUI.Contains('backLabel.alignment = TextAlignmentOptions.Center') -and $saveUI.Contains('"← メニュー", 22f')) 'Back label is not sized and centered'
'PASS: slot instructions removed; back-to-menu label centered with smaller type.'
$title = Get-Content (Join-Path $root 'Assets/Scripts/TitleMenu.cs') -Raw
Assert ($title.Contains('load.onClick = new Button.ButtonClickedEvent()') -and $title.Contains('load.onClick.AddListener(OpenLoad)')) 'Title load button retains serialized StartGame callback'
$openLoad = $title.Substring($title.IndexOf('    public void OpenLoad()'))
$openLoad = $openLoad.Substring(0, $openLoad.IndexOf('    private void Start()'))
Assert ($openLoad.Contains('OpenLoadFromTitle') -and -not $openLoad.Contains('ResetAll')) 'Title load entry resets progress'
Assert ($saveUI.Contains('saveLoadBackLabel.text = "← タイトル"') -and $saveUI.Contains('if (loadOpenedFromTitle) manager.CloseArchive()')) 'Title load back action does not return to title'
'PASS: title load entry uses shared slots without new-game reset; back returns to title.'
