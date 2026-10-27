using UnityEngine;

/// <summary>Fixed end numbers; replace the provisional titles when finalized.</summary>
public static class EndingCatalog
{
    public static bool TryGetEnding(string lineId, out int number, out string title)
    {
        number = 0;
        title = null;
        switch (lineId)
        {
            case "chapter_kayo_end_004":
                number = 1; title = "カヨ・ハッピーエンド（仮）"; break;
            case "chapter_kayo_3_032":
                number = 2; title = "カヨ・バッドエンド（仮）"; break;
            case "chapter_yowashi_rescue_038":
                number = 3; title = "ヨワシ・ハッピーエンド（仮）"; break;
            case "chapter_yowashi_catchfailure_009":
                number = 4; title = "ヨワシ・キャッチ失敗エンド（仮）"; break;
            case "chapter_yowashi_rescue_042":
                number = 5; title = "ヨワシ・救助失敗エンド（仮）"; break;
            case "chapter3_009":
                number = 6; title = "永遠の白い部屋エンド（仮）"; break;
            default: return false;
        }
        return true;
    }
}

/// <summary>
/// ゲーム全体の進行状況（セーブデータ）を管理します。
/// </summary>
public static class GameProgress
{
    private const string ArchiveSaveKey = "ArchiveState.v1";
    private const string AffectionSaveKey = "AffectionState";
    private const string Chapter1SearchSaveKey = "Chapter1Search.Acquired.v1";
    private const string KayoSearchSaveKey = "KayoSearch.Acquired.v1";
    private const string MoteruSearchSaveKey = "MoteruSearch.Acquired.v1";

    private static readonly string[] CheckpointKeys = {
        ArchiveSaveKey, AffectionSaveKey, Chapter1SearchSaveKey, KayoSearchSaveKey, MoteruSearchSaveKey
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
        // Earlier saves have the four original progress keys.
        if (state == null || (state.Length != CheckpointKeys.Length && state.Length != CheckpointKeys.Length - 1)) return false;
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
                if ((entry.key == Chapter1SearchSaveKey || entry.key == KayoSearchSaveKey || entry.key == MoteruSearchSaveKey) && probe.acquiredItemIds == null) return false;
            }
            catch (System.Exception) { return false; }
        }
        foreach (string key in CheckpointKeys)
            if (key != MoteruSearchSaveKey && !found.Contains(key)) return false;
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
        PlayerPrefs.DeleteKey(MoteruSearchSaveKey);
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
        TimingGameResults.ResetAll();
        TimingMeterGameFlowController.ResetPendingRequest();
        NovelDialogueController.ResetStoryState();
        PuzzleGameFlowController.ResetPendingRequest();
        PlayerPrefs.DeleteKey(ArchiveSaveKey);
        PlayerPrefs.DeleteKey(AffectionSaveKey);
        PlayerPrefs.DeleteKey(Chapter1SearchSaveKey);
        PlayerPrefs.DeleteKey(KayoSearchSaveKey);
        PlayerPrefs.DeleteKey(MoteruSearchSaveKey);
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
