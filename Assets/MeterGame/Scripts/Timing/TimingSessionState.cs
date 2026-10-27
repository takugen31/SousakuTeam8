using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SousakuTeam8.MeterGame.Timing
{
    /// <summary>Records all n results. Never ends early, even if m successes are already reached.</summary>
    public sealed class TimingSessionState
    {
        private readonly TimingMeterSettings settings;
        private readonly Random random;
        private readonly Random positionRandom;
        private readonly List<TimingRoundResult> results = new List<TimingRoundResult>();
        private bool roundRecorded;
        public int SetCount { get; }
        public int RequiredSuccesses { get; }
        public int RandomSeed { get; }
        public int CurrentSetNumber { get; private set; } = 1;
        public int SuccessCount { get; private set; }
        public bool IsFinished => results.Count == SetCount;
        public bool IsSuccessful => IsFinished && SuccessCount >= RequiredSuccesses;
        public ReadOnlyCollection<TimingRoundResult> Results { get; }
        public TimingRoundState Round { get; private set; }

        public TimingSessionState(TimingMeterSettings settings)
        {
            this.settings = settings;
            settings.Validate();
            SetCount = settings.setCount;
            RequiredSuccesses = settings.requiredSuccesses;
            RandomSeed = settings.randomSeed == 0 ? Guid.NewGuid().GetHashCode() : settings.randomSeed;
            random = new Random(RandomSeed);
            // Keep placement reproducible even when speed randomization is toggled.
            positionRandom = new Random(RandomSeed ^ 0x5f3759df);
            Results = results.AsReadOnly();
            CreateRound();
        }

        public void Tick(float deltaTime, bool pressed)
        {
            if (roundRecorded || IsFinished) return;
            Round.Tick(deltaTime, pressed);
            if (Round.Result == TimingRoundResult.Playing) return;
            results.Add(Round.Result);
            roundRecorded = true;
            if (Round.Result == TimingRoundResult.Success) SuccessCount++;
        }

        public bool BeginNextRound()
        {
            if (!roundRecorded || IsFinished) return false;
            CurrentSetNumber++;
            CreateRound();
            return true;
        }

        private void CreateRound()
        {
            settings.Validate();
            float variation = settings.randomizeSpeed
                ? settings.minimumSpeedMultiplier + (settings.maximumSpeedMultiplier - settings.minimumSpeedMultiplier) * (float)random.NextDouble()
                : 1f;
            float rate = settings.fillPerSecond * variation * (1f + settings.speedIncreasePerSet * (CurrentSetNumber - 1));
            float start = PickZoneStart();
            Round = new TimingRoundState(rate, start, start + settings.successZoneWidth);
            roundRecorded = false;
        }

        private float PickZoneStart()
        {
            double available = 1d - settings.successZoneWidth;
            if (available <= 0d) return 0f;
            if (Round == null || settings.minimumPositionChange <= 0f)
                return (float)(positionRandom.NextDouble() * available);

            double previous = Math.Min(Round.SuccessZoneStart, available);
            double distance = available * settings.minimumPositionChange;
            double leftLength = Math.Max(0d, previous - distance);
            double rightStart = Math.Min(available, previous + distance);
            double rightLength = available - rightStart;
            double total = leftLength + rightLength;
            // At the largest separation, a centered previous zone leaves only the two ends.
            if (total <= 0d) return positionRandom.Next(2) == 0 ? 0f : (float)available;
            double sample = positionRandom.NextDouble() * total;
            return (float)(sample < leftLength ? sample : rightStart + sample - leftLength);
        }
    }
}
