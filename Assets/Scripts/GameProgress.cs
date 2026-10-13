using UnityEngine;

/// <summary>
/// ゲーム全体の進行状況（セーブデータ）を管理します。
/// </summary>
public static class GameProgress
{
    private const string ArchiveSaveKey = "ArchiveState.v1";
    private const string AffectionSaveKey = "AffectionState";
    private const string Chapter1SearchSaveKey = "Chapter1Search.Acquired.v1";
    private const string KayoSearchSaveKey = "KayoSearch.Acquired.v1";

    private static readonly string[] CheckpointKeys = {
        ArchiveSaveKey, AffectionSaveKey, Chapter1SearchSaveKey, KayoSearchSaveKey
    };

    public static GameSavePreference[] CaptureCheckpoint()
    {
        AffectionManager.Instance?.Save();
        var state = new GameSavePreference[CheckpointKeys.Length];
        for (int i = 0; i < state.Length; i++)
            state[i] = new GameSavePreference { key = CheckpointKeys[i],
                present = PlayerPrefs.HasKey(CheckpointKeys[i]), value = PlayerPrefs.GetString(CheckpointKeys[i], "") };
        return state;
    }

    public static bool IsValidCheckpoint(GameSavePreference[] state)
    {
        if (state == null || state.Length != CheckpointKeys.Length) return false;
        var found = new System.Collections.Generic.HashSet<string>();
        foreach (GameSavePreference entry in state)
        {
            if (entry == null || System.Array.IndexOf(CheckpointKeys, entry.key) < 0 || !found.Add(entry.key)) return false;
            if (!entry.present) continue;
            try
            {
                ProgressProbe probe = JsonUtility.FromJson<ProgressProbe>(entry.value);
                if (probe == null) return false;
                if (entry.key == ArchiveSaveKey && (probe.unlockedIds == null || probe.readIds == null)) return false;
                if (entry.key == AffectionSaveKey && probe.entries == null) return false;
                if ((entry.key == Chapter1SearchSaveKey || entry.key == KayoSearchSaveKey) && probe.acquiredItemIds == null) return false;
            }
            catch (System.Exception) { return false; }
        }
        return true;
    }

    [System.Serializable]
    private sealed class ProgressProbe
    {
        public System.Collections.Generic.List<string> unlockedIds = null;
        public System.Collections.Generic.List<string> readIds = null;
        public System.Collections.Generic.List<AffectionEntry> entries = null;
        public System.Collections.Generic.List<string> acquiredItemIds = null;
    }

    public static void RestoreCheckpoint(GameSavePreference[] state)
    {
        foreach (GameSavePreference entry in state)
            if (entry.present) PlayerPrefs.SetString(entry.key, entry.value);
            else PlayerPrefs.DeleteKey(entry.key);
        PlayerPrefs.Save();
        if (PlayerPrefs.HasKey(AffectionSaveKey)) AffectionManager.Instance?.Load();
        else AffectionManager.Instance?.ResetAll();
        ArchiveManager.Instance.ReloadProgress();
    }

    /// <summary>
    /// ニューゲーム（タイトルから開始）時に、進行状況を初期化します。
    /// </summary>
    public static void ResetAll()
    {
        NovelDialogueController.ResetStoryState();
        PlayerPrefs.DeleteKey(ArchiveSaveKey);
        PlayerPrefs.DeleteKey(AffectionSaveKey);
        PlayerPrefs.DeleteKey(Chapter1SearchSaveKey);
        PlayerPrefs.DeleteKey(KayoSearchSaveKey);
        PlayerPrefs.Save();

        AffectionManager affection = AffectionManager.Instance;
        if (affection != null)
        {
            affection.ResetAll();
        }

        ArchiveManager archive = ArchiveManager.Instance;
        if (archive != null)
        {
            archive.ResetProgress();
        }
    }
}
