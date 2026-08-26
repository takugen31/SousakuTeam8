using UnityEngine;

namespace Sousakusai8.MiniGame
{
    public enum FallingItemKind
    {
        Good,
        Bad
    }

    /// <summary>Falls vertically and reports a catch when its bounds overlap the player.</summary>
    public sealed class FallingItem : MonoBehaviour
    {
        private CatchMiniGameController game;
        private PlayerCatcherController catcher;
        private SpriteRenderer spriteRenderer;
        private FallingItemKind kind;
        private float fallSpeed;
        private float rotationSpeed;
        private bool resolved;
        private bool stackedAtBottom;
        private float stackedUntil;
        private string pooledName;

        public string PooledName => pooledName;
        public bool IsStackedBad => kind == FallingItemKind.Bad && stackedAtBottom && !resolved;

        public void SetPooledName(string value)
        {
            pooledName = value;
        }

        public void Initialize(
            CatchMiniGameController controller,
            PlayerCatcherController playerCatcher,
            FallingItemKind itemKind,
            float speed)
        {
            game = controller;
            catcher = playerCatcher;
            spriteRenderer = GetComponent<SpriteRenderer>();
            kind = itemKind;
            fallSpeed = speed;
            rotationSpeed = Random.Range(-100f, 100f);
            resolved = false;
            stackedAtBottom = false;
            stackedUntil = 0f;
        }

        private void Update()
        {
            if (resolved || game == null || catcher == null)
            {
                return;
            }

            if (spriteRenderer.bounds.Intersects(catcher.CatchBounds))
            {
                resolved = true;
                game.RecordCatch(kind);
                game.ReleaseItem(this);
                return;
            }

            if (stackedAtBottom)
            {
                if (Time.time >= stackedUntil)
                {
                    resolved = true;
                    game.ReleaseItem(this);
                }

                return;
            }

            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (kind == FallingItemKind.Bad &&
                spriteRenderer.bounds.center.y <= catcher.GroundY)
            {
                StackAtBottom();
                return;
            }

            if (spriteRenderer.bounds.max.y < game.BottomEdge)
            {
                resolved = true;
                game.ReleaseItem(this);
            }
        }

        private void StackAtBottom()
        {
            stackedAtBottom = true;
            stackedUntil = Time.time + game.BadItemStackDuration;
            gameObject.name = "Stacked Bad Item";
            transform.position = new Vector3(
                transform.position.x,
                catcher.GroundY,
                transform.position.z);
            transform.rotation = Quaternion.identity;
        }
    }
}
