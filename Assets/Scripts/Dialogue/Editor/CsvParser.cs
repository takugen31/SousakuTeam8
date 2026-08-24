using System;
using System.Collections.Generic;
using System.Text;

public static class CsvParser
{
    public static List<CsvRecord> ParseRecords(string csvText)
    {
        List<List<string>> rows = ParseRows(csvText);

        if (rows.Count == 0)
        {
            throw new FormatException("CSVが空です。");
        }

        List<string> headers = rows[0];

        if (headers.Count == 0)
        {
            throw new FormatException("CSVのヘッダーがありません。");
        }

        headers[0] = headers[0].TrimStart('\uFEFF');

        HashSet<string> headerSet =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            headers[i] = headers[i].Trim();

            if (string.IsNullOrEmpty(headers[i]))
            {
                throw new FormatException(
                    $"CSVヘッダーの{i + 1}列目が空です。");
            }

            if (!headerSet.Add(headers[i]))
            {
                throw new FormatException(
                    $"CSVヘッダー「{headers[i]}」が重複しています。");
            }
        }

        List<CsvRecord> records = new List<CsvRecord>();

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> cells = rows[rowIndex];

            if (cells.Count > headers.Count)
            {
                throw new FormatException(
                    $"{rowIndex + 1}行目の列数がヘッダーより多いです。");
            }

            Dictionary<string, string> values =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int column = 0; column < headers.Count; column++)
            {
                string value =
                    column < cells.Count ? cells[column] : string.Empty;

                values.Add(headers[column], value);
            }

            records.Add(new CsvRecord(rowIndex + 1, values));
        }

        return records;
    }

    private static List<List<string>> ParseRows(string csvText)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> currentRow = new List<string>();
        StringBuilder currentField = new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char current = csvText[i];

            if (insideQuotes)
            {
                if (current == '"')
                {
                    bool escapedQuote =
                        i + 1 < csvText.Length &&
                        csvText[i + 1] == '"';

                    if (escapedQuote)
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(current);
                }

                continue;
            }

            switch (current)
            {
                case '"':
                    insideQuotes = true;
                    break;

                case ',':
                    AddField(currentRow, currentField);
                    break;

                case '\r':
                    if (i + 1 < csvText.Length &&
                        csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    AddRow(rows, currentRow, currentField);
                    break;

                case '\n':
                    AddRow(rows, currentRow, currentField);
                    break;

                default:
                    currentField.Append(current);
                    break;
            }
        }

        if (insideQuotes)
        {
            throw new FormatException(
                "CSV内のダブルクォーテーションが閉じられていません。");
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            AddRow(rows, currentRow, currentField);
        }

        return rows;
    }

    private static void AddField(
        List<string> row,
        StringBuilder field)
    {
        row.Add(field.ToString());
        field.Clear();
    }

    private static void AddRow(
        List<List<string>> rows,
        List<string> row,
        StringBuilder field)
    {
        AddField(row, field);

        bool isEmpty = true;

        foreach (string cell in row)
        {
            if (!string.IsNullOrWhiteSpace(cell))
            {
                isEmpty = false;
                break;
            }
        }

        if (!isEmpty)
        {
            rows.Add(new List<string>(row));
        }

        row.Clear();
    }
}

public sealed class CsvRecord
{
    private readonly Dictionary<string, string> values;

    public int RowNumber { get; }

    public CsvRecord(
        int rowNumber,
        Dictionary<string, string> values)
    {
        RowNumber = rowNumber;
        this.values = values;
    }

    public string Get(string columnName)
    {
        if (!values.TryGetValue(columnName, out string value))
        {
            throw new FormatException(
                $"{RowNumber}行目: 列「{columnName}」が存在しません。");
        }

        return value;
    }

    public bool TryGet(string columnName, out string value)
    {
        return values.TryGetValue(columnName, out value);
    }

    public string GetRequired(string columnName)
    {
        string value = Get(columnName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException(
                $"{RowNumber}行目: 「{columnName}」が空です。");
        }

        return value;
    }
}
