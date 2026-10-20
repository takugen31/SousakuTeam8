using System;
using UnityEditor;
using UnityEngine;

public sealed class DialogueCsvImporterWindow : EditorWindow
{
    [SerializeField]
    private TextAsset characterCsv;

    [SerializeField]
    private TextAsset dialogueCsv;

    [SerializeField]
    private CharacterDatabaseSO characterDatabase;

    [SerializeField]
    private DialogueScenarioSO dialogueScenario;

    [SerializeField] private bool registerForSync = true;

    [MenuItem("Tools/Dialogue/CSV Importer")]
    private static void OpenWindow()
    {
        GetWindow<DialogueCsvImporterWindow>(
            "Dialogue CSV Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "CSVからScriptableObjectを生成",
            EditorStyles.boldLabel);

        EditorGUILayout.Space();

        characterCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Character CSV",
            characterCsv,
            typeof(TextAsset),
            false);

        dialogueCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Dialogue CSV",
            dialogueCsv,
            typeof(TextAsset),
            false);

        EditorGUILayout.Space();

        characterDatabase =
            (CharacterDatabaseSO)EditorGUILayout.ObjectField(
                "Character Database",
                characterDatabase,
                typeof(CharacterDatabaseSO),
                false);

        dialogueScenario =
            (DialogueScenarioSO)EditorGUILayout.ObjectField(
                "Dialogue Scenario",
                dialogueScenario,
                typeof(DialogueScenarioSO),
                false);

        EditorGUILayout.Space();

        registerForSync = EditorGUILayout.ToggleLeft("インポート成功時に自動同期へ登録する", registerForSync);
        if (GUILayout.Button("CSV同期の登録一覧を開く")) DialogueCsvSyncWindow.OpenWindow();

        bool hasAllReferences =
            characterCsv != null &&
            dialogueCsv != null &&
            characterDatabase != null &&
            dialogueScenario != null;

        using (new EditorGUI.DisabledScope(!hasAllReferences))
        {
            if (GUILayout.Button("CSVをインポート"))
            {
                ImportCsv();
            }
        }

        if (!hasAllReferences)
        {
            EditorGUILayout.HelpBox(
                "CSVと出力先のScriptableObjectをすべて設定してください。",
                MessageType.Info);
        }
    }

    private void ImportCsv()
    {
        try
        {
            if (registerForSync)
                DialogueCsvSyncRegistry.instance.ValidateRegistration(
                    characterCsv, dialogueCsv, characterDatabase, dialogueScenario);
            var prepared = DialogueCsvImportService.Prepare(characterCsv, dialogueCsv);
            DialogueCsvImportService.Apply(prepared, characterDatabase, dialogueScenario, true);
            if (registerForSync)
                DialogueCsvSyncRegistry.instance.Register(
                    characterCsv, dialogueCsv, characterDatabase, dialogueScenario);
            Debug.Log($"CSVインポート完了: {prepared.Characters.Count}キャラクター、{prepared.Lines.Count}セリフ");
            EditorUtility.DisplayDialog("インポート完了",
                $"{prepared.Lines.Count}セリフを反映しました。" +
                (registerForSync ? "\n今後のCSV変更は自動で反映されます。" : string.Empty), "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("CSVインポートエラー", exception.Message, "OK");
        }
    }
}
