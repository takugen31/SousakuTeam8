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

        [Header("Catch Area")]
        [SerializeField, Range(0.1f, 1f)] private float catchBoundsScale = 0.75f;

        private CatchMiniGameController game;
        private Camera gameCamera;
        private SpriteRenderer spriteRenderer;
        private float groundY;
        private float groundSurfaceY;
        private float verticalVelocity;
        private Vector2 previousMousePosition;
        private bool mousePositionInitialized;
        private bool usingKeyboard;
        private bool inputWasPaused;
        private bool waitForMouseMovementAfterPause;

        public Bounds CatchBounds
        {
            get
            {
                Bounds bounds = spriteRenderer.bounds;
                bounds.size = Vector3.Scale(bounds.size, new Vector3(catchBoundsScale, catchBoundsScale, 1f));
                return bounds;
            }
        }
        public float GroundY => groundY;
        public float GroundSurfaceY => groundSurfaceY;

        public void Initialize(CatchMiniGameController controller, Camera targetCamera)
        {
            game = controller;
            gameCamera = targetCamera;
            spriteRenderer = GetComponent<SpriteRenderer>();
            groundY = transform.position.y;
            // Movement uses the sprite's center; grounded items share its visible bottom edge.
            groundSurfaceY = spriteRenderer.bounds.min.y;
            verticalVelocity = 0f;
        }

        private void Update()
        {
            if (game == null || gameCamera == null)
            {
                return;
            }

            if (game.IsPaused) { inputWasPaused = true; return; }
            if (inputWasPaused)
            {
                inputWasPaused = false;
                waitForMouseMovementAfterPause = true;
                mousePositionInitialized = Mouse.current != null;
                if (Mouse.current != null) previousMousePosition = Mouse.current.position.ReadValue();
                return;
            }

            float halfWidth = spriteRenderer.bounds.extents.x;
            float leftEdge = game.GetLeftEdge(halfWidth);
            float rightEdge = game.GetRightEdge(halfWidth);
            bool canPlayerMove = game.CanPlayerMove;
            float keyboardDirection = canPlayerMove ? ReadKeyboardDirection() : 0f;

            if (!Mathf.Approximately(keyboardDirection, 0f))
            {
                usingKeyboard = true;
            }

            bool mouseMoved = DidMouseMove(out Vector2 screenPosition);
            if (mouseMoved) waitForMouseMovementAfterPause = false;
            if (canPlayerMove && mouseMoved && Mathf.Approximately(keyboardDirection, 0f))
            {
                usingKeyboard = false;
            }

            float targetX = transform.position.x;
            if (canPlayerMove && usingKeyboard)
            {
                targetX += keyboardDirection * keyboardMoveSpeed * Time.deltaTime;
            }
            else if (canPlayerMove && Mouse.current != null && !waitForMouseMovementAfterPause)
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
