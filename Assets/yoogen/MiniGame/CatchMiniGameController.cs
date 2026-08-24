using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] private SpriteRenderer[] goodItemVisuals;
        [SerializeField] private SpriteRenderer[] badItemVisuals;

        [Header("UI References")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text legendText;
        [SerializeField] private Text controlsText;
        [SerializeField] private Text feedbackText;

        [Header("Object Pool")]
        [SerializeField, Min(1)] private int initialPoolSize = 24;

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

            if ((goodItemVisuals == null || goodItemVisuals.Length == 0) && itemPoolRoot != null)
            {
                goodItemVisuals = FindItemVisuals("Good Item Visual");
            }

            if ((badItemVisuals == null || badItemVisuals.Length == 0) && itemPoolRoot != null)
            {
                badItemVisuals = FindItemVisuals("Bad Item Visual");
            }

            ResolveHudReferences();

            if (gameCamera == null || catcher == null)
            {
                Debug.LogError(
                    "Player movement requires a Camera and Player Catcher reference.",
                    this);
                enabled = false;
                return;
            }

            catcher.Initialize(this, gameCamera);

            bool spawningReady = dropper != null && spawnedItemsRoot != null && itemPoolRoot != null &&
                goodItemVisuals != null && goodItemVisuals.Length > 0 &&
                badItemVisuals != null && badItemVisuals.Length > 0;
            if (spawningReady)
            {
                InitializeItemPool();
                dropper.Initialize(this);
            }
            else
            {
                if (dropper != null)
                {
                    dropper.enabled = false;
                }

                Debug.LogWarning(
                    "Item spawning references are incomplete. Player movement will continue.",
                    this);
            }

            if (HasHudReferences())
            {
                RefreshHud();
                feedbackText.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning(
                    "Catch Mini Game UI references are missing. Gameplay will continue without the HUD.",
                    this);
            }
        }

        private void Update()
        {
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            {
                ResetGame();
            }

            if (feedbackText != null && feedbackText.gameObject.activeSelf &&
                Time.unscaledTime >= feedbackVisibleUntil)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        public void SpawnItem(Vector3 dropperPosition)
        {
            bool isBad = Random.value < badItemChance;
            FallingItemKind kind = isBad ? FallingItemKind.Bad : FallingItemKind.Good;
            SpriteRenderer[] candidates = isBad ? badItemVisuals : goodItemVisuals;
            SpriteRenderer sourceVisual = candidates[Random.Range(0, candidates.Length)];
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

            if (itemPoolRoot == null)
            {
                return;
            }

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
            ShowFeedback();
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

        private SpriteRenderer[] FindItemVisuals(string namePrefix)
        {
            var matches = new List<SpriteRenderer>();
            foreach (SpriteRenderer visual in itemPoolRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (visual.name.StartsWith(namePrefix))
                {
                    matches.Add(visual);
                }
            }

            return matches.ToArray();
        }

        private void ResetGame()
        {
            score = 0;
            catchFeedback = "RESET";
            feedbackVisibleUntil = Time.unscaledTime + 0.55f;
            ShowFeedback();

            if (spawnedItemsRoot != null)
            {
                FallingItem[] activeItems = spawnedItemsRoot.GetComponentsInChildren<FallingItem>(true);
                foreach (FallingItem item in activeItems)
                {
                    ReleaseItem(item);
                }
            }
        }

        private void RefreshHud()
        {
            if (!HasHudReferences())
            {
                return;
            }

            scoreText.text = $"SCORE  {score}";
            legendText.text = $"YELLOW  +{goodItemScore}     RED  -{badItemPenalty}";
            controlsText.text = "Move: Mouse / A D     Reset: R";
        }

        private void ShowFeedback()
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE  {score}";
            }

            if (feedbackText != null)
            {
                feedbackText.text = catchFeedback;
                feedbackText.gameObject.SetActive(true);
            }
        }

        private void ResolveHudReferences()
        {
            Transform hud = transform.Find("Catch Mini Game UI");
            if (hud == null)
            {
                return;
            }

            scoreText ??= hud.Find("Score Text")?.GetComponent<Text>();
            legendText ??= hud.Find("Legend Text")?.GetComponent<Text>();
            controlsText ??= hud.Find("Controls Text")?.GetComponent<Text>();
            feedbackText ??= hud.Find("Feedback Text")?.GetComponent<Text>();
        }

        private bool HasHudReferences()
        {
            return scoreText != null && legendText != null &&
                controlsText != null && feedbackText != null;
        }

    }
}
