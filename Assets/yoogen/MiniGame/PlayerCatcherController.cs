using UnityEngine;
using UnityEngine.InputSystem;

namespace Sousakusai8.MiniGame
{
    /// <summary>Keeps the catcher at the bottom of the screen and follows the mouse on X.</summary>
    public sealed class PlayerCatcherController : MonoBehaviour
    {
        [Header("Keyboard Movement")]
        [SerializeField, Min(0f)] private float keyboardMoveSpeed = 8f;

        private CatchMiniGameController game;
        private Camera gameCamera;
        private SpriteRenderer spriteRenderer;
        private float movementY;
        private Vector2 previousMousePosition;
        private bool mousePositionInitialized;
        private bool usingKeyboard;

        public Bounds CatchBounds => spriteRenderer.bounds;

        public void Initialize(CatchMiniGameController controller, Camera targetCamera)
        {
            game = controller;
            gameCamera = targetCamera;
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementY = transform.position.y;
        }

        private void Update()
        {
            if (game == null || gameCamera == null)
            {
                return;
            }

            float halfWidth = spriteRenderer.bounds.extents.x;
            float leftEdge = game.GetLeftEdge(halfWidth);
            float rightEdge = game.GetRightEdge(halfWidth);
            float keyboardDirection = ReadKeyboardDirection();

            if (!Mathf.Approximately(keyboardDirection, 0f))
            {
                usingKeyboard = true;
            }

            bool mouseMoved = DidMouseMove(out Vector2 screenPosition);
            if (mouseMoved && Mathf.Approximately(keyboardDirection, 0f))
            {
                usingKeyboard = false;
            }

            float targetX = transform.position.x;
            if (usingKeyboard)
            {
                targetX += keyboardDirection * keyboardMoveSpeed * Time.deltaTime;
            }
            else if (Mouse.current != null)
            {
                float distanceToGamePlane = Mathf.Abs(gameCamera.transform.position.z - transform.position.z);
                Vector3 worldPosition = gameCamera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, distanceToGamePlane));
                targetX = worldPosition.x;
            }

            transform.position = new Vector3(
                Mathf.Clamp(targetX, leftEdge, rightEdge),
                movementY,
                transform.position.z);
        }

        private static float ReadKeyboardDirection()
        {
            if (Keyboard.current == null)
            {
                return 0f;
            }

            float direction = 0f;
            if (Keyboard.current.aKey.isPressed)
            {
                direction -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                direction += 1f;
            }

            return direction;
        }

        private bool DidMouseMove(out Vector2 currentPosition)
        {
            if (Mouse.current == null)
            {
                currentPosition = previousMousePosition;
                return false;
            }

            currentPosition = Mouse.current.position.ReadValue();
            bool moved = mousePositionInitialized &&
                (currentPosition - previousMousePosition).sqrMagnitude > 0.01f;
            previousMousePosition = currentPosition;
            mousePositionInitialized = true;
            return moved;
        }
    }
}
