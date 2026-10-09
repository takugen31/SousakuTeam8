using System;
using UnityEngine;

/// <summary>既存の会話シーン遷移と日記パズルの往復処理を接続する。</summary>
public sealed class YowashiPuzzleEntryController : MonoBehaviour
{
    [SerializeField] private NovelDialogueController dialogueController;
    [SerializeField] private string puzzleSceneName = "PuzzleGame";
    [SerializeField] private string puzzleEntryLineId = "chapter_yowashi_1_035";
    [SerializeField] private string diaryResumeLineId = "chapter_yowashi_diary_001";
    [Tooltip("イラスト班から届いた日記画像を設定。未設定時は既存パズルの仮画像を使用。")]
    [SerializeField] private Texture2D diaryPuzzleImage;

    private void Awake()
    {
        if (dialogueController == null) dialogueController = GetComponent<NovelDialogueController>();
        if (dialogueController == null)
        {
            Debug.LogError("ヨワシの日記パズルに会話コントローラーを設定してください。", this);
            enabled = false;
            return;
        }
        dialogueController.SceneTransitionStarting += OnSceneTransitionStarting;
    }

    private void OnSceneTransitionStarting(DialogueLine line)
    {
        if (line == null || line.lineId != puzzleEntryLineId) return;
        string targetName = System.IO.Path.GetFileNameWithoutExtension(line.nextScenePath);
        if (targetName != puzzleSceneName) return;

        string returnSceneName = gameObject.scene.name;
        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
            throw new InvalidOperationException("ヨワシの日記パズルの戻り先Sceneが無効です。");

        // 通常送りと章スキップの両方が、この同じ遷移通知を通る。
        KayoPuzzleGameFlowController.Queue(
            puzzleSceneName, returnSceneName, diaryResumeLineId, diaryPuzzleImage);
    }

    private void OnDestroy()
    {
        if (dialogueController != null)
            dialogueController.SceneTransitionStarting -= OnSceneTransitionStarting;
    }
}
