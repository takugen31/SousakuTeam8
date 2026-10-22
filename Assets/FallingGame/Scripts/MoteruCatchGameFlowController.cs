using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sousakusai8.MiniGame
{
    public sealed class MoteruCatchGameFlowController : MonoBehaviour
    {
        [SerializeField] private CatchMiniGameController miniGame;
        [SerializeField, Min(0f)] private float resultDisplayDuration = CatchGameFade.ResultDisplayDuration;
        [SerializeField, Min(0f)] private float fadeDuration = CatchGameFade.FadeDuration;
        [SerializeField] private string returnSceneName = "NovelScene_Moteru";
        [Tooltip("ゲーム終了後、このシナリオの先頭のセリフから再開します。")]
        [SerializeField] private DialogueScenarioSO resumeScenario;

        private Image fadeOverlay;
        private bool returning;

        private void Awake()
        {
            if (miniGame == null) miniGame = GetComponent<CatchMiniGameController>();
            if (miniGame == null || resumeScenario == null)
            {
                Debug.LogError("モテルのキャッチゲーム本体と再開シナリオを設定してください。", this);
                enabled = false;
                return;
            }
            BuildFadeOverlay();
            miniGame.RoundEnded += OnRoundEnded;
        }

        private void OnRoundEnded(int finalScore)
        {
            if (returning) return;
            if (!resumeScenario.TryGetFirstLine(out DialogueLine firstLine) ||
                string.IsNullOrWhiteSpace(firstLine.lineId) ||
                !Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                Debug.LogError("モテルの戻り先シーンまたは再開シナリオが不正です。", this);
                return;
            }
            returning = true;
            StartCoroutine(ReturnToNovel(firstLine.lineId));
        }

        private IEnumerator ReturnToNovel(string resumeLineId)
        {
            // RoundEnded is raised after the score/results have been displayed.
            yield return new WaitForSecondsRealtime(resultDisplayDuration);
            ArchiveManager.Close();
            yield return CatchGameFade.Play(fadeOverlay, 0f, 1f, fadeDuration);
            // The result selects Chapter_Moteru_2 in the progress timeline.
            if (!DialogueProgressTransitions.ReturnToScene(returnSceneName, resumeLineId))
            {
                fadeOverlay.gameObject.SetActive(false);
                returning = false;
            }
        }

        private void BuildFadeOverlay()
        {
            GameObject canvasObject = new GameObject("MoteruCatchTransitionCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672f, 941f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject overlayObject = new GameObject("MoteruCatchFadeOverlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            fadeOverlay = overlayObject.GetComponent<Image>();
            RectTransform rect = fadeOverlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            fadeOverlay.raycastTarget = false;
            overlayObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (miniGame != null) miniGame.RoundEnded -= OnRoundEnded;
        }
    }
}
