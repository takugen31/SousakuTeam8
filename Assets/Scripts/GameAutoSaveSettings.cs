using UnityEngine;

/// <summary>Player-facing automatic checkpoint setting, independent of dialogue progress JSON.</summary>
public static class GameAutoSaveSettings
{
    private const string EnabledKey = "GameSave.Auto.Enabled.v1";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
        set
        {
            if (Enabled == value) return;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
            GameAutoSaveController.CancelPending();
        }
    }
}
