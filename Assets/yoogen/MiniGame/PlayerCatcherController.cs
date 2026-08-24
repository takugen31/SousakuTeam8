using UnityEngine;
using UnityEngine.InputSystem;

namespace Sousakusai8.MiniGame
{
    /// <summary>Keeps the catcher at the bottom of the screen and follows the mouse on X.</summary>
    public sealed class PlayerCatcherController : MonoBehaviour
    {
        private CatchMiniGameController game;
        private Camera gameCamera;
        private SpriteRenderer spriteRenderer;
        private float movementY;

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
            if (game == null || gameCamera == null || Mouse.current == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            float distanceToGamePlane = Mathf.Abs(gameCamera.transform.position.z - transform.position.z);
            Vector3 worldPosition = gameCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceToGamePlane));

            float halfWidth = spriteRenderer.bounds.extents.x;
            float clampedX = Mathf.Clamp(
                worldPosition.x,
                game.GetLeftEdge(halfWidth),
                game.GetRightEdge(halfWidth));

            transform.position = new Vector3(clampedX, movementY, transform.position.z);
        }
    }
}
