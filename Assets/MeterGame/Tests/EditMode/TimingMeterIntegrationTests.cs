using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SousakuTeam8.MeterGame.Timing;

namespace SousakuTeam8.MeterGame.Tests
{
    public sealed class TimingMeterIntegrationTests
    {
        [UnityTest]
        public IEnumerator InputFeedbackFadesRestartAndFinalEventsWorkTogether()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode("Assets/MeterGame/Scenes/MeterGameExample.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            // Construct captured event counters after domain reload has completed.
            yield return CheckSessions();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CheckSessions()
        {
            var controller = Object.FindFirstObjectByType<TimingMeterController>();
            Assert.That(controller, Is.Not.Null);
            Configure(controller, true);
            controller.RestartGame();
            yield return null;
            Assert.That(controller.Session, Is.Not.Null);
            int setSuccess = 0, setFailure = 0, gameSuccess = 0, gameFailure = 0;
            Event(controller, "onSetSuccess").AddListener(() => setSuccess++);
            Event(controller, "onSetFailure").AddListener(() => setFailure++);
            Event(controller, "onSuccess").AddListener(() => gameSuccess++);
            Event(controller, "onFailure").AddListener(() => gameFailure++);

            controller.SubmitPress();
            Assert.That(controller.Phase, Is.EqualTo(TimingMeterPhase.RoundFeedback));
            Assert.That(setSuccess, Is.EqualTo(1));
            yield return WaitForPhase(controller, TimingMeterPhase.Playing);
            Assert.That(controller.Session.CurrentSetNumber, Is.EqualTo(2));
            // A new set waits for a fresh submitted press after feedback and fades.
            yield return null;
            Assert.That(controller.Session.Round.Result, Is.EqualTo(TimingRoundResult.Playing));
            Assert.That(setSuccess, Is.EqualTo(1));

            controller.SubmitPress();
            var data = new SerializedObject(controller);
            data.FindProperty("settings.successZoneStart").floatValue = 0.9f;
            data.ApplyModifiedProperties();
            yield return WaitForPhase(controller, TimingMeterPhase.Playing);
            Assert.That(controller.Session.CurrentSetNumber, Is.EqualTo(3));
            Assert.That(controller.Session.IsFinished, Is.False);
            controller.SubmitPress(); // Early input fails the third set.
            Assert.That(setFailure, Is.EqualTo(1));
            yield return WaitForPhase(controller, TimingMeterPhase.Complete);
            Assert.That(controller.Session.IsSuccessful, Is.True);
            Assert.That(controller.Session.SuccessCount, Is.EqualTo(2));
            Assert.That(gameSuccess, Is.EqualTo(1));
            Assert.That(gameFailure, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<CanvasGroup>().alpha, Is.EqualTo(1f));

            Configure(controller, false);
            controller.RestartGame();
            Assert.That(controller.Session.Round.Gauge, Is.Zero);
            Assert.That(controller.Session.Results.Count, Is.Zero);
            for (int i = 0; i < 3; i++)
            {
                controller.SubmitPress();
                yield return WaitForPhase(controller, i < 2 ? TimingMeterPhase.Playing : TimingMeterPhase.Complete);
            }
            Assert.That(controller.Phase, Is.EqualTo(TimingMeterPhase.Complete));
            Assert.That(controller.Session.IsSuccessful, Is.False);
            Assert.That(gameFailure, Is.EqualTo(1));
            Assert.That(gameSuccess, Is.EqualTo(1));
            yield return null;
            Assert.That(gameFailure, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        private static void Configure(TimingMeterController controller, bool fades)
        {
            var data = new SerializedObject(controller);
            data.FindProperty("settings.setCount").intValue = 3;
            data.FindProperty("settings.requiredSuccesses").intValue = 2;
            data.FindProperty("settings.successZoneStart").floatValue = fades ? 0f : 0.9f;
            data.FindProperty("settings.resultDisplaySeconds").floatValue = fades ? 0.05f : 0f;
            data.FindProperty("settings.useFade").boolValue = fades;
            data.FindProperty("settings.fadeOutSeconds").floatValue = 0.03f;
            data.FindProperty("settings.fadeHoldSeconds").floatValue = 0.02f;
            data.FindProperty("settings.fadeInSeconds").floatValue = 0.03f;
            data.ApplyModifiedProperties();
        }

        private static UnityEvent Event(TimingMeterController controller, string name)
            => (UnityEvent)typeof(TimingMeterController).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);

        private static IEnumerator WaitForPhase(TimingMeterController controller, TimingMeterPhase phase)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (controller.Phase != phase && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(controller.Phase, Is.EqualTo(phase));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
