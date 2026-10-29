using UnityEngine;
using UnityEngine.UI;

namespace Sousakusai8.MiniGame
{
    /// <summary>Presentation only; scoring, input, unlocks and round timing remain with the game.</summary>
    public sealed class CatchGameHud : MonoBehaviour
    {
        private static readonly Color Main = new(0.96f, 0.96f, 0.94f, 1f);
        private static readonly Color Muted = new(0.78f, 0.8f, 0.8f, 1f);
        private static readonly Color Positive = new(0.68f, 0.9f, 0.79f, 1f);
        private static readonly Color Warning = new(1f, 0.64f, 0.59f, 1f);
        private RectTransform root;
        private Font font;
        private Text score, time, message, prompt, feedback, jumpNotice, sweepNotice;
        private Text startHint;
        private Image timeFill, startShade;
        private RectTransform startCard, scorePanel, feedbackRect;
        private GameObject startOverlay, feedbackPanel, jumpPanel, sweepPanel;
        private CanvasGroup feedbackGroup;
        private int lastScore = int.MinValue;
        private int lastSecond = -1;
        private Camera playfieldCamera;
        private Rect originalCameraRect;
        private Canvas hudCanvas;
        private RenderMode originalRenderMode;
        private Camera originalUiCamera;
        private const float PlayfieldHeight = 0.88f;

        public static CatchGameHud Create(Text score, Text time, Text message, Text prompt,
            Text feedback, Text jumpNotice, Text sweepNotice, Font font, Camera gameCamera = null)
        {
            if (score == null || time == null || prompt == null) return null;
            var view = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(CanvasGroup), typeof(CatchGameHud));
            view.transform.SetParent(score.transform.parent, false);
            var hud = view.GetComponent<CatchGameHud>();
            hud.root = view.GetComponent<RectTransform>();
            Stretch(hud.root);
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            hud.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.score = score; hud.time = time; hud.message = message; hud.prompt = prompt;
            hud.feedback = feedback; hud.jumpNotice = jumpNotice; hud.sweepNotice = sweepNotice;
            hud.playfieldCamera = gameCamera;
            if (gameCamera != null)
            {
                hud.originalCameraRect = gameCamera.rect;
                hud.hudCanvas = score.canvas;
                hud.originalRenderMode = hud.hudCanvas.renderMode;
                hud.originalUiCamera = hud.hudCanvas.worldCamera;
                // HUD stays screen-sized while the game renders below its compact header.
                hud.hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.ApplyPlayfieldViewport();
            }
            hud.Build();
            return hud;
        }

