using UnityEngine;
using UnityEngine.InputSystem;

namespace Sousakusai8.MiniGame
{
    /// <summary>Moves the catcher horizontally and lets it jump over hazards.</summary>
    public sealed class PlayerCatcherController : MonoBehaviour
    {
        [Header("Keyboard Movement")]
        [SerializeField, Min(0f)] private float keyboardMoveSpeed = 8f;

        [Header("Jump Movement")]
        [SerializeField, Min(0f)] private float jumpSpeed = 7f;
        [SerializeField, Min(0f)] private float gravity = 18f;

        private CatchMiniGameController game;
        private Camera gameCamera;
        private SpriteRenderer spriteRenderer;
        private float groundY;
        private float verticalVelocity;
        private Vector2 previousMousePosition;
        private bool mousePositionInitialized;
        private bool usingKeyboard;

        public Bounds CatchBounds => spriteRenderer.bounds;
        public float GroundY => groundY;

        public void Initialize(CatchMiniGameController controller, Camera targetCamera)
        {
            game = controller;
            gameCamera = targetCamera;
            spriteRenderer = GetComponent<SpriteRenderer>();
            groundY = transform.position.y;
            verticalVelocity = 0f;
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

            bool isGrounded = transform.position.y <= groundY + 0.001f;
            if (isGrounded)
            {
                verticalVelocity = 0f;
                if (game.IsGameRunning && game.CanPlayerJump && WasJumpPressed())
                {
                    verticalVelocity = jumpSpeed;
                }
            }

            verticalVelocity -= gravity * Time.deltaTime;
            float targetY = transform.position.y + verticalVelocity * Time.deltaTime;
            if (targetY <= groundY)
            {
                targetY = groundY;
                verticalVelocity = 0f;
            }

            float halfHeight = spriteRenderer.bounds.extents.y;
            float ceilingY = game.TopEdge - halfHeight;
            if (targetY >= ceilingY)
            {
                targetY = ceilingY;
                verticalVelocity = Mathf.Min(0f, verticalVelocity);
            }

            transform.position = new Vector3(
                Mathf.Clamp(targetX, leftEdge, rightEdge),
                targetY,
                transform.position.z);
        }

        private static bool WasJumpPressed()
        {
            return Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.wKey.wasPressedThisFrame);
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
