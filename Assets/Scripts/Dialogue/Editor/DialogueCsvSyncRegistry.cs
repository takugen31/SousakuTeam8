using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
internal sealed class DialogueCsvSyncPair
{
    public string csvGuid;
    public string scenarioGuid;
    public string characterCsvGuid;
    public string databaseGuid;
    public string lastCsvName;
    public string lastScenarioName;
    public string lastContentHash;
    public bool enabled = true;
    [NonSerialized] public string lastError;
    [NonSerialized] public string lastNameError;
    [NonSerialized] public string lastContentError;

    internal T Load<T>(string guid) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
}

// GUIDs survive a rename/move; paths and matching filenames do not.
[FilePath("ProjectSettings/DialogueCsvSyncSettings.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class DialogueCsvSyncRegistry : ScriptableSingleton<DialogueCsvSyncRegistry>
{
    [SerializeField] private List<DialogueCsvSyncPair> pairs = new List<DialogueCsvSyncPair>();
    internal IReadOnlyList<DialogueCsvSyncPair> Pairs => pairs;

    internal void Persist() => Save(true);

    internal void ValidateRegistration(TextAsset characters, TextAsset csv,
        CharacterDatabaseSO database, DialogueScenarioSO scenario)
    {
        string csvGuid = RequireGuid(csv, ".csv");
        string scenarioGuid = RequireGuid(scenario, ".asset");
        string charactersGuid = RequireGuid(characters, ".csv");
        string databaseGuid = RequireGuid(database, ".asset");
        if (csvGuid == charactersGuid)
            throw new InvalidOperationException("会話CSVとキャラクターCSVには別のファイルを指定してください。");
        foreach (var pair in pairs)
        {
            bool samePair = pair.csvGuid == csvGuid && pair.scenarioGuid == scenarioGuid;
            if (!samePair && (pair.csvGuid == csvGuid || pair.scenarioGuid == scenarioGuid))
                throw new InvalidOperationException("このCSVまたは会話アセットは別の相手と登録済みです。先に登録を解除してください。");
            if (!samePair && pair.databaseGuid == databaseGuid && pair.characterCsvGuid != charactersGuid)
                throw new InvalidOperationException("同じキャラクターデータベースには同じキャラクターCSVを使ってください。");
        }
    }

    internal DialogueCsvSyncPair Register(TextAsset characters, TextAsset csv,
        CharacterDatabaseSO database, DialogueScenarioSO scenario)
    {
        ValidateRegistration(characters, csv, database, scenario);
        string csvGuid = RequireGuid(csv, ".csv");
        var pair = pairs.Find(item => item.csvGuid == csvGuid);
        if (pair == null)
        {
            pair = new DialogueCsvSyncPair();
            pairs.Add(pair);
        }
        pair.csvGuid = csvGuid;
        pair.scenarioGuid = RequireGuid(scenario, ".asset");
        pair.characterCsvGuid = RequireGuid(characters, ".csv");
        pair.databaseGuid = RequireGuid(database, ".asset");
        pair.lastCsvName = FileName(pair.csvGuid);
        pair.lastScenarioName = FileName(pair.scenarioGuid);
        pair.enabled = true;
        pair.lastError = null;
        pair.lastNameError = pair.lastContentError = null;
        pair.lastContentHash = DialogueCsvImportService.SourceHash(characters, csv);
        Persist();
        return pair;
    }

    internal void Remove(DialogueCsvSyncPair pair)
    {
        pairs.Remove(pair);
        Persist();
    }

    internal static string FileName(string guid) =>
        Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));

    private static string RequireGuid(UnityEngine.Object asset, string extension)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (asset == null || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(guid))
            throw new ArgumentException($"Assets 内の保存済み {extension} ファイルを指定してください。");
        return guid;
    }
}
