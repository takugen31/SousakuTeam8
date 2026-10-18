// Copied into an isolated Unity project by verify-dialogue-csv-sync.ps1.
// Never run this against the real game's Assets or registration settings.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DialogueCsvSyncIntegrationChecks
{
    private static readonly Queue<Action> steps = new Queue<Action>();
    private static double nextStep;
    private static double deadline;
    private static DialogueCsvSyncPair pair;
    private static DialogueScenarioSO scenario;
    private static CharacterDatabaseSO database;
    private static TextAsset characters;
    private static string expectedText;
    private static string root = "Assets/Checks";
    private static int passed;

    public static void Run()
    {
        if (!Application.dataPath.Contains("DialogueCsvSyncChecks-"))
            throw new InvalidOperationException("These checks require the isolated test project.");
        deadline = EditorApplication.timeSinceStartup + 150;
        steps.Enqueue(Setup);
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "Scenario" && Name(pair.scenarioGuid) == "sCENARIO", "registration unexpectedly normalized filenames");
            scenario.Lines[0].text = "initial local edit";
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Sync Registered Names Only"), "names-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "scenario" && Name(pair.scenarioGuid) == "Scenario", "unchanged registered filenames were not normalized");
            Check(scenario.Lines[0].text == "initial local edit", "initial normalization changed content");
            Check(!DialogueCsvSyncProcessor.SynchronizeNames(pair, true), "repeated normalization was not idempotent");
        });
        steps.Enqueue(() => WriteDialogue("edited"));
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "edited", "CSV edit not detected");
            Check(scenario.Lines[0].fadeBeforeLine && scenario.Lines[0].lineFadeHoldDuration == 0.3f, "fade settings lost");
            Check(scenario.Lines[0].affectionChanges[0].value == 2 && scenario.Lines[1].choices.Count == 1, "full importer semantics lost");
            Check(scenario.Lines[0].archiveUnlockIds[0] == "entry", "archive IDs lost");
            Rename(pair.csvGuid, "chapter_moteru_2");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.scenarioGuid) == "Chapter_Moteru_2", "CSV rename not capitalized per word");
            Rename(pair.scenarioGuid, "Chapter_Moteru_3");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "chapter_moteru_3", "asset rename did not produce lowercase CSV");
            Rename(pair.csvGuid, "CHAPTER_MOTERU_3");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "chapter_moteru_3" && Name(pair.scenarioGuid) == "Chapter_Moteru_3", "CSV case-only rename not normalized");
            Rename(pair.scenarioGuid, "chapter_moteru_3");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "chapter_moteru_3" && Name(pair.scenarioGuid) == "Chapter_Moteru_3", "asset case-only rename not normalized");
            Check(!DialogueCsvSyncProcessor.SynchronizeNames(pair), "canonical names triggered another sync");
            scenario.Lines[0].text = "asset local edit";
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            WriteDialogue("names-only pending");
            Rename(pair.csvGuid, "names_only_check");
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Sync Registered Names Only"), "names-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.scenarioGuid) == "Names_Only_Check" && scenario.Lines[0].text == "asset local edit", "names-only menu updated content");
            Check(pair.lastContentHash != DialogueCsvImportService.SourceHash(characters, pair.Load<TextAsset>(pair.csvGuid)), "rename marked content as applied");
            Rename(pair.csvGuid, "pending_content_name");
            WriteDialogue("contents-only pending");
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Update Registered Contents (CSV → Assets)"), "contents-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "pending_content_name" && Name(pair.scenarioGuid) == "Names_Only_Check", "contents-only menu renamed a file");
            Check(scenario.Lines[0].text == "contents-only pending", "contents-only menu did not apply CSV");
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Sync Registered Names Only"), "names-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.scenarioGuid) == "Pending_Content_Name" && scenario.Lines[0].text == "contents-only pending", "deferred name change was lost");
            File.WriteAllText(AssetDatabase.GUIDToAssetPath(pair.csvGuid), "lineId,speakerId,expressionId,text,nextLineId\na,doute,normal,broken,missing\n");
            AssetDatabase.Refresh();
            Rename(pair.csvGuid, "invalid_csv_names");
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Sync Registered Names Only"), "names-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.scenarioGuid) == "Invalid_Csv_Names" && scenario.Lines[0].text == "contents-only pending", "invalid CSV prevented name-only sync or damaged content");
            WriteDialogue("edited");
            Rename(pair.csvGuid, "chapter_moteru_3");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.scenarioGuid) == "Chapter_Moteru_3" && scenario.Lines[0].text == "edited", "automatic sync did not resume after a new edit");
            string error = AssetDatabase.MoveAsset(root, "Assets/MovedChecks");
            Check(string.IsNullOrEmpty(error), error);
            root = "Assets/MovedChecks";
        });
        steps.Enqueue(() => WriteDialogue("after move"));
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "after move", "folder move broke the association");
            Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scenario)) == pair.scenarioGuid, "scenario GUID changed");
            File.WriteAllText(AssetDatabase.GUIDToAssetPath(pair.characterCsvGuid), CharacterCsv("Renamed character"));
            AssetDatabase.Refresh();
        });
        steps.Enqueue(() =>
        {
            Check(database.Characters[0].displayName == "Renamed character", "character CSV edit not detected");
            string csvBefore = File.ReadAllText(AssetDatabase.GUIDToAssetPath(pair.csvGuid));
            scenario.Lines[0].text = "asset only";
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            Check(File.ReadAllText(AssetDatabase.GUIDToAssetPath(pair.csvGuid)) == csvBefore, "asset content flowed back into CSV");
            Check(EditorApplication.ExecuteMenuItem("Tools/Dialogue/Update Registered Contents (CSV → Assets)"), "contents-only menu missing");
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "after move", "manual sync did not restore CSV content");
            string assetPath = AssetDatabase.GetAssetPath(scenario);
            string before = File.ReadAllText(assetPath);
            DialogueCsvSyncProcessor.SynchronizeContent(pair);
            Check(File.ReadAllText(assetPath) == before && !EditorUtility.IsDirty(scenario), "unchanged sync rewrote asset");
            File.WriteAllText(AssetDatabase.GUIDToAssetPath(pair.csvGuid), "lineId,speakerId,expressionId,text,nextLineId\na,doute,normal,broken,missing\n");
            AssetDatabase.Refresh();
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "after move" && !string.IsNullOrEmpty(pair.lastError), "invalid CSV damaged previous data or lacked an error");
            WriteDialogue("recovered");
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "recovered" && string.IsNullOrEmpty(pair.lastError), "corrected CSV did not recover");
            var other = ScriptableObject.CreateInstance<DialogueScenarioSO>();
            AssetDatabase.CreateAsset(other, root + "/OtherTarget.asset");
            bool rejected = false;
            try { DialogueCsvSyncRegistry.instance.ValidateRegistration(characters, pair.Load<TextAsset>(pair.csvGuid), database, other); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "one CSV accepted two targets");
            rejected = false;
            File.WriteAllText(root + "/Other.csv", Dialogue("other"));
            AssetDatabase.Refresh();
            try { DialogueCsvSyncRegistry.instance.ValidateRegistration(characters, AssetDatabase.LoadAssetAtPath<TextAsset>(root + "/Other.csv"), database, scenario); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "one target accepted two CSVs");
            Rename(pair.scenarioGuid, "Other");
        });
        steps.Enqueue(() =>
        {
            Check(Name(pair.csvGuid) == "chapter_moteru_3" && !string.IsNullOrEmpty(pair.lastError), "name collision overwrote the other CSV");
            Check(File.ReadAllText(root + "/Other.csv").Contains("other"), "unregistered CSV was overwritten");
            Rename(pair.scenarioGuid, "Chapter_Moteru_3");
        });
        steps.Enqueue(() =>
        {
            Check(string.IsNullOrEmpty(pair.lastError), "rename collision did not recover");
            pair.enabled = false;
            DialogueCsvSyncRegistry.instance.Persist();
            WriteDialogue("paused edit");
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "recovered", "disabled pair was synchronized");
            pair.enabled = true;
            DialogueCsvSyncRegistry.instance.Persist();
            DialogueCsvSyncProcessor.RequestAll(false);
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "paused edit", "reenabled pair did not synchronize");
            // Exercise simultaneous renames without allowing the queued processor to run between them.
            Rename(pair.csvGuid, "simultaneous_csv");
            Rename(pair.scenarioGuid, "Simultaneous_Asset");
            bool rejected = false;
            try { DialogueCsvSyncProcessor.SynchronizeNames(pair); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && Name(pair.csvGuid) == "simultaneous_csv" && Name(pair.scenarioGuid) == "Simultaneous_Asset", "ambiguous simultaneous rename was guessed");
            Rename(pair.scenarioGuid, "Simultaneous_Csv");
        });
        steps.Enqueue(() =>
        {
            Check(pair.lastCsvName == "simultaneous_csv" && pair.lastScenarioName == "Simultaneous_Csv", "matching simultaneous rename did not settle");
            File.SetAttributes(AssetDatabase.GetAssetPath(scenario), FileAttributes.ReadOnly);
            WriteDialogue("read-only edit");
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "paused edit" && !string.IsNullOrEmpty(pair.lastError), "read-only target was modified");
            File.SetAttributes(AssetDatabase.GetAssetPath(scenario), FileAttributes.Normal);
            DialogueCsvSyncProcessor.RequestAll(false);
        });
        steps.Enqueue(() =>
        {
            Check(scenario.Lines[0].text == "read-only edit" && string.IsNullOrEmpty(pair.lastError), "read-only error did not recover");
            Check(File.Exists("ProjectSettings/DialogueCsvSyncSettings.asset"), "registry not persisted");
            string settings = File.ReadAllText("ProjectSettings/DialogueCsvSyncSettings.asset");
            Check(settings.Contains(pair.csvGuid) && settings.Contains(pair.scenarioGuid), "GUID association not persisted");
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(pair.databaseGuid));
        });
        steps.Enqueue(() =>
        {
            Check(!string.IsNullOrEmpty(pair.lastContentError) && scenario.Lines[0].text == "read-only edit", "database deletion was not detected or damaged the scenario");
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(pair.csvGuid));
        });
        steps.Enqueue(() =>
        {
            Check(AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>(AssetDatabase.GetAssetPath(scenario)) != null, "CSV deletion deleted the target");
            Check(!string.IsNullOrEmpty(pair.lastError) && DialogueCsvSyncRegistry.instance.Pairs.Count == 1, "missing pair was silently dropped");
            DialogueCsvSyncRegistry.instance.Remove(pair);
            Check(DialogueCsvSyncRegistry.instance.Pairs.Count == 0 && scenario != null, "unregister deleted files");
        });
        EditorApplication.update += Tick;
    }

    private static void Setup()
    {
        AssetDatabase.CreateFolder("Assets", "Checks");
        File.WriteAllText(root + "/Characters.csv", CharacterCsv("Original"));
        File.WriteAllText(root + "/Scenario.csv", Dialogue("initial"));
        AssetDatabase.Refresh();
        characters = AssetDatabase.LoadAssetAtPath<TextAsset>(root + "/Characters.csv");
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(root + "/Scenario.csv");
        database = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
        scenario = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        AssetDatabase.CreateAsset(database, root + "/Database.asset");
        AssetDatabase.CreateAsset(scenario, root + "/sCENARIO.asset");
        var prepared = DialogueCsvImportService.Prepare(characters, csv);
        DialogueCsvImportService.Apply(prepared, database, scenario, false);
        pair = DialogueCsvSyncRegistry.instance.Register(characters, csv, database, scenario);
        Check(scenario.Lines[0].text == "initial", "initial import failed");
    }

    private static string CharacterCsv(string name) =>
        "characterId,displayName,nameColor,nameKnownInitially,expressionId,portraitPath\n" +
        "doute," + name + ",#FFFFFF,true,normal,\n";

    private static string Dialogue(string text)
    {
        string[] headers = { "lineId", "speakerId", "expressionId", "backgroundPath", "text", "nextLineId",
            "affectionChanges", "branches", "revealSpeakerName", "nextScenePath", "archiveUnlockIds",
            "fadeBeforeLine", "lineFadeOutDuration", "lineFadeHoldDuration", "lineFadeInDuration", "choice1Text", "choice1NextLineId" };
        string[] first = { "a", "doute", "normal", "", text, "b", "doute+2", "", "", "", "entry", "true", "0.2", "0.3", "0.4", "", "" };
        string[] second = { "b", "doute", "normal", "", "second", "", "", "", "", "", "", "", "", "", "", "Again", "a" };
        return string.Join(",", headers) + "\n" + string.Join(",", first) + "\n" + string.Join(",", second) + "\n";
    }

    private static void WriteDialogue(string text)
    {
        expectedText = text;
        File.WriteAllText(AssetDatabase.GUIDToAssetPath(pair.csvGuid), Dialogue(text));
        AssetDatabase.Refresh();
    }

    private static string Name(string guid) => DialogueCsvSyncRegistry.FileName(guid);

    private static void Rename(string guid, string name)
    {
        string error = AssetDatabase.RenameAsset(AssetDatabase.GUIDToAssetPath(guid), name);
        Check(string.IsNullOrEmpty(error), error);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Sync integration checks timed out");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextStep) return;
            if (steps.Count == 0)
            {
                Debug.Log($"PASS: Dialogue CSV sync integration ({passed} steps): edits, full parser, bidirectional rename, folder move, character CSV, manual CSV authority, unchanged import, errors/recovery, duplicate association, collisions, pause, simultaneous rename, persistence, deletion, unregister.");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
                return;
            }
            steps.Dequeue()();
            passed++;
            nextStep = EditorApplication.timeSinceStartup + 0.8;
        }
        catch (Exception exception)
        {
            Debug.LogError($"FAIL: sync integration step {passed + 1}, last CSV text {expectedText}\n{exception}");
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
