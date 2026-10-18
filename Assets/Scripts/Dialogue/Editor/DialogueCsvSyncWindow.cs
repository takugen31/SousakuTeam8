using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class DialogueCsvSyncWindow : EditorWindow
{
    [SerializeField] private TextAsset characterCsv;
    [SerializeField] private CharacterDatabaseSO database;
    [SerializeField] private TextAsset dialogueCsv;
    [SerializeField] private DialogueScenarioSO scenario;
    [SerializeField] private string csvFolder = "Assets/Dialogue/CSV";
    [SerializeField] private string assetFolder = "Assets/Dialogue/Data";
    private Vector2 scroll;

    [MenuItem("Tools/Dialogue/CSV Sync Settings")]
    internal static void OpenWindow() => GetWindow<DialogueCsvSyncWindow>("Dialogue CSV Sync");

    internal static void RepaintOpenWindow()
    {
        if (HasOpenInstances<DialogueCsvSyncWindow>()) GetWindow<DialogueCsvSyncWindow>().Repaint();
    }

    private void OnEnable()
    {
        if (characterCsv == null)
            characterCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dialogue/CSV/Characters.csv");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("内容は常に CSV → .asset。改名時はCSVを小文字、アセットは _ 区切りの各語の先頭を大文字にします。\n" +
            "最初にペアを登録してください。未登録のファイルは自動で変更しません。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("登録に使うキャラクター設定", EditorStyles.boldLabel);
            characterCsv = (TextAsset)EditorGUILayout.ObjectField("Character CSV", characterCsv, typeof(TextAsset), false);
            database = (CharacterDatabaseSO)EditorGUILayout.ObjectField("Character Database", database, typeof(CharacterDatabaseSO), false);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1組ずつ登録する（名前が違っていても可）", EditorStyles.boldLabel);
            dialogueCsv = (TextAsset)EditorGUILayout.ObjectField("Dialogue CSV", dialogueCsv, typeof(TextAsset), false);
            scenario = (DialogueScenarioSO)EditorGUILayout.ObjectField("Dialogue Scenario", scenario, typeof(DialogueScenarioSO), false);
            using (new EditorGUI.DisabledScope(characterCsv == null || database == null || dialogueCsv == null || scenario == null))
            {
                if (GUILayout.Button("このペアを登録し、CSVから同期"))
                    TryRegister(dialogueCsv, scenario);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("既存の同名ペアを一括登録する", EditorStyles.boldLabel);
            csvFolder = EditorGUILayout.TextField("CSV Folder", csvFolder);
            assetFolder = EditorGUILayout.TextField("Asset Folder", assetFolder);
            EditorGUILayout.HelpBox("指定フォルダ内で、拡張子を除いた名前が一致する既存ファイルを登録・同期します。" +
                "同名候補が複数ある組は除外します。全ペアに上のキャラクター設定を使います。", MessageType.None);
            using (new EditorGUI.DisabledScope(characterCsv == null || database == null))
            {
                if (GUILayout.Button("同名の既存ペアを一括登録し、CSVから同期")) RegisterMatchingPairs();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"登録済み: {DialogueCsvSyncRegistry.instance.Pairs.Count} 組", EditorStyles.boldLabel);
            if (GUILayout.Button("全登録ペアの名前変更を検知し、改名だけ反映"))
            {
                DialogueCsvSyncProcessor.RequestNames(true);
                AssetDatabase.Refresh();
            }
            if (GUILayout.Button("全登録ペアの内容だけ更新（CSV → Asset）"))
            {
                DialogueCsvSyncProcessor.RequestContent(true);
                AssetDatabase.Refresh();
            }
            var displayPairs = DialogueCsvSyncRegistry.instance.Pairs
                .OrderBy(pair => DisplayName(pair.csvGuid, pair.lastCsvName), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => AssetDatabase.GUIDToAssetPath(pair.csvGuid), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.csvGuid, StringComparer.Ordinal)
                .ToList();
            foreach (var pair in displayPairs) DrawPair(pair);
            EditorGUILayout.EndScrollView();
        }
    }

    private bool TryRegister(TextAsset csv, DialogueScenarioSO target)
    {
        try
        {
            DialogueCsvSyncRegistry.instance.ValidateRegistration(characterCsv, csv, database, target);
            var prepared = DialogueCsvImportService.Prepare(characterCsv, csv);
            DialogueCsvImportService.Apply(prepared, database, target, true);
            DialogueCsvSyncRegistry.instance.Register(characterCsv, csv, database, target);
            Debug.Log($"CSV同期に登録しました: {AssetDatabase.GetAssetPath(csv)} → {AssetDatabase.GetAssetPath(target)}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"CSV同期の登録失敗 ({AssetDatabase.GetAssetPath(csv)}): {exception.Message}");
            return false;
        }
    }

    private void RegisterMatchingPairs()
    {
        if (!AssetDatabase.IsValidFolder(csvFolder) || !AssetDatabase.IsValidFolder(assetFolder))
        {
            Debug.LogError("CSV Folder と Asset Folder に既存のUnityフォルダを指定してください。");
            return;
        }
        var csvs = FindByName("t:TextAsset", csvFolder, ".csv");
        var scenarios = FindByName("t:DialogueScenarioSO", assetFolder, ".asset");
        int registered = 0, skipped = 0;
        foreach (var item in scenarios)
        {
            if (!csvs.TryGetValue(item.Key, out var sources)) continue;
            if (sources.Count != 1 || item.Value.Count != 1)
            {
                skipped++;
                Debug.LogWarning($"CSV同期: 同名候補が複数あるため除外しました: {item.Key}。1組ずつ指定してください。");
                continue;
            }
            if (TryRegister(AssetDatabase.LoadAssetAtPath<TextAsset>(sources[0]),
                AssetDatabase.LoadAssetAtPath<DialogueScenarioSO>(item.Value[0]))) registered++;
            else skipped++;
        }
        Debug.Log($"同名ペア登録: {registered} 組、除外/失敗 {skipped} 組。相手がないファイルは新規作成しません。");
    }

    private static Dictionary<string, List<string>> FindByName(string filter, string folder, string extension)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets(filter, new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            if (!result.TryGetValue(name, out var paths)) result[name] = paths = new List<string>();
            paths.Add(path);
        }
        return result;
    }

    private static void DrawPair(DialogueCsvSyncPair pair)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string csvName = DisplayName(pair.csvGuid, pair.lastCsvName);
        string assetName = DisplayName(pair.scenarioGuid, pair.lastScenarioName);
        bool enabled = EditorGUILayout.ToggleLeft($"自動同期: {csvName} ↔ {assetName}", pair.enabled);
        if (enabled != pair.enabled)
        {
            pair.enabled = enabled;
            DialogueCsvSyncRegistry.instance.Persist();
            if (enabled) { SyncPair(pair, true); SyncPair(pair, false); }
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("CSV", pair.Load<TextAsset>(pair.csvGuid), typeof(TextAsset), false);
            EditorGUILayout.ObjectField("Asset", pair.Load<DialogueScenarioSO>(pair.scenarioGuid), typeof(DialogueScenarioSO), false);
            EditorGUILayout.ObjectField("Character CSV", pair.Load<TextAsset>(pair.characterCsvGuid), typeof(TextAsset), false);
            EditorGUILayout.ObjectField("Character Database", pair.Load<CharacterDatabaseSO>(pair.databaseGuid), typeof(CharacterDatabaseSO), false);
        }
        if (!string.IsNullOrEmpty(pair.lastError)) EditorGUILayout.HelpBox(pair.lastError, MessageType.Error);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!pair.enabled))
        {
            if (GUILayout.Button("改名だけ反映")) SyncPair(pair, true);
            if (GUILayout.Button("内容だけ更新")) SyncPair(pair, false);
        }
        if (GUILayout.Button("登録解除（ファイルは残す）")) DialogueCsvSyncRegistry.instance.Remove(pair);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private static string DisplayName(string guid, string previousName)
    {
        string name = DialogueCsvSyncRegistry.FileName(guid);
        return string.IsNullOrEmpty(name) ? previousName : name;
    }

    private static void SyncPair(DialogueCsvSyncPair pair, bool namesOnly)
    {
        DialogueCsvSyncProcessor.RunPairManually(pair, namesOnly);
    }
}
