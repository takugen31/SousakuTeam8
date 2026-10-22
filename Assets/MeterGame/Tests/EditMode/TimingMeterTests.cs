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
            var round = new TimingRoundState(0.4f, 0.9f, 0.35f);
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
        [TestCase(2.85f, TimingRoundResult.Success)]
        [TestCase(2.86f, TimingRoundResult.Failure)]
        public void PressHasInclusiveSuccessWindowAndRejectsEarlyOrLateInput(float time, TimingRoundResult result)
        {
            var round = new TimingRoundState(0.4f, 0.9f, 0.35f);
            round.Tick(time, true);
            Assert.That(round.Result, Is.EqualTo(result));
            Assert.That(round.Gauge, Is.InRange(0f, 1f));
        }

        [Test]
        public void FullGaugeWaitsForGraceThenFailsWithoutInput()
        {
            var round = new TimingRoundState(0.4f, 0.9f, 0.35f);
            round.Tick(2.5f, false);
            Assert.That(round.Gauge, Is.EqualTo(1f));
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Playing));
            round.Tick(0.36f, false);
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Failure));
            Assert.That(round.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void ZeroGraceAndMaxOnlyCanStillSucceedExactlyAtMax()
        {
            var round = new TimingRoundState(1f, 1f, 0f);
            round.Tick(1f, true);
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Success));
        }

        [Test]
        public void ResolvedRoundIsFrozen()
        {
            var round = new TimingRoundState(1f, 0.9f, 0.1f);
            round.Tick(0.95f, true);
            round.Tick(10f, false);
            Assert.That(round.ElapsedSeconds, Is.EqualTo(0.95f));
            Assert.That(round.Result, Is.EqualTo(TimingRoundResult.Success));
        }

        [Test]
        public void FillProgressDoesNotDependOnFrameRate()
        {
            var slow = new TimingRoundState(0.4f, 0.9f, 0.35f);
            var fast = new TimingRoundState(0.4f, 0.9f, 0.35f);
            for (int i = 0; i < 30; i++) slow.Tick(1f / 30f, false);
            for (int i = 0; i < 120; i++) fast.Tick(1f / 120f, false);
            Assert.That(slow.Gauge, Is.EqualTo(fast.Gauge).Within(0.00001f));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(5, true)]
        public void ExactlyNResultsDetermineOverallOutcome(int successes, bool outcome)
        {
            var session = new TimingSessionState(new TimingMeterSettings { setCount = 5, requiredSuccesses = 3 });
            for (int i = 0; i < 5; i++)
            {
                Assert.That(session.IsFinished, Is.False);
                Assert.That(session.Round.Gauge, Is.Zero);
                session.Tick(i < successes ? session.Round.FillDuration : 0f, true);
                if (i < 4) Assert.That(session.BeginNextRound(), Is.True);
            }
            Assert.That(session.Results.Count, Is.EqualTo(5));
            Assert.That(session.SuccessCount, Is.EqualTo(successes));
            Assert.That(session.IsFinished, Is.True);
            Assert.That(session.IsSuccessful, Is.EqualTo(outcome));
            Assert.That(session.BeginNextRound(), Is.False);
        }

        [Test]
        public void CannotSkipPlayingRoundOrCountSameRoundTwice()
        {
            var session = new TimingSessionState(new TimingMeterSettings());
            Assert.That(session.BeginNextRound(), Is.False);
            session.Tick(session.Round.FillDuration, true);
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
                first.Tick(0f, true);
                second.Tick(0f, true);
                first.BeginNextRound();
                second.BeginNextRound();
            }
        }

        [Test]
        public void GameplaySettingsAreAppliedAtNextSetWithoutChangingCurrentRound()
        {
            var settings = new TimingMeterSettings();
            var session = new TimingSessionState(settings);
            settings.fillPerSecond = 0.8f;
            settings.successZoneStart = 0.8f;
            Assert.That(session.Round.FillPerSecond, Is.EqualTo(0.4f));
            session.Tick(0f, true);
            session.BeginNextRound();
            Assert.That(session.Round.FillPerSecond, Is.EqualTo(0.8f));
            Assert.That(session.Round.SuccessZoneStart, Is.EqualTo(0.8f));
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
            var scene = EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/MeterGameExample.unity");
            Assert.That(Object.FindFirstObjectByType<MeterGameController>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<MeterGameTimerView>(), Is.Null);
            var controller = Object.FindFirstObjectByType<TimingMeterController>();
            Assert.That(controller, Is.Not.Null);
            var data = new SerializedObject(controller);
            foreach (string field in new[] { "input", "presenter", "feedback", "transition" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
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
            EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/MeterGameExample.unity");
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
