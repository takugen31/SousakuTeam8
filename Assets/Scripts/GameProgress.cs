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

    /// <summary>
    /// ニューゲーム（タイトルから開始）時に、進行状況を初期化します。
    /// </summary>
    public static void ResetAll()
    {
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
