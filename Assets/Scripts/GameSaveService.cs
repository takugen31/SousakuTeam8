using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public string timestamp;
    public string scene;
    public string kind;
    public string label;
    public string text;
    public string scenario;
    public string lineId;
    public string stopLineId;
    public bool textFullyDisplayed;
    public string[] revealedNames;
    public string[] consultationLines;
    public GameSaveImage[] images;
    public GameSavePreference[] progress;
    public int room;
    public string searchMode;
    public string itemId;
    public bool paperVisible;
    public bool introDismissed;
    public bool kayoGaugeSucceeded;
    public bool hasPresentationState;
    public int visibleCharacters;
    public bool autoPlayEnabled;
    public string[] appliedLineIds;
    public DialogueProgressImage[] presentation;
    public string dialogueProgressJson;
    public TimingGameResults.Snapshot[] timingResults;
}

[Serializable]
public sealed class GameSavePreference
{
    public string key;
    public bool present;
    public string value;
}

[Serializable]
public sealed class GameSaveImage
{
    public string sprite;
    public string texture;
    public Rect rect;
    public bool enabled;
    public Vector2 position;
    public Vector3 scale;
}

/// <summary>Six manual checkpoints. New game resets progress, never these slots.</summary>
public static partial class GameSaveService
{
    public const int SlotCount = 6;
    private const string Prefix = "GameSave.Slot.v1.";
    private static GameSaveData pending;
    private static readonly string[] scenes = {
        "NovelScene", "NovelScene_Kayo", "NovelScene_Moteru", "NovelScene_Yowashi",
        "Chapter1_SearchScene", "Chapter_Kayo_SearchScene", "Chapter_Moteru_SearchScene", "SelectionPart"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPending() { pending = null; }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ClearSlotsOnPlay(UnityEditor.EnterPlayModeOptions options)
    {
        pending = null;
        for (int i = 0; i < SlotCount; i++) PlayerPrefs.DeleteKey(Prefix + i);
        PlayerPrefs.DeleteKey(ContinueKey);
        PlayerPrefs.Save();
    }
#endif

    public static GameSaveData Consume(string scene, string kind)
    {
        if (pending == null || pending.scene != scene || pending.kind != kind) return null;
        GameSaveData result = pending;
        pending = null;
        return result;
    }

    public static bool CanSave(out string reason)
    {
        return TryCapture(out _, out reason);
    }

    private static bool TryCapture(out GameSaveData data, out string reason)
    {
        data = new GameSaveData { scene = SceneManager.GetActiveScene().name };
        reason = "この場面ではセーブできません。";
        if (Array.IndexOf(scenes, data.scene) < 0) return false;
        Chapter1SearchController common = UnityEngine.Object.FindFirstObjectByType<Chapter1SearchController>();
        SearchSceneController search = UnityEngine.Object.FindFirstObjectByType<SearchSceneController>();
        SelectionPartController selection = UnityEngine.Object.FindFirstObjectByType<SelectionPartController>();
        bool ready;
        if (common != null) ready = common.CaptureCheckpoint(data);
        else if (search != null) ready = search.CaptureCheckpoint(data);
        else if (selection != null) ready = selection.CaptureCheckpoint(data);
        else
        {
            ready = false;
            foreach (NovelDialogueController dialogue in UnityEngine.Object.FindObjectsByType<NovelDialogueController>(FindObjectsSortMode.None))
                if (dialogue.gameObject.scene.name == data.scene && dialogue.CaptureCheckpoint(data)) { ready = true; break; }
        }
        if (!ready) { reason = "画面の切り替えや確認表示が終わってからセーブしてください。"; return false; }
        data.revealedNames = NovelDialogueController.CaptureRevealedNames();
        KayoGaugeOutcomeController gauge = UnityEngine.Object.FindFirstObjectByType<KayoGaugeOutcomeController>();
        data.kayoGaugeSucceeded = gauge != null && gauge.CurrentResult;
        reason = null;
        return true;
    }

    public static bool Save(int slot, out string message)
    {
        if (slot < 0 || slot >= SlotCount) { message = "保存先が無効です。"; return false; }
        if (!TryCapture(out GameSaveData data, out message)) return false;
        return WriteCheckpoint(Prefix + slot, data, out message);
    }

    private static bool WriteCheckpoint(string key, GameSaveData data, out string message)
    {
        data.timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        bool existed = PlayerPrefs.HasKey(key);
        string previous = PlayerPrefs.GetString(key, "");
        try
        {
            data.progress = GameProgress.CaptureCheckpoint();
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            message = "セーブしました。";
            return true;
        }
        catch (Exception exception)
        {
            if (existed) PlayerPrefs.SetString(key, previous);
            else PlayerPrefs.DeleteKey(key);
            try { PlayerPrefs.Save(); } catch (Exception) { }
            Debug.LogException(exception);
            message = "セーブできませんでした。";
            return false;
        }
    }

    public static GameSaveData Read(int slot, out string message)
    {
        message = null;
        if (slot < 0 || slot >= SlotCount) { message = "保存先が無効です。"; return null; }
        return ReadCheckpoint(Prefix + slot, out message);
    }

    private static GameSaveData ReadCheckpoint(string key, out string message)
    {
        message = null;
        if (!PlayerPrefs.HasKey(key)) return null;
        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(key));
            if (!IsValid(data)) { message = "このセーブデータは読み込めません。"; return null; }
            return data;
        }
        catch (Exception) { message = "セーブデータが壊れています。"; return null; }
    }

    public static bool IsValid(GameSaveData data)
    {
        if (data == null || data.version != 1 || Array.IndexOf(scenes, data.scene) < 0 ||
            data.progress == null || data.revealedNames == null || string.IsNullOrEmpty(data.timestamp)) return false;
        string expectedKind = data.scene == "SelectionPart" ? "selection"
            : data.scene.Contains("SearchScene") ? "search" : "dialogue";
        if (data.kind != expectedKind) return false;
        if (data.kind == "dialogue" || data.searchMode == "dialogue")
            if (string.IsNullOrEmpty(data.lineId) || string.IsNullOrEmpty(data.scenario) ||
                data.images == null || data.images.Length != 3 || Array.Exists(data.images, image => image == null)) return false;
        if (data.kind == "search" && (data.room < 0 || data.room > 1 ||
            (data.searchMode != "room" && data.searchMode != "modal" && data.searchMode != "dialogue") ||
            (data.searchMode != "room" && string.IsNullOrEmpty(data.itemId)))) return false;
        return GameProgress.IsValidCheckpoint(data.progress);
    }

    public static bool Load(int slot, out string message)
    {
        GameSaveData data = Read(slot, out message);
        if (data == null) { message ??= "この枠にはセーブデータがありません。"; return false; }
        return LoadCheckpoint(data, out message);
    }

    private static bool LoadCheckpoint(GameSaveData data, out string message, bool continuing = false)
    {
        if (!Application.CanStreamedLevelBeLoaded(data.scene)) { message = "保存した場面が見つかりません。"; return false; }
        GameSavePreference[] previousProgress;
        try { previousProgress = GameProgress.CaptureCheckpoint(); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            message = "現在の進行を保護できないため、ロードを中止しました。";
            return false;
        }
        string[] previousNames = NovelDialogueController.CaptureRevealedNames();
        DialogueProgressStore.Timeline previousTimeline = DialogueProgressStore.CaptureTimeline();
        var previousResults = TimingGameResults.Capture();
        ArchiveManager.Close();
        try
        {
            // A manual load starts a new timeline. Outgoing scene callbacks cannot re-save the future.
            DialogueProgressStore.Clear();
            if (continuing)
            {
                DialogueProgressStore.RestoreTimeline(new DialogueProgressStore.Timeline
                {
                    json = data.dialogueProgressJson,
                    generation = DialogueProgressStore.Generation
                });
                TimingGameResults.Restore(data.timingResults);
            }
            NovelDialogueController.RestoreRevealedNames(data.revealedNames);
            GameProgress.RestoreCheckpoint(data.progress);
            pending = data;
            Time.timeScale = 1f;
            SceneManager.LoadScene(data.scene, LoadSceneMode.Single);
            message = null;
            return true;
        }
        catch (Exception exception)
        {
            pending = null;
            TimingGameResults.Restore(previousResults);
            try { DialogueProgressStore.RestoreTimeline(previousTimeline); }
            catch (Exception rollbackException) { Debug.LogException(rollbackException); }
            try { GameProgress.RestoreCheckpoint(previousProgress); }
            catch (Exception rollbackException) { Debug.LogException(rollbackException); }
            NovelDialogueController.RestoreRevealedNames(previousNames);
            Debug.LogException(exception);
            ArchiveManager.Instance.OpenArchive();
            message = "ロードできませんでした。現在の進行に戻ります。";
            return false;
        }
    }
}
