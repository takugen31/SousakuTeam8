using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame
{
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class MeterGameInputPrompt : MonoBehaviour
    {
        [SerializeField] private MeterGameController controller;
        [SerializeField] private Text label;
        [SerializeField] private Shadow glow;
        [SerializeField] private bool animateEmphasis = true;
        [SerializeField] private bool animateShadow = true;
        [SerializeField] private string pressMessage = "Spaceキー : 増加、範囲内にゲージを留めよう！";
        [SerializeField] private string holdMessage = "Spaceキー : 増加、範囲内にゲージを留めよう！";
        [SerializeField, Min(0.1f), Tooltip("強調を繰り返す間隔（秒）")]
        private float emphasisInterval = 2.5f;
        [SerializeField, Min(0.1f), Tooltip("1回の強調アニメーションの長さ（秒）")]
        private float emphasisDuration = 0.7f;
        [SerializeField, Range(1f, 1.3f)] private float maximumScale = 1.1f;
        [SerializeField] private Color normalColor = new Color32(255, 244, 231, 255);
        [SerializeField] private Color emphasisColor = new Color32(255, 212, 111, 255);
        private Vector3 baseScale;
        private float elapsed;
        private bool externalPresentation;
        private bool externalVisible;
        private string externalMessage;

        public void SetPresentation(bool visible, string message)
        {
            if (!externalPresentation || externalVisible != visible || externalMessage != message)
                elapsed = 0f;
            externalPresentation = true;
            externalVisible = visible;
            externalMessage = message;
        }

        private void Awake()
        {
            if (label == null) label = GetComponent<Text>();
            baseScale = transform.localScale;
        }

        private void OnEnable() => elapsed = 0f;

        private void LateUpdate()
        {
            if (label == null) return;
            bool playing = externalPresentation ? externalVisible : controller == null || controller.State == null ||
                controller.State.Result == MeterGameResult.Playing;
            label.enabled = playing;
            if (!playing) return;

            label.text = externalPresentation ? externalMessage :
                controller != null && controller.InputMode == MeterInputMode.Hold ? holdMessage : pressMessage;
            if (!animateEmphasis)
            {
                transform.localScale = baseScale;
                label.color = normalColor;
                return;
            }
            // Externally supplied instructions/results animate once per message.
            elapsed = externalPresentation
                ? Mathf.Min(elapsed + Time.unscaledDeltaTime, emphasisDuration)
                : Mathf.Repeat(elapsed + Time.unscaledDeltaTime, emphasisInterval);
            float phase = Mathf.Clamp01(elapsed / emphasisDuration);
            float pulse = Mathf.Sin(phase * Mathf.PI);
            pulse *= pulse;
            transform.localScale = baseScale * Mathf.Lerp(1f, maximumScale, pulse);
            label.color = Color.Lerp(normalColor, emphasisColor, pulse);
            if (glow != null && animateShadow)
            {
                glow.effectColor = Color.Lerp(new Color(0.16f, 0.04f, 0.1f, 0.8f),
                    new Color(1f, 0.3f, 0.55f, 0.8f), pulse);
                glow.effectDistance = Vector2.Lerp(new Vector2(1f, -2f), new Vector2(3f, -4f), pulse);
            }
        }

        private void OnDisable()
        {
            if (baseScale != Vector3.zero) transform.localScale = baseScale;
            if (label != null) label.color = normalColor;
        }

        private void OnValidate()
        {
            emphasisInterval = Mathf.Max(0.1f, emphasisInterval);
            emphasisDuration = Mathf.Clamp(emphasisDuration, 0.1f, emphasisInterval);
        }
    }
}
