using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SousakuTeam8.PuzzleGame
{
    /// <summary>
    /// Creates the standalone scene shell. The reusable PuzzleGamePanel prefab deliberately
    /// omits Canvas and EventSystem so it can be embedded without duplicating host objects.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PuzzleGameStandaloneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject(
                "Puzzle Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var gameObject = new GameObject(
                "Puzzle Game Panel",
                typeof(RectTransform),
                typeof(PuzzleGameController));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }
}
