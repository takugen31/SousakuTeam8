using System;
using UnityEngine;

/// <summary>
/// Reserves the meter game's return scene, character and resume lines right
/// before a Novel scene transitions to TimingMeterGame.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimingMeterGameEntryController : MonoBehaviour
{
    [SerializeField] private NovelDialogueController dialogueController;
    [SerializeField] private string meterSceneName = "TimingMeterGame";
    [SerializeField] private string meterEntryLineId = "chapter_kayo_5_025";
    [SerializeField] private string characterId = "kayo";
    [SerializeField] private string successResumeLineId = "chapter_kayo_meter_success_001";
    [SerializeField] private string failureResumeLineId = "chapter_kayo_meter_failure_001";

    private void Awake()
    {
        if (dialogueController == null) dialogueController = GetComponent<NovelDialogueController>();
        if (dialogueController == null)
        {
            Debug.LogError("メーターゲーム遷移に会話コントローラーを設定してください。", this);
            enabled = false;
            return;
        }
        dialogueController.SceneTransitionStarting += OnSceneTransitionStarting;
    }

    private void OnSceneTransitionStarting(DialogueLine line)
    {
        if (line == null) return;
        if (!string.IsNullOrWhiteSpace(meterEntryLineId) && line.lineId != meterEntryLineId) return;
        string targetName = System.IO.Path.GetFileNameWithoutExtension(line.nextScenePath);
        if (targetName != meterSceneName) return;

        string returnSceneName = gameObject.scene.name;
        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
            throw new InvalidOperationException("メーターゲームの戻り先Sceneが無効です。");

        TimingMeterGameFlowController.Queue(
            meterSceneName,
            returnSceneName,
            characterId,
            successResumeLineId,
            failureResumeLineId);
    }

    private void OnDestroy()
    {
        if (dialogueController != null)
            dialogueController.SceneTransitionStarting -= OnSceneTransitionStarting;
    }
}
