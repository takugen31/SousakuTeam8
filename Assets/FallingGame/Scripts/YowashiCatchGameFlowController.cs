using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sousakusai8.MiniGame
{
    /// <summary>キャッチゲームの結果だけを既存のヨワシ会話へ渡す。</summary>
    public sealed class YowashiCatchGameFlowController : MonoBehaviour
    {
        [SerializeField] private CatchMiniGameController miniGame;
        [Tooltip("この点数を超えると成功。ちょうどの場合は失敗。")]
        [SerializeField] private int successScoreExclusive = 3000;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 2f;
        [SerializeField] private string returnSceneName = "NovelScene_Yowashi";
        [SerializeField] private string successResumeLineId = "chapter_yowashi_catchsuccess_001";
        [SerializeField] private string failureResumeLineId = "chapter_yowashi_catchfailure_001";

        private bool returning;

        public static bool IsSuccess(int finalScore, int thresholdExclusive)
        {
            return finalScore > thresholdExclusive;
        }

        private void Awake()
        {
            if (miniGame == null) miniGame = GetComponent<CatchMiniGameController>();
            if (miniGame == null)
            {
                Debug.LogError("ヨワシのキャッチゲーム接続にゲーム本体を設定してください。", this);
                enabled = false;
                return;
            }
            miniGame.RoundEnded += OnRoundEnded;
        }

        private void OnRoundEnded(int finalScore)
        {
            if (returning) return;
            string resume = miniGame.IsSuccessfulResult(finalScore, successScoreExclusive)
                ? successResumeLineId : failureResumeLineId;
            if (string.IsNullOrWhiteSpace(resume) ||
                !Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                Debug.LogError("ヨワシのキャッチゲームの戻り先・再開位置が不正です。", this);
                return;
            }
            returning = true;
            StartCoroutine(ReturnToNovel(resume));
        }

        private IEnumerator ReturnToNovel(string resumeLineId)
        {
            if (!miniGame.SkippedSuccessfully)
                yield return new WaitForSecondsRealtime(resultDisplayDuration);
            ArchiveManager.Close();
            // ゲーム前の会話を繰り返さず、結果に対応した原文の位置へ戻る。
            NovelDialogueController.QueueResumeLine(resumeLineId, true);
            SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (miniGame != null) miniGame.RoundEnded -= OnRoundEnded;
        }
    }
}
