using UnityEngine;
using UnityEngine.InputSystem;

namespace Sousakusai8.MiniGame
{
    /// <summary>
    /// Creates and coordinates the minimum playable version of the catch game.
    /// All visuals are generated at runtime, so no image assets or prefabs are required.
    /// </summary>
    public sealed class CatchMiniGameController : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private int goodItemScore = 100;
        [SerializeField] private int badItemPenalty = 150;
        [SerializeField, Range(0f, 1f)] private float badItemChance = 0.25f;

        [Header("Drop timing")]
        [SerializeField] private float minimumDropInterval = 0.65f;
        [SerializeField] private float maximumDropInterval = 1.25f;
        [SerializeField] private float minimumFallSpeed = 2.8f;
        [SerializeField] private float maximumFallSpeed = 4.2f;

        [Header("Dropper movement")]
        [SerializeField] private float minimumDropperSpeed = 1.5f;
        [SerializeField] private float maximumDropperSpeed = 3.2f;

        private static readonly Color BackgroundColor = new(0.055f, 0.075f, 0.12f, 1f);
        private static readonly Color DropperColor = new(0.72f, 0.4f, 0.95f, 1f);
        private static readonly Color CatcherColor = new(0.25f, 0.75f, 1f, 1f);
        private static readonly Color GoodItemColor = new(1f, 0.82f, 0.18f, 1f);
        private static readonly Color BadItemColor = new(1f, 0.25f, 0.3f, 1f);

        private Camera gameCamera;
        private Sprite runtimeSprite;
        private PlayerCatcherController catcher;
        private int score;
        private string catchFeedback = string.Empty;
        private float feedbackVisibleUntil;
        private GUIStyle scoreStyle;
        private GUIStyle helpStyle;
        private GUIStyle feedbackStyle;

        public float BottomEdge => gameCamera.transform.position.y - gameCamera.orthographicSize;
        public float TopEdge => gameCamera.transform.position.y + gameCamera.orthographicSize;
        public float DropperY => TopEdge - 1f;
        public float CatcherY => BottomEdge + 0.8f;
        public float MinimumDropInterval => minimumDropInterval;
        public float MaximumDropInterval => maximumDropInterval;
        public float MinimumDropperSpeed => minimumDropperSpeed;
        public float MaximumDropperSpeed => maximumDropperSpeed;

        private void Awake()
        {
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }

            gameCamera.orthographic = true;
            gameCamera.orthographicSize = Mathf.Max(5f, gameCamera.orthographicSize);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = BackgroundColor;

            runtimeSprite = CreateBlockSprite();
            CreateActors();
        }

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            {
                ResetGame();
            }
        }

        private void CreateActors()
        {
            var dropperObject = CreateBlock(
                "Dropper",
                new Vector2(1.4f, 0.65f),
                DropperColor,
                new Vector3(0f, DropperY, 0f),
                10);

            var dropper = dropperObject.AddComponent<DropperController>();
            dropper.Initialize(this);

            var catcherObject = CreateBlock(
                "Player Catcher",
                new Vector2(2f, 0.45f),
                CatcherColor,
                new Vector3(0f, CatcherY, 0f),
                10);

            catcher = catcherObject.AddComponent<PlayerCatcherController>();
            catcher.Initialize(this, gameCamera);
        }

        public void SpawnItem(Vector3 dropperPosition)
        {
            bool isBad = Random.value < badItemChance;
            FallingItemKind kind = isBad ? FallingItemKind.Bad : FallingItemKind.Good;
            Color color = isBad ? BadItemColor : GoodItemColor;
            Vector2 size = isBad ? new Vector2(0.62f, 0.62f) : new Vector2(0.5f, 0.5f);
            Vector3 spawnPosition = new(dropperPosition.x, dropperPosition.y - 0.65f, 0f);

            var itemObject = CreateBlock(
                isBad ? "Bad Item" : "Good Item",
                size,
                color,
                spawnPosition,
                5);

            var item = itemObject.AddComponent<FallingItem>();
            item.Initialize(
                this,
                catcher,
                kind,
                Random.Range(minimumFallSpeed, maximumFallSpeed));
        }

        public void RecordCatch(FallingItemKind kind)
        {
            bool isGood = kind == FallingItemKind.Good;
            int difference = isGood ? goodItemScore : -badItemPenalty;
            score += difference;
            catchFeedback = difference > 0 ? $"+{difference}" : difference.ToString();
            feedbackVisibleUntil = Time.unscaledTime + 0.55f;
        }

        public float GetLeftEdge(float objectHalfWidth)
        {
            float halfViewWidth = gameCamera.orthographicSize * gameCamera.aspect;
            return gameCamera.transform.position.x - halfViewWidth + objectHalfWidth;
        }

        public float GetRightEdge(float objectHalfWidth)
        {
            float halfViewWidth = gameCamera.orthographicSize * gameCamera.aspect;
            return gameCamera.transform.position.x + halfViewWidth - objectHalfWidth;
        }

        private GameObject CreateBlock(
            string objectName,
            Vector2 size,
            Color color,
            Vector3 position,
            int sortingOrder)
        {
            var block = new GameObject(objectName);
            block.transform.SetParent(transform);
            block.transform.position = position;
            block.transform.localScale = new Vector3(size.x, size.y, 1f);

            var spriteRenderer = block.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = runtimeSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            return block;
        }

        private static Sprite CreateBlockSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Block Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = "Runtime Block Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void ResetGame()
        {
            score = 0;
            catchFeedback = "RESET";
            feedbackVisibleUntil = Time.unscaledTime + 0.55f;

            foreach (FallingItem item in GetComponentsInChildren<FallingItem>())
            {
                Destroy(item.gameObject);
            }
        }

        private void OnGUI()
        {
            EnsureGuiStyles();

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 540f), 0.65f, 1.5f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(22f, 18f, 470f, 145f));
            GUILayout.Label($"SCORE  {score}", scoreStyle);
            GUILayout.Label($"YELLOW  +{goodItemScore}     RED  -{badItemPenalty}", helpStyle);
            GUILayout.Label("Move: Mouse     Reset: R", helpStyle);
            GUILayout.EndArea();

            if (Time.unscaledTime < feedbackVisibleUntil)
            {
                float virtualWidth = Screen.width / scale;
                GUI.Label(new Rect(0f, 85f, virtualWidth, 70f), catchFeedback, feedbackStyle);
            }

            GUI.matrix = previousMatrix;
        }

        private void EnsureGuiStyles()
        {
            if (scoreStyle != null)
            {
                return;
            }

            scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            helpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.8f, 0.86f, 0.95f) }
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void OnDestroy()
        {
            if (runtimeSprite == null)
            {
                return;
            }

            Texture2D texture = runtimeSprite.texture;
            Destroy(runtimeSprite);
            Destroy(texture);
        }
    }
}