        private void Build()
        {
            RectTransform header = Panel("Status Header", root, 0.86f, false);
            header.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 1f);
            Region(header, new Vector2(0f, PlayfieldHeight), Vector2.one);
            scorePanel = Panel("Score Panel", header, 0.78f, false);
            Place(scorePanel, Vector2.up, Vector2.up, new Vector2(12f, -5f), new Vector2(100f, 46f));
            Style(score, scorePanel, 20, TextAnchor.MiddleLeft, Main);
            Region(score.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.92f, 0.64f));
            Text scoreLabel = Label("Score Label", scorePanel, "スコア", 11, Muted);
            scoreLabel.alignment = TextAnchor.MiddleLeft;
            Region(scoreLabel.rectTransform, new Vector2(0.08f, 0.64f), new Vector2(0.92f, 1f));

            RectTransform timePanel = Panel("Time Panel", header, 0.78f, false);
            Place(timePanel, Vector2.one, Vector2.one, new Vector2(-12f, -5f), new Vector2(88f, 46f));
            Style(time, timePanel, 18, TextAnchor.MiddleRight, Main);
            Region(time.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.64f));
            Text timeLabel = Label("Time Label", timePanel, "残り時間", 11, Muted);
            timeLabel.alignment = TextAnchor.MiddleRight;
            Region(timeLabel.rectTransform, new Vector2(0.08f, 0.64f), new Vector2(0.92f, 1f));
            RectTransform track = Panel("Time Track", timePanel, 0.2f, false);
            track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f);
            track.offsetMin = new Vector2(7f, 0f); track.offsetMax = new Vector2(-7f, 2f);
            timeFill = Panel("Time Remaining", track, 1f, false).GetComponent<Image>();
            timeFill.color = Main;
            Stretch(timeFill.rectTransform);

            RectTransform context = Panel("Round Context", header, 0f, false);
            Place(context, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(240f, 34f));
            if (message != null)
            {
                Style(message, context, 14, TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.3f, 1f));
                Inset(message.rectTransform, 4f, 2f);
            }

            feedbackPanel = Notice("Catch Feedback", feedback, Vector2.up, new Vector2(140f, -11f), new Vector2(150f, 30f), 14, header);
            if (feedbackPanel != null)
            {
                feedbackGroup = feedbackPanel.AddComponent<CanvasGroup>();
                feedbackRect = feedbackPanel.GetComponent<RectTransform>();
                feedbackRect.pivot = Vector2.up;
            }
            jumpPanel = Notice("Jump Acquired", jumpNotice, new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(240f, 34f), 14, header);
            sweepPanel = Notice("Sweep Acquired", sweepNotice, new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(240f, 34f), 14, header);

            startShade = Panel("Start Overlay", root, 0.3f, false).GetComponent<Image>();
            startOverlay = startShade.gameObject;
            Stretch(startShade.rectTransform);
            startCard = Panel("Start Card", startShade.transform, 0.86f);
            Place(startCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 96f));
            Style(prompt, startCard, 24, TextAnchor.MiddleCenter, Main);
            Region(prompt.rectTransform, new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.92f));
            startHint = Label("Start Hint", startCard, "", 14, Muted);
            Region(startHint.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.34f));
        }

        public void Present(bool waiting, bool countdown, bool finished, int value, float remaining, float duration,
            bool jumpUnlocked, bool sweepUnlocked, float sweepCooldown, int jumpThreshold, int sweepThreshold,
            int sweepCost, int goodScore, int penalty, float feedbackUntil)
        {
            root.gameObject.SetActive(!finished);
            if (finished) return;
            ApplyPlayfieldViewport();
            // Legacy HUD writers can still update their fields; presentation owns the formatted values.
            if (lastScore != value || score.text.StartsWith("スコア"))
            {
                score.text = value.ToString("N0");
                lastScore = value;
            }
            scorePanel.sizeDelta = new Vector2(Mathf.Clamp((score.preferredWidth + 4f) / 0.84f, 80f, 160f), 46f);
            int second = Mathf.CeilToInt(remaining);
            if (lastSecond != second || time.text.StartsWith("残り"))
            {
                time.text = $"{second / 60:00}:{second % 60:00}";
                lastSecond = second;
            }
            Color timerColor = !waiting && !countdown && remaining <= 10f ? Warning : Main;
            time.color = timerColor;
            timeFill.color = timerColor;
            timeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(remaining / Mathf.Max(duration, 1f)), 1f);
            bool statusFeedback = feedback != null && feedback.gameObject.activeSelf &&
                !feedback.text.StartsWith("+") && !feedback.text.StartsWith("-");
            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(feedback.gameObject.activeSelf);
                feedbackRect.sizeDelta = new Vector2(Mathf.Clamp(feedback.preferredWidth + 24f, 60f, 210f), 30f);
                feedbackRect.anchorMin = feedbackRect.anchorMax = statusFeedback ? new Vector2(0.5f, 1f) : Vector2.up;
                feedbackRect.pivot = statusFeedback ? new Vector2(0.5f, 1f) : Vector2.up;
                feedbackRect.anchoredPosition = statusFeedback ? new Vector2(0f, -11f) : new Vector2(scorePanel.sizeDelta.x + 22f, -11f);
                feedback.color = feedback.text.StartsWith("+") ? Positive : feedback.text.StartsWith("-") ? Warning : Main;
                feedbackGroup.alpha = Mathf.Clamp01((feedbackUntil - Time.unscaledTime) / 0.18f);
            }
            bool showSweep = sweepNotice != null && sweepNotice.gameObject.activeSelf && !waiting && !countdown && !statusFeedback;
            bool showJump = jumpNotice != null && jumpNotice.gameObject.activeSelf && !showSweep && !waiting && !countdown && !statusFeedback;
            if (jumpPanel != null) { jumpPanel.SetActive(showJump); jumpNotice.color = Positive; }
            if (sweepPanel != null) { sweepPanel.SetActive(showSweep); sweepNotice.color = Positive; }
            if (message != null) message.gameObject.SetActive(!waiting && !countdown && !showJump && !showSweep && !statusFeedback);
            startOverlay.SetActive(waiting || countdown);
            if (waiting || countdown)
            {
                prompt.fontSize = countdown ? 40 : 24;
                Region(prompt.rectTransform, new Vector2(0.06f, countdown ? 0.1f : 0.38f), new Vector2(0.94f, 0.92f));
                startHint.gameObject.SetActive(waiting);
                startHint.text = countdown ? "" : $"<color=#ADE5C9>小物 +{goodScore}</color>　 /　 <color=#FFA397>皿 −{penalty}</color>";
                startShade.color = new Color(0f, 0f, 0f, countdown ? 0.08f : 0.16f);
                startCard.sizeDelta = new Vector2(Mathf.Min(420f, root.rect.width * 0.7f), 96f);
            }
        }

        public void Hide() { if (root != null) root.gameObject.SetActive(false); RestorePlayfieldViewport(); }

        private void ApplyPlayfieldViewport()
        {
            if (playfieldCamera != null)
                playfieldCamera.rect = new Rect(originalCameraRect.x, originalCameraRect.y,
                    originalCameraRect.width, originalCameraRect.height * PlayfieldHeight);
        }

        private void RestorePlayfieldViewport() { if (playfieldCamera != null) playfieldCamera.rect = originalCameraRect; }
        private void OnDisable() { RestorePlayfieldViewport(); }
        private void OnDestroy()
        {
            RestorePlayfieldViewport();
            if (hudCanvas != null) { hudCanvas.renderMode = originalRenderMode; hudCanvas.worldCamera = originalUiCamera; }
        }

        private GameObject Notice(string name, Text label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, Transform parent)
        {
            if (label == null) return null;
            RectTransform panel = Panel(name, parent, 0.78f, false);
            Place(panel, anchor, new Vector2(0.5f, 1f), position, size);
            Style(label, panel, fontSize, TextAnchor.MiddleCenter, Main);
            Inset(label.rectTransform, 12f, 2f);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        private RectTransform Panel(string name, Transform parent, float alpha, bool feather = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;
            if (feather) go.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(10f, 6f));
            return go.GetComponent<RectTransform>();
        }

        private Text Label(string name, Transform parent, string text, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Text label = go.GetComponent<Text>();
            Style(label, parent, size, TextAnchor.MiddleCenter, color);
            label.text = text;
            return label;
        }

        private void Style(Text label, Transform parent, int size, TextAnchor alignment, Color color)
        {
            label.transform.SetParent(parent, false);
            label.rectTransform.localScale = Vector3.one;
            label.rectTransform.localRotation = Quaternion.identity;
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            label.color = color;
            label.alignment = alignment;
            label.supportRichText = true;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
            label.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static void Stretch(RectTransform rect) { Region(rect, Vector2.zero, Vector2.one); }
        private static void Inset(RectTransform rect, float x, float y)
        {
            Stretch(rect); rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y);
        }
        private static void Region(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
