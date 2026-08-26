using System;
using UnityEngine;

[Serializable]
public sealed class AffectionCondition
{
    public enum ComparisonType
    {
        GreaterOrEqual,
        Greater,
        LessOrEqual,
        Less,
        Equal,
        NotEqual
    }

    public string characterId;

    public ComparisonType comparison =
        ComparisonType.GreaterOrEqual;

    public int value;

    public bool IsMet(int currentValue)
    {
        switch (comparison)
        {
            case ComparisonType.GreaterOrEqual:
                return currentValue >= value;

            case ComparisonType.Greater:
                return currentValue > value;

            case ComparisonType.LessOrEqual:
                return currentValue <= value;

            case ComparisonType.Less:
                return currentValue < value;

            case ComparisonType.Equal:
                return currentValue == value;

            case ComparisonType.NotEqual:
                return currentValue != value;

            default:
                return false;
        }
    }

    public bool IsMet(AffectionManager manager)
    {
        if (manager == null ||
            string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        return IsMet(manager.GetAffection(characterId));
    }
}
