using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class AffectionManager : MonoBehaviour
{
    private const string DefaultSaveKey = "AffectionState";

    [Header("Range")]
    [SerializeField, Min(0)]
    private int minValue = 0;

    [SerializeField, Min(0)]
    private int maxValue = 100;

    [Header("Initial Values")]
    [SerializeField]
    private List<AffectionEntry> initialValues =
        new List<AffectionEntry>();

    [Header("Persistence")]
    [SerializeField]
    private string saveKey = DefaultSaveKey;

    [SerializeField]
    private bool loadOnAwake = true;

    [SerializeField]
    private bool saveOnChange = true;

    private static AffectionManager instance;

    private readonly Dictionary<string, int> values =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public static AffectionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AffectionManager>();
            }

            return instance;
        }
    }

    /// <summary>
    /// 好感度が変化したときに呼ばれます。
    /// 引数: (characterId, 変更前の値, 変更後の値)
    /// </summary>
    public event Action<string, int, int> OnAffectionChanged;

    /// <summary>
    /// すべての好感度がリセットされたときに呼ばれます。
    /// </summary>
    public event Action OnAffectionReset;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        if (loadOnAwake)
        {
            Load();
        }
        else
        {
            ApplyInitialValues();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxValue < minValue)
        {
            maxValue = minValue;
        }
    }
#endif

    public int GetAffection(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return 0;
        }

        if (values.TryGetValue(characterId, out int value))
        {
            return value;
        }

        return 0;
    }

    public bool TryGetAffection(
        string characterId,
        out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        return values.TryGetValue(characterId, out value);
    }

    public void SetAffection(string characterId, int value)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            Debug.LogWarning(
                "好感度設定: characterIdが空です。");
            return;
        }

        int clamped = Mathf.Clamp(value, minValue, maxValue);
        int oldValue = GetAffection(characterId);

        if (oldValue == clamped)
        {
            return;
        }

        values[characterId] = clamped;

        OnAffectionChanged?.Invoke(
            characterId,
            oldValue,
            clamped);

        if (saveOnChange)
        {
            Save();
        }
    }

    public void AddAffection(string characterId, int delta)
    {
        int current = GetAffection(characterId);
        SetAffection(characterId, current + delta);
    }

    public void ApplyDelta(AffectionDelta delta)
    {
        if (delta == null ||
            string.IsNullOrWhiteSpace(delta.characterId))
        {
            return;
        }

        AddAffection(delta.characterId, delta.value);
    }

    public void ApplyDeltas(IReadOnlyList<AffectionDelta> deltas)
    {
        if (deltas == null)
        {
            return;
        }

        foreach (AffectionDelta delta in deltas)
        {
            ApplyDelta(delta);
        }
    }

    public bool Evaluate(AffectionCondition condition)
    {
        return condition != null &&
            condition.IsMet(this);
    }

    public bool EvaluateAll(
        IReadOnlyList<AffectionCondition> conditions)
    {
        if (conditions == null)
        {
            return true;
        }

        foreach (AffectionCondition condition in conditions)
        {
            if (!Evaluate(condition))
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        return new Dictionary<string, int>(
            values,
            StringComparer.Ordinal);
    }

    public void ResetAll()
    {
        values.Clear();
        ApplyInitialValues();

        OnAffectionReset?.Invoke();

        if (saveOnChange)
        {
            Save();
        }
    }

    public void Save()
    {
        AffectionSaveData data =
            new AffectionSaveData();

        foreach (KeyValuePair<string, int> pair in values)
        {
            data.entries.Add(
                new AffectionEntry
                {
                    characterId = pair.Key,
                    value = pair.Value
                });
        }

        string json = JsonUtility.ToJson(data);

        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(saveKey))
        {
            ApplyInitialValues();
            return;
        }

        string json = PlayerPrefs.GetString(saveKey);

        AffectionSaveData data =
            JsonUtility.FromJson<AffectionSaveData>(json);

        values.Clear();

        if (data != null && data.entries != null)
        {
            foreach (AffectionEntry entry in data.entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.characterId))
                {
                    continue;
                }

                values[entry.characterId] =
                    Mathf.Clamp(entry.value, minValue, maxValue);
            }
        }
    }

    private void ApplyInitialValues()
    {
        if (initialValues == null)
        {
            return;
        }

        foreach (AffectionEntry entry in initialValues)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.characterId))
            {
                continue;
            }

            values[entry.characterId] =
                Mathf.Clamp(entry.value, minValue, maxValue);
        }
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Affection/Clear Saved Data")]
    private static void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(DefaultSaveKey);
        EditorUtility.DisplayDialog(
            "Affection",
            "保存された好感度データを削除しました。",
            "OK");
    }

    [InitializeOnEnterPlayMode]
    private static void ClearAffectionProgressOnEnterPlayMode()
    {
        PlayerPrefs.DeleteKey(DefaultSaveKey);
        PlayerPrefs.Save();
    }
#endif
}

[Serializable]
public sealed class AffectionEntry
{
    public string characterId;
    public int value;
}

[Serializable]
internal sealed class AffectionSaveData
{
    public List<AffectionEntry> entries =
        new List<AffectionEntry>();
}
