using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SousakuTeam8.MeterGame.Timing
{
    public enum TimingMeterPhase { Playing, RoundFeedback, FadingOut, FadingIn, Complete }

    [DisallowMultipleComponent]
    public sealed class TimingMeterController : MonoBehaviour
    {
        [SerializeField, InspectorName("難易度・セット・演出設定")] private TimingMeterSettings settings = new TimingMeterSettings();
        [SerializeField] private TimingSpaceInput input;
        [SerializeField] private TimingMeterPresenter presenter;
        [SerializeField] private TimingMeterFeedback feedback;
        [SerializeField] private TimingMeterTransition transition;
        [SerializeField] private UnityEvent onSetSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onSetFailure = new UnityEvent();
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();
        public TimingSessionState Session { get; private set; }
        public TimingMeterPhase Phase { get; private set; }
        private int runVersion;

        private void Start() => RestartGame();

        [ContextMenu("現在の設定で再スタート")]
        public void RestartGame()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            runVersion++;
            settings.Validate();
            Session = new TimingSessionState(settings);
            Phase = TimingMeterPhase.Playing;
            feedback.ResetVisuals();
            transition.ResetVisuals();
            presenter.Render(Session, Phase);
        }

        private void Update() => ProcessFrame(Time.deltaTime, input != null && input.PressedThisFrame);

        /// <summary>Allows a UI button or another input adapter to submit one press.</summary>
        public void SubmitPress() => ProcessFrame(0f, true);

        private void ProcessFrame(float deltaTime, bool pressed)
        {
            if (Session == null) return;
            if (Phase == TimingMeterPhase.Playing)
            {
                Session.Tick(deltaTime, pressed);
                if (Session.Round.Result != TimingRoundResult.Playing)
                {
                    Phase = TimingMeterPhase.RoundFeedback;
                    bool success = Session.Round.Result == TimingRoundResult.Success;
                    int version = runVersion;
                    presenter.Render(Session, Phase);
                    if (success) onSetSuccess.Invoke(); else onSetFailure.Invoke();
                    // A listener may restart or disable this game while handling the event.
                    if (version != runVersion || !isActiveAndEnabled) return;
                    StartCoroutine(FinishRound(success));
                }
            }
            presenter.Render(Session, Phase);
        }

        private IEnumerator FinishRound(bool success)
        {
            feedback.ShowResult(success, settings);
            if (settings.resultDisplaySeconds > 0f) yield return feedback.Play(success, settings);
            Phase = TimingMeterPhase.FadingOut;
            if (settings.useFade) yield return transition.FadeOut(settings);
            feedback.ResetVisuals();
            bool complete = Session.IsFinished;
            if (!complete) Session.BeginNextRound();
            Phase = TimingMeterPhase.FadingIn;
            presenter.Render(Session, Phase);
            if (complete) feedback.ShowFinal(Session.IsSuccessful);
            if (settings.useFade) yield return transition.FadeIn(settings);
            else transition.ResetVisuals();
            Phase = complete ? TimingMeterPhase.Complete : TimingMeterPhase.Playing;
            presenter.Render(Session, Phase);
            if (complete)
            {
                if (Session.IsSuccessful) onSuccess.Invoke(); else onFailure.Invoke();
            }
        }

        private void OnValidate() => settings.Validate();
    }
}
