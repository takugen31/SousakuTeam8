using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DialogueCsvSyncProcessor
{
    [Flags] private enum Operation { None = 0, Names = 1, Content = 2, Export = 4, AutoContent = 8, Both = Names | AutoContent }
    // Manual commands defer the other operation for the currently observed changes.
    // A later genuine edit/rename or an explicit command releases that deferral.
    private static readonly Dictionary<string, string> deferredContent = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> deferredNames = new Dictionary<string, string>();
    private static bool queued, reportSummary, processing;
    private static Operation requested;
    private static double nextPoll;

    static DialogueCsvSyncProcessor()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) RequestAll(false);
        };
        RequestAll(false);
        // Inspector edits can remain dirty in memory without an asset import event.
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup < nextPoll || !CanSync()) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            Queue();
        };
    }

    [MenuItem("Tools/Dialogue/Sync Registered Names Only")]
    private static void RenameAllMenu()
    {
        RequestNames(true);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Dialogue/Update Registered Contents (CSV → Assets)")]
    private static void UpdateAllMenu()
    {
        RequestContent(true);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Dialogue/Update Registered Contents (Assets → CSV)")]
    private static void ExportAllMenu()
    {
        RequestExport(true);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Dialogue/Sync Registered Names Only", true)]
    [MenuItem("Tools/Dialogue/Update Registered Contents (CSV → Assets)", true)]
    [MenuItem("Tools/Dialogue/Update Registered Contents (Assets → CSV)", true)]
    private static bool CanSync() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    internal static void RequestAll(bool report) => Request(Operation.Both, report);
    internal static void RequestNames(bool report) => Request(Operation.Names, report);
    internal static void RequestContent(bool report) => Request(Operation.Content, report);
    internal static void RequestExport(bool report) => Request(Operation.Export, report);

    private static void Request(Operation operation, bool report)
    {
        requested |= operation;
        reportSummary |= report;
        Queue();
    }

    internal static void AssetsChanged(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (processing) return;
        if (imported.Length + deleted.Length + moved.Length + movedFrom.Length > 0) Queue();
    }

    private static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += ProcessPending;
    }

    private static void ProcessPending()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (processing || EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
        Operation operation = requested;
        bool report = reportSummary;
        requested = Operation.None;
        reportSummary = false;
        int renamed = 0, updated = 0, failed = 0;
        processing = true;
        try
        {
            foreach (var pair in DialogueCsvSyncRegistry.instance.Pairs)
            {
                if (!pair.enabled) continue;
                string currentNames = NamesSnapshot(pair);
                string currentHash = SourceHash(pair);
                var scenario = pair.Load<DialogueScenarioSO>(pair.scenarioGuid);
                string scenarioHash = scenario == null ? null : DialogueCsvExportService.ContentHash(scenario.Lines);
                string contentSnapshot = currentHash + "\0" + scenarioHash;
                bool missingContentFile = currentHash == null || scenario == null || pair.Load<CharacterDatabaseSO>(pair.databaseGuid) == null;
                bool auto = operation == Operation.None || (operation & Operation.AutoContent) != 0;
                if (operation == Operation.Names) deferredContent[pair.csvGuid] = contentSnapshot;
                if ((operation & (Operation.Content | Operation.Export)) != 0) deferredNames[pair.csvGuid] = currentNames;
                bool doNames = (operation & Operation.Names) != 0 ||
                    (auto && !IsDeferred(deferredNames, pair.csvGuid, currentNames));
                bool csvChanged = currentHash != pair.lastContentHash;
                // Old registries have no asset baseline. Compare to the imported CSV, preserving asset-only edits.
                if (string.IsNullOrEmpty(pair.lastScenarioHash) && !missingContentFile && !csvChanged)
                {
                    try
                    {
                        pair.lastScenarioHash = DialogueCsvExportService.ContentHash(DialogueCsvImportService.Prepare(
                            pair.Load<TextAsset>(pair.characterCsvGuid), pair.Load<TextAsset>(pair.csvGuid)).Lines);
                        DialogueCsvSyncRegistry.instance.Persist();
                    }
                    catch (Exception exception) { ReportError(pair, exception, false, report); }
                }
                bool assetChanged = scenarioHash != pair.lastScenarioHash;
                bool doContent = (operation & (Operation.Content | Operation.Export)) != 0 ||
                    (auto && !IsDeferred(deferredContent, pair.csvGuid, contentSnapshot) &&
                    (missingContentFile || csvChanged || assetChanged));
                if (doNames)
                {
                    deferredNames.Remove(pair.csvGuid);
                    try { if (SynchronizeNames(pair, operation == Operation.Names)) renamed++; pair.lastNameError = null; }
                    catch (Exception exception) { failed++; ReportError(pair, exception, true, report); }
                }
                if (doContent)
                {
                    deferredContent.Remove(pair.csvGuid);
                    try
                    {
                        // Explicit CSV wins if both manual directions are queued; automatic conflicts also prefer CSV.
                        bool export = (operation & Operation.Content) == 0 &&
                            ((operation & Operation.Export) != 0 || (!missingContentFile && !csvChanged && assetChanged));
                        if (export) SynchronizeAssetToCsv(pair); else SynchronizeContent(pair);
                        updated++; pair.lastContentError = null;
                    }
                    catch (Exception exception) { failed++; ReportError(pair, exception, false, report); }
                }
                pair.lastError = string.IsNullOrEmpty(pair.lastNameError) ? pair.lastContentError :
                    pair.lastNameError + (string.IsNullOrEmpty(pair.lastContentError) ? "" : "\n" + pair.lastContentError);
            }
        }
        finally { processing = false; }
        if (report) Debug.Log($"CSV同期: 改名 {renamed} 組、内容更新 {updated} 組、失敗 {failed} 件。");
        DialogueCsvSyncWindow.RepaintOpenWindow();
    }

    private static bool IsDeferred(Dictionary<string, string> snapshots, string guid, string current) =>
        snapshots.TryGetValue(guid, out string previous) && previous == current;

    private static string NamesSnapshot(DialogueCsvSyncPair pair) =>
        DialogueCsvSyncRegistry.FileName(pair.csvGuid) + "\0" + DialogueCsvSyncRegistry.FileName(pair.scenarioGuid);

    private static string SourceHash(DialogueCsvSyncPair pair)
    {
        string csvPath = AssetDatabase.GUIDToAssetPath(pair.csvGuid);
        string characterPath = AssetDatabase.GUIDToAssetPath(pair.characterCsvGuid);
        // Detect disk edits even before Unity's next automatic import, so an Inspector
        // change cannot overwrite an externally edited CSV using a stale TextAsset.
        if (!File.Exists(csvPath) || !File.Exists(characterPath)) return null;
        try { return DialogueCsvImportService.SourceHash(File.ReadAllText(characterPath), File.ReadAllText(csvPath)); }
        catch (IOException) { return null; }
    }

    private static void ReportError(DialogueCsvSyncPair pair, Exception exception, bool names, bool report)
    {
        string error = $"{(names ? "改名" : "内容更新")}: {pair.lastCsvName} / {pair.lastScenarioName}\n{exception.Message}";
        string previous = names ? pair.lastNameError : pair.lastContentError;
        if (error != previous || report) Debug.LogError(error);
        if (names) pair.lastNameError = error; else pair.lastContentError = error;
    }

    internal static void SynchronizeContent(DialogueCsvSyncPair pair)
    {
        RefreshCsvFiles(pair);
        var csv = pair.Load<TextAsset>(pair.csvGuid);
        var scenario = pair.Load<DialogueScenarioSO>(pair.scenarioGuid);
        var characters = pair.Load<TextAsset>(pair.characterCsvGuid);
        var database = pair.Load<CharacterDatabaseSO>(pair.databaseGuid);
        if (csv == null || scenario == null || characters == null || database == null)
            throw new InvalidOperationException("登録ファイルが見つかりません。削除や .meta の変更を確認してください。");
        var prepared = DialogueCsvImportService.Prepare(characters, csv);
        DialogueCsvImportService.Apply(prepared, database, scenario, false);
        pair.lastContentHash = DialogueCsvImportService.SourceHash(characters, csv);
        pair.lastScenarioHash = DialogueCsvExportService.ContentHash(scenario.Lines);
        DialogueCsvSyncRegistry.instance.Persist();
    }

    internal static void SynchronizeAssetToCsv(DialogueCsvSyncPair pair)
    {
        RefreshCsvFiles(pair);
        var csv = pair.Load<TextAsset>(pair.csvGuid);
        var scenario = pair.Load<DialogueScenarioSO>(pair.scenarioGuid);
        var characters = pair.Load<TextAsset>(pair.characterCsvGuid);
        if (csv == null || scenario == null || characters == null || pair.Load<CharacterDatabaseSO>(pair.databaseGuid) == null)
            throw new InvalidOperationException("登録ファイルが見つかりません。削除や .meta の変更を確認してください。");
        string text = DialogueCsvExportService.Export(scenario, csv, characters);
        DialogueCsvImportService.EnsureWritable(csv);
        DialogueCsvImportService.EnsureWritable(scenario);
        AssetDatabase.SaveAssetIfDirty(scenario);
        string path = AssetDatabase.GetAssetPath(csv);
        if (File.ReadAllText(path) != text) File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        pair.lastContentHash = SourceHash(pair);
        pair.lastScenarioHash = DialogueCsvExportService.ContentHash(scenario.Lines);
        DialogueCsvSyncRegistry.instance.Persist();
    }

    private static void RefreshCsvFiles(DialogueCsvSyncPair pair)
    {
        foreach (string guid in new[] { pair.csvGuid, pair.characterCsvGuid })
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    internal static void RunPairManually(DialogueCsvSyncPair pair, bool namesOnly, bool assetToCsv = false)
    {
        if (namesOnly)
        {
            var scenario = pair.Load<DialogueScenarioSO>(pair.scenarioGuid);
            deferredContent[pair.csvGuid] = SourceHash(pair) + "\0" +
                (scenario == null ? null : DialogueCsvExportService.ContentHash(scenario.Lines));
            deferredNames.Remove(pair.csvGuid);
        }
        else
        {
            deferredNames[pair.csvGuid] = NamesSnapshot(pair);
            deferredContent.Remove(pair.csvGuid);
        }
        processing = true;
        try
        {
            if (namesOnly) { SynchronizeNames(pair, true); pair.lastNameError = null; }
            else
            {
                if (assetToCsv) SynchronizeAssetToCsv(pair); else SynchronizeContent(pair);
                pair.lastContentError = null;
            }
        }
        catch (Exception exception) { ReportError(pair, exception, namesOnly, true); }
        finally { processing = false; }
        pair.lastError = string.IsNullOrEmpty(pair.lastNameError) ? pair.lastContentError :
            pair.lastNameError + (string.IsNullOrEmpty(pair.lastContentError) ? "" : "\n" + pair.lastContentError);
    }

    internal static bool SynchronizeNames(DialogueCsvSyncPair pair, bool normalizeExisting = false)
    {
        if (pair.Load<TextAsset>(pair.csvGuid) == null || pair.Load<DialogueScenarioSO>(pair.scenarioGuid) == null)
            throw new InvalidOperationException("登録したCSVまたは会話アセットが見つかりません。削除や .meta の変更を確認してください。");
        string csvName = DialogueCsvSyncRegistry.FileName(pair.csvGuid);
        string scenarioName = DialogueCsvSyncRegistry.FileName(pair.scenarioGuid);
        bool csvRenamed = csvName != pair.lastCsvName;
        bool scenarioRenamed = scenarioName != pair.lastScenarioName;
        if (csvRenamed && scenarioRenamed && ToCsvName(csvName) != ToCsvName(scenarioName))
            throw new InvalidOperationException("CSVとアセットの名前が同時に別々の名前へ変わりました。大文字・小文字を除いて対応する名前に直してください。");
        if (!csvRenamed && !scenarioRenamed && !normalizeExisting) return false;
        // Without a detected rename, an explicit names-only command uses the CSV
        // as the base name and repairs both files' spelling/capitalization.
        string sourceName = scenarioRenamed && !csvRenamed ? scenarioName : csvName;
        string targetCsvName = ToCsvName(sourceName);
        string targetScenarioName = ToScenarioName(sourceName);
        if (!csvRenamed && !scenarioRenamed && csvName == targetCsvName && scenarioName == targetScenarioName)
            return false;
        if (csvName != targetCsvName) CheckRename(pair.csvGuid, targetCsvName);
        if (scenarioName != targetScenarioName) CheckRename(pair.scenarioGuid, targetScenarioName);
        if (csvName != targetCsvName) RenameChecked(pair.csvGuid, targetCsvName);
        if (scenarioName != targetScenarioName) RenameChecked(pair.scenarioGuid, targetScenarioName);
        pair.lastCsvName = DialogueCsvSyncRegistry.FileName(pair.csvGuid);
        pair.lastScenarioName = DialogueCsvSyncRegistry.FileName(pair.scenarioGuid);
        DialogueCsvSyncRegistry.instance.Persist();
        return true;
    }

    internal static string ToCsvName(string name) => name.ToLowerInvariant();

    internal static string ToScenarioName(string name)
    {
        char[] characters = ToCsvName(name).ToCharArray();
        for (int i = 0; i < characters.Length; i++)
            if (i == 0 || characters[i - 1] == '_') characters[i] = char.ToUpperInvariant(characters[i]);
        return new string(characters);
    }

    private static void CheckRename(string guid, string name)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        string target = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name + Path.GetExtension(path);
        string existingGuid = AssetDatabase.AssetPathToGUID(target);
        if (!string.IsNullOrEmpty(existingGuid) && existingGuid != guid)
            throw new InvalidOperationException($"変更先の名前が既に使われています: {target}");
        DialogueCsvImportService.EnsureWritable(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
    }

    private static void RenameChecked(string guid, string name)
    {
        CheckRename(guid, name);
        string path = AssetDatabase.GUIDToAssetPath(guid);
        string error = AssetDatabase.RenameAsset(path, name);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }
}

internal sealed class DialogueCsvSyncPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        DialogueCsvSyncProcessor.AssetsChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
    }
}
