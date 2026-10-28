using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame.Timing
{
    public enum TimingMeterPhase { Playing, RoundFeedback, FadingOut, FadingIn, Complete, AwaitingStart }

    [DisallowMultipleComponent]
    public sealed class TimingMeterController : MonoBehaviour
    {
        [SerializeField, InspectorName("難易度・セット・演出設定")] private TimingMeterSettings settings = new TimingMeterSettings();
        [SerializeField] private TimingSpaceInput input;
        [SerializeField] private TimingMeterPresenter presenter;
        [SerializeField] private TimingMeterFeedback feedback;
        [SerializeField] private TimingMeterTransition transition;
        [SerializeField] private bool requireStartInput = true;
        [SerializeField] private Font startPromptFont;
        [SerializeField] private UnityEvent onSetSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onSetFailure = new UnityEvent();
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();
        public TimingSessionState Session { get; private set; }
        public TimingMeterPhase Phase { get; private set; }
        private int runVersion;
        private GameObject startOverlay;
        private int waitingSinceFrame;
        private int startedAtFrame = -1;

        private void Start() => RestartGame();

        [ContextMenu("現在の設定で再スタート")]
        public void RestartGame()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            runVersion++;
            settings.Validate();
            Session = new TimingSessionState(settings);
            Phase = requireStartInput ? TimingMeterPhase.AwaitingStart : TimingMeterPhase.Playing;
            waitingSinceFrame = Time.frameCount;
            startedAtFrame = -1;
            feedback.ResetVisuals();
            transition.ResetVisuals();
            presenter.Render(Session, Phase);
            if (requireStartInput)
            {
                if (startOverlay == null) BuildStartOverlay();
                startOverlay.SetActive(true);
                startOverlay.transform.SetAsLastSibling();
            }
            else if (startOverlay != null) startOverlay.SetActive(false);
        }

        private void Update()
        {
            if (Phase == TimingMeterPhase.AwaitingStart)
            {
                // Ignore the click/Space that loaded this scene, and inputs while the menu pauses it.
                if (Time.frameCount > waitingSinceFrame && Time.timeScale > 0f &&
                    input != null && input.StartPressedThisFrame) BeginGame();
                return;
            }
            ProcessFrame(Time.deltaTime, input != null && input.PressedThisFrame);
        }

        public void BeginGame()
        {
            if (Phase != TimingMeterPhase.AwaitingStart || Session == null) return;
            Phase = TimingMeterPhase.Playing;
            startedAtFrame = Time.frameCount;
            if (startOverlay != null) startOverlay.SetActive(false);
            presenter.Render(Session, Phase);
        }

        private void BuildStartOverlay()
        {
            startOverlay = new GameObject("TimingStartOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var canvas = GetComponentInParent<Canvas>();
            startOverlay.transform.SetParent(canvas != null ? canvas.transform : transform, false);
            var rect = startOverlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var backdrop = startOverlay.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.65f);
            backdrop.raycastTarget = true;

            var labelObject = new GameObject("StartPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(startOverlay.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.font = startPromptFont != null ? startPromptFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = "クリックorSpaceキーで開始してください";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.1f, 0.4f);
            labelRect.anchorMax = new Vector2(0.9f, 0.6f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }

        /// <summary>Allows a UI button or another input adapter to submit one press.</summary>
        public void SubmitPress() => ProcessFrame(0f, true);

        private void ProcessFrame(float deltaTime, bool pressed)
        {
            if (Session == null) return;
            if (Phase == TimingMeterPhase.AwaitingStart || Time.frameCount == startedAtFrame) return;
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
