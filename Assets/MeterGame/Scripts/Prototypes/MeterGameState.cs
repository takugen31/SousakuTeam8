using UnityEngine;

namespace SousakuTeam8.MeterGame
{
    public enum MeterGameResult { Playing, Success, Failure }

    /// <summary>Gameplay without input or UI. Values use normalized units (0–1).</summary>
    public sealed class MeterGameState
    {
        private readonly MeterGameSettings settings;
        private TargetMotion centerMotion;
        private TargetMotion widthMotion;
        private double centerTime;
        private double widthTime;
        private float outsideSeconds;
        public float Gauge { get; private set; }
        public float Heart { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float HoldSeconds { get; private set; }
        public float TargetMinimum { get; private set; }
        public float TargetMaximum { get; private set; }
        public MeterGameResult Result { get; private set; }
        public bool IsInTarget => Gauge >= TargetMinimum && Gauge <= TargetMaximum;
        public float RemainingSeconds => Mathf.Max(0f, settings.timeLimitSeconds - ElapsedSeconds);

        public MeterGameState(MeterGameSettings settings)
        {
            this.settings = settings;
            settings.Validate();
            Restart();
        }

        public void Restart()
        {
            settings.Validate();
            Gauge = settings.initialGauge;
            Heart = settings.initialHeart;
            ElapsedSeconds = HoldSeconds = outsideSeconds = 0f;
            centerTime = widthTime = 0d;
            Result = Heart >= 1f ? MeterGameResult.Success : MeterGameResult.Playing;
            var random = new System.Random(settings.randomSeed == 0
                ? System.Guid.NewGuid().GetHashCode() : settings.randomSeed);
            centerMotion = new TargetMotion(settings, false, new System.Random(random.Next()));
            widthMotion = new TargetMotion(settings, true, new System.Random(random.Next()));
            UpdateTarget();
        }

        public void Tick(float deltaTime, bool spacePressed, bool spaceHeld = false)
        {
            if (Result != MeterGameResult.Playing || deltaTime <= 0f) return;

            // Input is applied once per press; small simulation steps keep hold detection
            // and moving boundaries consistent during slow rendering frames.
            if (settings.inputMode == MeterInputMode.Press && spacePressed)
                Gauge = Mathf.Clamp01(Gauge + settings.gainPerPress * settings.gaugeSpeedMultiplier);
            if (!IsInTarget && settings.outsideGraceSeconds <= 0f) HoldSeconds = 0f;
            float remaining = Mathf.Min(deltaTime, RemainingSeconds);
            while (remaining > 0f && Result == MeterGameResult.Playing)
            {
                float step = Mathf.Min(remaining, 1f / 120f);
                remaining -= step;
                float gain = settings.inputMode == MeterInputMode.Hold && spaceHeld
                    ? settings.gainPerSecondWhileHeld : 0f;
                Gauge = Mathf.Clamp01(Gauge + (gain - settings.drainPerSecond) * settings.gaugeSpeedMultiplier * step);
                ElapsedSeconds = Mathf.Min(settings.timeLimitSeconds, ElapsedSeconds + step);
                centerTime += step * settings.positionSpeedMultiplier;
                widthTime += step * settings.widthSpeedMultiplier;
                UpdateTarget();

                if (IsInTarget)
                {
                    outsideSeconds = 0f;
                    float previousHold = HoldSeconds;
                    HoldSeconds += step;
                    float eligibleSeconds = Mathf.Max(0f, HoldSeconds -
                        Mathf.Max(previousHold, settings.requiredHoldSeconds));
                    Heart = Mathf.Clamp01(Heart + settings.heartGainPerSecond * eligibleSeconds);
                }
                else
                {
                    float previousOutside = outsideSeconds;
                    outsideSeconds += step;
                    if (outsideSeconds > settings.outsideGraceSeconds) HoldSeconds = 0f;
                    float penaltySeconds = Mathf.Max(0f, outsideSeconds -
                        Mathf.Max(previousOutside, settings.outsideGraceSeconds));
                    Heart = Mathf.Max(0f, Heart - settings.heartLossPerSecond * penaltySeconds);
                }

                // Finishing the heart exactly at the deadline counts as success.
                if (Heart >= 1f - 0.000001f)
                {
                    Heart = 1f;
                    Result = MeterGameResult.Success;
                }
            }

            if (Result == MeterGameResult.Playing && RemainingSeconds <= 0.00001f)
                Result = MeterGameResult.Failure;
        }

        private void UpdateTarget()
        {
            float width = widthMotion.Evaluate(widthTime);
            float center = Mathf.Clamp(centerMotion.Evaluate(centerTime), width * 0.5f, 1f - width * 0.5f);
            TargetMinimum = center - width * 0.5f;
            TargetMaximum = center + width * 0.5f;
        }

        // Each axis has its own random stream and absolute-time schedule. Frame rate
        // cannot change which waypoints are chosen. Quintic easing preserves position,
        // velocity and acceleration at waypoint boundaries, including sudden moves.
        private sealed class TargetMotion
        {
            private readonly MeterGameSettings settings;
            private readonly bool isWidth;
            private readonly System.Random random;
            private double startedAt;
            private double duration;
            private float from;
            private float to;
            private bool chooseMinimum = true;

            public TargetMotion(MeterGameSettings settings, bool isWidth, System.Random random)
            {
                this.settings = settings;
                this.isWidth = isWidth;
                this.random = random;
                to = settings.useCustomInitialTarget
                    ? (isWidth ? settings.initialTargetWidth : settings.initialTargetCenter)
                    : (isWidth ? settings.maximumWidth : settings.maximumCenter);
                NextSegment();
            }

            public float Evaluate(double time)
            {
                while (time >= startedAt + duration)
                {
                    startedAt += duration;
                    NextSegment();
                }
                float t = Mathf.Clamp01((float)((time - startedAt) / duration));
                float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
                float minimum = isWidth ? settings.minimumWidth : settings.minimumCenter;
                float maximum = isWidth ? settings.maximumWidth : settings.maximumCenter;
                return Mathf.Clamp(Mathf.Lerp(from, to, eased), minimum, maximum);
            }

            private void NextSegment()
            {
                from = to;
                float minimum = isWidth ? settings.minimumWidth : settings.minimumCenter;
                float maximum = isWidth ? settings.maximumWidth : settings.maximumCenter;
                float axisRandomness = settings.randomness * (isWidth ? settings.widthRandomness : settings.positionRandomness);
                bool sudden = random.NextDouble() < settings.suddenMoveChance * settings.randomness;
                bool narrow = isWidth && random.NextDouble() < settings.narrowChance * axisRandomness;
                float destination = (float)random.NextDouble();
                float randomized = narrow ? destination * settings.narrowWidthFraction : destination;
                float normalized = Mathf.Lerp(chooseMinimum ? 0f : 1f, randomized, axisRandomness);
                chooseMinimum = !chooseMinimum;
                to = Mathf.Lerp(minimum, maximum, normalized);
                float period = isWidth ? settings.widthPeriod : settings.positionPeriod;
                float randomFactor = Mathf.Lerp(0.6f, 1.2f, (float)random.NextDouble());
                float factor = Mathf.Lerp(0.9f, randomFactor, settings.randomness * settings.timingRandomness);
                duration = System.Math.Max(settings.minimumTransitionSeconds, period * 0.5 * factor /
                    (sudden ? settings.suddenSpeedMultiplier : 1f));
            }
        }
    }
}
