using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲージゲーム未実装の間はCSVの成功／失敗選択で結果を受け取る。
/// 本実装時も救助後のChapter 6を開始する前にSetResultを呼ぶ。
/// </summary>
public sealed class KayoGaugeOutcomeController : MonoBehaviour
{
    [SerializeField] private NovelDialogueController dialogueController;
    [SerializeField] private DialogueScenarioSO rescueAftermath;
    [SerializeField] private string successLineId = "chapter_kayo_5_success_001";
    [SerializeField] private string failureLineId = "chapter_kayo_5_failure_001";
    [Tooltip("原文の『ゲージゲームをクリアしていた場合』の直前の行。")]
    [SerializeField] private string lastCommonLineId = "chapter_kayo_6_056";

    private DialogueScenarioSO failureAftermath;

    private void Awake()
    {
        if (dialogueController == null || rescueAftermath == null)
        {
            Debug.LogError("カヨのゲージ結果接続に会話とChapter 6を設定してください。", this);
            enabled = false;
            return;
        }

        // Assetは変更せず、失敗時だけ成功限定の会話を除いた再生用コピーを使う。
        failureAftermath = Instantiate(rescueAftermath);
        failureAftermath.name = rescueAftermath.name + " (Gauge Failure)";
        var commonLines = new List<DialogueLine>();
        bool foundBoundary = false;
        foreach (DialogueLine line in failureAftermath.Lines)
        {
            commonLines.Add(line);
            if (line.lineId != lastCommonLineId) continue;
            line.nextLineId = string.Empty;
            foundBoundary = true;
            break;
        }

        if (!foundBoundary)
        {
            Debug.LogError("カヨのゲージ成功限定会話の境界が見つかりません。", this);
            Destroy(failureAftermath);
            failureAftermath = null;
            enabled = false;
            return;
        }

        failureAftermath.ReplaceAll(commonLines);
        dialogueController.LineStarted += OnLineStarted;
        // 結果を選ばず章スキップした場合も、紙の発見を成功扱いにしない。
        SetResult(false);
    }

    public void SetResult(bool succeeded)
    {
        if (failureAftermath == null || dialogueController == null) return;
        DialogueScenarioSO target = succeeded ? rescueAftermath : failureAftermath;
        DialogueScenarioSO previous = succeeded ? failureAftermath : rescueAftermath;
        dialogueController.ReplaceFollowingScenario(previous, target);
    }

    private void OnLineStarted(DialogueLine line)
    {
        if (line.lineId == successLineId) SetResult(true);
        else if (line.lineId == failureLineId) SetResult(false);
    }

    private void OnDestroy()
    {
        if (dialogueController != null) dialogueController.LineStarted -= OnLineStarted;
        if (failureAftermath != null) Destroy(failureAftermath);
    }
}
