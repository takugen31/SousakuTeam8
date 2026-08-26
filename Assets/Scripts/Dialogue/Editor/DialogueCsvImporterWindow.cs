using System;
using System.Collections.Generic;
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
            List<CharacterData> characters =
                ParseCharacterCsv(characterCsv.text);

            List<DialogueLine> dialogueLines =
                ParseDialogueCsv(dialogueCsv.text, characters);

            Undo.RecordObjects(
                new UnityEngine.Object[]
                {
                    characterDatabase,
                    dialogueScenario
                },
                "Import Dialogue CSV");

            characterDatabase.ReplaceAll(characters);
            dialogueScenario.ReplaceAll(dialogueLines);

            EditorUtility.SetDirty(characterDatabase);
            EditorUtility.SetDirty(dialogueScenario);

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"CSVインポート完了: " +
                $"{characters.Count}キャラクター、" +
                $"{dialogueLines.Count}セリフ");

            EditorUtility.DisplayDialog(
                "インポート完了",
                $"{characters.Count}キャラクター\n" +
                $"{dialogueLines.Count}セリフを読み込みました。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "CSVインポートエラー",
                exception.Message,
                "OK");
        }
    }

    private static List<CharacterData> ParseCharacterCsv(
        string csvText)
    {
        List<CsvRecord> records =
            CsvParser.ParseRecords(csvText);

        List<CharacterData> result =
            new List<CharacterData>();

        Dictionary<string, CharacterData> characterById =
            new Dictionary<string, CharacterData>(
                StringComparer.Ordinal);

        foreach (CsvRecord record in records)
        {
            string characterId =
                record.GetRequired("characterId").Trim();

            string displayName =
                record.Get("displayName").Trim();

            string colorText =
                record.Get("nameColor").Trim();

            string expressionId =
                record.GetRequired("expressionId").Trim();

            string portraitPath =
                record.GetRequired("portraitPath").Trim();

            if (!characterById.TryGetValue(
                    characterId,
                    out CharacterData character))
            {
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    throw new FormatException(
                        $"{record.RowNumber}行目: " +
                        "新しいキャラクターのdisplayNameが空です。");
                }

                character = new CharacterData
                {
                    characterId = characterId,
                    displayName = displayName,
                    nameColor = ParseColor(
                        colorText,
                        record.RowNumber),
                    expressions =
                        new List<CharacterExpression>()
                };

                characterById.Add(characterId, character);
                result.Add(character);
            }
            else
            {
                if (!string.IsNullOrEmpty(displayName) &&
                    character.displayName != displayName)
                {
                    throw new FormatException(
                        $"{record.RowNumber}行目: " +
                        $"characterId「{characterId}」の" +
                        "displayNameが他の行と一致しません。");
                }
            }

            if (character.HasExpression(expressionId))
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: " +
                    $"キャラクター「{characterId}」の表情" +
                    $"「{expressionId}」が重複しています。");
            }

            Sprite portrait =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    portraitPath);

            if (portrait == null)
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: " +
                    $"Spriteを読み込めませんでした。\n" +
                    $"パス: {portraitPath}\n" +
                    "画像のTexture TypeがSpriteになっているか確認してください。");
            }

            character.expressions.Add(
                new CharacterExpression
                {
                    expressionId = expressionId,
                    portrait = portrait
                });
        }

        return result;
    }

    private static List<DialogueLine> ParseDialogueCsv(
        string csvText,
        List<CharacterData> characters)
    {
        List<CsvRecord> records =
            CsvParser.ParseRecords(csvText);

        Dictionary<string, CharacterData> characterById =
            new Dictionary<string, CharacterData>(
                StringComparer.Ordinal);

        foreach (CharacterData character in characters)
        {
            characterById.Add(
                character.characterId,
                character);
        }

        List<DialogueLine> result =
            new List<DialogueLine>();

        HashSet<string> lineIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (CsvRecord record in records)
        {
            string lineId =
                record.GetRequired("lineId").Trim();

            string speakerId =
                record.Get("speakerId").Trim();

            string expressionId =
                record.Get("expressionId").Trim();

            string text =
                record.GetRequired("text");

            string nextLineId =
                record.Get("nextLineId").Trim();

            List<DialogueChoice> choices =
                ParseChoices(record);

            if (!lineIds.Add(lineId))
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: " +
                    $"lineId「{lineId}」が重複しています。");
            }

            if (choices.Count > 0 &&
                !string.IsNullOrEmpty(nextLineId))
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: 選択肢があるセリフには" +
                    "nextLineIdを設定できません。");
            }

            if (!string.IsNullOrEmpty(speakerId))
            {
                if (!characterById.TryGetValue(
                        speakerId,
                        out CharacterData character))
                {
                    throw new FormatException(
                        $"{record.RowNumber}行目: " +
                        $"speakerId「{speakerId}」が" +
                        "キャラクターCSVに存在しません。");
                }

                if (!string.IsNullOrEmpty(expressionId) &&
                    !character.HasExpression(expressionId))
                {
                    throw new FormatException(
                        $"{record.RowNumber}行目: " +
                        $"キャラクター「{speakerId}」に" +
                        $"表情「{expressionId}」がありません。");
                }
            }

            result.Add(
                new DialogueLine
                {
                    lineId = lineId,
                    speakerId = speakerId,
                    expressionId = expressionId,
                    text = text.Replace("\\n", "\n"),
                    nextLineId = nextLineId,
                    choices = choices
                });
        }

        foreach (DialogueLine line in result)
        {
            if (!string.IsNullOrEmpty(line.nextLineId) &&
                !lineIds.Contains(line.nextLineId))
            {
                throw new FormatException(
                    $"セリフ「{line.lineId}」のnextLineId " +
                    $"「{line.nextLineId}」が存在しません。");
            }

            if (line.choices == null)
            {
                continue;
            }

            foreach (DialogueChoice choice in line.choices)
            {
                if (!lineIds.Contains(choice.nextLineId))
                {
                    throw new FormatException(
                        $"セリフ「{line.lineId}」の選択肢" +
                        $"「{choice.text}」の遷移先 " +
                        $"「{choice.nextLineId}」が存在しません。");
                }
            }
        }

        return result;
    }

    private static List<DialogueChoice> ParseChoices(
        CsvRecord record)
    {
        List<DialogueChoice> result =
            new List<DialogueChoice>();

        HashSet<string> choiceTexts =
            new HashSet<string>(StringComparer.Ordinal);

        SortedSet<int> choiceIndices = new SortedSet<int>();

        foreach (string columnName in record.ColumnNames)
        {
            if (TryGetChoiceColumnIndex(
                    columnName,
                    "Text",
                    out int textIndex))
            {
                choiceIndices.Add(textIndex);
            }

            if (TryGetChoiceColumnIndex(
                    columnName,
                    "NextLineId",
                    out int nextLineIndex))
            {
                choiceIndices.Add(nextLineIndex);
            }
        }

        foreach (int index in choiceIndices)
        {
            string textColumn = $"choice{index}Text";
            string nextLineColumn =
                $"choice{index}NextLineId";

            bool hasTextColumn =
                record.TryGet(textColumn, out string choiceText);

            bool hasNextLineColumn =
                record.TryGet(
                    nextLineColumn,
                    out string choiceNextLineId);

            if (!hasTextColumn && !hasNextLineColumn)
            {
                continue;
            }

            if (!hasTextColumn || !hasNextLineColumn)
            {
                throw new FormatException(
                    $"CSVには{textColumn}と{nextLineColumn}を" +
                    "両方用意してください。");
            }

            choiceText = choiceText.Trim();
            choiceNextLineId = choiceNextLineId.Trim();

            if (string.IsNullOrEmpty(choiceText) &&
                string.IsNullOrEmpty(choiceNextLineId))
            {
                continue;
            }

            if (string.IsNullOrEmpty(choiceText) ||
                string.IsNullOrEmpty(choiceNextLineId))
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: {textColumn}と" +
                    $"{nextLineColumn}は両方入力してください。");
            }

            if (!choiceTexts.Add(choiceText))
            {
                throw new FormatException(
                    $"{record.RowNumber}行目: 選択肢" +
                    $"「{choiceText}」が重複しています。");
            }

            result.Add(
                new DialogueChoice
                {
                    text = choiceText.Replace("\\n", "\n"),
                    nextLineId = choiceNextLineId
                });
        }

        return result;
    }

    private static bool TryGetChoiceColumnIndex(
        string columnName,
        string suffix,
        out int index)
    {
        const string prefix = "choice";

        index = 0;

        if (!columnName.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase) ||
            !columnName.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int numberLength =
            columnName.Length - prefix.Length - suffix.Length;

        if (numberLength <= 0)
        {
            return false;
        }

        string numberText = columnName.Substring(
            prefix.Length,
            numberLength);

        return int.TryParse(numberText, out index) && index >= 1;
    }

    private static Color ParseColor(
        string colorText,
        int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(colorText))
        {
            return Color.white;
        }

        if (ColorUtility.TryParseHtmlString(
                colorText,
                out Color color))
        {
            return color;
        }

        throw new FormatException(
            $"{rowNumber}行目: " +
            $"色「{colorText}」を読み込めません。" +
            "例: #FFFFFF");
    }
}
