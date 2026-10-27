using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame.Timing
{
    public sealed class TimingMeterFeedback : MonoBehaviour
    {
        [SerializeField] private MeterGameBarView bar;
        [SerializeField] private Shadow glow;
        [SerializeField] private Color successColor = new Color(1f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color failureColor = new Color(0.48f, 0.38f, 0.85f, 1f);
        private Vector3 baseScale;

        private void Awake() { if (bar != null) baseScale = bar.transform.localScale; }

        public void ResetVisuals()
        {
            if (bar != null)
            {
                bar.color = Color.white;
                if (baseScale != Vector3.zero) bar.transform.localScale = baseScale;
            }
            if (glow != null) glow.effectColor = Color.clear;
        }

        public IEnumerator Play(bool success, TimingMeterSettings settings)
        {
            float elapsed = 0f;
            Apply(success, 0f, settings.animateResults, settings.resultScaleAmount);
            while (elapsed < settings.resultDisplaySeconds)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / settings.resultDisplaySeconds);
                float pulse = Mathf.Sin(normalized * Mathf.PI * (success ? settings.successFlashCount : 1f));
                Apply(success, Mathf.Abs(pulse), settings.animateResults, settings.resultScaleAmount);
                yield return null;
            }
        }

        public void ShowResult(bool success, TimingMeterSettings settings)
            => Apply(success, 0f, settings.animateResults, settings.resultScaleAmount);

        public void ShowFinal(bool success) => Apply(success, 0.5f, true, 0f);

        private void Apply(bool success, float pulse, bool animate, float scaleAmount)
        {
            if (bar == null) return;
            Color tint = success ? successColor : failureColor;
            bar.color = Color.Lerp(Color.white, tint, success ? 0.35f + pulse * 0.35f : 0.8f);
            if (animate)
            {
                bar.transform.localScale = baseScale * (1f + (success ? 1f : -0.5f) * pulse * scaleAmount);
                if (glow != null)
                {
                    glow.effectColor = new Color(tint.r, tint.g, tint.b, 0.2f + pulse * 0.65f);
                    glow.effectDistance = new Vector2(0f, success ? 3f + pulse * 7f : -3f - pulse * 3f);
                }
            }
        }
    }
}
