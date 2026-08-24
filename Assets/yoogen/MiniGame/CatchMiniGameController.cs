using System.Collections.Generic;
using UnityEngine;
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
        [SerializeField] private Text feedbackText;
        [SerializeField] private Text timeText;

        [Header("Object Pool")]
        [SerializeField, Min(1)] private int initialPoolSize = 24;

        [Header("Score")]
        [SerializeField] private int goodItemScore = 100;
        [SerializeField] private int badItemPenalty = 150;
        [SerializeField, Range(0f, 1f)] private float badItemChance = 0.25f;

        [Header("Time and Difficulty")]
        [SerializeField, Min(1f)] private float gameDuration = 30f;
        [SerializeField, Min(1)] private int minimumSpawnCount = 1;
        [SerializeField, Min(1)] private int maximumSpawnCount = 4;
        [SerializeField, Min(0f)] private float itemHorizontalSpread = 0.75f;
        [SerializeField, Min(1f)] private float maximumDropperSpeedMultiplier = 2f;
        [SerializeField, Min(0f)] private float badItemStackDuration = 4f;

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
        private float remainingTime;
        private bool isGameOver;

        public float BottomEdge => gameCamera.transform.position.y - gameCamera.orthographicSize;
        public float TopEdge => gameCamera.transform.position.y + gameCamera.orthographicSize;
        public float MinimumDropInterval => minimumDropInterval;
        public float MaximumDropInterval => maximumDropInterval;
        public float MinimumDropperSpeed => minimumDropperSpeed;
        public float MaximumDropperSpeed => maximumDropperSpeed;
        public float CurrentDropperSpeedMultiplier => Mathf.Lerp(
            1f,
            maximumDropperSpeedMultiplier,
            GetDifficultyProgress());
        public float BadItemStackDuration => badItemStackDuration;
        public bool IsGameRunning => !isGameOver;

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

            StartRound();
        }

        private void Update()
        {
            if (!isGameOver)
            {
                remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
                UpdateTimeText();
                if (remainingTime <= 0f)
                {
                    EndRound();
                }
            }

            if (!isGameOver && feedbackText != null && feedbackText.gameObject.activeSelf &&
                Time.unscaledTime >= feedbackVisibleUntil)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        public void SpawnItems(Vector3 dropperPosition)
        {
            if (isGameOver)
            {
                return;
            }

            int spawnCount = GetCurrentSpawnCount();
            float centerOffset = (spawnCount - 1) * 0.5f;
            for (int i = 0; i < spawnCount; i++)
            {
                float xOffset = (i - centerOffset) * itemHorizontalSpread;
                Vector3 position = dropperPosition + new Vector3(
                    xOffset + Random.Range(-0.12f, 0.12f),
                    Random.Range(-0.12f, 0.12f),
                    0f);
                position.x = Mathf.Clamp(position.x, GetLeftEdge(0.35f), GetRightEdge(0.35f));
                SpawnItem(position);
            }
        }

        private void SpawnItem(Vector3 dropperPosition)
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

        private void RefreshHud()
        {
            if (!HasHudReferences())
            {
                return;
            }

            scoreText.text = $"SCORE  {score}";
            UpdateTimeText();
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
            feedbackText ??= hud.Find("Feedback Text")?.GetComponent<Text>();
            timeText ??= hud.Find("Time Text")?.GetComponent<Text>();
        }

        private bool HasHudReferences()
        {
            return scoreText != null && feedbackText != null && timeText != null;
        }

        private void StartRound()
        {
            remainingTime = gameDuration;
            isGameOver = false;
            RefreshHud();
        }

        private void EndRound()
        {
            remainingTime = 0f;
            isGameOver = true;
            ClearActiveItems();
            catchFeedback = "TIME UP";
            if (feedbackText != null)
            {
                feedbackText.text = catchFeedback;
                feedbackText.gameObject.SetActive(true);
            }

            UpdateTimeText();
        }

        private int GetCurrentSpawnCount()
        {
            int minCount = Mathf.Max(1, minimumSpawnCount);
            int maxCount = Mathf.Max(minCount, maximumSpawnCount);
            float progress = GetDifficultyProgress();
            return Mathf.Clamp(
                minCount + Mathf.FloorToInt(progress * (maxCount - minCount + 1)),
                minCount,
                maxCount);
        }

        private float GetDifficultyProgress()
        {
            return Mathf.Clamp01(1f - remainingTime / Mathf.Max(1f, gameDuration));
        }

        private void UpdateTimeText()
        {
            if (timeText != null)
            {
                timeText.text = $"TIME  {Mathf.CeilToInt(remainingTime)}";
            }
        }

        private void ClearActiveItems()
        {
            if (spawnedItemsRoot == null)
            {
                return;
            }

            FallingItem[] activeItems = spawnedItemsRoot.GetComponentsInChildren<FallingItem>(true);
            foreach (FallingItem item in activeItems)
            {
                ReleaseItem(item);
            }
        }

    }
}
