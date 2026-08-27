using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ArchiveCategory
{
    Person,
    Place,
    Clue,
    Record,
    Tips
}

[Serializable]
public sealed class ArchiveEntry
{
    [SerializeField]
    private string id;

    [SerializeField]
    private ArchiveCategory category;

    [SerializeField]
    private string title;

    [SerializeField]
    private string subtitle;

    [SerializeField, TextArea(5, 16)]
    private string body;

    [SerializeField]
    private Sprite icon;

    [SerializeField]
    private string acquisitionHint;

    [SerializeField]
    private int sortOrder;

    [SerializeField]
    private bool unlockedAtStart;

    [SerializeField]
    private bool showBeforeUnlock;

    public string Id => id;
    public ArchiveCategory Category => category;
    public string Title => title;
    public string Subtitle => subtitle;
    public string Body => body;
    public Sprite Icon => icon;
    public string AcquisitionHint => acquisitionHint;
    public int SortOrder => sortOrder;
    public bool UnlockedAtStart => unlockedAtStart;
    public bool ShowBeforeUnlock => showBeforeUnlock;

    internal ArchiveEntry(
        string id,
        ArchiveCategory category,
        string title,
        string subtitle,
        string body,
        int sortOrder,
        bool unlockedAtStart)
    {
        this.id = id;
        this.category = category;
        this.title = title;
        this.subtitle = subtitle;
        this.body = body;
        this.sortOrder = sortOrder;
        this.unlockedAtStart = unlockedAtStart;
    }
}

[CreateAssetMenu(
    fileName = "ArchiveDatabase",
    menuName = "Game/Archive Database")]
public sealed class ArchiveDatabase : ScriptableObject
{
    [SerializeField]
    private TMP_FontAsset uiFont;

    [SerializeField]
    private List<ArchiveEntry> entries = new List<ArchiveEntry>();

    public TMP_FontAsset UiFont => uiFont;
    public IReadOnlyList<ArchiveEntry> Entries => entries;

    public bool TryGetEntry(string id, out ArchiveEntry entry)
    {
        entry = null;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        foreach (ArchiveEntry candidate in entries)
        {
            if (candidate != null &&
                string.Equals(candidate.Id, id, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    internal static ArchiveDatabase CreateFallback()
    {
        ArchiveDatabase database = CreateInstance<ArchiveDatabase>();
        database.entries.Add(
            new ArchiveEntry(
                "system.archive_guide",
                ArchiveCategory.Tips,
                "アーカイブについて",
                "獲得した情報を、いつでも振り返ることができます。",
                "人物・場所・手がかり・記録など、ゲーム中に獲得した情報がここへ追加されます。\n\nBキーで開閉し、情報タブから項目を選択してください。",
                0,
                true));
        return database;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (ArchiveEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
            {
                Debug.LogWarning(
                    $"{name}: IDが空のアーカイブ項目があります。",
                    this);
                continue;
            }

            if (!ids.Add(entry.Id))
            {
                Debug.LogError(
                    $"{name}: アーカイブID「{entry.Id}」が重複しています。",
                    this);
            }
        }
    }
#endif
}
