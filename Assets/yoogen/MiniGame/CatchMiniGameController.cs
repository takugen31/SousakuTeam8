using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sousakusai8.MiniGame
{
    /// <summary>
    /// Creates and coordinates the minimum playable version of the catch game.
    /// The main actors are scene objects; only falling items are created at runtime.
    /// </summary>
    public sealed class CatchMiniGameController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera gameCamera;
        [SerializeField] private DropperController dropper;
        [SerializeField] private PlayerCatcherController catcher;
        [SerializeField] private Transform spawnedItemsRoot;
        [SerializeField] private Transform itemPoolRoot;
        [SerializeField] private SpriteRenderer goodItemVisual;
        [SerializeField] private SpriteRenderer badItemVisual;

        [Header("Object Pool")]
        [SerializeField, Min(1)] private int initialPoolSize = 12;

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

        private readonly Queue<FallingItem> availableItems = new();
        private int nextPoolNumber = 1;
        private int score;
        private string catchFeedback = string.Empty;
        private float feedbackVisibleUntil;
        private GUIStyle scoreStyle;
        private GUIStyle helpStyle;
        private GUIStyle feedbackStyle;

        public float BottomEdge => gameCamera.transform.position.y - gameCamera.orthographicSize;
        public float TopEdge => gameCamera.transform.position.y + gameCamera.orthographicSize;
        public float MinimumDropInterval => minimumDropInterval;
        public float MaximumDropInterval => maximumDropInterval;
        public float MinimumDropperSpeed => minimumDropperSpeed;
        public float MaximumDropperSpeed => maximumDropperSpeed;

        private void Awake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }

            if (dropper == null)
            {
                dropper = GetComponentInChildren<DropperController>(true);
            }

            if (catcher == null)
            {
                catcher = GetComponentInChildren<PlayerCatcherController>(true);
            }

            if (spawnedItemsRoot == null)
            {
                spawnedItemsRoot = transform.Find("Spawned Items");
            }

            if (itemPoolRoot == null)
            {
                itemPoolRoot = transform.Find("Item Pool");
            }

            if (goodItemVisual == null && itemPoolRoot != null)
            {
                goodItemVisual = itemPoolRoot.Find("Good Item Visual")?.GetComponent<SpriteRenderer>();
            }

            if (badItemVisual == null && itemPoolRoot != null)
            {
                badItemVisual = itemPoolRoot.Find("Bad Item Visual")?.GetComponent<SpriteRenderer>();
            }

            if (gameCamera == null || dropper == null || catcher == null ||
                spawnedItemsRoot == null || itemPoolRoot == null ||
                goodItemVisual == null || badItemVisual == null)
            {
                Debug.LogError(
                    "Catch Mini Game is missing a scene reference. " +
                    "Assign the Camera, actors, hierarchy roots, and item visuals in the Inspector.",
                    this);
                enabled = false;
                return;
            }

            InitializeItemPool();
            dropper.Initialize(this);
            catcher.Initialize(this, gameCamera);
        }

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            {
                ResetGame();
            }
        }

        public void SpawnItem(Vector3 dropperPosition)
        {
            bool isBad = Random.value < badItemChance;
            FallingItemKind kind = isBad ? FallingItemKind.Bad : FallingItemKind.Good;
            SpriteRenderer sourceVisual = isBad ? badItemVisual : goodItemVisual;
            Vector3 spawnPosition = new(dropperPosition.x, dropperPosition.y - 0.65f, 0f);

            FallingItem item = GetPooledItem();
            GameObject itemObject = item.gameObject;
            itemObject.name = isBad ? "Bad Item" : "Good Item";
            itemObject.transform.SetParent(spawnedItemsRoot, false);
            itemObject.transform.position = spawnPosition;
            itemObject.transform.localRotation = sourceVisual.transform.localRotation;
            itemObject.transform.localScale = sourceVisual.transform.localScale;

            SpriteRenderer spriteRenderer = itemObject.GetComponent<SpriteRenderer>();
            CopyVisual(sourceVisual, spriteRenderer);

            item.Initialize(
                this,
                catcher,
                kind,
                Random.Range(minimumFallSpeed, maximumFallSpeed));
            itemObject.SetActive(true);
        }

        public void ReleaseItem(FallingItem item)
        {
            if (item == null)
            {
                return;
            }

            GameObject itemObject = item.gameObject;
            itemObject.SetActive(false);
            itemObject.name = item.PooledName;
            itemObject.transform.SetParent(itemPoolRoot, false);
            itemObject.transform.localPosition = Vector3.zero;
            itemObject.transform.localRotation = Quaternion.identity;
            availableItems.Enqueue(item);
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

        private void InitializeItemPool()
        {
            availableItems.Clear();

            FallingItem[] existingItems = itemPoolRoot.GetComponentsInChildren<FallingItem>(true);
            foreach (FallingItem item in existingItems)
            {
                item.SetPooledName($"Pooled Item {nextPoolNumber++:00}");
                item.gameObject.name = item.PooledName;
                item.gameObject.SetActive(false);
                availableItems.Enqueue(item);
            }

            while (availableItems.Count < initialPoolSize)
            {
                availableItems.Enqueue(CreatePooledItem());
            }
        }

        private FallingItem GetPooledItem()
        {
            return availableItems.Count > 0 ? availableItems.Dequeue() : CreatePooledItem();
        }

        private FallingItem CreatePooledItem()
        {
            var itemObject = new GameObject($"Pooled Item {nextPoolNumber++:00}");
            itemObject.transform.SetParent(itemPoolRoot, false);

            var spriteRenderer = itemObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 5;

            FallingItem item = itemObject.AddComponent<FallingItem>();
            item.SetPooledName(itemObject.name);
            itemObject.SetActive(false);
            return item;
        }

        private static void CopyVisual(SpriteRenderer source, SpriteRenderer destination)
        {
            destination.sprite = source.sprite;
            destination.sharedMaterial = source.sharedMaterial;
            destination.color = source.color;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            destination.flipX = source.flipX;
            destination.flipY = source.flipY;
            destination.drawMode = source.drawMode;
            destination.size = source.size;
            destination.maskInteraction = source.maskInteraction;
            destination.spriteSortPoint = source.spriteSortPoint;
        }

        private void ResetGame()
        {
            score = 0;
            catchFeedback = "RESET";
            feedbackVisibleUntil = Time.unscaledTime + 0.55f;

            FallingItem[] activeItems = spawnedItemsRoot.GetComponentsInChildren<FallingItem>(true);
            foreach (FallingItem item in activeItems)
            {
                ReleaseItem(item);
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

    }
}
