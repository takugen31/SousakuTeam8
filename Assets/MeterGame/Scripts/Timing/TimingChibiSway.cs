using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame.Timing
{
    /// <summary>
    /// Rocks a chibi character left and right for a gentle "swaying" effect.
    /// When an alternate sprite is provided it is swapped in each time the
    /// character changes tilt direction, so two poses alternate naturally.
    /// On the result screen the sway smoothly winds down until the character
    /// stands upright in its base pose.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class TimingChibiSway : MonoBehaviour
    {
        [SerializeField] private TimingMeterController controller;
        [SerializeField] private Sprite alternateSprite;
        [SerializeField, Min(0f)] private float swayAngle = 7f;
        [SerializeField, Min(0.01f)] private float swayPeriod = 1.8f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.6f;

        private Image image;
        private Sprite baseSprite;
        private float elapsed;
        private int lastDirection;
        private float amplitude;
        private float amplitudeVelocity;

        private void Awake()
        {
            image = GetComponent<Image>();
            baseSprite = image != null ? image.sprite : null;
        }

        private void OnEnable()
        {
            elapsed = 0f;
            lastDirection = 0;
            amplitude = swayAngle;
            amplitudeVelocity = 0f;
            transform.localRotation = Quaternion.identity;
            if (image != null && baseSprite != null) image.sprite = baseSprite;
        }

        private void Update()
        {
            if (image == null || swayPeriod <= 0f) return;

            bool showingResult = controller != null && controller.Phase == TimingMeterPhase.Complete;
            float targetAmplitude = showingResult ? 0f : swayAngle;
            amplitude = settleSeconds > 0f
                ? Mathf.SmoothDamp(amplitude, targetAmplitude, ref amplitudeVelocity, settleSeconds)
                : targetAmplitude;

            elapsed += Time.deltaTime;
            float phase = Mathf.Sin(elapsed * (Mathf.PI * 2f / swayPeriod));
            transform.localRotation = Quaternion.Euler(0f, 0f, phase * amplitude);

            if (showingResult)
            {
                // Keep the base pose while the tilt eases back to upright.
                if (baseSprite != null && image.sprite != baseSprite) image.sprite = baseSprite;
                if (amplitude < 0.01f)
                {
                    amplitude = 0f;
                    lastDirection = 0;
                    transform.localRotation = Quaternion.identity;
                }
                return;
            }

            int direction = phase < 0f ? -1 : 1;
            if (lastDirection != 0 && direction != lastDirection && alternateSprite != null)
                image.sprite = image.sprite == baseSprite ? alternateSprite : baseSprite;
            lastDirection = direction;
        }
    }
}
