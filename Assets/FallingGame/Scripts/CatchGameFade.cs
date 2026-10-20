using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sousakusai8.MiniGame
{
    // Shared by the Kayo and Moteru result transitions.
    internal static class CatchGameFade
    {
        internal const float ResultDisplayDuration = 2f;
        internal const float FadeDuration = 0.8f;

        internal static IEnumerator Play(Image overlay, float from, float to, float duration)
        {
            overlay.gameObject.SetActive(true);
            overlay.transform.SetAsLastSibling();
            overlay.raycastTarget = true;
            SetAlpha(overlay, from);
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetAlpha(overlay, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                    yield return null;
                }
            }
            SetAlpha(overlay, to);
            overlay.raycastTarget = to > 0f;
        }

        private static void SetAlpha(Image overlay, float alpha)
        {
            overlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }
    }
}
