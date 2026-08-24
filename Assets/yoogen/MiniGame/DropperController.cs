using UnityEngine;

namespace Sousakusai8.MiniGame
{
    /// <summary>Moves across the top of the screen and drops items at random intervals.</summary>
    public sealed class DropperController : MonoBehaviour
    {
        private CatchMiniGameController game;
        private SpriteRenderer spriteRenderer;
        private float targetX;
        private float baseMoveSpeed;
        private float nextDropTime;
        private float movementY;

        public void Initialize(CatchMiniGameController controller)
        {
            game = controller;
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementY = transform.position.y;
            PickNextTarget();
            ScheduleNextDrop(0.4f);
        }

        private void Update()
        {
            if (game == null || !game.IsGameRunning)
            {
                return;
            }

            Vector3 position = transform.position;
            float currentMoveSpeed = baseMoveSpeed * game.CurrentDropperSpeedMultiplier;
            position.x = Mathf.MoveTowards(position.x, targetX, currentMoveSpeed * Time.deltaTime);
            position.y = movementY;
            transform.position = position;

            if (Mathf.Abs(position.x - targetX) < 0.01f)
            {
                PickNextTarget();
            }

            if (Time.time >= nextDropTime)
            {
                game.SpawnItems(transform.position);
                ScheduleNextDrop();
            }
        }

        private void PickNextTarget()
        {
            float halfWidth = spriteRenderer.bounds.extents.x;
            targetX = Random.Range(game.GetLeftEdge(halfWidth), game.GetRightEdge(halfWidth));
            baseMoveSpeed = Random.Range(game.MinimumDropperSpeed, game.MaximumDropperSpeed);
        }

        private void ScheduleNextDrop(float additionalDelay = 0f)
        {
            nextDropTime = Time.time
                + additionalDelay
                + Random.Range(game.MinimumDropInterval, game.MaximumDropInterval);
        }

    }
}
