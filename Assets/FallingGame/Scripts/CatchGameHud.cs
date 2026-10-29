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
        private Text startHint, controls, jumpStatus, sweepStatus;
        private Image timeFill, startShade;
        private RectTransform startCard;
        private GameObject startOverlay, feedbackPanel, jumpPanel, sweepPanel;
        private CanvasGroup feedbackGroup;
        private int lastScore = int.MinValue;
        private int lastSecond = -1;

        public static CatchGameHud Create(Text score, Text time, Text message, Text prompt,
            Text feedback, Text jumpNotice, Text sweepNotice, Font font)
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
            hud.Build();
            return hud;
        }

        private void Build()
        {
            RectTransform scorePanel = Panel("Score Panel", root, 0.78f);
            Place(scorePanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(204f, 78f));
            Style(score, scorePanel, 28, TextAnchor.MiddleLeft, Main);
            Region(score.rectTransform, new Vector2(0.09f, 0.08f), new Vector2(0.91f, 0.72f));
            Text scoreLabel = Label("Score Label", scorePanel, "スコア", 11, Muted);
            scoreLabel.alignment = TextAnchor.MiddleLeft;
            Region(scoreLabel.rectTransform, new Vector2(0.09f, 0.72f), new Vector2(0.91f, 0.96f));

            RectTransform timePanel = Panel("Time Panel", root, 0.78f);
            Place(timePanel, Vector2.one, Vector2.one, new Vector2(-16f, -16f), new Vector2(154f, 78f));
            Style(time, timePanel, 26, TextAnchor.MiddleRight, Main);
            Region(time.rectTransform, new Vector2(0.12f, 0.17f), new Vector2(0.88f, 0.75f));
            Text timeLabel = Label("Time Label", timePanel, "残り時間", 11, Muted);
            timeLabel.alignment = TextAnchor.MiddleRight;
            Region(timeLabel.rectTransform, new Vector2(0.12f, 0.73f), new Vector2(0.88f, 0.97f));
            RectTransform track = Panel("Time Track", timePanel, 0.2f, false);
            track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f);
            track.offsetMin = new Vector2(18f, 9f); track.offsetMax = new Vector2(-18f, 12f);
            timeFill = Panel("Time Remaining", track, 1f, false).GetComponent<Image>();
            timeFill.color = Main;
            Stretch(timeFill.rectTransform);

            RectTransform context = Panel("Round Context", root, 0.66f);
            Place(context, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(260f, 54f));
            Text title = Label("Game Title", context, "キャッチゲーム", 11, Muted);
            Region(title.rectTransform, new Vector2(0.1f, 0.57f), new Vector2(0.9f, 0.95f));
            if (message != null)
            {
                Style(message, context, 16, TextAnchor.MiddleCenter, Main);
                Region(message.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.6f));
            }

            RectTransform movePanel = Panel("Move Help", root, 0.62f);
            Place(movePanel, Vector2.zero, Vector2.zero, new Vector2(16f, 10f), new Vector2(188f, 28f));
            controls = Label("Move Keys", movePanel, "移動  A / D ・マウス", 12, Main);
            Inset(controls.rectTransform, 10f, 2f);
            RectTransform abilities = Panel("Ability Status", root, 0.68f);
            Place(abilities, Vector2.right, Vector2.right, new Vector2(-16f, 10f), new Vector2(460f, 28f));
            jumpStatus = Label("Jump Status", abilities, "", 12, Muted);
            Region(jumpStatus.rectTransform, new Vector2(0.025f, 0f), new Vector2(0.47f, 1f));
            sweepStatus = Label("Sweep Status", abilities, "", 12, Muted);
            Region(sweepStatus.rectTransform, new Vector2(0.49f, 0f), new Vector2(0.975f, 1f));

            feedbackPanel = Notice("Catch Feedback", feedback, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(300f, 34f), 16);
            if (feedbackPanel != null) feedbackGroup = feedbackPanel.AddComponent<CanvasGroup>();
            jumpPanel = Notice("Jump Acquired", jumpNotice, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(400f, 38f), 16);
            sweepPanel = Notice("Sweep Acquired", sweepNotice, new Vector2(0.5f, 0.72f), new Vector2(0f, -44f), new Vector2(400f, 38f), 16);

            startShade = Panel("Start Overlay", root, 0.3f, false).GetComponent<Image>();
            startOverlay = startShade.gameObject;
            Stretch(startShade.rectTransform);
            startCard = Panel("Start Card", startShade.transform, 0.86f);
            startCard.anchorMin = new Vector2(0.18f, 0.30f);
            startCard.anchorMax = new Vector2(0.82f, 0.70f);
            startCard.offsetMin = startCard.offsetMax = Vector2.zero;
            Text heading = Label("Start Heading", startCard, "キャッチゲーム", 13, Muted);
            Region(heading.rectTransform, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.96f));
            Style(prompt, startCard, 26, TextAnchor.MiddleCenter, Main);
            Region(prompt.rectTransform, new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.8f));
            startHint = Label("Start Hint", startCard, "", 14, Muted);
            Region(startHint.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.44f));
        }

        public void Present(bool waiting, bool countdown, bool finished, int value, float remaining, float duration,
            bool jumpUnlocked, bool sweepUnlocked, float sweepCooldown, int jumpThreshold, int sweepThreshold,
            int sweepCost, int goodScore, int penalty, float feedbackUntil)
        {
            root.gameObject.SetActive(!finished);
            if (finished) return;
            // Legacy HUD writers can still update their fields; presentation owns the formatted values.
            if (lastScore != value || score.text.StartsWith("スコア"))
            {
                score.text = value.ToString("N0");
                lastScore = value;
            }
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
            jumpStatus.text = jumpUnlocked ? "ジャンプ  Space / W" : $"ジャンプ  {jumpThreshold:N0}点で解放";
            jumpStatus.color = jumpUnlocked ? Main : Muted;
            sweepStatus.text = !sweepUnlocked ? $"一掃  {sweepThreshold:N0}点で解放"
                : sweepCooldown > 0f ? $"一掃  あと{Mathf.CeilToInt(sweepCooldown):00}秒"
                : $"一掃  Shift / 右クリック  −{sweepCost}";
            sweepStatus.color = sweepUnlocked && sweepCooldown <= 0f ? Main : Muted;
            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(feedback.gameObject.activeSelf);
                feedback.color = feedback.text.StartsWith("+") ? Positive : feedback.text.StartsWith("-") ? Warning : Main;
                feedbackGroup.alpha = Mathf.Clamp01((feedbackUntil - Time.unscaledTime) / 0.18f);
            }
            if (jumpPanel != null) { jumpPanel.SetActive(jumpNotice.gameObject.activeSelf); jumpNotice.color = Positive; }
            if (sweepPanel != null) { sweepPanel.SetActive(sweepNotice.gameObject.activeSelf); sweepNotice.color = Positive; }
            startOverlay.SetActive(waiting || countdown);
            if (waiting || countdown)
            {
                prompt.fontSize = countdown ? 48 : 26;
                startHint.text = countdown ? "まもなくスタート"
                    : $"<color=#ADE5C9>小物 +{goodScore}</color>　 /　 <color=#FFA397>皿 −{penalty}</color>\nキーまたはクリックでスタート";
                startShade.color = new Color(0f, 0f, 0f, countdown ? 0.18f : 0.3f);
            }
        }

        public void Hide() { if (root != null) root.gameObject.SetActive(false); }

        private GameObject Notice(string name, Text label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            if (label == null) return null;
            RectTransform panel = Panel(name, root, 0.78f);
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
