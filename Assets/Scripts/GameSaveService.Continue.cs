using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class GameSaveService
{
    private const string ContinueKey = "GameSave.Continue.v1";

    public static GameSaveData ReadContinue(out string message) => ReadCheckpoint(ContinueKey, out message);

    public static bool ReturnToTitle(out string message)
    {
        if (!Application.CanStreamedLevelBeLoaded("Title"))
        {
            message = "タイトルシーンが見つかりません。";
            return false;
        }
        if (!TryCapture(out var data, out message)) return false;
        foreach (var dialogue in UnityEngine.Object.FindObjectsByType<NovelDialogueController>(FindObjectsSortMode.None))
            dialogue.SaveProgressForContinuation();
        data.dialogueProgressJson = DialogueProgressStore.CaptureTimeline().json;
        data.timingResults = TimingGameResults.Capture();
        if (!WriteCheckpoint(ContinueKey, data, out message)) return false;

        ArchiveManager.Close();
        try
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Title", LoadSceneMode.Single);
            message = null;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ArchiveManager.Instance.OpenArchive();
            message = "タイトルに戻れませんでした。現在の場面を続けられます。";
            return false;
        }
    }

    public static bool Continue(out string message)
    {
        var data = ReadContinue(out message);
        if (data == null) { message ??= "続きのデータがありません。"; return false; }
        return LoadCheckpoint(data, out message, true);
    }
}
