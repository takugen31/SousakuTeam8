using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SousakuTeam8.MeterGame.Timing;

namespace SousakuTeam8.MeterGame.Tests
{
    public sealed class TimingMeterTests
    {
        [Test]
        public void RoundStartsAtZeroAndRisesWithoutInput()
        {
            var round = new TimingRoundState(0.4f, 0.9f);
            Assert.That(round.Gauge, Is.Zero);
            round.Tick(1f, false);
            Assert.That(round.Gauge, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Playing));
        }

        [TestCase(0f, TimingRoundResult.Failure)]
        [TestCase(2f, TimingRoundResult.Failure)]
        [TestCase(2.25f, TimingRoundResult.Success)]
        [TestCase(2.5f, TimingRoundResult.Success)]
        [TestCase(2.7f, TimingRoundResult.Success)]
        [TestCase(2.85f, TimingRoundResult.Failure)]
        [TestCase(5f, TimingRoundResult.Failure)]
        [TestCase(7.5f, TimingRoundResult.Success)]
        public void PressResolvesSuccessOrFailureOnEitherPass(float time, TimingRoundResult result)
        {
            var round = new TimingRoundState(0.4f, 0.9f);
            round.Tick(time, true);
            Assert.That(round.Result, Is.EqualTo(result));
            Assert.That(round.Gauge, Is.InRange(0f, 1f));
        }

        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(1.25f, 0.75f)]
        [TestCase(1.5f, 0.5f)]
        [TestCase(2f, 0f)]
        [TestCase(2.5f, 0.5f)]
        [TestCase(1001.25f, 0.75f)]
        public void GaugeBouncesAtBothEndsAtTheSameSpeedWithoutTimeout(float time, float expectedGauge)
        {
            var round = new TimingRoundState(1f, 0.4f, 0.6f);
            round.Tick(time, false);
            Assert.That(round.Gauge, Is.EqualTo(expectedGauge).Within(0.00001f));
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Playing));
            Assert.That(round.ElapsedSeconds, Is.EqualTo(time));
        }

        [Test]
        public void MaxOnlyCanSucceedExactlyAtTurningPoint()
        {
            var round = new TimingRoundState(1f, 1f);
            round.Tick(1f, true);
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Success));
        }

        [TestCase(0.39f, TimingRoundResult.Failure)]
        [TestCase(0.4f, TimingRoundResult.Success)]
        [TestCase(0.5f, TimingRoundResult.Success)]
        [TestCase(0.6f, TimingRoundResult.Success)]
        [TestCase(0.601f, TimingRoundResult.Failure)]
        [TestCase(1.39f, TimingRoundResult.Failure)]
        [TestCase(1.4f, TimingRoundResult.Success)]
        [TestCase(1.5f, TimingRoundResult.Success)]
        [TestCase(1.6f, TimingRoundResult.Success)]
        [TestCase(1.61f, TimingRoundResult.Failure)]
        public void CentralRangeAcceptsOnlyItsInclusiveBounds(float time, TimingRoundResult result)
        {
            var round = new TimingRoundState(1f, 0.4f, 0.6f);
            round.Tick(time, true);
            Assert.That(round.Result, Is.EqualTo(result));
        }

        [Test]
        public void PassingTheRangeWithoutPressingCanSucceedOnTheReturnPass()
        {
            var low = new TimingRoundState(1f, 0.1f, 0.3f);
            low.Tick(0.2f, true);
            Assert.That(low.Result, Is.EqualTo(TimingRoundResult.Success));
            var middle = new TimingRoundState(1f, 0.4f, 0.6f);
            middle.Tick(0.61f, false);
            Assert.That(middle.Result, Is.EqualTo(TimingRoundResult.Playing));
            middle.Tick(0.89f, true);
            Assert.That(middle.Gauge, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(middle.Result, Is.EqualTo(TimingRoundResult.Success));
        }

        [TestCase(-1f, 0.01f)]
        [TestCase(0f, 0.01f)]
        [TestCase(0.2f, 0.2f)]
        [TestCase(2f, 1f)]
        public void WidthIsValidatedToAPlayableRange(float input, float expected)
        {
            var session = new TimingSessionState(new TimingMeterSettings { successZoneWidth = input });
            Assert.That(session.Round.SuccessZoneEnd - session.Round.SuccessZoneStart,
                Is.EqualTo(expected).Within(0.000001f));
            Assert.That(session.Round.SuccessZoneStart, Is.InRange(0f, 1f - expected));
            Assert.That(session.Round.SuccessZoneEnd, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void PresenterDrawsTheActualRandomizedRange()
        {
            EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/TimingMeterGame.unity");
            var presenter = Object.FindFirstObjectByType<TimingMeterPresenter>();
            var session = new TimingSessionState(new TimingMeterSettings
            {
                successZoneWidth = 0.2f, randomSeed = 1234
            });
            presenter.Render(session, TimingMeterPhase.Playing);
            var bar = new SerializedObject(Object.FindFirstObjectByType<MeterGameBarView>());
            Assert.That(bar.FindProperty("minimum").floatValue, Is.EqualTo(session.Round.SuccessZoneStart));
            Assert.That(bar.FindProperty("maximum").floatValue, Is.EqualTo(session.Round.SuccessZoneEnd));
        }

        [TestCase(0.95f, TimingRoundResult.Success)]
        [TestCase(0.5f, TimingRoundResult.Failure)]
        public void ResolvedRoundIsFrozen(float pressTime, TimingRoundResult expected)
        {
            var round = new TimingRoundState(1f, 0.9f);
            round.Tick(pressTime, true);
            round.Tick(10f, true);
            Assert.That(round.ElapsedSeconds, Is.EqualTo(pressTime));
            Assert.That(round.Result, Is.EqualTo(expected));
        }

        [Test]
        public void FillProgressDoesNotDependOnFrameRate()
        {
            var slow = new TimingRoundState(0.4f, 0.9f);
            var fast = new TimingRoundState(0.4f, 0.9f);
            for (int i = 0; i < 330; i++) slow.Tick(1f / 30f, false);
            for (int i = 0; i < 1320; i++) fast.Tick(1f / 120f, false);
            Assert.That(slow.Gauge, Is.EqualTo(fast.Gauge).Within(0.00001f));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(5, true)]
        public void AllNSetsRunAndRequiredSuccessesDetermineOverallOutcome(int successes, bool expected)
        {
            var session = new TimingSessionState(new TimingMeterSettings { setCount = 5, requiredSuccesses = 3, randomSeed = 1234 });
            for (int i = 0; i < 5; i++)
            {
                Assert.That(session.IsFinished, Is.False);
                Assert.That(session.Round.Gauge, Is.Zero);
                session.Tick(0f, false);
                Assert.That(session.Results.Count, Is.EqualTo(i));
                session.Tick(session.Round.FillDuration * 2f, false); // One complete bounce.
                Assert.That(session.Round.Result, Is.EqualTo(TimingRoundResult.Playing));
                if (i < successes) WinRoundAtCenter(session);
                else
                {
                    // An endpoint outside the randomized zone always fails.
                    float missGauge = session.Round.SuccessZoneStart > 0f ? 0f : 1f;
                    session.Tick(missGauge / session.Round.FillPerSecond, true);
                    Assert.That(session.Round.Result, Is.EqualTo(TimingRoundResult.Failure));
                    session.Tick(0f, true); // Repeated input cannot count another result.
                }
                Assert.That(session.Results.Count, Is.EqualTo(i + 1));
                if (i < 4) Assert.That(session.BeginNextRound(), Is.True);
            }
            Assert.That(session.Results.Count, Is.EqualTo(5));
            Assert.That(session.SuccessCount, Is.EqualTo(successes));
            Assert.That(session.IsFinished, Is.True);
            Assert.That(session.IsSuccessful, Is.EqualTo(expected));
            Assert.That(session.BeginNextRound(), Is.False);
        }

        [Test]
        public void CannotSkipPlayingRoundOrCountSameRoundTwice()
        {
            var session = new TimingSessionState(new TimingMeterSettings());
            Assert.That(session.BeginNextRound(), Is.False);
            WinRoundAtCenter(session);
            session.Tick(1f, true);
            Assert.That(session.SuccessCount, Is.EqualTo(1));
            Assert.That(session.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void SetCountAndRequiredSuccessesAreValidatedAndSnapshotted()
        {
            var settings = new TimingMeterSettings { setCount = 1, requiredSuccesses = 9 };
            var session = new TimingSessionState(settings);
            Assert.That(session.SetCount, Is.EqualTo(2));
            Assert.That(session.RequiredSuccesses, Is.EqualTo(1));
            settings.setCount = 20;
            settings.requiredSuccesses = 19;
            Assert.That(session.SetCount, Is.EqualTo(2));
            Assert.That(session.RequiredSuccesses, Is.EqualTo(1));
        }

        [Test]
        public void SeedReproducesSpeedAndProgressionAppliesPerSet()
        {
            var settings = new TimingMeterSettings
            {
                randomizeSpeed = true, randomSeed = 1234, speedIncreasePerSet = 0.1f
            };
            var first = new TimingSessionState(settings);
            var second = new TimingSessionState(settings);
            for (int i = 0; i < 5; i++)
            {
                float progression = 1f + i * settings.speedIncreasePerSet;
                Assert.That(first.Round.FillPerSecond, Is.EqualTo(second.Round.FillPerSecond));
                Assert.That(first.Round.FillPerSecond, Is.InRange(settings.fillPerSecond * settings.minimumSpeedMultiplier * progression,
                    settings.fillPerSecond * settings.maximumSpeedMultiplier * progression));
                Assert.That(first.Round.SuccessZoneStart, Is.EqualTo(second.Round.SuccessZoneStart));
                WinRoundAtCenter(first);
                WinRoundAtCenter(second);
                first.BeginNextRound();
                second.BeginNextRound();
            }
        }

        [Test]
        public void GameplaySettingsAreAppliedAtNextSetWithoutChangingCurrentRound()
        {
            var settings = new TimingMeterSettings();
            var session = new TimingSessionState(settings);
            float originalStart = session.Round.SuccessZoneStart;
            settings.fillPerSecond = 0.8f;
            settings.successZoneWidth = 0.3f;
            Assert.That(session.Round.FillPerSecond, Is.EqualTo(0.4f));
            Assert.That(session.Round.SuccessZoneStart, Is.EqualTo(originalStart));
            Assert.That(session.Round.SuccessZoneEnd - originalStart, Is.EqualTo(0.1f).Within(0.000001f));
            WinRoundAtCenter(session);
            session.BeginNextRound();
            Assert.That(session.Round.FillPerSecond, Is.EqualTo(0.8f));
            Assert.That(session.Round.SuccessZoneEnd - session.Round.SuccessZoneStart, Is.EqualTo(0.3f).Within(0.000001f));
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(0.5f)]
        public void PlacementKeepsWidthBoundsAndMinimumChangeAcrossManySets(float minimumChange)
        {
            var settings = new TimingMeterSettings
            {
                setCount = 100, successZoneWidth = 0.2f, minimumPositionChange = minimumChange, randomSeed = 1234
            };
            var session = new TimingSessionState(settings);
            float previousStart = session.Round.SuccessZoneStart;
            for (int i = 0; i < 100; i++)
            {
                float start = session.Round.SuccessZoneStart;
                Assert.That(start, Is.InRange(0f, 0.8f));
                Assert.That(session.Round.SuccessZoneEnd - start, Is.EqualTo(0.2f).Within(0.000001f));
                if (i > 0) Assert.That(Mathf.Abs(start - previousStart), Is.GreaterThanOrEqualTo(0.8f * minimumChange - 0.000001f));
                session.Tick(session.Round.FillDuration * 4f, false);
                Assert.That(session.Round.SuccessZoneStart, Is.EqualTo(start));
                previousStart = start;
                WinRoundAtCenter(session);
                session.BeginNextRound();
            }
            Assert.That(session.IsSuccessful, Is.True);
        }

        [Test]
        public void FullWidthCoversTheBarOnEverySet()
        {
            var session = new TimingSessionState(new TimingMeterSettings { successZoneWidth = 1f });
            for (int i = 0; i < session.SetCount; i++)
            {
                Assert.That(session.Round.SuccessZoneStart, Is.Zero);
                Assert.That(session.Round.SuccessZoneEnd, Is.EqualTo(1f));
                session.Tick(0f, true);
                session.BeginNextRound();
            }
            Assert.That(session.IsSuccessful, Is.True);
        }

        [Test]
        public void PositionSeedIsReproducibleAndIndependentOfSpeedRandomization()
        {
            var first = new TimingSessionState(new TimingMeterSettings { randomSeed = 1234 });
            var second = new TimingSessionState(new TimingMeterSettings { randomSeed = 1234, randomizeSpeed = true });
            var different = new TimingSessionState(new TimingMeterSettings { randomSeed = 9876 });
            Assert.That(first.Round.SuccessZoneStart, Is.Not.EqualTo(different.Round.SuccessZoneStart));
            for (int i = 0; i < first.SetCount; i++)
            {
                Assert.That(first.Round.SuccessZoneStart, Is.EqualTo(second.Round.SuccessZoneStart));
                WinRoundAtCenter(first);
                WinRoundAtCenter(second);
                first.BeginNextRound();
                second.BeginNextRound();
            }
        }

        private static void WinRoundAtCenter(TimingSessionState session)
        {
            var round = session.Round;
            float center = (round.SuccessZoneStart + round.SuccessZoneEnd) * 0.5f;
            float nextCenter = (Mathf.Floor(round.ElapsedSeconds / round.FillDuration / 2f) + 1f) * round.FillDuration * 2f
                + center / round.FillPerSecond;
            session.Tick(nextCenter - round.ElapsedSeconds, true);
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Success));
        }

        [Test]
        public void TimingSceneSharesTheSourceLayoutAndHasOnlyTimingController()
        {
            EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/TensionManagementMinigame.unity");
            string[] names = { "MeterBar", "InputPrompt", "RemainingTime" };
            var positions = new Vector2[3];
            var sizes = new Vector2[3];
            for (int i = 0; i < names.Length; i++)
            {
                var rect = GameObject.Find(names[i]).GetComponent<RectTransform>();
                positions[i] = rect.anchoredPosition;
                sizes[i] = rect.sizeDelta;
            }
            var scene = EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/TimingMeterGame.unity");
            Assert.That(Object.FindFirstObjectByType<MeterGameController>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<MeterGameTimerView>(), Is.Null);
            var controller = Object.FindFirstObjectByType<TimingMeterController>();
            Assert.That(controller, Is.Not.Null);
            var data = new SerializedObject(controller);
            foreach (string field in new[] { "input", "presenter", "feedback", "transition" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(GameObject.Find("GameplayUI").GetComponentsInChildren<Graphic>().Length, Is.EqualTo(3));
            Assert.That(Object.FindFirstObjectByType<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(new SerializedObject(Object.FindFirstObjectByType<MeterGameBarView>()).FindProperty("gauge").floatValue, Is.Zero);
            for (int i = 0; i < names.Length; i++)
            {
                var rect = GameObject.Find(names[i]).GetComponent<RectTransform>();
                Assert.That(rect.anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(rect.sizeDelta, Is.EqualTo(sizes[i]));
            }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
        }

        [Test]
        public void ResultFeedbackUsesDifferentTintsAndDisabledFadeRestoresVisibility()
        {
            EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/TimingMeterGame.unity");
            var feedback = Object.FindFirstObjectByType<TimingMeterFeedback>();
            var bar = Object.FindFirstObjectByType<MeterGameBarView>();
            var settings = new TimingMeterSettings { animateResults = false, resultDisplaySeconds = 0f, useFade = false };
            Assert.That(feedback.Play(true, settings).MoveNext(), Is.False);
            Color success = bar.color;
            feedback.ResetVisuals();
            Assert.That(feedback.Play(false, settings).MoveNext(), Is.False);
            Assert.That(bar.color.b, Is.GreaterThan(bar.color.r));
            Assert.That(bar.color, Is.Not.EqualTo(success));
            var group = Object.FindFirstObjectByType<CanvasGroup>();
            group.alpha = 0f;
            var transition = Object.FindFirstObjectByType<TimingMeterTransition>();
            Assert.That(transition.FadeOut(settings).MoveNext(), Is.False);
            Assert.That(transition.FadeIn(settings).MoveNext(), Is.False);
            Assert.That(group.alpha, Is.EqualTo(1f));
        }
    }
}
