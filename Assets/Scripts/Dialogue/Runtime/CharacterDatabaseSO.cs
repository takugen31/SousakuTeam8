using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Dialogue/Character Database",
    fileName = "CharacterDatabase")]
public sealed class CharacterDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<CharacterData> characters = new List<CharacterData>();

    private Dictionary<string, CharacterData> characterById;

    public IReadOnlyList<CharacterData> Characters => characters;

    public bool TryGetCharacter(
        string characterId,
        out CharacterData character)
    {
        EnsureCache();

        return characterById.TryGetValue(characterId, out character);
    }

    public void ReplaceAll(List<CharacterData> newCharacters)
    {
        characters = newCharacters ?? new List<CharacterData>();
        RebuildCache();
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
        if (characterById == null)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        characterById =
            new Dictionary<string, CharacterData>(StringComparer.Ordinal);

        if (characters == null)
        {
            return;
        }

        foreach (CharacterData character in characters)
        {
            if (character == null ||
                string.IsNullOrWhiteSpace(character.characterId))
            {
                continue;
            }

            characterById[character.characterId] = character;
        }
    }
}

[Serializable]
public sealed class CharacterData
{
    public string characterId;
    public string displayName;
    public Color nameColor = Color.white;
    public bool nameKnownInitially;

    public List<CharacterExpression> expressions =
        new List<CharacterExpression>();

    public bool HasExpression(string expressionId)
    {
        if (string.IsNullOrWhiteSpace(expressionId))
        {
            return true;
        }

        foreach (CharacterExpression expression in expressions)
        {
            if (expression != null &&
                string.Equals(
                    expression.expressionId,
                    expressionId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public Sprite GetPortrait(string expressionId)
    {
        if (expressions == null || expressions.Count == 0)
        {
            return null;
        }

        // 指定された表情を探す
        if (!string.IsNullOrWhiteSpace(expressionId))
        {
            foreach (CharacterExpression expression in expressions)
            {
                if (expression != null &&
                    string.Equals(
                        expression.expressionId,
                        expressionId,
                        StringComparison.Ordinal))
                {
                    return expression.portrait;
                }
            }
        }

        // 指定がなければnormalを使う
        foreach (CharacterExpression expression in expressions)
        {
            if (expression != null &&
                expression.expressionId == "normal")
            {
                return expression.portrait;
            }
        }

        // normalもなければ最初の画像
        return expressions[0]?.portrait;
    }
}

[Serializable]
public sealed class CharacterExpression
{
    public string expressionId;
    public Sprite portrait;
}
