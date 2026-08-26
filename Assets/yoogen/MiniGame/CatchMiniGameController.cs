using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sousakusai8.MiniGame
{
    /// <summary>
    /// Creates and coordinates the minimum playable version of the catch game.
    /// Main actors, UI, visual templates, and the normal item pool are scene objects.
    /// </summary>
    public sealed class CatchMiniGameController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class ScoreMessageTier
        {
            public int minimumScore;
            public string message;
        }

        private enum GamePhase
        {
            AwaitingInput,
            Countdown,
            Playing,
            GameOver
        }

        [Header("Scene References")]
        [SerializeField] private Camera gameCamera;
        [SerializeField] private DropperController dropper;
        [SerializeField] private PlayerCatcherController catcher;
        [SerializeField] private Transform spawnedItemsRoot;
        [SerializeField] private Transform itemPoolRoot;
        [SerializeField] private SpriteRenderer[] goodItemVisuals;
        [SerializeField] private SpriteRenderer[] badItemVisuals;
        [SerializeField] private Vector2[] badItemFallingSizes =
        {
            new(0.93f, 0.93f),
            new(0.93f, 0.93f),
            new(0.93f, 0.93f)
        };

        [Header("Falling Item Visual Size")]
        [SerializeField] private Vector2 fallingItemSize = new(0.62f, 0.62f);

        [Header("Stacked Bad Item Visual")]
        [SerializeField] private Sprite stackedBadItemSprite;
        [SerializeField] private Sprite[] stackedBadItemSprites;
        [SerializeField] private Vector2 stackedBadItemSize = new(1.4f, 0.85f);
        [SerializeField] private Vector2[] stackedBadItemSizes =
        {
            new(2.1f, 1.275f),
            new(2.1f, 1.275f),
            new(2.1f, 1.275f)
        };

        [Header("Rendering Quality")]
        [SerializeField] private bool forceHighestQualityLevel = true;
        [SerializeField, Range(1f, 2f)] private float renderScale = 2f;
        [SerializeField] private int antiAliasingSamples = 4;

        [Header("UI References")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text scoreMessageText;
        [SerializeField] private Text jumpUnlockText;
        [SerializeField] private Text sweepUnlockText;
        [SerializeField] private Text startPromptText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Image resultPortrait;
        [SerializeField] private Text resultScoreText;
        [SerializeField] private Text resultDialogueText;
        [SerializeField] private Text resultCountsText;

        [Header("Object Pool")]
        [SerializeField, Min(1)] private int initialPoolSize = 64;

        [Header("Score")]
        [SerializeField] private int goodItemScore = 100;
        [SerializeField] private int badItemPenalty = 150;
        [SerializeField, Range(0f, 1f)] private float badItemChance = 0.25f;

        [Header("Jump Unlock")]
        [SerializeField] private int jumpUnlockScore = 2000;
        [SerializeField, Min(0f)] private float jumpUnlockMessageDuration = 3f;
        [SerializeField] private string jumpUnlockMessage = "\"ジャンプ\"を獲得!";

        [Header("Sweep Unlock")]
        [SerializeField] private int sweepUnlockScore = 6000;
        [SerializeField, Min(0)] private int sweepScoreCost = 200;
        [SerializeField, Min(0f)] private float sweepCooldown = 10f;
        [SerializeField, Min(0f)] private float sweepUnlockMessageDuration = 3f;
        [SerializeField] private string sweepUnlockMessage = "\"一掃\"を獲得!";

        [Header("Score Messages")]
        [SerializeField] private ScoreMessageTier[] scoreMessageTiers =
        {
            new() { minimumScore = -1000000, message = "まだまだ!" },
            new() { minimumScore = 1000, message = "こっから！" },
            new() { minimumScore = 2000, message = "やるじゃん？" },
            new() { minimumScore = 3000, message = "ええええ（ドン引き）" }
        };

        [Header("Start Sequence")]
        [SerializeField] private string startPromptMessage = "大変！かよがピンチ！";
        [SerializeField, Min(0.1f)] private float countdownStepDuration = 1f;

        [Header("Game Over Presentation")]
        [SerializeField] private string resultDialogue = "助かったよ！ありがとう！";

        [Header("Time and Difficulty")]
        [SerializeField, Min(1f)] private float gameDuration = 100f;
        [SerializeField, Min(1)] private int minimumSpawnCount = 1;
        [SerializeField, Min(1)] private int maximumSpawnCount = 8;
        [SerializeField, Min(0f)] private float itemHorizontalSpread = 0.75f;
        [SerializeField, Min(1f)] private float maximumDropperSpeedMultiplier = 2f;
        [SerializeField, Min(0f)] private float badItemStackDuration = 2f;

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
        private int collectedGoodItemCount;
        private int collectedBadItemCount;
        private string catchFeedback = string.Empty;
        private float feedbackVisibleUntil;
        private float jumpUnlockVisibleUntil;
        private float sweepUnlockVisibleUntil;
        private float nextSweepAvailableTime;
        private float remainingTime;
        private float countdownRemaining;
        private bool jumpUnlocked;
        private bool sweepUnlocked;
        private GamePhase phase;
        private int originalQualityLevel;
        private int originalTextureMipmapLimit;
        private int originalAntiAliasing;
        private RenderPipelineAsset originalRenderPipeline;
        private UniversalRenderPipelineAsset runtimeRenderPipeline;
        private bool renderingQualityApplied;

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
        public Vector2 FallingItemSize => fallingItemSize;
        public Sprite StackedBadItemSprite => stackedBadItemSprite;
        public Vector2 StackedBadItemSize => stackedBadItemSize;
        public bool IsGameRunning => phase == GamePhase.Playing;
        public bool CanPlayerMove => phase == GamePhase.Countdown || phase == GamePhase.Playing;
        public bool CanPlayerJump => jumpUnlocked;

        private void Awake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }

            ApplyRenderingQuality();

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

            PrepareForStart();
        }

        private void Update()
        {
            if (phase == GamePhase.AwaitingInput && WasAnyStartButtonPressed())
            {
                BeginCountdown();
            }
            else if (phase == GamePhase.Countdown)
            {
                UpdateCountdown();
            }
            else if (phase == GamePhase.Playing)
            {
                remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
                UpdateTimeText();
                if (remainingTime <= 0f)
                {
                    EndRound();
                }

                if (phase == GamePhase.Playing && sweepUnlocked && WasSweepPressed())
                {
                    TryUseSweep();
                }
            }

            if (phase == GamePhase.Playing && feedbackText != null && feedbackText.gameObject.activeSelf &&
                Time.unscaledTime >= feedbackVisibleUntil)
            {
                feedbackText.gameObject.SetActive(false);
            }

            if (jumpUnlockText != null && jumpUnlockText.gameObject.activeSelf &&
                Time.unscaledTime >= jumpUnlockVisibleUntil)
            {
                jumpUnlockText.gameObject.SetActive(false);
            }

            if (sweepUnlockText != null && sweepUnlockText.gameObject.activeSelf &&
                Time.unscaledTime >= sweepUnlockVisibleUntil)
            {
                sweepUnlockText.gameObject.SetActive(false);
            }
        }

        private void ApplyRenderingQuality()
        {
            originalQualityLevel = QualitySettings.GetQualityLevel();
            originalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            originalAntiAliasing = QualitySettings.antiAliasing;
            originalRenderPipeline = QualitySettings.renderPipeline;
            renderingQualityApplied = true;

            if (forceHighestQualityLevel && QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            }

            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.antiAliasing = antiAliasingSamples;
            ScalableBufferManager.ResizeBuffers(1f, 1f);

            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset sourcePipelineAsset)
            {
                runtimeRenderPipeline = Instantiate(sourcePipelineAsset);
                runtimeRenderPipeline.name = $"{sourcePipelineAsset.name} (Mini Game Runtime)";
                runtimeRenderPipeline.hideFlags = HideFlags.HideAndDontSave;
                runtimeRenderPipeline.renderScale = renderScale;
                runtimeRenderPipeline.msaaSampleCount = antiAliasingSamples;
                QualitySettings.renderPipeline = runtimeRenderPipeline;
            }

            if (gameCamera != null)
            {
                gameCamera.allowMSAA = true;
                gameCamera.allowDynamicResolution = false;
            }
        }

        private void OnDestroy()
        {
            if (!renderingQualityApplied)
            {
                return;
            }

            QualitySettings.SetQualityLevel(originalQualityLevel, true);
            QualitySettings.globalTextureMipmapLimit = originalTextureMipmapLimit;
            QualitySettings.antiAliasing = originalAntiAliasing;
            QualitySettings.renderPipeline = originalRenderPipeline;
            ScalableBufferManager.ResizeBuffers(1f, 1f);

            if (runtimeRenderPipeline != null)
            {
                Destroy(runtimeRenderPipeline);
            }
        }

        public void SpawnItems(Vector3 dropperPosition)
        {
            if (!IsGameRunning)
            {
                return;
            }

            int spawnCount = GetCurrentSpawnCount();
            int badItemsRemaining = spawnCount >= 4
                ? Mathf.Clamp(Mathf.RoundToInt(spawnCount * badItemChance), 1, spawnCount - 1)
                : -1;
            float centerOffset = (spawnCount - 1) * 0.5f;
            for (int i = 0; i < spawnCount; i++)
            {
                float xOffset = (i - centerOffset) * itemHorizontalSpread;
                Vector3 position = dropperPosition + new Vector3(
                    xOffset + Random.Range(-0.12f, 0.12f),
                    Random.Range(-0.12f, 0.12f),
                    0f);
                position.x = Mathf.Clamp(position.x, GetLeftEdge(0.35f), GetRightEdge(0.35f));

                FallingItemKind? forcedKind = null;
                if (badItemsRemaining >= 0)
                {
                    int slotsRemaining = spawnCount - i;
                    bool spawnBad = Random.value < badItemsRemaining / (float)slotsRemaining;
                    forcedKind = spawnBad ? FallingItemKind.Bad : FallingItemKind.Good;
                    if (spawnBad)
                    {
                        badItemsRemaining--;
                    }
                }

                SpawnItem(position, forcedKind);
            }
        }

        private void SpawnItem(Vector3 dropperPosition, FallingItemKind? forcedKind = null)
        {
            FallingItemKind kind = forcedKind ??
                (Random.value < badItemChance ? FallingItemKind.Bad : FallingItemKind.Good);
            bool isBad = kind == FallingItemKind.Bad;
            SpriteRenderer[] candidates = isBad ? badItemVisuals : goodItemVisuals;
            int visualIndex = Random.Range(0, candidates.Length);
            SpriteRenderer sourceVisual = candidates[visualIndex];
            Vector2 badFallingSize = isBad
                ? GetBadItemSize(badItemFallingSizes, visualIndex, fallingItemSize)
                : fallingItemSize;
            Vector2 badStackedSize = isBad
                ? GetBadItemSize(stackedBadItemSizes, visualIndex, stackedBadItemSize)
                : stackedBadItemSize;
            Vector3 spawnPosition = new(dropperPosition.x, dropperPosition.y - 0.65f, 0f);

            FallingItem item = GetPooledItem();
            GameObject itemObject = item.gameObject;
            itemObject.name = isBad ? "Bad Item" : "Good Item";
            itemObject.transform.SetParent(spawnedItemsRoot, false);
            itemObject.transform.position = spawnPosition;
            itemObject.transform.localRotation = sourceVisual.transform.localRotation;
            itemObject.transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = itemObject.GetComponent<SpriteRenderer>();
            CopyVisual(sourceVisual, spriteRenderer);
            if (isBad)
            {
                ApplySimpleSpriteSize(
                    spriteRenderer,
                    itemObject.transform,
                    badFallingSize);
            }
            else
            {
                ApplySimpleSpriteSize(
                    spriteRenderer,
                    itemObject.transform,
                    fallingItemSize);
            }

            item.Initialize(
                this,
                catcher,
                kind,
                Random.Range(minimumFallSpeed, maximumFallSpeed),
                isBad ? GetStackedBadItemSprite(visualIndex) : null,
                badStackedSize,
                isBad ? badFallingSize : fallingItemSize);
            itemObject.SetActive(true);
        }

        private static Vector2 GetBadItemSize(
            Vector2[] sizes,
            int visualIndex,
            Vector2 fallbackSize)
        {
            if (sizes != null &&
                visualIndex >= 0 && visualIndex < sizes.Length &&
                sizes[visualIndex].x > 0f && sizes[visualIndex].y > 0f)
            {
                return sizes[visualIndex];
            }

            return fallbackSize;
        }

        private Sprite GetStackedBadItemSprite(int visualIndex)
        {
            if (stackedBadItemSprites != null &&
                visualIndex >= 0 && visualIndex < stackedBadItemSprites.Length &&
                stackedBadItemSprites[visualIndex] != null)
            {
                return stackedBadItemSprites[visualIndex];
            }

            return stackedBadItemSprite;
        }

        private static void ApplySimpleSpriteSize(
            SpriteRenderer spriteRenderer,
            Transform itemTransform,
            Vector2 targetSize)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            Vector2 nativeSize = sprite.bounds.size;
            if (nativeSize.x <= 0f || nativeSize.y <= 0f ||
                targetSize.x <= 0f || targetSize.y <= 0f)
            {
                return;
            }

            float uniformScale = Mathf.Min(
                targetSize.x / nativeSize.x,
                targetSize.y / nativeSize.y);
            itemTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
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
            itemObject.transform.localRotation = Quaternion.identity;
            itemObject.transform.localScale = Vector3.one;

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
            if (isGood)
            {
                collectedGoodItemCount++;
            }
            else
            {
                collectedBadItemCount++;
            }

            score += difference;
            catchFeedback = difference > 0 ? $"+{difference}" : difference.ToString();
            feedbackVisibleUntil = Time.unscaledTime + 0.55f;
            ShowFeedback();
            UpdateScoreMessage();

            if (!jumpUnlocked && score >= jumpUnlockScore)
            {
                UnlockJump();
            }

            if (!sweepUnlocked && score >= sweepUnlockScore)
            {
                UnlockSweep();
            }
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

            scoreText.text = $"スコア  {score}";
            UpdateTimeText();
            UpdateScoreMessage();
        }

        private void ShowFeedback()
        {
            if (scoreText != null)
            {
                scoreText.text = $"スコア  {score}";
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
            scoreMessageText ??= hud.Find("Score Message Text")?.GetComponent<Text>();
            jumpUnlockText ??= hud.Find("Jump Unlock Text")?.GetComponent<Text>();
            sweepUnlockText ??= hud.Find("Sweep Unlock Text")?.GetComponent<Text>();
            startPromptText ??= hud.Find("Start Prompt Text")?.GetComponent<Text>();
            Transform resultRoot = hud.Find("Game Over UI");
            if (resultRoot != null)
            {
                gameOverPanel ??= resultRoot.gameObject;
                resultPortrait ??= resultRoot.Find("Game Over Portrait")?.GetComponent<Image>();
                resultScoreText ??= resultRoot.Find("Final Score Text")?.GetComponent<Text>();
                resultDialogueText ??= resultRoot.Find("Final Dialogue Text")?.GetComponent<Text>();
                resultCountsText ??= resultRoot.Find("Final Counts Text")?.GetComponent<Text>();
            }
        }

        private bool HasHudReferences()
        {
            return scoreText != null && feedbackText != null && timeText != null &&
                scoreMessageText != null && jumpUnlockText != null && sweepUnlockText != null &&
                startPromptText != null && gameOverPanel != null && resultPortrait != null &&
                resultScoreText != null && resultDialogueText != null && resultCountsText != null;
        }

        private void PrepareForStart()
        {
            score = 0;
            collectedGoodItemCount = 0;
            collectedBadItemCount = 0;
            jumpUnlocked = score >= jumpUnlockScore;
            sweepUnlocked = score >= sweepUnlockScore;
            nextSweepAvailableTime = 0f;
            remainingTime = gameDuration;
            phase = GamePhase.AwaitingInput;
            RefreshHud();

            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }

            if (jumpUnlockText != null)
            {
                jumpUnlockText.gameObject.SetActive(false);
            }

            if (sweepUnlockText != null)
            {
                sweepUnlockText.gameObject.SetActive(false);
            }

            if (startPromptText != null)
            {
                startPromptText.text = startPromptMessage;
                startPromptText.gameObject.SetActive(true);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        private void BeginCountdown()
        {
            phase = GamePhase.Countdown;
            countdownRemaining = 3f * countdownStepDuration;
            UpdateCountdownText();
        }

        private void UpdateCountdown()
        {
            countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.unscaledDeltaTime);
            if (countdownRemaining <= 0f)
            {
                StartRound();
                return;
            }

            UpdateCountdownText();
        }

        private void UpdateCountdownText()
        {
            if (startPromptText != null)
            {
                int number = Mathf.Clamp(
                    Mathf.CeilToInt(countdownRemaining / countdownStepDuration),
                    1,
                    3);
                startPromptText.text = number.ToString();
            }
        }

        private void StartRound()
        {
            remainingTime = gameDuration;
            phase = GamePhase.Playing;
            if (startPromptText != null)
            {
                startPromptText.gameObject.SetActive(false);
            }

            dropper?.BeginRound();
            RefreshHud();
        }

        private void EndRound()
        {
            remainingTime = 0f;
            phase = GamePhase.GameOver;
            ClearActiveItems();
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }

            UpdateTimeText();
            ShowGameOverPresentation();
        }

        private void ShowGameOverPresentation()
        {
            if (gameOverPanel == null)
            {
                return;
            }

            if (resultScoreText != null)
            {
                resultScoreText.text = $"あなたのスコア\n{score}";
            }

            if (resultDialogueText != null)
            {
                resultDialogueText.text = resultDialogue;
            }

            if (resultCountsText != null)
            {
                resultCountsText.text =
                    $"取得した黄色  {collectedGoodItemCount}個\n取得した赤     {collectedBadItemCount}個";
            }

            gameOverPanel.SetActive(true);
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
                timeText.text = $"残り  {Mathf.CeilToInt(remainingTime)}";
            }
        }

        private void UpdateScoreMessage()
        {
            if (scoreMessageText == null || scoreMessageTiers == null || scoreMessageTiers.Length == 0)
            {
                return;
            }

            string selectedMessage = string.Empty;
            int selectedThreshold = int.MinValue;
            foreach (ScoreMessageTier tier in scoreMessageTiers)
            {
                if (tier != null && score >= tier.minimumScore && tier.minimumScore >= selectedThreshold)
                {
                    selectedThreshold = tier.minimumScore;
                    selectedMessage = tier.message;
                }
            }

            scoreMessageText.text = selectedMessage;
        }

        private void UnlockJump()
        {
            jumpUnlocked = true;
            if (jumpUnlockText == null)
            {
                return;
            }

            jumpUnlockText.text = jumpUnlockMessage;
            jumpUnlockText.gameObject.SetActive(true);
            jumpUnlockVisibleUntil = Time.unscaledTime + jumpUnlockMessageDuration;
        }

        private void UnlockSweep()
        {
            sweepUnlocked = true;
            if (sweepUnlockText == null)
            {
                return;
            }

            sweepUnlockText.text = sweepUnlockMessage;
            sweepUnlockText.gameObject.SetActive(true);
            sweepUnlockVisibleUntil = Time.unscaledTime + sweepUnlockMessageDuration;
        }

        private void TryUseSweep()
        {
            float cooldownRemaining = nextSweepAvailableTime - Time.unscaledTime;
            if (cooldownRemaining > 0f)
            {
                ShowStatusFeedback($"一掃 再使用まで {Mathf.CeilToInt(cooldownRemaining)}秒");
                return;
            }

            int clearedCount = ClearStackedBadItems();
            if (clearedCount == 0)
            {
                ShowStatusFeedback("地面の赤オブジェクトなし");
                return;
            }

            score -= sweepScoreCost;
            nextSweepAvailableTime = Time.unscaledTime + sweepCooldown;
            catchFeedback = $"一掃！  -{sweepScoreCost}";
            feedbackVisibleUntil = Time.unscaledTime + 0.9f;
            ShowFeedback();
            UpdateScoreMessage();
        }

        private void ShowStatusFeedback(string message)
        {
            catchFeedback = message;
            feedbackVisibleUntil = Time.unscaledTime + 0.9f;
            ShowFeedback();
        }

        private int ClearStackedBadItems()
        {
            if (spawnedItemsRoot == null)
            {
                return 0;
            }

            int clearedCount = 0;
            FallingItem[] activeItems = spawnedItemsRoot.GetComponentsInChildren<FallingItem>(true);
            foreach (FallingItem item in activeItems)
            {
                if (!item.IsStackedBad)
                {
                    continue;
                }

                ReleaseItem(item);
                clearedCount++;
            }

            return clearedCount;
        }

        private static bool WasSweepPressed()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null &&
                (keyboard.leftShiftKey.wasPressedThisFrame ||
                 keyboard.rightShiftKey.wasPressedThisFrame))
            {
                return true;
            }

            return UnityEngine.InputSystem.Mouse.current?.rightButton.wasPressedThisFrame == true;
        }

        private static bool WasAnyStartButtonPressed()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true)
            {
                return true;
            }

            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null &&
                (mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame);
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
