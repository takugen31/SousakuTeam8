using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Dialogue/Dialogue Scenario",
    fileName = "DialogueScenario")]
public sealed class DialogueScenarioSO : ScriptableObject
{
    [SerializeField]
    private List<DialogueLine> lines = new List<DialogueLine>();

    private Dictionary<string, int> indexByLineId;

    public IReadOnlyList<DialogueLine> Lines => lines;

    public void ReplaceAll(List<DialogueLine> newLines)
    {
        lines = newLines ?? new List<DialogueLine>();
        RebuildCache();
    }

    public bool TryGetFirstLine(out DialogueLine line)
    {
        if (lines != null && lines.Count > 0)
        {
            line = lines[0];
            return true;
        }

        line = null;
        return false;
    }

    public bool TryGetLine(string lineId, out DialogueLine line)
    {
        EnsureCache();

        if (indexByLineId.TryGetValue(lineId, out int index))
        {
            line = lines[index];
            return true;
        }

        line = null;
        return false;
    }

    public bool TryGetNextLine(
        string currentLineId,
        out DialogueLine nextLine)
    {
        EnsureCache();

        if (!indexByLineId.TryGetValue(currentLineId, out int index))
        {
            nextLine = null;
            return false;
        }

        int nextIndex = index + 1;

        if (nextIndex >= lines.Count)
        {
            nextLine = null;
            return false;
        }

        nextLine = lines[nextIndex];
        return true;
    }

    private void OnEnable()
    {
        RebuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildCache();
    }
#endif

    private void EnsureCache()
    {
        if (indexByLineId == null)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        indexByLineId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        if (lines == null)
        {
            return;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            DialogueLine line = lines[i];

            if (line == null || string.IsNullOrWhiteSpace(line.lineId))
            {
                continue;
            }

            indexByLineId[line.lineId] = i;
        }
    }
}

[Serializable]
public sealed class DialogueLine
{
    public string lineId;
    public string speakerId;
    public string expressionId;

    [TextArea(2, 8)]
    public string text;

    public string nextLineId;
}
