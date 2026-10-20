using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Keep the existing column order and custom columns, and validate before touching the CSV.
internal static class DialogueCsvExportService
{
    internal static string ContentHash(IReadOnlyList<DialogueLine> lines)
    {
        var content = new StringBuilder();
        foreach (var line in lines)
        {
            string spriteId = "";
            if (line?.background != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(line.background, out string guid, out long localId))
                spriteId = guid + ":" + localId;
            // Unity instance IDs change on reload; persistent GUID/local IDs do not.
            content.Append(line == null ? "null" : System.Text.RegularExpressions.Regex.Replace(
                JsonUtility.ToJson(line), "\"background\":\\{[^}]*\\}", "\"background\":\"" + spriteId + "\""));
            content.Append('\n');
        }
        using (var hash = SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString())));
    }

    internal static string Export(DialogueScenarioSO scenario, TextAsset original, TextAsset characters)
    {
        string csv = Build(scenario.Lines, original.text);
        var temporary = new TextAsset(csv);
        try
        {
            var prepared = DialogueCsvImportService.Prepare(characters, temporary);
            // Reimporting must preserve every CSV-representable value, not silently discard edits.
            if (Build(prepared.Lines, original.text) != csv)
                throw new InvalidOperationException("CSVへ変換すると内容が変わる設定があります。ID・空白・改行・背景を確認してください。");
            for (int i = 0; i < prepared.Lines.Count; i++)
                if (prepared.Lines[i].background != scenario.Lines[i].background)
                    throw new InvalidOperationException("背景のSpriteをCSVのパスで再現できません。画像の代表Spriteを指定してください。");
            return csv;
        }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
    }

    private static string Build(IReadOnlyList<DialogueLine> lines, string original)
    {
        var records = CsvParser.ParseRecords(original);
        var columns = records.Count > 0 ? records[0].ColumnNames.ToList() : new List<string>();
        var required = new[] { "lineId", "speakerId", "expressionId", "backgroundPath", "text", "nextLineId",
            "affectionChanges", "branches", "archiveUnlockIds", "revealSpeakerName", "consultationTitle",
            "nextScenePath", "fadeBeforeLine", "lineFadeOutDuration", "lineFadeHoldDuration", "lineFadeInDuration" };
        foreach (string column in required)
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase)) columns.Add(column);
        int choiceCount = lines.Count == 0 ? 0 : lines.Max(line => line?.choices?.Count ?? 0);
        for (int i = 1; i <= choiceCount; i++)
            foreach (string column in new[] { $"choice{i}Text", $"choice{i}NextLineId" })
                if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase)) columns.Add(column);
        var previous = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
        foreach (var record in records)
            if (record.TryGet("lineId", out string id)) previous[id] = record;
        string newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var result = new StringBuilder(string.Join(",", columns.Select(Escape)) + newline);
        foreach (DialogueLine line in lines)
        {
            if (line == null) throw new InvalidOperationException("空の会話行はCSVへ保存できません。");
            var values = Values(line);
            previous.TryGetValue(line.lineId ?? "", out CsvRecord old);
            result.Append(string.Join(",", columns.Select(column => Escape(values.TryGetValue(column, out string value)
                ? value : System.Text.RegularExpressions.Regex.IsMatch(column, "^choice[0-9]+(Text|NextLineId)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    ? "" : old != null && old.TryGet(column, out string retained) ? retained : ""))));
            result.Append(newline);
        }
        return result.ToString();
    }

    private static Dictionary<string, string> Values(DialogueLine line)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["lineId"] = line.lineId, ["speakerId"] = line.speakerId, ["expressionId"] = line.expressionId,
            ["backgroundPath"] = line.background == null ? "" : AssetDatabase.GetAssetPath(line.background),
            ["text"] = line.text, ["nextLineId"] = line.nextLineId,
            ["affectionChanges"] = Deltas(line.affectionChanges), ["branches"] = Branches(line.branches),
            ["archiveUnlockIds"] = string.Join(";", line.archiveUnlockIds ?? new List<string>()),
            ["revealSpeakerName"] = line.revealSpeakerName ? "true" : "false",
            ["consultationTitle"] = line.consultationTitle, ["nextScenePath"] = line.nextScenePath,
            ["fadeBeforeLine"] = line.fadeBeforeLine ? "true" : "false",
            ["lineFadeOutDuration"] = line.lineFadeOutDuration.ToString("R", CultureInfo.InvariantCulture),
            ["lineFadeHoldDuration"] = line.lineFadeHoldDuration.ToString("R", CultureInfo.InvariantCulture),
            ["lineFadeInDuration"] = line.lineFadeInDuration.ToString("R", CultureInfo.InvariantCulture)
        };
        if (line.choices != null)
            for (int i = 0; i < line.choices.Count; i++)
            {
                if (line.choices[i] == null) throw new InvalidOperationException("空の選択肢があります。");
                values[$"choice{i + 1}Text"] = line.choices[i].text;
                values[$"choice{i + 1}NextLineId"] = line.choices[i].nextLineId;
            }
        // Clear removed choices instead of resurrecting their old CSV cells.
        return values;
    }

    private static string Deltas(List<AffectionDelta> deltas) => deltas == null ? "" : string.Join(";",
        deltas.Select(delta => delta.characterId + (delta.value >= 0 ? "+" : "") + delta.value.ToString(CultureInfo.InvariantCulture)));

    private static string Branches(List<DialogueBranch> branches)
    {
        if (branches == null) return "";
        return string.Join(";", branches.Select(branch => {
            if (branch == null || (branch.affectionChanges != null && branch.affectionChanges.Count > 0))
                throw new InvalidOperationException("分岐ごとの好感度変更は既存CSV形式に対応していないため、書き戻せません。");
            string[] symbols = { ">=", ">", "<=", "<", "==", "!=" };
            return string.Join("&", branch.conditions.Select(condition => condition.characterId +
                symbols[(int)condition.comparison] + condition.value.ToString(CultureInfo.InvariantCulture))) + "->" + branch.nextLineId;
        }));
    }

    private static string Escape(string value)
    {
        value = value ?? "";
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
