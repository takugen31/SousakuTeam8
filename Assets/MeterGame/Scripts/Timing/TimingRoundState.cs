using UnityEngine;

namespace SousakuTeam8.MeterGame.Timing
{
    public enum TimingRoundResult { Playing, Success, Failure }

    /// <summary>A gauge that bounces between zero and one until a press resolves the set.</summary>
    public sealed class TimingRoundState
    {
        public float FillPerSecond { get; }
        public float SuccessZoneStart { get; }
        public float SuccessZoneEnd { get; }
        private double elapsedSeconds;
        public float ElapsedSeconds => (float)elapsedSeconds;
        public float FillDuration => 1f / FillPerSecond;
        public float Gauge
        {
            get
            {
                double phase = elapsedSeconds * FillPerSecond % 2d;
                return (float)(phase <= 1d ? phase : 2d - phase);
            }
        }
        public TimingRoundResult Result { get; private set; }

        public TimingRoundState(float fillPerSecond, float successZoneStart, float successZoneEnd = 1f)
        {
            FillPerSecond = Mathf.Max(0.01f, fillPerSecond);
            float start = Mathf.Clamp01(successZoneStart);
            float end = Mathf.Clamp01(successZoneEnd);
            SuccessZoneStart = Mathf.Min(start, end);
            SuccessZoneEnd = Mathf.Max(start, end);
        }

        public void Tick(float deltaTime, bool pressed)
        {
            if (Result != TimingRoundResult.Playing || deltaTime < 0f) return;
            elapsedSeconds += deltaTime;
            if (pressed)
            {
                bool inside = Gauge + 0.000001f >= SuccessZoneStart && Gauge <= SuccessZoneEnd + 0.000001f;
                Result = inside ? TimingRoundResult.Success : TimingRoundResult.Failure;
            }
        }
    }
}
