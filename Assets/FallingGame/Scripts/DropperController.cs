using UnityEngine;

namespace Sousakusai8.MiniGame
{
    /// <summary>Moves across the top of the screen and drops items at random intervals.</summary>
    public sealed class DropperController : MonoBehaviour
    {
        [Header("Visual Animation")]
        [SerializeField] private Sprite[] visualFrames;
        [SerializeField, Min(0.1f)] private float visualSwitchInterval = 1f;

        private CatchMiniGameController game;
        private SpriteRenderer spriteRenderer;
        private float targetX;
        private float baseMoveSpeed;
        private float nextDropTime;
        private float nextVisualSwitchTime;
        private float movementY;
        private int currentVisualIndex;

        public void Initialize(CatchMiniGameController controller)
        {
            game = controller;
            spriteRenderer = GetComponent<SpriteRenderer>();
            movementY = transform.position.y;
            currentVisualIndex = 0;
            ApplyCurrentVisual();
            nextVisualSwitchTime = Time.time + visualSwitchInterval;
            PickNextTarget();
        }

        public void BeginRound()
        {
            PickNextTarget();
            ScheduleNextDrop(0.4f);
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            UpdateVisualAnimation();

            if (!game.IsGameRunning)
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

        private void UpdateVisualAnimation()
        {
            if (spriteRenderer == null || visualFrames == null || visualFrames.Length < 2 ||
                Time.time < nextVisualSwitchTime)
            {
                return;
            }

            currentVisualIndex = (currentVisualIndex + 1) % visualFrames.Length;
            ApplyCurrentVisual();
            nextVisualSwitchTime = Time.time + visualSwitchInterval;
        }

        private void ApplyCurrentVisual()
        {
            if (spriteRenderer == null || visualFrames == null || visualFrames.Length == 0)
            {
                return;
            }

            Sprite visual = visualFrames[Mathf.Clamp(currentVisualIndex, 0, visualFrames.Length - 1)];
            if (visual != null)
            {
                spriteRenderer.sprite = visual;
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
