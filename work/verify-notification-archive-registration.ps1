$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content "$root/Assets/Scripts/Archive/ArchiveManager.cs" -Raw
$database = Get-Content "$root/Assets/Scripts/Archive/ArchiveDatabase.cs" -Raw
function Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start)
}
$methods = @(
    'public static void UnlockInformationTitle(', 'public bool UnlockEntry(',
    'public void ResetProgress(', 'private void UnlockInitialEntries(',
    'private void LoadState(', 'private void SaveState('
) | ForEach-Object { Method $_ }
$entryStart = $database.IndexOf('[Serializable]')
$entryEnd = $database.IndexOf('[CreateAssetMenu')
$entryClass = $database.Substring($entryStart, $entryEnd - $entryStart)
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable 0649
public class SerializeField : Attribute {}
public class TextArea : Attribute { public TextArea(int a, int b) {} }
public class Sprite {}
public enum ArchiveCategory { Person, Place, Clue, Record, Tips }
$entryClass
public class ArchiveDatabase {
 public List<ArchiveEntry> Entries = new List<ArchiveEntry>();
 public bool TryGetEntry(string id, out ArchiveEntry entry) { entry=Entries.Find(e=>e.Id==id); return entry!=null; }
 public void AddEntry(ArchiveEntry entry) { if(!TryGetEntry(entry.Id,out _)) Entries.Add(entry); }
 public void RemoveEntry(string id) { Entries.RemoveAll(e=>e.Id==id); }
}
public class ArchiveMenuUI {
 public Action<string> onNotice;
 public void ShowUnlockNotification(string title) { onNotice(title); }
}
public static class Debug { public static void LogWarning(string text) { throw new Exception(text); } }
public static class PlayerPrefs {
 public static string saved;
 public static bool HasKey(string key) => saved!=null;
 public static string GetString(string key) => saved;
 public static void SetString(string key,string value) { saved=value; }
 public static void Save() {}
}
// Snapshot stub isolates storage while exercising the real save/load entry lists.
public static class JsonUtility {
 public static object snapshot;
 public static string ToJson(object state) { snapshot=state; return "saved"; }
 public static T FromJson<T>(string json) { return (T)snapshot; }
}
public class ArchiveManager {
 const string SaveKey="ArchiveState.v1";
 public static ArchiveManager Instance;
 public ArchiveDatabase database=new ArchiveDatabase();
 readonly List<ArchiveEntry> acquiredInformation=new List<ArchiveEntry>();
 readonly HashSet<string> unlockedIds=new HashSet<string>();
 readonly HashSet<string> readIds=new HashSet<string>();
 public ArchiveMenuUI menu=new ArchiveMenuUI();
 public event Action<ArchiveEntry> EntryUnlocked;
 public event Action ArchiveChanged;
 class ArchiveSaveData {
  public List<string> unlockedIds,readIds;
  public List<ArchiveEntry> acquiredInformation;
 }
 $($methods -join "`n")
 public static void Check(bool value) { if(!value) throw new Exception("Archive registration assertion failed"); }
 public static void Run() {
  Instance=new ArchiveManager(); var first=Instance;
  first.database.AddEntry(new ArchiveEntry("known",ArchiveCategory.Record,"既存情報","","本文",1,false));
  int notices=0;
  first.menu.onNotice=title=> { notices++; Check(first.database.Entries.Any(e=>e.Title==title && first.unlockedIds.Contains(e.Id))); };
  UnlockInformationTitle("既存情報"); Check(first.database.Entries.Count==1);
  UnlockInformationTitle(" 新しい情報 "); UnlockInformationTitle("新しい情報");
  UnlockInformationTitle(" "); Check(notices==2 && first.database.Entries.Count==2);
  var entry=first.database.Entries.Find(e=>e.Title=="新しい情報");
  first.readIds.Add(entry.Id); first.SaveState();
  Instance=new ArchiveManager(); Instance.LoadState();
  Check(Instance.database.TryGetEntry(entry.Id,out var restored));
  Check(restored.Title==entry.Title && Instance.unlockedIds.Contains(entry.Id) && Instance.readIds.Contains(entry.Id));
  Instance.menu.onNotice=title=>throw new Exception("Duplicate notice after reload");
  UnlockInformationTitle("新しい情報"); Check(Instance.database.Entries.Count==1);
  Instance.ResetProgress(); Check(Instance.database.Entries.Count==0 && Instance.unlockedIds.Count==0);
 }
}
"@
[ArchiveManager]::Run()
if (-not $source.Contains('database = Instantiate(database)')) { throw 'Shared asset must not be mutated' }
'PASS: known/new information registered before toast, duplicate suppression, save/load metadata and read state, reset, runtime asset isolation.'
