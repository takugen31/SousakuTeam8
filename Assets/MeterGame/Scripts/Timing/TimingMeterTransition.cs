using System.Collections;
using UnityEngine;

namespace SousakuTeam8.MeterGame.Timing
{
    public sealed class TimingMeterTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup uiGroup;
        public void ResetVisuals() { if (uiGroup != null) uiGroup.alpha = 1f; }

        public IEnumerator FadeOut(TimingMeterSettings settings)
        {
            if (!settings.useFade) yield break;
            yield return Fade(0f, settings.fadeOutSeconds);
            if (settings.fadeHoldSeconds > 0f) yield return new WaitForSeconds(settings.fadeHoldSeconds);
        }

        public IEnumerator FadeIn(TimingMeterSettings settings)
        {
            if (settings.useFade) yield return Fade(1f, settings.fadeInSeconds);
            else ResetVisuals();
        }

        private IEnumerator Fade(float destination, float seconds)
        {
            if (uiGroup == null) yield break;
            float initial = uiGroup.alpha;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
                uiGroup.alpha = Mathf.Lerp(initial, destination, t);
                yield return null;
            }
            uiGroup.alpha = destination;
        }
    }
}
