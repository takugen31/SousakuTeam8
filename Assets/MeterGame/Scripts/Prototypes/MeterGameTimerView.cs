using UnityEngine;
using UnityEngine.UI;

namespace SousakuTeam8.MeterGame
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class MeterGameTimerView : MonoBehaviour
    {
        [SerializeField] private MeterGameController controller;
        [SerializeField] private Text label;
        private int displayedSeconds = -1;

        private void OnEnable()
        {
            if (label == null) label = GetComponent<Text>();
            displayedSeconds = -1;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (controller == null || label == null) return;
            // Match the model's timeout tolerance so a failed game always shows zero.
            int seconds = Mathf.Max(0, Mathf.CeilToInt(controller.RemainingSeconds - 0.00001f));
            if (displayedSeconds == seconds) return;
            displayedSeconds = seconds;
            label.text = $"残り {seconds} 秒";
        }
    }
}
