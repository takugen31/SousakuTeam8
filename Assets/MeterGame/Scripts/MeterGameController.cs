using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace SousakuTeam8.MeterGame
{
    [DisallowMultipleComponent]
    public sealed class MeterGameController : MonoBehaviour
    {
        [SerializeField] private MeterGameSettings settings = new MeterGameSettings();
        [SerializeField] private MeterGameBarView barView;
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();
        public MeterGameState State { get; private set; }
        public MeterInputMode InputMode => settings.inputMode;
        private bool resultNotified;

        private void Start() => RestartGame();

        private void Update()
        {
            if (State == null || resultNotified) return;
            if (State.Result == MeterGameResult.Playing)
            {
                bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
                bool held = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
                State.Tick(Time.deltaTime, pressed, held);
                RefreshView();
            }
            if (State.Result == MeterGameResult.Playing) return;
            resultNotified = true;
            if (State.Result == MeterGameResult.Success) onSuccess.Invoke();
            else onFailure.Invoke();
        }

        [ContextMenu("Restart Meter Game")]
        public void RestartGame()
        {
            if (!Application.isPlaying) return;
            State = new MeterGameState(settings);
            resultNotified = false;
            RefreshView();
        }

        private void RefreshView()
        {
            if (barView != null) barView.Show(State);
        }

        private void OnValidate() => settings.Validate();
    }
}
