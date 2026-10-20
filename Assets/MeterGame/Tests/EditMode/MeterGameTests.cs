using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame.Tests
{
    public sealed class MeterGameTests
    {
        private static MeterGameSettings StationarySettings() => new MeterGameSettings
        {
            initialGauge = 0.8f,
            drainPerSecond = 0f,
            inputMode = MeterInputMode.Press,
            gainPerSecondWhileHeld = 0.3f,
            minimumCenter = 0.5f,
            maximumCenter = 0.5f,
            minimumWidth = 1f,
            maximumWidth = 1f,
            requiredHoldSeconds = 1f,
            heartGainPerSecond = 0.25f,
            timeLimitSeconds = 30f
        };

        [Test]
        public void StartsAtEightyPercentWithEmptyHeartInsideTarget()
        {
            var state = new MeterGameState(new MeterGameSettings());
            Assert.That(state.Gauge, Is.EqualTo(0.8f));
            Assert.That(state.Heart, Is.Zero);
            Assert.That(state.IsInTarget, Is.True);
        }

        [Test]
        public void GaugeDrainsAndSinglePressAddsConfiguredAmount()
        {
            var settings = StationarySettings();
            settings.drainPerSecond = 0.1f;
            settings.gainPerPress = 0.15f;
            var state = new MeterGameState(settings);
            state.Tick(1f, false);
            Assert.That(state.Gauge, Is.EqualTo(0.7f).Within(0.0001f));
            state.Tick(1f, true);
            Assert.That(state.Gauge, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void GaugeClampsAtBothEnds()
        {
            var settings = StationarySettings();
            settings.gainPerPress = 10f;
            var state = new MeterGameState(settings);
            state.Tick(0.01f, true);
            Assert.That(state.Gauge, Is.EqualTo(1f));
            settings.drainPerSecond = 10f;
            state.Tick(1f, false);
            Assert.That(state.Gauge, Is.Zero);
        }

        [Test]
        public void HeartWaitsForHoldAndOnlyCreditsTimeAfterThreshold()
        {
            var state = new MeterGameState(StationarySettings());
            state.Tick(0.5f, false);
            Assert.That(state.Heart, Is.Zero);
            state.Tick(1f, false);
            Assert.That(state.Heart, Is.EqualTo(0.125f).Within(0.0001f));
        }

        [Test]
        public void LeavingRangeResetsHoldButRetainsHeartAndRequiresNewHold()
        {
            var settings = StationarySettings();
            settings.minimumWidth = settings.maximumWidth = 0.4f;
            settings.initialGauge = 0.5f;
            var state = new MeterGameState(settings);
            state.Tick(2f, false);
            float earnedHeart = state.Heart;
            settings.gainPerPress = 0.4f;
            state.Tick(0.1f, true);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.Heart, Is.EqualTo(earnedHeart));
            settings.drainPerSecond = 4f;
            state.Tick(0.1f, false);
            settings.drainPerSecond = 0f;
            state.Tick(0.5f, false);
            Assert.That(state.IsInTarget, Is.True);
            Assert.That(state.Heart, Is.EqualTo(earnedHeart));
        }

        [Test]
        public void TargetMovesResizesAndStaysWithinGauge()
        {
            var state = new MeterGameState(new MeterGameSettings { randomSeed = 1234 });
            float initialMinimum = state.TargetMinimum;
            float initialWidth = state.TargetMaximum - state.TargetMinimum;
            state.Tick(1f, false);
            Assert.That(state.TargetMinimum, Is.Not.EqualTo(initialMinimum).Within(0.001f));
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.Not.EqualTo(initialWidth).Within(0.001f));
            for (int i = 0; i < 100; i++)
            {
                state.Tick(0.1f, false);
                Assert.That(state.TargetMinimum, Is.GreaterThanOrEqualTo(0f));
                Assert.That(state.TargetMaximum, Is.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void RandomMotionIsContinuousEvenDuringFastNarrowing()
        {
            var settings = new MeterGameSettings
            {
                randomSeed = 1234, suddenMoveChance = 1f, suddenSpeedMultiplier = 20f,
                narrowChance = 1f, requiredHoldSeconds = 100f
            };
            var state = new MeterGameState(settings);
            for (int i = 0; i < 5000; i++)
            {
                float minimum = state.TargetMinimum;
                float maximum = state.TargetMaximum;
                state.Tick(0.001f, false);
                Assert.That(Mathf.Abs(state.TargetMinimum - minimum), Is.LessThan(0.02f));
                Assert.That(Mathf.Abs(state.TargetMaximum - maximum), Is.LessThan(0.02f));
                Assert.That(state.TargetMinimum, Is.GreaterThanOrEqualTo(0f));
                Assert.That(state.TargetMaximum, Is.LessThanOrEqualTo(1f));
                Assert.That(state.TargetMaximum - state.TargetMinimum,
                    Is.InRange(settings.minimumWidth - 0.00001f, settings.maximumWidth + 0.00001f));
            }
        }

        [Test]
        public void SeedReproducesMotionAcrossFrameRatesAndRestart()
        {
            var settings = new MeterGameSettings { randomSeed = 4567, requiredHoldSeconds = 100f };
            var slow = new MeterGameState(settings);
            var fast = new MeterGameState(settings);
            for (int i = 0; i < 240; i++) slow.Tick(1f / 30f, false);
            for (int i = 0; i < 960; i++) fast.Tick(1f / 120f, false);
            Assert.That(slow.TargetMinimum, Is.EqualTo(fast.TargetMinimum).Within(0.0002f));
            Assert.That(slow.TargetMaximum, Is.EqualTo(fast.TargetMaximum).Within(0.0002f));
            float minimum = slow.TargetMinimum;
            slow.Restart();
            for (int i = 0; i < 240; i++) slow.Tick(1f / 30f, false);
            Assert.That(slow.TargetMinimum, Is.EqualTo(minimum));
        }

        [Test]
        public void DifferentSeedsProduceDifferentMovement()
        {
            var first = new MeterGameState(new MeterGameSettings { randomSeed = 1234 });
            var second = new MeterGameState(new MeterGameSettings { randomSeed = 5678 });
            first.Tick(2f, false);
            second.Tick(2f, false);
            Assert.That(first.TargetMinimum, Is.Not.EqualTo(second.TargetMinimum).Within(0.001f));
        }

        [Test]
        public void NarrowingEventsChooseWidthsNearConfiguredMinimum()
        {
            var settings = new MeterGameSettings
            {
                randomSeed = 1234, narrowChance = 1f, widthPeriod = 0.4f,
                minimumWidth = 0.1f, maximumWidth = 0.4f, requiredHoldSeconds = 100f
            };
            var state = new MeterGameState(settings);
            state.Tick(1f, false);
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.InRange(0.09999f, 0.14501f));
        }

        [Test]
        public void SuddenSpeedMultiplierAcceleratesTheSameContinuousPath()
        {
            var normalSettings = new MeterGameSettings
            {
                randomSeed = 1234, suddenMoveChance = 1f, suddenSpeedMultiplier = 1f,
                requiredHoldSeconds = 100f
            };
            var quickSettings = new MeterGameSettings
            {
                randomSeed = 1234, suddenMoveChance = 1f, suddenSpeedMultiplier = 4f,
                requiredHoldSeconds = 100f
            };
            var normal = new MeterGameState(normalSettings);
            var quick = new MeterGameState(quickSettings);
            normal.Tick(2f, false);
            quick.Tick(0.5f, false);
            Assert.That(quick.TargetMinimum, Is.EqualTo(normal.TargetMinimum).Within(0.0001f));
            Assert.That(quick.TargetMaximum, Is.EqualTo(normal.TargetMaximum).Within(0.0001f));
        }

        [Test]
        public void FullHeartSucceedsAndResultFreezes()
        {
            var state = new MeterGameState(StationarySettings());
            state.Tick(6f, false);
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Success));
            Assert.That(state.Heart, Is.EqualTo(1f));
            float elapsed = state.ElapsedSeconds;
            state.Tick(100f, true);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(elapsed));
        }

        [Test]
        public void TimeoutFailsAndDoesNotCountTimeBeyondDeadline()
        {
            var settings = StationarySettings();
            settings.timeLimitSeconds = 2f;
            var state = new MeterGameState(settings);
            state.Tick(100f, false);
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Failure));
            Assert.That(state.ElapsedSeconds, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(state.Heart, Is.EqualTo(0.25f).Within(0.0001f));
            state.Tick(1f, true);
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Failure));
        }

        [Test]
        public void CompletingHeartAtDeadlineSucceeds()
        {
            var settings = StationarySettings();
            settings.requiredHoldSeconds = 0f;
            settings.heartGainPerSecond = 1f;
            settings.timeLimitSeconds = 1f;
            var state = new MeterGameState(settings);
            state.Tick(1f, false);
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Success));
        }

        [Test]
        public void RestartRestoresInitialState()
        {
            var state = new MeterGameState(StationarySettings());
            state.Tick(6f, true);
            state.Restart();
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Playing));
            Assert.That(state.Gauge, Is.EqualTo(0.8f));
            Assert.That(state.ElapsedSeconds, Is.Zero);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.Heart, Is.Zero);
        }

        [Test]
        public void SimulationIsConsistentAcrossFrameRates()
        {
            var slow = new MeterGameState(StationarySettings());
            var fast = new MeterGameState(StationarySettings());
            slow.Tick(2f, false);
            for (int i = 0; i < 240; i++) fast.Tick(1f / 120f, false);
            Assert.That(slow.Heart, Is.EqualTo(fast.Heart).Within(0.0001f));
            Assert.That(slow.Gauge, Is.EqualTo(fast.Gauge).Within(0.0001f));
        }

        [Test]
        public void ExampleSceneHasOneTopCenteredBarAndWiredController()
        {
            EditorSceneManager.OpenScene("Assets/MeterGame/Scenes/MeterGameExample.unity", OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<MeterGameController>();
            var bar = Object.FindFirstObjectByType<MeterGameBarView>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(bar, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            var timer = Object.FindFirstObjectByType<MeterGameTimerView>();
            Assert.That(timer, Is.Not.Null);
            var timerText = timer.GetComponent<Text>();
            Assert.That(timerText.text, Is.EqualTo("残り 60 秒"));
            Assert.That(timerText.font, Is.Not.Null);
            Assert.That(timerText.rectTransform.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(timerText.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            var timerData = new UnityEditor.SerializedObject(timer);
            Assert.That(timerData.FindProperty("controller").objectReferenceValue, Is.EqualTo(controller));
            var prompt = Object.FindFirstObjectByType<MeterGameInputPrompt>();
            Assert.That(prompt, Is.Not.Null);
            var promptText = prompt.GetComponent<Text>();
            Assert.That(promptText.text, Is.EqualTo("Spaceキー : 増加、範囲内にゲージを留めよう！"));
            Assert.That(promptText.font, Is.Not.Null);
            Assert.That(promptText.rectTransform.anchoredPosition.y, Is.GreaterThan(bar.rectTransform.anchoredPosition.y));
            var promptData = new UnityEditor.SerializedObject(prompt);
            Assert.That(promptData.FindProperty("controller").objectReferenceValue, Is.EqualTo(controller));
            Assert.That(bar.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(bar.rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(bar.rectTransform.anchoredPosition.y, Is.LessThan(0f));
            var serialized = new UnityEditor.SerializedObject(controller);
            Assert.That(serialized.FindProperty("barView").objectReferenceValue, Is.EqualTo(bar));
            Canvas.ForceUpdateCanvases();
            var mesh = bar.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
        }

        [Test]
        public void GaugeSpeedScalesBothDrainAndPressGain()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.5f;
            settings.gaugeSpeedMultiplier = 2f;
            settings.drainPerSecond = 0.1f;
            settings.gainPerPress = 0.15f;
            var state = new MeterGameState(settings);
            state.Tick(1f, true);
            Assert.That(state.Gauge, Is.EqualTo(0.6f).Within(0.0001f));
            settings.gaugeSpeedMultiplier = 0f;
            state.Tick(1f, true);
            Assert.That(state.Gauge, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void HoldModeAddsContinuouslyOnlyWhileHeld()
        {
            var settings = StationarySettings();
            settings.inputMode = MeterInputMode.Hold;
            settings.initialGauge = 0.3f;
            settings.drainPerSecond = 0.1f;
            settings.gainPerSecondWhileHeld = 0.3f;
            var state = new MeterGameState(settings);
            state.Tick(1f, true, true);
            Assert.That(state.Gauge, Is.EqualTo(0.5f).Within(0.0001f));
            state.Tick(1f, false, false);
            Assert.That(state.Gauge, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void HoldModeHasNoPressJumpAndAddsSmallContinuousSteps()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.4f;
            settings.inputMode = MeterInputMode.Hold;
            settings.gainPerPress = 0.5f;
            settings.gainPerSecondWhileHeld = 0.3f;
            settings.drainPerSecond = 0.14f;
            var state = new MeterGameState(settings);
            state.Tick(0.01f, true, true);
            Assert.That(state.Gauge, Is.EqualTo(0.4016f).Within(0.00001f));
            state.Tick(0.01f, false, true);
            Assert.That(state.Gauge, Is.EqualTo(0.4032f).Within(0.00001f));
        }

        [Test]
        public void HoldIncreaseIsConsistentAcrossFrameRates()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.2f;
            settings.inputMode = MeterInputMode.Hold;
            settings.gainPerSecondWhileHeld = 0.3f;
            settings.drainPerSecond = 0.14f;
            var slow = new MeterGameState(settings);
            var fast = new MeterGameState(settings);
            for (int i = 0; i < 30; i++) slow.Tick(1f / 30f, i == 0, true);
            for (int i = 0; i < 120; i++) fast.Tick(1f / 120f, i == 0, true);
            Assert.That(slow.Gauge, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That(fast.Gauge, Is.EqualTo(slow.Gauge).Within(0.0001f));
        }

        [Test]
        public void InputModeCanSwitchDuringGameWithoutResettingGauge()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.4f;
            settings.gainPerPress = 0.1f;
            var state = new MeterGameState(settings);
            state.Tick(0.1f, true, true);
            Assert.That(state.Gauge, Is.EqualTo(0.5f).Within(0.0001f));
            state.Tick(0.1f, false, true);
            Assert.That(state.Gauge, Is.EqualTo(0.5f).Within(0.0001f));
            settings.inputMode = MeterInputMode.Hold;
            state.Tick(0.1f, false, true);
            Assert.That(state.Gauge, Is.EqualTo(0.53f).Within(0.0001f));
            settings.inputMode = MeterInputMode.Press;
            state.Tick(0.1f, false, true);
            Assert.That(state.Gauge, Is.EqualTo(0.53f).Within(0.0001f));
        }

        [Test]
        public void RangeSpeedCanFreezeAxesIndependentlyAndResumeWithoutJumping()
        {
            var settings = new MeterGameSettings { randomSeed = 1234, positionSpeedMultiplier = 0f };
            var state = new MeterGameState(settings);
            state.Tick(1f, false);
            Assert.That((state.TargetMinimum + state.TargetMaximum) * 0.5f, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.LessThan(0.3f));
            settings.widthSpeedMultiplier = 0f;
            settings.positionSpeedMultiplier = 2f;
            float width = state.TargetMaximum - state.TargetMinimum;
            float previous = state.TargetMinimum;
            state.Tick(0.001f, false);
            Assert.That(state.TargetMinimum, Is.EqualTo(previous).Within(0.0001f));
            state.Tick(0.5f, false);
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.EqualTo(width).Within(0.0001f));
            Assert.That(state.TargetMinimum, Is.Not.EqualTo(previous).Within(0.001f));
        }

        [Test]
        public void ZeroRandomnessMakesMotionIndependentOfSeedAndDisablesRandomEvents()
        {
            var first = new MeterGameState(new MeterGameSettings { randomSeed = 1234, randomness = 0f, suddenMoveChance = 1f, narrowChance = 1f });
            var second = new MeterGameState(new MeterGameSettings { randomSeed = 5678, randomness = 0f, suddenMoveChance = 0f, narrowChance = 0f });
            for (int i = 0; i < 600; i++)
            {
                first.Tick(1f / 60f, false);
                second.Tick(1f / 60f, false);
                Assert.That(first.TargetMinimum, Is.EqualTo(second.TargetMinimum));
                Assert.That(first.TargetMaximum, Is.EqualTo(second.TargetMaximum));
            }
        }

        [Test]
        public void CustomInitialTargetAndHeartAreClampedAndAppliedOnRestart()
        {
            var settings = new MeterGameSettings
            {
                useCustomInitialTarget = true, initialTargetCenter = 0.6f,
                initialTargetWidth = 0.24f, initialHeart = 0.4f
            };
            var state = new MeterGameState(settings);
            Assert.That(state.TargetMinimum, Is.EqualTo(0.48f).Within(0.0001f));
            Assert.That(state.TargetMaximum, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(state.Heart, Is.EqualTo(0.4f));
            settings.initialTargetWidth = 2f;
            settings.initialTargetCenter = -2f;
            state.Restart();
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.EqualTo(settings.maximumWidth).Within(0.0001f));
            Assert.That((state.TargetMinimum + state.TargetMaximum) * 0.5f, Is.EqualTo(settings.minimumCenter).Within(0.0001f));
        }

        [Test]
        public void GraceKeepsHoldDuringBriefExitAndResumesHeartOnReentry()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.5f;
            settings.minimumWidth = settings.maximumWidth = 0.4f;
            settings.outsideGraceSeconds = 0.2f;
            settings.gainPerPress = 0.4f;
            var state = new MeterGameState(settings);
            state.Tick(2f, false);
            float heart = state.Heart;
            state.Tick(0.1f, true);
            Assert.That(state.HoldSeconds, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(state.Heart, Is.EqualTo(heart));
            settings.drainPerSecond = 40f;
            state.Tick(0.01f, false);
            Assert.That(state.IsInTarget, Is.True);
            Assert.That(state.Heart, Is.GreaterThan(heart));
        }

        [Test]
        public void HeartPenaltyOnlyCountsTimePastGraceAndResetsHold()
        {
            var settings = StationarySettings();
            settings.initialGauge = 0.5f;
            settings.minimumWidth = settings.maximumWidth = 0.4f;
            settings.outsideGraceSeconds = 0.2f;
            settings.heartLossPerSecond = 0.1f;
            settings.gainPerPress = 0.4f;
            var state = new MeterGameState(settings);
            state.Tick(2f, false);
            float heart = state.Heart;
            state.Tick(0.3f, true);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.Heart, Is.EqualTo(heart - 0.01f).Within(0.0001f));
            state.Tick(10f, false);
            Assert.That(state.Heart, Is.Zero);
        }

        [Test]
        public void ZeroNarrowWidthFractionSelectsExactMinimumWidth()
        {
            var state = new MeterGameState(new MeterGameSettings
            {
                randomSeed = 1234, narrowChance = 1f, narrowWidthFraction = 0f, widthPeriod = 0.4f,
                requiredHoldSeconds = 100f
            });
            state.Tick(1f, false);
            Assert.That(state.TargetMaximum - state.TargetMinimum, Is.EqualTo(0.18f).Within(0.0001f));
        }

        [Test]
        public void FullInitialHeartImmediatelySucceeds()
        {
            var state = new MeterGameState(new MeterGameSettings { initialHeart = 1f });
            Assert.That(state.Result, Is.EqualTo(MeterGameResult.Success));
            state.Tick(1f, false);
            Assert.That(state.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void JapaneseInspectorAppliesAllThreeDifficultyPresets()
        {
            var go = new GameObject("Preset test");
            UnityEditor.Editor editor = null;
            try
            {
                var controller = go.AddComponent<MeterGameController>();
                editor = UnityEditor.Editor.CreateEditor(controller);
                Assert.That(editor.GetType().Name, Is.EqualTo("MeterGameControllerEditor"));
                var apply = editor.GetType().GetMethod("ApplyPreset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(apply, Is.Not.Null);
                apply.Invoke(editor, new object[] { 0 });
                var data = new UnityEditor.SerializedObject(controller);
                Assert.That(data.FindProperty("settings.minimumWidth").floatValue, Is.EqualTo(0.28f));
                apply.Invoke(editor, new object[] { 2 });
                data.Update();
                Assert.That(data.FindProperty("settings.heartLossPerSecond").floatValue, Is.EqualTo(0.025f));
                apply.Invoke(editor, new object[] { 1 });
                data.Update();
                Assert.That(data.FindProperty("settings.minimumWidth").floatValue, Is.EqualTo(0.18f));
                Assert.That(data.FindProperty("settings.heartLossPerSecond").floatValue, Is.Zero);
            }
            finally
            {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(go);
            }
        }
    }
}
