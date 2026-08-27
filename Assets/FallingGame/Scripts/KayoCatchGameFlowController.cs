using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sousakusai8.MiniGame
{
    public sealed class KayoCatchGameFlowController : MonoBehaviour
    {
        private enum EventPhase
        {
            None,
            SuccessBeforeTenMinutes,
            SuccessAfterTenMinutes,
            Failure,
            Transitioning
        }

        private static readonly Color Gold = new(1f, 0.72f, 0.16f, 1f);
        private static readonly Color DarkPanel = new(0.012f, 0.02f, 0.032f, 0.94f);
        private static readonly Color MainText = new(0.96f, 0.97f, 0.94f, 1f);

        [Header("Game")]
        [SerializeField] private CatchMiniGameController miniGame;
        [SerializeField] private int successScoreExclusive = 3000;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 2f;

        [Header("Event CSV")]
        [SerializeField] private TextAsset successBeforeTenMinutesCsv;
        [SerializeField] private TextAsset successAfterTenMinutesCsv;
        [SerializeField] private TextAsset failureCsv;
        [SerializeField] private CharacterDatabaseSO characterDatabase;
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private Sprite conversationBackground;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.8f;
        [SerializeField, Min(0f)] private float tenMinutesTitleDuration = 1.6f;
        [SerializeField] private string returnSceneName = "NovelScene_Kayo";
        [SerializeField] private string resumeLineId = "chapter_kayo_2_001";

        private DialogueScenarioSO successBeforeScenario;
        private DialogueScenarioSO successAfterScenario;
        private DialogueScenarioSO failureScenario;
        private NovelDialogueController eventDialogueController;
        private GameObject dialogueRoot;
        private GameObject speakerPlate;
        private TMP_Text speakerText;
        private TMP_Text dialogueText;
        private Image dialogueBackground;
        private Image leftPortrait;
        private Image rightPortrait;
        private Image fadeOverlay;
        private TMP_Text intertitleText;
        private EventPhase eventPhase;
        private bool roundSequenceStarted;

        private void Awake()
        {
            if (miniGame == null)
            {
                miniGame = GetComponent<CatchMiniGameController>();
            }

            if (miniGame == null || characterDatabase == null ||
                successBeforeTenMinutesCsv == null ||
                successAfterTenMinutesCsv == null ||
                failureCsv == null ||
                conversationBackground == null)
            {
                Debug.LogError("カヨのキャッチゲーム後イベント設定が不足しています。", this);
                enabled = false;
                return;
            }

            successBeforeScenario = DialogueRuntimeCsv.CreateScenario(
                successBeforeTenMinutesCsv,
                conversationBackground);
            successAfterScenario = DialogueRuntimeCsv.CreateScenario(
                successAfterTenMinutesCsv,
                conversationBackground);
            failureScenario = DialogueRuntimeCsv.CreateScenario(
                failureCsv,
                conversationBackground);

            BuildEventInterface();
            miniGame.RoundEnded += OnRoundEnded;
        }

        private void OnDestroy()
        {
            if (miniGame != null)
            {
                miniGame.RoundEnded -= OnRoundEnded;
            }

            if (eventDialogueController != null)
            {
                eventDialogueController.DialogueCompleted -= OnEventDialogueCompleted;
            }

            DestroyRuntimeScenario(successBeforeScenario);
            DestroyRuntimeScenario(successAfterScenario);
            DestroyRuntimeScenario(failureScenario);
        }

        private void OnRoundEnded(int finalScore)
        {
            if (roundSequenceStarted)
            {
                return;
            }

            roundSequenceStarted = true;
            StartCoroutine(ShowEventAfterResult(finalScore));
        }

        private IEnumerator ShowEventAfterResult(int finalScore)
        {
            yield return new WaitForSecondsRealtime(resultDisplayDuration);
            yield return Fade(0f, 1f);

            miniGame.HideGameOverPresentation();
            DialogueScenarioSO scenario;

            if (finalScore > successScoreExclusive)
            {
                eventPhase = EventPhase.SuccessBeforeTenMinutes;
                scenario = successBeforeScenario;
            }
            else
            {
                eventPhase = EventPhase.Failure;
                scenario = failureScenario;
            }

            PlayEventScenario(scenario);
            yield return Fade(1f, 0f);
        }

        private void OnEventDialogueCompleted()
        {
            if (eventPhase == EventPhase.SuccessBeforeTenMinutes)
            {
                StartCoroutine(ShowTenMinutesIntertitle());
                return;
            }

            if (eventPhase == EventPhase.SuccessAfterTenMinutes ||
                eventPhase == EventPhase.Failure)
            {
                StartCoroutine(ReturnToKayoNovelScene());
            }
        }

        private IEnumerator ShowTenMinutesIntertitle()
        {
            eventPhase = EventPhase.Transitioning;
            yield return Fade(0f, 1f);

            intertitleText.text = "十分後——。";
            intertitleText.gameObject.SetActive(true);

            if (tenMinutesTitleDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(tenMinutesTitleDuration);
            }

            PlayEventScenario(successAfterScenario);
            eventPhase = EventPhase.SuccessAfterTenMinutes;
            intertitleText.gameObject.SetActive(false);
            yield return Fade(1f, 0f);
        }

        private IEnumerator ReturnToKayoNovelScene()
        {
            eventPhase = EventPhase.Transitioning;
            yield return Fade(0f, 1f);

            if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                intertitleText.text = $"シーン「{returnSceneName}」を読み込めません。";
                intertitleText.gameObject.SetActive(true);
                Debug.LogError(intertitleText.text, this);
                yield break;
            }

            NovelDialogueController.QueueResumeLineIfEmpty(resumeLineId, true);
            SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
        }

        private void PlayEventScenario(DialogueScenarioSO scenario)
        {
            eventDialogueController.ConfigureEmbeddedDialogue(
                scenario,
                characterDatabase,
                dialogueRoot,
                speakerPlate,
                speakerText,
                dialogueText,
                leftPortrait,
                rightPortrait,
                dialogueBackground);
            eventDialogueController.StartEmbeddedDialogue();
        }

        private void BuildEventInterface()
        {
            GameObject canvasObject = new(
                "KayoCatchEventCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            dialogueRoot = CreateImage(
                "KayoCatchDialogueRoot",
                canvasObject.transform,
                new Color(0f, 0f, 0f, 0.28f),
                true).gameObject;
            Stretch(dialogueRoot.GetComponent<RectTransform>());

            dialogueBackground = CreateImage(
                "ConversationBackground",
                dialogueRoot.transform,
                Color.white,
                false);
            Stretch(dialogueBackground.rectTransform);
            dialogueBackground.sprite = conversationBackground;
            dialogueBackground.preserveAspect = false;

            leftPortrait = CreateImage("LeftPortrait", dialogueRoot.transform, Color.white, false);
            SetAnchors(leftPortrait.gameObject, new Vector2(0.015f, 0.12f), new Vector2(0.30f, 0.89f));
            leftPortrait.preserveAspect = true;
            leftPortrait.enabled = false;

            rightPortrait = CreateImage("RightPortrait", dialogueRoot.transform, Color.white, false);
            SetAnchors(rightPortrait.gameObject, new Vector2(0.70f, 0.12f), new Vector2(0.985f, 0.89f));
            rightPortrait.preserveAspect = true;
            rightPortrait.enabled = false;

            Image dialoguePanel = CreateImage("DialoguePanel", dialogueRoot.transform, DarkPanel, false);
            SetAnchors(dialoguePanel.gameObject, new Vector2(0.075f, 0.045f), new Vector2(0.925f, 0.32f));
            AddBorder(dialoguePanel.transform, 3f, Gold);

            Image speaker = CreateImage("SpeakerPlate", dialoguePanel.transform, DarkPanel, false);
            speakerPlate = speaker.gameObject;
            SetAnchors(speakerPlate, new Vector2(0.035f, 0.73f), new Vector2(0.25f, 0.98f));
            AddBorder(speakerPlate.transform, 3f, Gold);

            speakerText = CreateText("SpeakerName", speakerPlate.transform, string.Empty, 22f, MainText, FontStyles.Bold);
            Stretch(speakerText.rectTransform, 18f, 18f, 0f, 0f);
            speakerText.alignment = TextAlignmentOptions.MidlineLeft;

            dialogueText = CreateText("DialogueText", dialoguePanel.transform, string.Empty, 29f, MainText);
            SetAnchors(dialogueText.gameObject, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.72f));
            dialogueText.alignment = TextAlignmentOptions.TopLeft;

            GameObject controllerObject = new("KayoCatchEventDialogueController", typeof(RectTransform));
            controllerObject.transform.SetParent(canvasObject.transform, false);
            eventDialogueController = controllerObject.AddComponent<NovelDialogueController>();
            eventDialogueController.ConfigureEmbeddedDialogue(
                failureScenario,
                characterDatabase,
                dialogueRoot,
                speakerPlate,
                speakerText,
                dialogueText,
                leftPortrait,
                rightPortrait,
                dialogueBackground);
            eventDialogueController.DialogueCompleted += OnEventDialogueCompleted;
            dialogueRoot.SetActive(false);

            fadeOverlay = CreateImage(
                "KayoCatchFadeOverlay",
                canvasObject.transform,
                new Color(0f, 0f, 0f, 0f),
                false);
            Stretch(fadeOverlay.rectTransform);

            intertitleText = CreateText(
                "TenMinutesIntertitle",
                fadeOverlay.transform,
                string.Empty,
                48f,
                MainText,
                FontStyles.Bold);
            SetAnchors(intertitleText.gameObject, new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.62f));
            intertitleText.alignment = TextAlignmentOptions.Center;
            intertitleText.gameObject.SetActive(false);
        }

        private IEnumerator Fade(float startAlpha, float endAlpha)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.transform.SetAsLastSibling();
            fadeOverlay.raycastTarget = true;
            SetFadeAlpha(startAlpha);

            if (fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetFadeAlpha(Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(elapsed / fadeDuration)));
                    yield return null;
                }
            }

            SetFadeAlpha(endAlpha);
            fadeOverlay.raycastTarget = endAlpha > 0f;
        }

        private void SetFadeAlpha(float alpha)
        {
            fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }

        private Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
        {
            GameObject imageObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private TMP_Text CreateText(
            string objectName,
            Transform parent,
            string value,
            float fontSize,
            Color color,
            FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private void AddBorder(Transform parent, float thickness, Color color)
        {
            GameObject border = new("Border", typeof(RectTransform));
            border.transform.SetParent(parent, false);
            Stretch(border.GetComponent<RectTransform>());

            CreateEdge("Top", border.transform, color, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -thickness), Vector2.zero);
            CreateEdge("Bottom", border.transform, color, Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, thickness));
            CreateEdge("Left", border.transform, color, Vector2.zero, new Vector2(0f, 1f),
                Vector2.zero, new Vector2(thickness, 0f));
            CreateEdge("Right", border.transform, color, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-thickness, 0f), Vector2.zero);
        }

        private void CreateEdge(
            string objectName,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            Image edge = CreateImage(objectName, parent, color, false);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetAnchors(GameObject target, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(
            RectTransform rect,
            float left = 0f,
            float right = 0f,
            float bottom = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void DestroyRuntimeScenario(DialogueScenarioSO scenario)
        {
            if (scenario != null)
            {
                Destroy(scenario);
            }
        }
    }
}
