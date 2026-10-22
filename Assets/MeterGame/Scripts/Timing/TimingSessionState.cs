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
        private readonly List<TimingRoundResult> results = new List<TimingRoundResult>();
        private bool roundRecorded;
        public int SetCount { get; }
        public int RequiredSuccesses { get; }
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
            random = new Random(settings.randomSeed == 0 ? Guid.NewGuid().GetHashCode() : settings.randomSeed);
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
            Round = new TimingRoundState(rate, settings.successZoneStart, settings.maximumGraceSeconds);
            roundRecorded = false;
        }
    }
}
