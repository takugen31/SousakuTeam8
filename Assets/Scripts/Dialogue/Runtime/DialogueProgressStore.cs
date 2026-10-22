using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistence and settings only. Never restores global affection or acquisitions.</summary>
public static class DialogueProgressStore
{
    private const string StateKey = "Dialogue.SceneProgress.v1";
    private const string EnabledKey = "Dialogue.SceneProgress.Enabled.v1";
    private static Dictionary<string, DialogueProgressState> entries;
    public static int Generation { get; private set; }
    public static event Action SettingsChanged;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
        set
        {
            if (Enabled == value) return;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            // Starting again with ON must not resurrect a position from before OFF.
            Clear();
            NovelDialogueController.ClearResumeRequest();
            SettingsChanged?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries = null;
        Generation = 0;
        SettingsChanged = null;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ClearOnPlay(UnityEditor.EnterPlayModeOptions options)
    {
        // The project's affection/acquisition stores also reset on entering Play Mode.
        Clear();
    }
#endif

    public static bool TryGet(string key, out DialogueProgressState state)
    {
        EnsureLoaded();
        state = null;
        return Enabled && entries.TryGetValue(key, out state);
    }

    public static void Save(DialogueProgressState state, int generation)
    {
        if (!Enabled || generation != Generation || !IsValid(state)) return;
        EnsureLoaded();
        entries[state.key] = state;
        Flush();
    }

    public static void Clear()
    {
        Generation++;
        entries = new Dictionary<string, DialogueProgressState>(StringComparer.Ordinal);
        PlayerPrefs.DeleteKey(StateKey);
        PlayerPrefs.Save();
    }

    // Used to protect this separate timeline if a manual load fails before scene entry.
    public sealed class Timeline
    {
        internal string json;
        internal int generation;
    }

    public static Timeline CaptureTimeline() => new Timeline
    {
        json = PlayerPrefs.GetString(StateKey, ""), generation = Generation
    };

    public static void RestoreTimeline(Timeline timeline)
    {
        if (string.IsNullOrEmpty(timeline.json)) PlayerPrefs.DeleteKey(StateKey);
        else PlayerPrefs.SetString(StateKey, timeline.json);
        Generation = timeline.generation;
        entries = null;
        PlayerPrefs.Save();
    }

    private static void EnsureLoaded()
    {
        if (entries != null) return;
        entries = new Dictionary<string, DialogueProgressState>(StringComparer.Ordinal);
        if (!PlayerPrefs.HasKey(StateKey)) return;
        try
        {
            var archive = JsonUtility.FromJson<DialogueProgressArchive>(PlayerPrefs.GetString(StateKey));
            if (archive == null || archive.version != 1 || archive.entries == null) return;
            foreach (var state in archive.entries)
                if (IsValid(state)) entries[state.key] = state;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("会話プログレスを読み込めないため、通常の開始位置を使用します。 " + exception.Message);
        }
    }

    private static bool IsValid(DialogueProgressState state) => state != null &&
        !string.IsNullOrEmpty(state.key) && !string.IsNullOrEmpty(state.scenario) &&
        !string.IsNullOrEmpty(state.lineId) && state.visibleCharacters >= 0 &&
        state.revealedNames != null && state.consultationLines != null && state.appliedLineIds != null &&
        state.images != null && state.images.Length == 3 && Array.TrueForAll(state.images, image => image != null);

    private static void Flush()
    {
        try
        {
            var archive = new DialogueProgressArchive { entries = new List<DialogueProgressState>(entries.Values) };
            PlayerPrefs.SetString(StateKey, JsonUtility.ToJson(archive));
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("会話プログレスを保存できませんでした。 " + exception.Message);
        }
    }
}
