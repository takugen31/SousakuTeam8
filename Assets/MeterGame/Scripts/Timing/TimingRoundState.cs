using UnityEngine;

namespace SousakuTeam8.MeterGame.Timing
{
    public enum TimingRoundResult { Playing, Success, Failure }

    /// <summary>One rising gauge. Input is evaluated at the end of the supplied time step.</summary>
    public sealed class TimingRoundState
    {
        public float FillPerSecond { get; }
        public float SuccessZoneStart { get; }
        public float GraceSeconds { get; }
        public float ElapsedSeconds { get; private set; }
        public float FillDuration => 1f / FillPerSecond;
        public float Deadline => FillDuration + GraceSeconds;
        public float Gauge => Mathf.Clamp01(ElapsedSeconds * FillPerSecond);
        public float RemainingSeconds => Mathf.Max(0f, Deadline - ElapsedSeconds);
        public TimingRoundResult Result { get; private set; }

        public TimingRoundState(float fillPerSecond, float successZoneStart, float graceSeconds)
        {
            FillPerSecond = Mathf.Max(0.01f, fillPerSecond);
            SuccessZoneStart = Mathf.Clamp01(successZoneStart);
            GraceSeconds = Mathf.Max(0f, graceSeconds);
        }

        public void Tick(float deltaTime, bool pressed)
        {
            if (Result != TimingRoundResult.Playing || deltaTime < 0f) return;
            float inputTime = ElapsedSeconds + deltaTime;
            ElapsedSeconds = Mathf.Min(inputTime, Deadline);
            if (pressed)
            {
                bool insideWindow = Gauge + 0.000001f >= SuccessZoneStart && inputTime <= Deadline + 0.000001f;
                Result = insideWindow ? TimingRoundResult.Success : TimingRoundResult.Failure;
            }
            else if (inputTime >= Deadline)
                Result = TimingRoundResult.Failure;
        }
    }
}
