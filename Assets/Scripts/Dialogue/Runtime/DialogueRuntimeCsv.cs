using System;
using System.Collections.Generic;
using UnityEngine;

public static class DialogueRuntimeCsv
{
    public static DialogueScenarioSO CreateScenario(
        TextAsset csvAsset,
        Sprite backgroundForPathRows = null)
    {
        if (csvAsset == null)
        {
            throw new ArgumentNullException(nameof(csvAsset));
        }

        List<List<string>> rows = Parse(csvAsset.text);
        if (rows.Count == 0)
        {
            throw new FormatException($"CSV「{csvAsset.name}」にヘッダーがありません。");
        }

        Dictionary<string, int> columns = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows[0].Count; i++)
        {
            string name = rows[0][i].Trim();
            if (!string.IsNullOrEmpty(name))
            {
                columns[name] = i;
            }
        }

        List<DialogueLine> lines = new();
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row = rows[rowIndex];
            string lineId = Get(row, columns, "lineId").Trim();
            if (string.IsNullOrEmpty(lineId))
            {
                continue;
            }

            DialogueLine line = new()
            {
                lineId = lineId,
                speakerId = Get(row, columns, "speakerId").Trim(),
                expressionId = Get(row, columns, "expressionId").Trim(),
                text = Get(row, columns, "text"),
                nextLineId = Get(row, columns, "nextLineId").Trim(),
                nextScenePath = Get(row, columns, "nextScenePath").Trim(),
                consultationTitle = Get(row, columns, "consultationTitle").Trim(),
                revealSpeakerName = ParseBoolean(Get(row, columns, "revealSpeakerName"))
            };

            if (!string.IsNullOrWhiteSpace(Get(row, columns, "backgroundPath")))
            {
                line.background = backgroundForPathRows;
            }

            for (int choiceIndex = 1; choiceIndex <= 4; choiceIndex++)
            {
                string choiceText = Get(row, columns, $"choice{choiceIndex}Text").Trim();
                string choiceNextLineId = Get(
                    row,
                    columns,
                    $"choice{choiceIndex}NextLineId").Trim();

                if (string.IsNullOrEmpty(choiceText) && string.IsNullOrEmpty(choiceNextLineId))
                {
                    continue;
                }

                line.choices.Add(new DialogueChoice
                {
                    text = choiceText,
                    nextLineId = choiceNextLineId
                });
            }

            lines.Add(line);
        }

        DialogueScenarioSO scenario = ScriptableObject.CreateInstance<DialogueScenarioSO>();
        scenario.name = csvAsset.name;
        scenario.hideFlags = HideFlags.HideAndDontSave;
        scenario.SetDefaultBackground(backgroundForPathRows);
        scenario.ReplaceAll(lines);
        return scenario;
    }

    private static string Get(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columns,
        string columnName)
    {
        return columns.TryGetValue(columnName, out int index) &&
               index >= 0 &&
               index < row.Count
            ? row[index]
            : string.Empty;
    }

    private static bool ParseBoolean(string value)
    {
        string normalized = value?.Trim();
        return string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static List<List<string>> Parse(string csv)
    {
        List<List<string>> rows = new();
        List<string> row = new();
        System.Text.StringBuilder field = new();
        bool insideQuotes = false;

        for (int index = 0; index < csv.Length; index++)
        {
            char current = csv[index];

            if (current == '"')
            {
                if (insideQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (!insideQuotes && current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!insideQuotes && (current == '\r' || current == '\n'))
            {
                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }

            field.Append(current);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
