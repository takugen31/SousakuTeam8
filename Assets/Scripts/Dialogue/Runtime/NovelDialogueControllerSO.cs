using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class NovelDialogueController : MonoBehaviour
{
    private static string pendingResumeLineId;
    private static bool pendingSceneFadeIn;
    private static readonly HashSet<string> revealedCharacterIds =
        new HashSet<string>(System.StringComparer.Ordinal);

    public event Action DialogueCompleted;
    public event Action<DialogueLine> LineStarted;
    public event Action<DialogueLine> SceneTransitionStarting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingResumeLine()
    {
        pendingResumeLineId = null;
        pendingSceneFadeIn = false;
        revealedCharacterIds.Clear();
    }

    public static void QueueResumeLine(string lineId)
    {
        QueueResumeLine(lineId, false);
    }

    public static void ResetStoryState()
    {
        ResetPendingResumeLine();
    }

    public static void QueueResumeLine(
        string lineId,
        bool fadeInAfterSceneLoad)
    {
        pendingResumeLineId =
            string.IsNullOrWhiteSpace(lineId)
                ? null
                : lineId.Trim();
        pendingSceneFadeIn = fadeInAfterSceneLoad;
    }

    public static void QueueResumeLineIfEmpty(
        string fallbackLineId,
        bool fadeInAfterSceneLoad)
    {
        if (string.IsNullOrWhiteSpace(pendingResumeLineId))
        {
            pendingResumeLineId = string.IsNullOrWhiteSpace(fallbackLineId)
                ? null
                : fallbackLineId.Trim();
        }

        pendingSceneFadeIn = fadeInAfterSceneLoad;
    }

    [Header("Dialogue Data")]
    [SerializeField]
    [Tooltip("最初に再生するシナリオです。通常はプロローグを登録します。")]
    private DialogueScenarioSO scenario;

    [SerializeField]
    [Tooltip(
        "プロローグの後に再生するシナリオを、" +
        "Chapter 1、Chapter 2の順に登録します。")]
    private List<DialogueScenarioSO> followingScenarios =
        new List<DialogueScenarioSO>();

    [SerializeField]
    private CharacterDatabaseSO characterDatabase;

    [SerializeField]
    [Tooltip("画面左側へ常時表示する主人公のcharacterIdです。")]
    private string protagonistCharacterId = "doute";

    [SerializeField]
    [Tooltip("名前がまだ公開されていないキャラクターの表示名です。")]
    private string unknownSpeakerName = "???";

    [SerializeField]
    private string startLineId;

    [Header("Affection")]
    [SerializeField]
    private AffectionManager affectionManager;

    [Header("Background")]
    [SerializeField]
    private Image backgroundImage;

    [Header("Chapter Transition")]
    [SerializeField]
    [Tooltip("チャプター切替時の暗転演出を有効にします。")]
    private bool useChapterTransitionFade = true;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転するまでの秒数です。")]
    private float chapterFadeOutDuration = 1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転したまま待機する秒数です。")]
    private float chapterFadeHoldDuration = 1.7f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("暗転から画面を表示するまでの秒数です。")]
    private float chapterFadeInDuration = 1f;

    [SerializeField]
    [Tooltip("チャプター切替時に画面を覆う色です。")]
    private Color chapterFadeColor = Color.black;

    [Header("Consultation Transition")]
    [SerializeField]
    [Tooltip("consultationTitleが設定されたセリフの直前に相談開始演出を再生します。")]
    private bool useConsultationTransition = true;

    [SerializeField, Min(0f)]
    private float consultationFadeOutDuration = 0.55f;

    [SerializeField, Min(0f)]
    private float consultationTitleHoldDuration = 1.25f;

    [SerializeField, Min(0f)]
    private float consultationFadeInDuration = 0.55f;

    [Header("Scene Transition")]
    [SerializeField]
    [Tooltip("別のUnity Sceneへ移動する際の暗転を有効にします。")]
    private bool useSceneTransitionFade = true;

    [SerializeField, Min(0f)]
    private float sceneFadeOutDuration = 1f;

    [SerializeField, Min(0f)]
    private float sceneFadeHoldDuration = 0.15f;

    [Header("UI")]
    [SerializeField]
    private GameObject dialogueRoot;

    [SerializeField]
    private GameObject namePlate;

    [SerializeField]
    private TMP_Text speakerNameText;

    [SerializeField]
    private TMP_Text bodyText;

    [SerializeField, Tooltip("名前付きのセリフは、表示時だけ外側の「」を省略します。原文データは変更しません。")]
    private bool omitOuterDialogueQuotes;

    [SerializeField] private bool normalizeBustPortraitSizes;
    [SerializeField] private bool mirrorDoutePortrait;
    [SerializeField, Min(0.01f)] private float kayoPortraitScale = 1.0f;
    [SerializeField] private float kayoPortraitUpwardOffset = 0.09f;
    private bool rightPortraitBaseCaptured;
    private Vector2 rightPortraitBasePosition;
    private Vector3 rightPortraitBaseScale;

    [SerializeField]
    [FormerlySerializedAs("portraitImage")]
    private Image leftPortraitImage;

    [SerializeField]
    private Image rightPortraitImage;

    [Header("Playback Controls")]
    [SerializeField]
    private RectTransform playbackControlsRoot;

    [SerializeField]
    private Button autoPlayButton;

    [SerializeField]
    private TMP_Text autoPlayButtonText;

    [SerializeField]
    private Button skipChapterButton;

    [SerializeField]
    private GameObject skipConfirmationRoot;

    [SerializeField]
    private Button confirmSkipButton;

    [SerializeField]
    private Button cancelSkipButton;

    [Header("Choices")]
    [SerializeField]
    private RectTransform choiceOptionsRoot;

    [SerializeField]
    private Button choiceButtonTemplate;

    [SerializeField]
    [Min(1f)]
    private float choiceButtonHeight = 64f;

    [SerializeField]
    [Min(0f)]
    private float choiceButtonSpacing = 12f;

    [Header("Auto Play")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("タイプライター表示完了後、次のセリフへ進むまでの秒数です。")]
    private float autoAdvanceDelaySeconds = 0.5f;

    [SerializeField]
    private bool autoPlayOnStart;

    [Header("Text Animation")]
    [SerializeField]
    [Min(1f)]
    private float charactersPerSecond = 40f;

    [SerializeField]
    private bool playOnStart = true;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onDialogueCompleted;

    private DialogueLine currentLine;
    private DialogueScenarioSO currentScenario;
    private Coroutine chapterTransitionCoroutine;
    private Coroutine choiceActivationCoroutine;
    private Coroutine typingCoroutine;
    private Image chapterTransitionOverlay;
    private TMP_Text consultationTransitionTitle;

    private readonly List<Button> spawnedChoiceButtons =
        new List<Button>();

    private readonly HashSet<string> knownCharacterIds =
        new HashSet<string>(System.StringComparer.Ordinal);

    private readonly HashSet<string> shownConsultationLineIds =
        new HashSet<string>(System.StringComparer.Ordinal);

    private int currentScenarioIndex = -1;
    private float autoAdvanceAt = -1f;
    private float autoAdvanceRemainingBeforeConfirmation = -1f;
    private float timeScaleBeforeConfirmation = 1f;
    private bool autoPlayEnabled;
    private bool isChapterTransitioning;
    private bool isChoiceSelectionOpen;
    private bool isPlaying;
    private bool isSceneLoading;
    private bool isSkipConfirmationOpen;
    private bool isTyping;
    private bool isRoomLookPlaying;
    private Coroutine roomLookCoroutine;
    private GameObject roomLookViewport;
    private CanvasGroup roomLookPresentation;
    private float roomLookOriginalAlpha;
    private bool roomLookOriginalInteractable;
    private bool roomLookOriginalBlocksRaycasts;
    private bool awakeningRoomLookShown;
    private bool moteruEntranceLookShown;
    private readonly HashSet<string> shownIntroductionLookIds = new HashSet<string>(StringComparer.Ordinal);
    private RectTransform[] shakingWindowRects;
    private Vector2[] shakingWindowPositions;
    private Quaternion[] shakingWindowRotations;
    private float windowShakeElapsed;
    private bool useSearchPortraitSlide;
    private bool isSearchPortraitSliding;
    private float searchPortraitSlideElapsed;
    private float searchPortraitHeadMidpoint;
    private SearchPortraitPose searchPortraitOriginalPose;
    private Image searchSlideRoomBackground;
    private Image searchParallaxBackground;
    private Color searchDouteOriginalColor;
    private Vector2 searchDouteOriginalPosition;
    private Color searchSpeakerOriginalColor;
    private int searchPortraitOriginalSibling;
    private bool isGatheringCompositionActive;
    private SearchPortraitPose gatheringLeftPose, gatheringRightPose;
    private Image gatheringLeftBackground, gatheringRightBackground;
    private Image gatheringDivider;
    private RectTransform gatheringLeftFrame, gatheringRightFrame;
    private Transform gatheringLeftParent, gatheringRightParent;
    private int gatheringLeftSibling, gatheringRightSibling;
    private string gatheringAnimationTarget;
    private float gatheringDouteSlideElapsed;
    private float kayoFallProgress, kayoFallFrom, kayoFallTo, kayoFallElapsed, kayoFallDuration;
    private bool kayoFallMoving, kayoScreamTriggered;
    private GameObject yowashiBandageView;
    private bool isYowashiBandageViewActive;
    private bool bandageLeftWasEnabled, bandageRightWasEnabled;
    private GameObject moteruRevealView;
    private bool isMoteruRevealViewActive;
    private bool revealLeftWasEnabled, revealRightWasEnabled;

    private struct SearchPortraitPose
    {
        public Vector2 anchorMin, anchorMax, pivot, size, position;
        public Vector3 scale;
        public Quaternion rotation;
    }
    private string stopAfterLineId;

    public string CurrentSpeakerId => currentLine?.speakerId;
    // Opt-in for an embedded scene whose owner fades out the last dialogue frame.
    public bool KeepPresentationOnCompletion { get; set; }
    public bool CanAdvanceCurrentPage => isActiveAndEnabled && isPlaying &&
        currentLine != null && !isTyping && !currentLine.HasChoices &&
        !isChoiceSelectionOpen && !isChapterTransitioning && !isSceneLoading &&
        !isSkipConfirmationOpen && !ArchiveManager.IsOpen && !isRoomLookPlaying;

    private void OnEnable()
    {
        BindSkipConfirmationButtons();
    }

    private void BindSkipConfirmationButtons()
    {
        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.RemoveListener(ConfirmSkip);
            confirmSkipButton.onClick.AddListener(ConfirmSkip);
        }
        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.RemoveListener(CancelSkip);
            cancelSkipButton.onClick.AddListener(CancelSkip);
        }
    }

    private void ConfigureSkipConfirmationUI()
    {
        if (skipConfirmationRoot == null) return;
        Image backdrop = skipConfirmationRoot.GetComponent<Image>();
        if (backdrop != null) backdrop.color = new Color(0f, 0f, 0f, 0.45f);
        if (skipConfirmationRoot.transform.childCount > 0)
        {
            GameObject panel = skipConfirmationRoot.transform.GetChild(0).gameObject;
            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0f, 0f, 0f, 0.90f);
                if (panel.GetComponent<DialogueWindowFeather>() == null) panel.AddComponent<DialogueWindowFeather>();
            }
        }
        foreach (TMP_Text text in skipConfirmationRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = Color.white;
            text.raycastTarget = false;
            if (text.gameObject.name == "ConfirmationMessage")
            {
                text.text = "現在の章をスキップします、\n本当によろしいですか？";
                text.fontSize = 32f;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.Center;
            }
        }
        foreach (Button button in new[] { confirmSkipButton, cancelSkipButton })
        {
            if (button == null) continue;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                button.targetGraphic = image;
            }
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            colors.pressedColor = new Color(0.03f, 0.03f, 0.03f, 1f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            if (button.targetGraphic != null)
                button.targetGraphic.CrossFadeColor(colors.normalColor, 0f, true, true);
            if (button.GetComponent<DialogueWindowFeather>() == null)
                button.gameObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(24f, 10f));
            if (button.GetComponent<ChoiceButtonHoverScale>() == null)
                button.gameObject.AddComponent<ChoiceButtonHoverScale>();
        }
    }

    private void Awake()
    {
        // Playback controls now live in the Esc menu.
        if (autoPlayButton != null) autoPlayButton.gameObject.SetActive(false);
        if (skipChapterButton != null) skipChapterButton.gameObject.SetActive(false);
        if (autoPlayButton != null)
        {
            autoPlayButton.onClick.AddListener(ToggleAutoPlay);
        }

        if (skipChapterButton != null)
        {
            skipChapterButton.onClick.AddListener(ShowSkipConfirmation);
        }

        BindSkipConfirmationButtons();
        ConfigureSkipConfirmationUI();

        if (skipConfirmationRoot != null)
        {
            skipConfirmationRoot.SetActive(false);
        }

        if (choiceButtonTemplate != null)
        {
            choiceButtonTemplate.gameObject.SetActive(false);
        }

        if (choiceOptionsRoot != null)
        {
            choiceOptionsRoot.gameObject.SetActive(false);
        }

        autoPlayEnabled = autoPlayOnStart;
        UpdateAutoPlayButton();
    }

    private void Update()
    {
        UpdateGatheringDouteSlide();
        UpdateKayoFall();
        UpdateSearchPortraitSlide();
        UpdateWindowShake();
        if (isRoomLookPlaying || ArchiveManager.IsOpen ||
            isChapterTransitioning ||
            isSceneLoading ||
            isSkipConfirmationOpen ||
            isChoiceSelectionOpen)
        {
            return;
        }

        if (ShouldAutoAdvance())
        {
            autoAdvanceAt = -1f;
            Advance();
            return;
        }

        if (WasDialogueAdvancePressed())
        {
            Advance();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartDialogue();
        }
    }

    public void StartDialogue()
    {
        stopAfterLineId = null;
        bool shouldFadeIn = pendingSceneFadeIn;
        pendingSceneFadeIn = false;

        if (!TryStartPendingResumeLine())
        {
            StartDialogueInternal();
        }

        if (shouldFadeIn && isPlaying)
        {
            BeginSceneEntryFadeIn();
        }
    }

    // Read-only preview for sceneLoaded: resolve resume music before Start or intro animation.
    public bool TryGetSceneEntryMusic(out DialogueScenarioSO entryScenario, out DialogueLine entryLine)
    {
        entryScenario = null;
        entryLine = null;
        if (!playOnStart || !isActiveAndEnabled || scenario == null) return false;
        if (!string.IsNullOrWhiteSpace(pendingResumeLineId))
        {
            int count = 1 + (followingScenarios?.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                DialogueScenarioSO candidate = i == 0 ? scenario : followingScenarios[i - 1];
                if (candidate != null && candidate.TryGetLine(pendingResumeLineId, out entryLine))
                {
                    entryScenario = candidate;
                    return true;
                }
            }
        }
        entryScenario = scenario;
        if (!string.IsNullOrWhiteSpace(startLineId) && scenario.TryGetLine(startLineId, out entryLine)) return true;
        return scenario.TryGetFirstLine(out entryLine);
    }

    private bool TryStartPendingResumeLine()
    {
        string resumeLineId = pendingResumeLineId;
        pendingResumeLineId = null;

        if (string.IsNullOrWhiteSpace(resumeLineId))
        {
            return false;
        }

        if (characterDatabase == null)
        {
            Debug.LogError("CharacterDatabaseが設定されていません。");
            return false;
        }

        int scenarioCount = 1 + (followingScenarios?.Count ?? 0);

        for (int index = 0; index < scenarioCount; index++)
        {
            DialogueScenarioSO candidate =
                index == 0 ? scenario : followingScenarios[index - 1];

            if (candidate == null ||
                !candidate.TryGetLine(resumeLineId, out DialogueLine resumeLine))
            {
                continue;
            }

            CancelChapterTransition();
            HideChoices();
            ResetKnownCharacterNames();
            shownConsultationLineIds.Clear();
            currentScenario = candidate;
            currentScenarioIndex = index;
            isPlaying = true;

            if (dialogueRoot != null)
            {
                dialogueRoot.SetActive(true);
            }

            ResetBackgroundForScenario();
            ResetPortraitsForScenario();
            ShowLine(resumeLine);
            return true;
        }

        Debug.LogWarning(
            $"再開セリフ「{resumeLineId}」が登録シナリオ内に存在しません。" +
            "通常の開始位置から再生します。",
            this);
        return false;
    }

    private void StartDialogueInternal()
    {
        CancelChapterTransition();
        awakeningRoomLookShown = false;
        moteruEntranceLookShown = false;
        shownIntroductionLookIds.Clear();
        gatheringAnimationTarget = null;
        gatheringDouteSlideElapsed = 0f;
        HideChoices();

        if (scenario == null)
        {
            Debug.LogError("DialogueScenarioが設定されていません。");
            return;
        }

        if (characterDatabase == null)
        {
            Debug.LogError("CharacterDatabaseが設定されていません。");
            return;
        }

        ResetKnownCharacterNames();
        shownConsultationLineIds.Clear();

        currentScenario = scenario;
        currentScenarioIndex = 0;

        DialogueLine firstLine;

        if (!string.IsNullOrWhiteSpace(startLineId))
        {
            if (!currentScenario.TryGetLine(startLineId, out firstLine))
            {
                Debug.LogError(
                    $"開始セリフ「{startLineId}」が存在しません。");

                currentScenario = null;
                currentScenarioIndex = -1;
                return;
            }
        }
        else
        {
            if (!currentScenario.TryGetFirstLine(out firstLine))
            {
                Debug.LogError("シナリオにセリフがありません。");

                currentScenario = null;
                currentScenarioIndex = -1;
                return;
            }
        }

        isPlaying = true;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(true);
        }

        ResetBackgroundForScenario();
        ResetPortraitsForScenario();
        ShowLine(firstLine);
    }

    public void StartDialogueAt(string lineId)
    {
        startLineId = lineId;
        stopAfterLineId = null;
        StartDialogueInternal();
    }

    public void ConfigureEmbeddedDialogue(
        DialogueScenarioSO scenarioData,
        CharacterDatabaseSO database,
        GameObject root,
        GameObject speakerPlate,
        TMP_Text speakerText,
        TMP_Text dialogueText,
        Image leftPortrait,
        Image rightPortrait,
        Image dialogueBackground = null)
    {
        scenario = scenarioData;
        followingScenarios.Clear();
        characterDatabase = database;
        backgroundImage = dialogueBackground;
        dialogueRoot = root;
        namePlate = speakerPlate;
        speakerNameText = speakerText;
        bodyText = dialogueText;
        omitOuterDialogueQuotes = true;
        normalizeBustPortraitSizes = true;
        mirrorDoutePortrait = true;
        leftPortraitImage = leftPortrait;
        rightPortraitImage = rightPortrait;
        playbackControlsRoot = null;
        autoPlayOnStart = false;
        playOnStart = false;
    }

    public void ConfigureScenarioSequence(
        IReadOnlyList<DialogueScenarioSO> scenarios)
    {
        scenario = null;
        followingScenarios.Clear();

        if (scenarios == null || scenarios.Count == 0)
        {
            return;
        }

        scenario = scenarios[0];

        for (int index = 1; index < scenarios.Count; index++)
        {
            if (scenarios[index] != null)
            {
                followingScenarios.Add(scenarios[index]);
            }
        }
    }

    // Opt in only from the chapter-one exploration scene, not the normal novel scenes.
    public void ConfigureSearchPortraitSlide(Image roomBackground)
    {
        useSearchPortraitSlide = true;
        searchSlideRoomBackground = roomBackground;
    }

    // Replace only an upcoming chapter; the current dialogue and playback settings stay intact.
    public bool ReplaceFollowingScenario(
        DialogueScenarioSO original,
        DialogueScenarioSO replacement)
    {
        if (original == null || replacement == null)
        {
            return false;
        }

        int index = followingScenarios.IndexOf(original);
        if (index < 0)
        {
            return false;
        }

        followingScenarios[index] = replacement;
        return true;
    }

    public void StartEmbeddedDialogue()
    {
        stopAfterLineId = null;
        StartDialogueInternal();
    }

    public void PlayDialogueRange(
        DialogueScenarioSO scenarioData,
        string firstLineId,
        string lastLineId)
    {
        scenario = scenarioData;
        followingScenarios.Clear();
        startLineId = firstLineId;
        stopAfterLineId = lastLineId;
        StartDialogueInternal();
    }

    public void Advance()
    {
        if (isRoomLookPlaying || !isPlaying ||
            currentLine == null ||
            currentScenario == null ||
            isChapterTransitioning ||
            isSceneLoading ||
            isChoiceSelectionOpen)
        {
            return;
        }

        // タイプ表示中の入力では、現在のセリフを全文表示する
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        if (!string.IsNullOrWhiteSpace(stopAfterLineId) &&
            string.Equals(
                currentLine.lineId,
                stopAfterLineId,
                StringComparison.Ordinal))
        {
            EndDialogue();
            return;
        }

        if (currentLine.HasSceneTransition)
        {
            LoadDialogueScene(currentLine.nextScenePath, currentLine);
            return;
        }

        if (!TryResolveNextLine(
                out DialogueLine nextLine,
                out bool reachedScenarioEnd))
        {
            if (!reachedScenarioEnd || !TryStartNextScenario())
            {
                EndDialogue();
            }

            return;
        }

        ShowLine(nextLine);
    }

    private bool TryResolveNextLine(
        out DialogueLine nextLine,
        out bool reachedScenarioEnd)
    {
        nextLine = null;
        reachedScenarioEnd = false;

        AffectionManager affection = ResolveAffectionManager();

        // 1. 好感度に応じた分岐（先頭から順に評価）
        if (currentLine.branches != null &&
            currentLine.branches.Count > 0)
        {
            if (affection == null)
            {
                Debug.LogWarning(
                    $"セリフ「{currentLine.lineId}」に分岐がありますが、" +
                    "AffectionManagerが見つかりません。分岐をスキップします。");
            }
            else
            {
                foreach (DialogueBranch branch in currentLine.branches)
                {
                    if (branch == null)
                    {
                        continue;
                    }

                    if (!affection.EvaluateAll(branch.conditions))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(branch.nextLineId))
                    {
                        continue;
                    }

                    if (!currentScenario.TryGetLine(
                            branch.nextLineId,
                            out nextLine))
                    {
                        Debug.LogError(
                            $"分岐先のセリフ「{branch.nextLineId}」が" +
                            "存在しません。");
                        return false;
                    }

                    affection.ApplyDeltas(branch.affectionChanges);
                    return true;
                }
            }
        }

        // 2. nextLineIdが設定されていればそこへ移動
        if (!string.IsNullOrWhiteSpace(currentLine.nextLineId))
        {
            if (!currentScenario.TryGetLine(
                    currentLine.nextLineId,
                    out nextLine))
            {
                Debug.LogError(
                    $"次のセリフ「{currentLine.nextLineId}」が" +
                    "存在しません。");
                return false;
            }

            return true;
        }

        // 3. nextLineIdが空ならCSV上の次の行へ進む
        if (currentScenario.TryGetNextLine(
                currentLine.lineId,
                out nextLine))
        {
            return true;
        }

        reachedScenarioEnd = true;
        return false;
    }

    private AffectionManager ResolveAffectionManager()
    {
        if (affectionManager != null)
        {
            return affectionManager;
        }

        return AffectionManager.Instance;
    }

    public bool IsDialoguePlaying => isActiveAndEnabled && isPlaying && !isSceneLoading;
    public bool IsAutoPlayEnabled => autoPlayEnabled;
    public RectTransform ChoiceOptionsRoot => choiceOptionsRoot;
    public string CurrentOperationHelp => isChoiceSelectionOpen
        ? "選択肢を選ぶ：左クリック"
        : isSkipConfirmationOpen
            ? "確認・戻るを選ぶ：左クリック"
            : "会話を送る：左クリック\n文字を全て表示：左クリック";
    public bool CanSkipFromMenu => IsDialoguePlaying && currentScenario != null &&
        !isRoomLookPlaying && !isChapterTransitioning && !isSkipConfirmationOpen && skipConfirmationRoot != null;

    public void ToggleAutoPlay()
    {
        autoPlayEnabled = !autoPlayEnabled;
        UpdateAutoPlayButton();

        if (autoPlayEnabled &&
            isPlaying &&
            currentLine != null &&
            !isTyping &&
            !isChoiceSelectionOpen)
        {
            ScheduleAutoAdvance();
        }
        else if (!autoPlayEnabled)
        {
            autoAdvanceAt = -1f;
        }
    }

    public void ShowSkipConfirmation()
    {
        if (isRoomLookPlaying || !isPlaying ||
            currentScenario == null ||
            isChapterTransitioning ||
            isSceneLoading ||
            isSkipConfirmationOpen)
        {
            return;
        }

        if (skipConfirmationRoot == null)
        {
            Debug.LogError(
                "スキップ確認画面が設定されていません。");
            return;
        }

        autoAdvanceRemainingBeforeConfirmation =
            autoAdvanceAt >= 0f
                ? Mathf.Max(0f, autoAdvanceAt - Time.unscaledTime)
                : -1f;
        autoAdvanceAt = -1f;
        timeScaleBeforeConfirmation = Time.timeScale;
        isSkipConfirmationOpen = true;
        BindSkipConfirmationButtons();
        ConfigureSkipConfirmationUI();
        skipConfirmationRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ConfirmSkip()
    {
        if (!isSkipConfirmationOpen)
        {
            return;
        }

        CloseSkipConfirmation(false);
        SkipToNextScenario();
    }

    public void CancelSkip()
    {
        if (!isSkipConfirmationOpen && (skipConfirmationRoot == null || !skipConfirmationRoot.activeSelf))
        {
            return;
        }

        CloseSkipConfirmation(true);
    }

    public void SkipToNextScenario()
    {
        if (isRoomLookPlaying || !isPlaying ||
            currentScenario == null ||
            isChapterTransitioning ||
            isSceneLoading)
        {
            return;
        }

        if (isSkipConfirmationOpen)
        {
            CloseSkipConfirmation(false);
        }

        autoAdvanceAt = -1f;

        if (TryLoadUpcomingSceneTransition())
        {
            return;
        }

        if (!TryStartNextScenario())
        {
            EndDialogue();
        }
    }

    private void OnDestroy()
    {
        CancelChapterTransition();

        if (autoPlayButton != null)
        {
            autoPlayButton.onClick.RemoveListener(ToggleAutoPlay);
        }

        if (skipChapterButton != null)
        {
            skipChapterButton.onClick.RemoveListener(ShowSkipConfirmation);
        }

        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.RemoveListener(ConfirmSkip);
        }

        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.RemoveListener(CancelSkip);
        }

        if (isSkipConfirmationOpen)
        {
            Time.timeScale = timeScaleBeforeConfirmation;
        }
    }

    private bool TryStartNextScenario()
    {
        if (!TryFindNextScenario(
                out DialogueScenarioSO nextScenario,
                out DialogueLine firstLine,
                out int nextScenarioIndex))
        {
            return false;
        }

        StopTypingAndRevealText();
        HideChoices();
        autoAdvanceAt = -1f;

        if (!useChapterTransitionFade)
        {
            StartScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex);
            return true;
        }

        Image overlay = EnsureChapterTransitionOverlay();

        if (overlay == null)
        {
            Debug.LogWarning(
                "チャプター切替用のCanvasが見つからないため、" +
                "フェードせずに次のチャプターを開始します。");

            StartScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex);
            return true;
        }

        isChapterTransitioning = true;
        overlay.transform.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        SetChapterTransitionAlpha(0f);

        chapterTransitionCoroutine = StartCoroutine(
            TransitionToScenario(
                nextScenario,
                firstLine,
                nextScenarioIndex));

        return true;
    }

    private bool TryFindNextScenario(
        out DialogueScenarioSO nextScenario,
        out DialogueLine firstLine,
        out int nextScenarioIndex)
    {
        nextScenario = null;
        firstLine = null;
        nextScenarioIndex = -1;

        int scenarioCount = 1 +
            (followingScenarios?.Count ?? 0);

        for (int nextIndex = currentScenarioIndex + 1;
             nextIndex < scenarioCount;
             nextIndex++)
        {
            DialogueScenarioSO candidateScenario =
                followingScenarios[nextIndex - 1];

            if (candidateScenario == null)
            {
                Debug.LogWarning(
                    $"Dialogueの{nextIndex + 1}番目のシナリオが" +
                    "設定されていないためスキップします。");
                continue;
            }

            if (!candidateScenario.TryGetFirstLine(
                    out DialogueLine candidateFirstLine))
            {
                Debug.LogWarning(
                    $"シナリオ「{candidateScenario.name}」にセリフがないため" +
                    "スキップします。");
                continue;
            }

            nextScenario = candidateScenario;
            firstLine = candidateFirstLine;
            nextScenarioIndex = nextIndex;
            return true;
        }

        return false;
    }

    private IEnumerator TransitionToScenario(
        DialogueScenarioSO nextScenario,
        DialogueLine firstLine,
        int nextScenarioIndex)
    {
        yield return FadeChapterTransition(
            0f,
            1f,
            chapterFadeOutDuration);

        StartScenario(
            nextScenario,
            firstLine,
            nextScenarioIndex);

        if (chapterFadeHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                chapterFadeHoldDuration);
        }

        yield return FadeChapterTransition(
            1f,
            0f,
            chapterFadeInDuration);

        if (chapterTransitionOverlay != null)
        {
            chapterTransitionOverlay.gameObject.SetActive(false);
        }

        isChapterTransitioning = false;
        chapterTransitionCoroutine = null;
    }

    private void BeginSceneEntryFadeIn()
    {
        Image overlay = EnsureChapterTransitionOverlay();
        if (overlay == null)
        {
            return;
        }

        isChapterTransitioning = true;
        overlay.transform.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        SetChapterTransitionAlpha(1f);
        chapterTransitionCoroutine = StartCoroutine(FadeInAtSceneEntry());
    }

    private IEnumerator FadeInAtSceneEntry()
    {
        yield return FadeChapterTransition(1f, 0f, chapterFadeInDuration);

        if (chapterTransitionOverlay != null)
        {
            chapterTransitionOverlay.gameObject.SetActive(false);
        }

        isChapterTransitioning = false;
        chapterTransitionCoroutine = null;
    }

    private void StartScenario(
        DialogueScenarioSO nextScenario,
        DialogueLine firstLine,
        int nextScenarioIndex)
    {
        currentScenario = nextScenario;
        currentScenarioIndex = nextScenarioIndex;
        ResetBackgroundForScenario();
        ResetPortraitsForScenario();
        ShowLine(firstLine);
    }

    private IEnumerator FadeChapterTransition(
        float startAlpha,
        float endAlpha,
        float duration)
    {
        SetChapterTransitionAlpha(startAlpha);

        if (duration <= 0f)
        {
            SetChapterTransitionAlpha(endAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            SetChapterTransitionAlpha(
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    Mathf.Clamp01(elapsed / duration)));

            yield return null;
        }

        SetChapterTransitionAlpha(endAlpha);
    }

    private Image EnsureChapterTransitionOverlay()
    {
        if (chapterTransitionOverlay != null)
        {
            return chapterTransitionOverlay;
        }

        Canvas canvas =
            backgroundImage != null
                ? backgroundImage.canvas
                : GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return null;
        }

        GameObject overlayObject = new GameObject(
            "ChapterTransitionFadeOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform overlayRect =
            overlayObject.GetComponent<RectTransform>();

        overlayRect.SetParent(canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        chapterTransitionOverlay =
            overlayObject.GetComponent<Image>();
        chapterTransitionOverlay.raycastTarget = true;

        SetChapterTransitionAlpha(0f);
        overlayObject.SetActive(false);

        return chapterTransitionOverlay;
    }

    private void SetChapterTransitionAlpha(float alpha)
    {
        if (chapterTransitionOverlay == null)
        {
            return;
        }

        Color color = chapterFadeColor;
        color.a *= Mathf.Clamp01(alpha);
        chapterTransitionOverlay.color = color;
    }

    private void CancelChapterTransition()
    {
        StopMoteruRevealView();
        StopYowashiBandageView();
        StopGatheringComposition();
        StopSearchPortraitSlide();
        StopWindowShake();
        ResetKayoFall();
        CancelRoomLook();
        if (chapterTransitionCoroutine != null)
        {
            StopCoroutine(chapterTransitionCoroutine);
            chapterTransitionCoroutine = null;
        }

        isChapterTransitioning = false;
        SetConsultationTransitionTitle(null, false);

        if (chapterTransitionOverlay == null)
        {
            return;
        }

        SetChapterTransitionAlpha(0f);
        chapterTransitionOverlay.gameObject.SetActive(false);
    }

    private void ShowLine(DialogueLine line)
    {
        if (TryStartConsultationTransition(line))
        {
            return;
        }

        ShowLineImmediately(line);
    }

    private void ShowLineImmediately(DialogueLine line)
    {
        // Music belongs to the incoming scene/segment, not to the end of its entrance fade.
        StoryMusicController.ApplyDialogue(gameObject.scene, currentScenario, line);
        bool changeBandageView = IsYowashiBandageLine(line?.lineId) != isYowashiBandageViewActive;
        bool changeRevealView = IsMoteruRevealLine(line?.lineId) != isMoteruRevealViewActive;
        if (changeBandageView || changeRevealView || (useSearchPortraitSlide && line != null && (line.speakerId == "kayo" || line.speakerId == "yowashi")))
        {
            roomLookCoroutine = StartCoroutine(FadeToDialogueComposition(line));
            return;
        }
        PrepareLine(line);
        if (!moteruEntranceLookShown && line.lineId == "chapter1_001" && line.speakerId == "moteru" &&
            rightPortraitImage != null && rightPortraitImage.sprite != null && dialogueRoot != null &&
            backgroundImage != null && backgroundImage.GetComponentInParent<Canvas>() != null)
        {
            moteruEntranceLookShown = true;
            roomLookCoroutine = StartCoroutine(PlayCharacterEntranceLook(line));
            return;
        }
        if (((line.lineId == "chapter2_kayo_001" && line.speakerId == "kayo") ||
             (line.lineId == "chapter2_yowashi_001" && line.speakerId == "yowashi")) &&
            rightPortraitImage != null && rightPortraitImage.sprite != null && dialogueRoot != null &&
            backgroundImage != null && backgroundImage.sprite != null &&
            backgroundImage.GetComponentInParent<Canvas>() != null && shownIntroductionLookIds.Add(line.lineId))
        {
            roomLookCoroutine = StartCoroutine(PlayCharacterEntranceLook(line));
            return;
        }
        // The waking scene alone gets this introduction; dialogue data is unchanged.
        if (!awakeningRoomLookShown && line.lineId == "prologue2_001" &&
            backgroundImage != null && backgroundImage.sprite != null && dialogueRoot != null &&
            backgroundImage.GetComponentInParent<Canvas>() != null)
        {
            awakeningRoomLookShown = true;
            roomLookCoroutine = StartCoroutine(PlayAwakeningRoomLook(line));
            return;
        }
        StartTyping(GetDialogueDisplayText(line, omitOuterDialogueQuotes));
    }

    private IEnumerator FadeToDialogueComposition(DialogueLine line)
    {
        // Do not apply the incoming speaker before black: that would flash its normal portrait.
        StopTypingAndRevealText();
        autoAdvanceAt = -1f;
        isRoomLookPlaying = true;
        Image overlay = EnsureChapterTransitionOverlay();
        if (overlay != null)
        {
            overlay.transform.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            SetChapterTransitionAlpha(0f);
            yield return FadeLookReturn(0f, 1f);
        }
        PrepareLine(line);
        if (bodyText != null) { bodyText.text = string.Empty; bodyText.maxVisibleCharacters = 0; }
        // Render at least one fully black frame with the close-up already prepared underneath.
        yield return null;
        while (ArchiveManager.IsOpen) yield return null;
        if (overlay != null)
        {
            yield return FadeLookReturn(1f, 0f);
            overlay.gameObject.SetActive(false);
        }
        isRoomLookPlaying = false;
        roomLookCoroutine = null;
        if (isPlaying && currentLine == line) StartTyping(GetDialogueDisplayText(line, omitOuterDialogueQuotes));
    }

    private IEnumerator PlayAwakeningRoomLook(DialogueLine line)
    {
        HideLookPresentation();

        // Animate a clipped copy, leaving the normal background's transform untouched.
        Canvas canvas = backgroundImage.GetComponentInParent<Canvas>();
        roomLookViewport = new GameObject("AwakeningRoomLook", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = roomLookViewport.GetComponent<RectTransform>();
        viewport.SetParent(canvas.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        Image first = CreateRoomLookImage(viewport, "FirstView");
        Image second = CreateRoomLookImage(viewport, "SecondView");
        SetRoomLookPose(first, viewport.rect.size, new Vector2(0.38f, 0.32f), 1.5f);
        SetRoomLookPose(second, viewport.rect.size, new Vector2(0.64f, 0.67f), 1.5f);
        second.color = new Color(1f, 1f, 1f, 0f);
        // Stay beneath the existing chapter fade, so no portraits flash during waking.
        if (chapterTransitionOverlay != null) chapterTransitionOverlay.transform.SetAsLastSibling();
        while (isChapterTransitioning || ArchiveManager.IsOpen) yield return null;

        // Reference: 0.1 s samples; two slow close-up tilts joined by a brief dissolve.
        float elapsed = 0f;
        const float firstShot = 3.6f, dissolve = 0.35f, secondShot = 2.3f;
        float duration = firstShot + dissolve + secondShot;
        while (elapsed < duration)
        {
            if (ArchiveManager.IsOpen) { yield return null; continue; }
            elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
            float firstProgress = Mathf.SmoothStep(0f, 1f, elapsed / firstShot);
            SetRoomLookPose(first, viewport.rect.size,
                Vector2.Lerp(new Vector2(0.38f, 0.32f), new Vector2(0.43f, 0.40f), firstProgress), 1.5f);
            float blend = Mathf.SmoothStep(0f, 1f, (elapsed - firstShot) / dissolve);
            second.color = new Color(1f, 1f, 1f, blend);
            float secondProgress = Mathf.SmoothStep(0f, 1f, (elapsed - firstShot) / (dissolve + secondShot));
            Vector2 center = Vector2.Lerp(new Vector2(0.64f, 0.67f), new Vector2(0.56f, 0.60f), secondProgress);
            SetRoomLookPose(second, viewport.rect.size, center, 1.5f);
            yield return null;
        }
        yield return ReturnLookToDialogue(line);
    }

    private void HideLookPresentation()
    {
        isRoomLookPlaying = true;
        autoAdvanceAt = -1f;
        if (bodyText != null) { bodyText.text = string.Empty; bodyText.maxVisibleCharacters = 0; }
        roomLookPresentation = dialogueRoot.GetComponent<CanvasGroup>();
        if (roomLookPresentation == null) roomLookPresentation = dialogueRoot.AddComponent<CanvasGroup>();
        roomLookOriginalAlpha = roomLookPresentation.alpha;
        roomLookOriginalInteractable = roomLookPresentation.interactable;
        roomLookOriginalBlocksRaycasts = roomLookPresentation.blocksRaycasts;
        roomLookPresentation.alpha = 0f;
        roomLookPresentation.interactable = false;
        roomLookPresentation.blocksRaycasts = false;
    }

    private IEnumerator PlayCharacterEntranceLook(DialogueLine line)
    {
        HideLookPresentation();
        Canvas canvas = backgroundImage.GetComponentInParent<Canvas>();
        roomLookViewport = new GameObject(line.speakerId + "EntranceLook", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = roomLookViewport.GetComponent<RectTransform>();
        viewport.SetParent(canvas.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        Image room = CreateRoomLookImage(viewport, "RoomBehindCharacter");
        Image portrait = CreateRoomLookImage(viewport, "CharacterCloseUp");
        portrait.sprite = rightPortraitImage.sprite;
        float startCenter = line.speakerId == "moteru" ? 0.27f : 0.50f;
        float faceCenter = line.speakerId == "kayo" ? 0.84f : line.speakerId == "yowashi" ? 0.89f : 0.82f;
        SetCharacterLookPose(room, portrait, viewport.rect.size, 0f, startCenter, faceCenter);
        if (chapterTransitionOverlay != null) chapterTransitionOverlay.transform.SetAsLastSibling();
        while (isChapterTransitioning || ArchiveManager.IsOpen) yield return null;
        float elapsed = 0f;
        // Full sprites begin with the knees near the screen bottom, matching Moteru's framing.
        const float scanDuration = 9.5f, faceHold = 0.6f;
        while (elapsed < scanDuration + faceHold)
        {
            if (ArchiveManager.IsOpen) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            SetCharacterLookPose(room, portrait, viewport.rect.size,
                Mathf.SmoothStep(0f, 1f, elapsed / scanDuration), startCenter, faceCenter);
            yield return null;
        }
        yield return ReturnLookToDialogue(line);
    }

    private IEnumerator ReturnLookToDialogue(DialogueLine line)
    {
        // Hold the close-up unchanged; switch to the standard composition only while black.
        Image overlay = EnsureChapterTransitionOverlay();
        if (overlay != null)
        {
            overlay.transform.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            yield return FadeLookReturn(0f, 1f);
        }
        RestoreRoomLookPresentation();
        isRoomLookPlaying = true;
        if (overlay != null)
        {
            yield return FadeLookReturn(1f, 0f);
            overlay.gameObject.SetActive(false);
        }
        isRoomLookPlaying = false;
        roomLookCoroutine = null;
        if (isPlaying && currentLine == line) StartTyping(GetDialogueDisplayText(line, omitOuterDialogueQuotes));
    }

    private IEnumerator FadeLookReturn(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            if (ArchiveManager.IsOpen) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            SetChapterTransitionAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / 0.3f)));
            yield return null;
        }
        SetChapterTransitionAlpha(to);
    }

    private static void SetCharacterLookPose(Image room, Image portrait, Vector2 viewportSize, float progress,
        float startCenter, float faceCenter)
    {
        float centerY = Mathf.Lerp(startCenter, faceCenter, progress);
        RectTransform rect = portrait.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float height = viewportSize.y * 2.4f;
        rect.sizeDelta = new Vector2(height * portrait.sprite.rect.width / portrait.sprite.rect.height, height);
        rect.anchoredPosition = new Vector2(0f, (0.5f - centerY) * height);
        // A small matching background tilt makes this a camera move rather than a sliding portrait.
        SetRoomLookPose(room, viewportSize, new Vector2(0.5f, Mathf.Lerp(0.42f, 0.58f, progress)), 1.35f);
    }

    private Image CreateRoomLookImage(RectTransform parent, string objectName)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = backgroundImage.sprite;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        return image;
    }

    private static void SetRoomLookPose(Image image, Vector2 viewportSize, Vector2 center, float zoom)
    {
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        float height = Mathf.Max(viewportSize.y, viewportSize.x / aspect) * zoom;
        Vector2 size = new Vector2(height * aspect, height);
        rect.sizeDelta = size;
        // Clamp against the viewport, including unusually wide/tall Game view sizes.
        Vector2 position = Vector2.Scale(new Vector2(0.5f, 0.5f) - center, size);
        Vector2 margin = (size - viewportSize) * 0.5f;
        rect.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -margin.x, margin.x),
            Mathf.Clamp(position.y, -margin.y, margin.y));
    }

    private void CancelRoomLook()
    {
        bool wasLookPlaying = isRoomLookPlaying;
        if (roomLookCoroutine != null) StopCoroutine(roomLookCoroutine);
        roomLookCoroutine = null;
        RestoreRoomLookPresentation();
        if (wasLookPlaying && !isChapterTransitioning && chapterTransitionOverlay != null)
        {
            SetChapterTransitionAlpha(0f);
            chapterTransitionOverlay.gameObject.SetActive(false);
        }
    }

    private void RestoreRoomLookPresentation()
    {
        if (roomLookViewport != null)
        {
            roomLookViewport.SetActive(false);
            Destroy(roomLookViewport);
            roomLookViewport = null;
        }
        if (roomLookPresentation != null)
        {
            roomLookPresentation.alpha = roomLookOriginalAlpha;
            roomLookPresentation.interactable = roomLookOriginalInteractable;
            roomLookPresentation.blocksRaycasts = roomLookOriginalBlocksRaycasts;
            roomLookPresentation = null;
        }
        isRoomLookPlaying = false;
    }

    private void OnDisable()
    {
        StopMoteruRevealView();
        StopYowashiBandageView();
        StopGatheringComposition();
        StopSearchPortraitSlide();
        StopWindowShake();
        ResetKayoFall();
        CancelRoomLook();
    }

    private void PrepareLine(DialogueLine line)
    {
        StopMoteruRevealView();
        StopYowashiBandageView();
        StopGatheringComposition();
        StopSearchPortraitSlide();
        StopWindowShake();
        HideChoices();

        currentLine = line;
        autoAdvanceAt = -1f;

        StoryMusicController.ApplyDialogue(gameObject.scene, currentScenario, line);

        ApplyBackground(line);
        ApplyAffectionChanges(line);
        ApplyArchiveUnlocks(line);
        ApplyCharacter(line);
        StartGatheringComposition(line);
        StartSearchPortraitSlide(line);
        StartYowashiBandageView(line);
        StartMoteruRevealView(line);
        LineStarted?.Invoke(line);
    }

    public static bool IsMoteruRevealLine(string lineId)
    {
        const string prefix = "chapter2_moteru_";
        return lineId != null && lineId.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(lineId.Substring(prefix.Length), out int row) && row >= 9 && row <= 18;
    }

    private void StartMoteruRevealView(DialogueLine line)
    {
        if (!IsMoteruRevealLine(line?.lineId) || dialogueRoot == null || backgroundImage == null ||
            backgroundImage.sprite == null || characterDatabase == null ||
            !characterDatabase.TryGetCharacter("doute", out CharacterData doute) ||
            !characterDatabase.TryGetCharacter("moteru", out CharacterData moteru)) return;
        Sprite douteSprite = doute.GetPortrait("normal"), moteruSprite = moteru.GetPortrait("normal");
        if (douteSprite == null || moteruSprite == null) return;
        revealLeftWasEnabled = leftPortraitImage != null && leftPortraitImage.enabled;
        revealRightWasEnabled = rightPortraitImage != null && rightPortraitImage.enabled;
        if (leftPortraitImage != null) leftPortraitImage.enabled = false;
        if (rightPortraitImage != null) rightPortraitImage.enabled = false;
        isMoteruRevealViewActive = true;

        moteruRevealView = new GameObject("MoteruRevealSplitView", typeof(RectTransform), typeof(RectMask2D));
        RectTransform frame = moteruRevealView.GetComponent<RectTransform>();
        frame.SetParent(dialogueRoot.transform, false);
        frame.SetSiblingIndex(rightPortraitImage != null ? rightPortraitImage.transform.GetSiblingIndex() + 1 : 0);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        Vector2 viewport = frame.rect.size;
        RectTransform top = CreateRevealHalf(frame, "DouteTopHalf", viewport.y, true);
        RectTransform bottom = CreateRevealHalf(frame, "MoteruBottomHalf", viewport.y, false);
        Image topRoom = CreateRoomLookImage(top, "TopZoomedRoom");
        SetRoomLookPose(topRoom, viewport, new Vector2(0.5f, 0.54f), 1.6f);
        Image topPortrait = CreateRoomLookImage(top, "DouteFace");
        topPortrait.sprite = douteSprite;
        // Upper face centered inside the top half, with both eyes and chin visible.
        SetGatheringPortrait(topPortrait, viewport, 0.35f, 2.6f, 0.25f - (0.90f - 0.5f) * 2.6f,
            mirrorDoutePortrait ? -1f : 1f);
        Image bottomRoom = CreateRoomLookImage(bottom, "BottomZoomedRoom");
        SetRoomLookPose(bottomRoom, viewport, new Vector2(0.5f, 0.46f), 1.6f);
        Image bottomPortrait = CreateRoomLookImage(bottom, "MoteruMouth");
        bottomPortrait.sprite = moteruSprite;
        // The split edge crops Moteru above the mouth, rather than exposing the eyes.
        SetGatheringPortrait(bottomPortrait, viewport, 0.70f, 2.7f, -(0.85f - 0.5f) * 2.7f, 1f);
        var dividerObject = new GameObject("RevealCenterDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform divider = dividerObject.GetComponent<RectTransform>();
        divider.SetParent(frame, false);
        divider.anchorMin = new Vector2(0f, 0.475f);
        divider.anchorMax = new Vector2(1f, 0.525f);
        divider.offsetMin = divider.offsetMax = Vector2.zero;
        Image image = dividerObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        dividerObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(0f, viewport.y * 0.02f));
    }

    private static RectTransform CreateRevealHalf(RectTransform parent, string name, float height, bool top)
    {
        var half = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        RectTransform rect = half.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        half.GetComponent<RectMask2D>().padding = top
            ? new Vector4(0f, height * 0.5f, 0f, 0f) : new Vector4(0f, 0f, 0f, height * 0.5f);
        return rect;
    }

    private void StopMoteruRevealView()
    {
        if (!isMoteruRevealViewActive) return;
        if (leftPortraitImage != null) leftPortraitImage.enabled = revealLeftWasEnabled;
        if (rightPortraitImage != null) rightPortraitImage.enabled = revealRightWasEnabled;
        if (moteruRevealView != null)
        {
            moteruRevealView.SetActive(false);
            moteruRevealView.transform.SetParent(null, false);
            Destroy(moteruRevealView);
            moteruRevealView = null;
        }
        isMoteruRevealViewActive = false;
    }

    public static bool IsYowashiBandageLine(string lineId)
    {
        const string prefix = "chapter2_yowashi_";
        return lineId != null && lineId.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(lineId.Substring(prefix.Length), out int row) && row >= 9 && row <= 16;
    }

    private void StartYowashiBandageView(DialogueLine line)
    {
        if (!IsYowashiBandageLine(line?.lineId) || dialogueRoot == null || backgroundImage == null ||
            backgroundImage.sprite == null || characterDatabase == null ||
            !characterDatabase.TryGetCharacter("yowashi", out CharacterData character)) return;
        Sprite portraitSprite = character.GetPortrait("normal");
        if (portraitSprite == null) return;
        bandageLeftWasEnabled = leftPortraitImage != null && leftPortraitImage.enabled;
        bandageRightWasEnabled = rightPortraitImage != null && rightPortraitImage.enabled;
        if (leftPortraitImage != null) leftPortraitImage.enabled = false;
        if (rightPortraitImage != null) rightPortraitImage.enabled = false;
        isYowashiBandageViewActive = true;

        yowashiBandageView = new GameObject("YowashiBandageCloseUp", typeof(RectTransform), typeof(RectMask2D));
        RectTransform frame = yowashiBandageView.GetComponent<RectTransform>();
        frame.SetParent(dialogueRoot.transform, false);
        frame.SetSiblingIndex(rightPortraitImage != null ? rightPortraitImage.transform.GetSiblingIndex() + 1 : 0);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        Vector2 viewport = frame.rect.size;
        Image room = CreateRoomLookImage(frame, "BandageZoomedRoom");
        SetRoomLookPose(room, viewport, new Vector2(0.5f, 0.58f), 1.65f);
        Image portrait = CreateRoomLookImage(frame, "YowashiTorso");
        portrait.sprite = portraitSprite;
        // Chest and left forearm occupy the center; face and legs remain outside this crop.
        float height = viewport.y * 4f;
        portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        portrait.rectTransform.sizeDelta = new Vector2(height * portraitSprite.rect.width / portraitSprite.rect.height, height);
        portrait.rectTransform.anchoredPosition = new Vector2(0f, (0.5f - 0.65f) * height);
        CreateBandageSideBar(frame, "LeftBand", new Vector2(0f, 0f), new Vector2(0.14f, 1f));
        CreateBandageSideBar(frame, "RightBand", new Vector2(0.86f, 0f), new Vector2(1f, 1f));
    }

    private static void CreateBandageSideBar(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var band = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = band.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = band.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    private void StopYowashiBandageView()
    {
        if (!isYowashiBandageViewActive) return;
        if (leftPortraitImage != null) leftPortraitImage.enabled = bandageLeftWasEnabled;
        if (rightPortraitImage != null) rightPortraitImage.enabled = bandageRightWasEnabled;
        if (yowashiBandageView != null)
        {
            yowashiBandageView.SetActive(false);
            yowashiBandageView.transform.SetParent(null, false);
            Destroy(yowashiBandageView);
            yowashiBandageView = null;
        }
        isYowashiBandageViewActive = false;
    }

    private void StartGatheringComposition(DialogueLine line)
    {
        // Only the invitations between exploration and the later self-introductions.
        string target = line != null && line.lineId != null && line.lineId.StartsWith("chapter1_kayo_", StringComparison.Ordinal)
            ? "kayo" : line != null && line.lineId != null && line.lineId.StartsWith("chapter1_yowashi_", StringComparison.Ordinal)
                ? "yowashi" : null;
        if (target == null) { gatheringAnimationTarget = null; ResetKayoFall(); return; }
        if (leftPortraitImage == null || rightPortraitImage == null ||
            dialogueRoot == null || backgroundImage == null || backgroundImage.sprite == null ||
            characterDatabase == null || !characterDatabase.TryGetCharacter(target, out CharacterData character)) return;
        Sprite portrait = character.GetPortrait(line.speakerId == target ? line.expressionId : "normal");
        if (portrait == null) return;
        // Continue through page advances; do not restart the slide on every dialogue line.
        if (gatheringAnimationTarget != target)
        {
            gatheringAnimationTarget = target;
            gatheringDouteSlideElapsed = 0f;
            ResetKayoFall();
        }
        // The other person is present even on Doute's opening greeting.
        SetPortrait(rightPortraitImage, portrait);
        gatheringLeftPose = CapturePortraitPose(leftPortraitImage.rectTransform);
        gatheringRightPose = CapturePortraitPose(rightPortraitImage.rectTransform);
        gatheringLeftParent = leftPortraitImage.transform.parent;
        gatheringRightParent = rightPortraitImage.transform.parent;
        gatheringLeftSibling = leftPortraitImage.transform.GetSiblingIndex();
        gatheringRightSibling = rightPortraitImage.transform.GetSiblingIndex();
        isGatheringCompositionActive = true;
        Canvas.ForceUpdateCanvases();
        RectTransform parent = dialogueRoot.GetComponent<RectTransform>();
        if (parent == null) { StopGatheringComposition(); return; }
        Vector2 viewport = parent.rect.size;

        gatheringLeftFrame = CreateGatheringHalfFrame(parent, "GatheringLeftHalf", viewport.x, true);
        gatheringLeftFrame.SetSiblingIndex(leftPortraitImage.transform.GetSiblingIndex());
        leftPortraitImage.transform.SetParent(gatheringLeftFrame, false);
        gatheringRightFrame = CreateGatheringHalfFrame(parent, "GatheringRightHalf", viewport.x, false);
        gatheringRightFrame.SetSiblingIndex(rightPortraitImage.transform.GetSiblingIndex());
        rightPortraitImage.transform.SetParent(gatheringRightFrame, false);
        // Each half has its own camera height; room lines no longer join across the split.
        gatheringLeftBackground = CreateRoomLookImage(gatheringLeftFrame, "GatheringLeftRoom");
        gatheringLeftBackground.transform.SetAsFirstSibling();
        gatheringRightBackground = CreateRoomLookImage(gatheringRightFrame, "GatheringRightRoom");
        gatheringRightBackground.transform.SetAsFirstSibling();
        SetRoomLookPose(gatheringRightBackground, viewport, new Vector2(0.5f, 0.48f), 1.35f);
        ApplyGatheringDouteSlide(viewport);
        float cropLine = target == "kayo" ? 0.83f : 0.87f;
        SetGatheringPortrait(rightPortraitImage, viewport, 0.76f, 3.1f,
            0.5f - (cropLine - 0.5f) * 3.1f, 1f);
        ApplyKayoFallPose();

        var dividerObject = new GameObject("GatheringCenterDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dividerObject.transform.SetParent(parent, false);
        dividerObject.transform.SetSiblingIndex(gatheringRightFrame.GetSiblingIndex() + 1);
        gatheringDivider = dividerObject.GetComponent<Image>();
        gatheringDivider.color = Color.black;
        gatheringDivider.raycastTarget = false;
        RectTransform divider = gatheringDivider.rectTransform;
        divider.anchorMin = new Vector2(0.41f, 0f);
        divider.anchorMax = new Vector2(0.59f, 1f);
        divider.offsetMin = divider.offsetMax = Vector2.zero;
        dividerObject.AddComponent<DialogueWindowFeather>().ConfigureFeather(new Vector2(viewport.x * 0.08f, 0f));
    }

    private void UpdateGatheringDouteSlide()
    {
        if (!isGatheringCompositionActive || leftPortraitImage == null || dialogueRoot == null ||
            ArchiveManager.IsOpen || isChapterTransitioning || isSkipConfirmationOpen || isRoomLookPlaying) return;
        gatheringDouteSlideElapsed = Mathf.Min(5.5f, gatheringDouteSlideElapsed + Time.unscaledDeltaTime);
        ApplyGatheringDouteSlide(dialogueRoot.GetComponent<RectTransform>().rect.size);
    }

    private void UpdateKayoFall()
    {
        if (!isGatheringCompositionActive || gatheringAnimationTarget != "kayo" ||
            ArchiveManager.IsOpen || isChapterTransitioning || isSkipConfirmationOpen || isRoomLookPlaying) return;
        if (!kayoScreamTriggered && currentLine?.lineId == "chapter1_kayo_014" && bodyText != null)
        {
            string displayText = GetDialogueDisplayText(currentLine, omitOuterDialogueQuotes);
            int screamAt = displayText.IndexOf("きゃ", StringComparison.Ordinal);
            if (screamAt >= 0 && bodyText.maxVisibleCharacters >= screamAt + 2) TriggerKayoFall();
        }
        if (!kayoFallMoving) return;
        kayoFallElapsed = Mathf.Min(kayoFallDuration, kayoFallElapsed + Time.unscaledDeltaTime);
        float t = kayoFallElapsed / kayoFallDuration;
        float eased = kayoFallTo > kayoFallFrom ? t * (2f - t) : Mathf.SmoothStep(0f, 1f, t);
        kayoFallProgress = Mathf.Lerp(kayoFallFrom, kayoFallTo, eased);
        ApplyKayoFallPose();
        if (kayoFallElapsed >= kayoFallDuration) kayoFallMoving = false;
    }

    private void TriggerKayoFall()
    {
        if (kayoScreamTriggered || !isGatheringCompositionActive || gatheringAnimationTarget != "kayo") return;
        kayoScreamTriggered = true;
        BeginKayoFallMotion(1f, 0.28f);
    }

    private void BeginKayoFallMotion(float destination, float duration)
    {
        kayoFallFrom = kayoFallProgress;
        kayoFallTo = destination;
        kayoFallDuration = duration;
        kayoFallElapsed = 0f;
        kayoFallMoving = true;
    }

    private void ApplyKayoFallPose()
    {
        if (!isGatheringCompositionActive || gatheringAnimationTarget != "kayo" ||
            rightPortraitImage == null || dialogueRoot == null) return;
        float viewportHeight = dialogueRoot.GetComponent<RectTransform>().rect.height;
        float normalY = 0.5f - (0.83f - 0.5f) * 3.1f;
        // Formal Kayo's drawn crown is at 92.2% of the full sprite height.
        // Leave just its top 10% of viewport height visible above the bottom screen edge.
        float drop = -0.40f - (normalY + (0.922f - 0.5f) * 3.1f);
        RectTransform rect = rightPortraitImage.rectTransform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
            (normalY + drop * kayoFallProgress) * viewportHeight);
    }

    private void ResetKayoFall()
    {
        kayoFallProgress = kayoFallFrom = kayoFallTo = kayoFallElapsed = 0f;
        kayoFallMoving = kayoScreamTriggered = false;
    }

    private void ApplyGatheringDouteSlide(Vector2 viewport)
    {
        float progress = Mathf.SmoothStep(0f, 1f, gatheringDouteSlideElapsed / 5.5f);
        float startY = 0.5f - (0.90f - 0.5f) * 2.7f;
        SetGatheringPortrait(leftPortraitImage, viewport, 0.23f, 2.7f, Mathf.Lerp(startY, -0.91f, progress),
            mirrorDoutePortrait ? -1f : 1f);
        if (gatheringLeftBackground != null)
            SetRoomLookPose(gatheringLeftBackground, viewport,
                new Vector2(0.5f, Mathf.Lerp(0.48f, 0.53f, progress)), 1.35f);
    }

    private static RectTransform CreateGatheringHalfFrame(RectTransform parent, string name, float width, bool left)
    {
        var frameObject = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        RectTransform frame = frameObject.GetComponent<RectTransform>();
        frame.SetParent(parent, false);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        frameObject.GetComponent<RectMask2D>().padding = left
            ? new Vector4(0f, 0f, width * 0.5f, 0f) : new Vector4(width * 0.5f, 0f, 0f, 0f);
        return frame;
    }

    private static SearchPortraitPose CapturePortraitPose(RectTransform rect)
    {
        return new SearchPortraitPose
        {
            anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
            size = rect.sizeDelta, position = rect.anchoredPosition, scale = rect.localScale, rotation = rect.localRotation
        };
    }

    private static void RestorePortraitPose(RectTransform rect, SearchPortraitPose pose)
    {
        rect.anchorMin = pose.anchorMin; rect.anchorMax = pose.anchorMax; rect.pivot = pose.pivot;
        rect.sizeDelta = pose.size; rect.anchoredPosition = pose.position;
        rect.localScale = pose.scale; rect.localRotation = pose.rotation;
    }

    private static void SetGatheringPortrait(Image image, Vector2 viewport, float x, float heightFactor, float y, float facing)
    {
        if (image.sprite == null) return;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float height = viewport.y * heightFactor;
        rect.sizeDelta = new Vector2(height * image.sprite.rect.width / image.sprite.rect.height, height);
        rect.localScale = new Vector3(facing, 1f, 1f);
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2((x - 0.5f) * viewport.x, y * viewport.y);
    }

    private void StopGatheringComposition()
    {
        if (!isGatheringCompositionActive) return;
        if (leftPortraitImage != null)
        {
            leftPortraitImage.transform.SetParent(gatheringLeftParent, false);
            RestorePortraitPose(leftPortraitImage.rectTransform, gatheringLeftPose);
        }
        if (rightPortraitImage != null)
        {
            rightPortraitImage.transform.SetParent(gatheringRightParent, false);
            RestorePortraitPose(rightPortraitImage.rectTransform, gatheringRightPose);
        }
        foreach (Image image in new[] { gatheringLeftBackground, gatheringRightBackground, gatheringDivider })
        {
            if (image == null) continue;
            image.gameObject.SetActive(false);
            image.transform.SetParent(null, false);
            Destroy(image.gameObject);
        }
        gatheringLeftBackground = gatheringRightBackground = gatheringDivider = null;
        foreach (RectTransform frame in new[] { gatheringLeftFrame, gatheringRightFrame })
        {
            if (frame == null) continue;
            frame.gameObject.SetActive(false);
            frame.SetParent(null, false);
            Destroy(frame.gameObject);
        }
        gatheringLeftFrame = gatheringRightFrame = null;
        if (leftPortraitImage != null) leftPortraitImage.transform.SetSiblingIndex(gatheringLeftSibling);
        if (rightPortraitImage != null) rightPortraitImage.transform.SetSiblingIndex(gatheringRightSibling);
        isGatheringCompositionActive = false;
    }

    private void StartSearchPortraitSlide(DialogueLine line)
    {
        if (!useSearchPortraitSlide || line == null || rightPortraitImage == null ||
            rightPortraitImage.sprite == null || (line.speakerId != "kayo" && line.speakerId != "yowashi")) return;
        RectTransform rect = rightPortraitImage.rectTransform;
        searchPortraitOriginalPose = new SearchPortraitPose
        {
            anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
            size = rect.sizeDelta, position = rect.anchoredPosition,
            scale = rect.localScale, rotation = rect.localRotation
        };
        // Crop between eyes and mouth, preserving the whole mouth for each imported sprite.
        searchPortraitHeadMidpoint = line.speakerId == "kayo" ? 0.83f : 0.87f;
        searchPortraitOriginalSibling = rect.GetSiblingIndex();
        searchSpeakerOriginalColor = rightPortraitImage.color;
        if (leftPortraitImage != null)
        {
            searchDouteOriginalColor = leftPortraitImage.color;
            searchDouteOriginalPosition = leftPortraitImage.rectTransform.anchoredPosition;
        }
        if (searchSlideRoomBackground != null && searchSlideRoomBackground.sprite != null)
        {
            var backgroundObject = new GameObject("SearchParallaxBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(rect.parent, false);
            backgroundObject.transform.SetAsFirstSibling();
            searchParallaxBackground = backgroundObject.GetComponent<Image>();
            searchParallaxBackground.sprite = searchSlideRoomBackground.sprite;
            searchParallaxBackground.color = searchSlideRoomBackground.color;
            searchParallaxBackground.raycastTarget = false;
        }
        // Keep the enlarged speaker above Doute, but below the text window and name plate.
        if (leftPortraitImage != null && rect.parent == leftPortraitImage.transform.parent)
            rect.SetSiblingIndex(leftPortraitImage.transform.GetSiblingIndex() + 1);
        searchPortraitSlideElapsed = 0f;
        isSearchPortraitSliding = true;
        Canvas.ForceUpdateCanvases();
        ApplySearchPortraitSlidePose(0f);
    }

    private void UpdateSearchPortraitSlide()
    {
        if (!isSearchPortraitSliding || isRoomLookPlaying || rightPortraitImage == null || ArchiveManager.IsOpen ||
            isChapterTransitioning || isSkipConfirmationOpen) return;
        searchPortraitSlideElapsed = Mathf.Min(8f, searchPortraitSlideElapsed + Time.unscaledDeltaTime);
        ApplySearchPortraitSlidePose(Mathf.SmoothStep(0f, 1f, searchPortraitSlideElapsed / 8f));
    }

    private void ApplySearchPortraitSlidePose(float progress)
    {
        RectTransform rect = rightPortraitImage.rectTransform;
        RectTransform parent = rect.parent as RectTransform;
        if (parent == null || parent.rect.height <= 0f) return;
        rightPortraitImage.color = searchSpeakerOriginalColor;
        float height = parent.rect.height * 3.1f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = new Vector2(height * rightPortraitImage.sprite.rect.width / rightPortraitImage.sprite.rect.height, height);
        // Keep the eyes above the screen top throughout the horizontal move.
        rect.anchoredPosition = new Vector2(parent.rect.width * Mathf.Lerp(0.20f, 0.08f, progress),
            parent.rect.height * 0.5f - (searchPortraitHeadMidpoint - 0.5f) * height);
        if (searchParallaxBackground != null)
        {
            SetRoomLookPose(searchParallaxBackground, parent.rect.size,
                new Vector2(Mathf.Lerp(0.48f, 0.52f, progress), 0.5f), 1.35f);
            searchParallaxBackground.color = searchSlideRoomBackground.color;
        }
        if (leftPortraitImage != null)
        {
            Color dimmed = searchDouteOriginalColor;
            const float brightness = 0.45f;
            dimmed.r *= brightness; dimmed.g *= brightness; dimmed.b *= brightness;
            leftPortraitImage.color = dimmed;
            leftPortraitImage.rectTransform.anchoredPosition = searchDouteOriginalPosition +
                Vector2.right * (parent.rect.width * 0.035f * progress);
        }
    }

    private void StopSearchPortraitSlide()
    {
        if (!isSearchPortraitSliding) return;
        if (searchParallaxBackground != null)
        {
            searchParallaxBackground.gameObject.SetActive(false);
            searchParallaxBackground.transform.SetParent(null, false);
            Destroy(searchParallaxBackground.gameObject);
            searchParallaxBackground = null;
        }
        if (leftPortraitImage != null)
        {
            leftPortraitImage.color = searchDouteOriginalColor;
            leftPortraitImage.rectTransform.anchoredPosition = searchDouteOriginalPosition;
        }
        if (rightPortraitImage != null)
        {
            rightPortraitImage.color = searchSpeakerOriginalColor;
            RectTransform rect = rightPortraitImage.rectTransform;
            rect.anchorMin = searchPortraitOriginalPose.anchorMin;
            rect.anchorMax = searchPortraitOriginalPose.anchorMax;
            rect.pivot = searchPortraitOriginalPose.pivot;
            rect.sizeDelta = searchPortraitOriginalPose.size;
            rect.anchoredPosition = searchPortraitOriginalPose.position;
            rect.localScale = searchPortraitOriginalPose.scale;
            rect.localRotation = searchPortraitOriginalPose.rotation;
            rect.SetSiblingIndex(searchPortraitOriginalSibling);
        }
        isSearchPortraitSliding = false;
    }

    private bool TryStartConsultationTransition(DialogueLine line)
    {
        if (!useConsultationTransition ||
            line == null ||
            string.IsNullOrWhiteSpace(line.consultationTitle) ||
            isChapterTransitioning ||
            !shownConsultationLineIds.Add(line.lineId))
        {
            return false;
        }

        Image overlay = EnsureChapterTransitionOverlay();

        if (overlay == null)
        {
            Debug.LogWarning(
                "相談開始演出用のCanvasが見つからないため、" +
                "フェードせずに相談パートを開始します。");
            return false;
        }

        EnsureConsultationTransitionTitle(overlay.transform);
        StopTypingAndRevealText();
        HideChoices();
        autoAdvanceAt = -1f;
        isChapterTransitioning = true;

        overlay.transform.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        SetConsultationTransitionTitle(null, false);
        SetChapterTransitionAlpha(0f);

        chapterTransitionCoroutine = StartCoroutine(
            TransitionToConsultation(line));
        return true;
    }

    private IEnumerator TransitionToConsultation(DialogueLine line)
    {
        yield return FadeChapterTransition(
            0f,
            1f,
            consultationFadeOutDuration);

        SetConsultationTransitionTitle(line.consultationTitle, true);

        if (consultationTitleHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                consultationTitleHoldDuration);
        }

        SetConsultationTransitionTitle(null, false);
        PrepareLine(line);

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
            bodyText.maxVisibleCharacters = 0;
        }

        yield return FadeChapterTransition(
            1f,
            0f,
            consultationFadeInDuration);

        if (chapterTransitionOverlay != null)
        {
            chapterTransitionOverlay.gameObject.SetActive(false);
        }

        isChapterTransitioning = false;
        chapterTransitionCoroutine = null;
        StartTyping(GetDialogueDisplayText(line, omitOuterDialogueQuotes));
    }

    private void EnsureConsultationTransitionTitle(Transform overlayTransform)
    {
        if (consultationTransitionTitle != null || overlayTransform == null)
        {
            return;
        }

        GameObject titleObject = new GameObject(
            "ConsultationTransitionTitle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        RectTransform titleRect =
            titleObject.GetComponent<RectTransform>();
        titleRect.SetParent(overlayTransform, false);
        titleRect.anchorMin = new Vector2(0.1f, 0.32f);
        titleRect.anchorMax = new Vector2(0.9f, 0.68f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        consultationTransitionTitle =
            titleObject.GetComponent<TextMeshProUGUI>();
        consultationTransitionTitle.alignment =
            TextAlignmentOptions.Center;
        consultationTransitionTitle.fontSize = 46f;
        consultationTransitionTitle.fontStyle = FontStyles.Bold;
        consultationTransitionTitle.textWrappingMode =
            TextWrappingModes.NoWrap;
        consultationTransitionTitle.raycastTarget = false;

        if (bodyText != null && bodyText.font != null)
        {
            consultationTransitionTitle.font = bodyText.font;
        }

        titleObject.SetActive(false);
    }

    private void SetConsultationTransitionTitle(
        string title,
        bool visible)
    {
        if (consultationTransitionTitle == null)
        {
            return;
        }

        consultationTransitionTitle.text =
            visible
                ? "<size=58%><color=#D7B56D>CONSULTATION</color></size>\n" +
                  "相談パート\n" +
                  $"<size=72%>— {title} —</size>"
                : string.Empty;
        consultationTransitionTitle.color = Color.white;
        consultationTransitionTitle.gameObject.SetActive(visible);
    }

    private void ResetBackgroundForScenario()
    {
        SetBackground(
            currentScenario != null
                ? currentScenario.DefaultBackground
                : null);
    }

    private void ApplyBackground(DialogueLine line)
    {
        if (line == null || line.background == null)
        {
            return;
        }

        SetBackground(line.background);
    }

    private void SetBackground(Sprite background)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.sprite = background;
        backgroundImage.enabled = background != null;
    }

    private void ApplyAffectionChanges(DialogueLine line)
    {
        if (line == null ||
            line.affectionChanges == null ||
            line.affectionChanges.Count == 0)
        {
            return;
        }

        AffectionManager affection = ResolveAffectionManager();

        if (affection == null)
        {
            Debug.LogWarning(
                $"セリフ「{line.lineId}」に好感度の変化がありますが、" +
                "AffectionManagerが見つかりません。");
            return;
        }

        affection.ApplyDeltas(line.affectionChanges);
    }

    private static void ApplyArchiveUnlocks(DialogueLine line)
    {
        if (line == null || line.archiveUnlockIds == null)
        {
            return;
        }

        foreach (string entryId in line.archiveUnlockIds)
        {
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                ArchiveManager.Unlock(entryId.Trim());
            }
        }
    }

    private void ApplyCharacter(DialogueLine line)
    {
        // speakerIdが空なら地の文
        if (string.IsNullOrWhiteSpace(line.speakerId))
        {
            if (namePlate != null)
            {
                namePlate.SetActive(false);
            }

            if (speakerNameText != null)
            {
                speakerNameText.text = string.Empty;
            }

            return;
        }

        if (!characterDatabase.TryGetCharacter(
                line.speakerId,
                out CharacterData character))
        {
            Debug.LogError(
                $"キャラクター「{line.speakerId}」が存在しません。");

            return;
        }

        if (namePlate != null)
        {
            namePlate.SetActive(true);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = GetSpeakerDisplayName(character,
                knownCharacterIds.Contains(line.speakerId) || revealedCharacterIds.Contains(line.speakerId),
                unknownSpeakerName);
            speakerNameText.color = character.nameColor;
        }

        Sprite portrait =
            character.GetPortrait(line.expressionId);

        if (string.Equals(
                line.speakerId,
                protagonistCharacterId,
                System.StringComparison.Ordinal))
        {
            SetPortrait(leftPortraitImage, portrait);
            ApplyProtagonistFacing();
        }
        else
        {
            SetPortrait(rightPortraitImage, portrait);
            ApplyBustPortraitSize(line.speakerId);
        }
    }

    private void ApplyBustPortraitSize(string characterId)
    {
        if (!normalizeBustPortraitSizes || rightPortraitImage == null) return;
        RectTransform rect = rightPortraitImage.rectTransform;
        if (!rightPortraitBaseCaptured)
        {
            rightPortraitBasePosition = rect.anchoredPosition;
            rightPortraitBaseScale = rect.localScale;
            rightPortraitBaseCaptured = true;
        }
        float factor = characterId == "moteru" ? 0.68f
            : characterId == "kayo" ? kayoPortraitScale : 1f;
        rect.localScale = rightPortraitBaseScale * factor;
        // Zoom out around the top center, retaining the head's vertical placement.
        rect.anchoredPosition = rightPortraitBasePosition + Vector2.up *
            (rect.rect.height * (1f - rect.pivot.y) * rightPortraitBaseScale.y * (1f - factor));
        // The formal Kayo image has extra space above the head compared with Doute.
        if (characterId == "kayo")
            rect.anchoredPosition += Vector2.up *
                (rect.rect.height * rightPortraitBaseScale.y * kayoPortraitUpwardOffset);
    }

    public static string GetSpeakerDisplayName(CharacterData character, bool revealed, string unknownName)
    {
        if (character == null) return unknownName;
        // Doute is already known. Never let an empty per-controller cache hide his name.
        bool known = string.Equals(character.characterId, "doute", StringComparison.Ordinal) ||
            character.nameKnownInitially || revealed;
        return known ? character.displayName : unknownName;
    }

    private void ApplyProtagonistFacing()
    {
        if (!mirrorDoutePortrait || protagonistCharacterId != "doute" || leftPortraitImage == null) return;
        Vector3 scale = leftPortraitImage.rectTransform.localScale;
        scale.x = -Mathf.Abs(scale.x);
        leftPortraitImage.rectTransform.localScale = scale;
    }

    private void ResetPortraitsForScenario()
    {
        SetPortrait(rightPortraitImage, null);
        ApplyBustPortraitSize(null);

        if (string.IsNullOrWhiteSpace(protagonistCharacterId))
        {
            Debug.LogWarning(
                "主人公のcharacterIdが設定されていません。");
            SetPortrait(leftPortraitImage, null);
            return;
        }

        if (!characterDatabase.TryGetCharacter(
                protagonistCharacterId,
                out CharacterData protagonist))
        {
            Debug.LogWarning(
                $"主人公「{protagonistCharacterId}」が存在しません。");
            SetPortrait(leftPortraitImage, null);
            return;
        }

        SetPortrait(
            leftPortraitImage,
            protagonist.GetPortrait(null));
        ApplyProtagonistFacing();
    }

    private static void SetPortrait(
        Image image,
        Sprite portrait)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = portrait;
        image.preserveAspect = true;
        image.enabled = portrait != null;
    }

    public static string GetDialogueDisplayText(DialogueLine line, bool omitQuotes)
    {
        string text = line?.text ?? string.Empty;
        if (omitQuotes && !string.IsNullOrWhiteSpace(line?.speakerId) &&
            text.Length >= 2 && text[0] == '「' && text[text.Length - 1] == '」')
            return text.Substring(1, text.Length - 2);
        return text;
    }

    private void StartTyping(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        bodyText.text = text;
        bodyText.maxVisibleCharacters = 0;

        typingCoroutine = StartCoroutine(TypeText());
        // There is no cough line in this scene; stand up on the existing recovery reply.
        if (currentLine?.lineId == "chapter1_kayo_017" && kayoScreamTriggered)
            BeginKayoFallMotion(0f, 0.45f);
        if (currentLine != null && currentLine.lineId == "chapter1_003" && currentLine.speakerId == "moteru")
            StartWindowShake();
    }

    private void StartWindowShake()
    {
        StopWindowShake();
        RectTransform panel = bodyText != null ? bodyText.transform.parent as RectTransform : null;
        // Never animate DialogueRoot: it also owns the character portraits.
        if (panel == null || (dialogueRoot != null && panel == dialogueRoot.transform)) return;
        RectTransform plate = namePlate != null ? namePlate.GetComponent<RectTransform>() : null;
        shakingWindowRects = plate != null && plate != panel && !plate.IsChildOf(panel)
            ? new[] { panel, plate } : new[] { panel };
        shakingWindowPositions = new Vector2[shakingWindowRects.Length];
        shakingWindowRotations = new Quaternion[shakingWindowRects.Length];
        for (int i = 0; i < shakingWindowRects.Length; i++)
        {
            shakingWindowPositions[i] = shakingWindowRects[i].anchoredPosition;
            shakingWindowRotations[i] = shakingWindowRects[i].localRotation;
        }
        windowShakeElapsed = 0f;
    }

    private void UpdateWindowShake()
    {
        if (shakingWindowRects == null || ArchiveManager.IsOpen || isChapterTransitioning || isSkipConfirmationOpen) return;
        const float duration = 2.2f;
        windowShakeElapsed += Time.unscaledDeltaTime;
        if (windowShakeElapsed >= duration) { StopWindowShake(); return; }
        float strength = 1f - Mathf.SmoothStep(0f, 1f, windowShakeElapsed / duration);
        float phase = windowShakeElapsed * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Sin(phase * 5f) * 10f, Mathf.Sin(phase * 7f) * 6f) * strength;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 4f) * 1.2f * strength);
        for (int i = 0; i < shakingWindowRects.Length; i++)
        {
            if (shakingWindowRects[i] == null) continue;
            shakingWindowRects[i].anchoredPosition = shakingWindowPositions[i] + offset;
            shakingWindowRects[i].localRotation = shakingWindowRotations[i] * rotation;
        }
    }

    private void StopWindowShake()
    {
        if (shakingWindowRects == null) return;
        for (int i = 0; i < shakingWindowRects.Length; i++)
        {
            if (shakingWindowRects[i] == null) continue;
            shakingWindowRects[i].anchoredPosition = shakingWindowPositions[i];
            shakingWindowRects[i].localRotation = shakingWindowRotations[i];
        }
        shakingWindowRects = null;
        shakingWindowPositions = null;
        shakingWindowRotations = null;
    }

    private IEnumerator TypeText()
    {
        isTyping = true;

        // TMPの文字情報が更新されるのを待つ
        yield return null;

        bodyText.ForceMeshUpdate();

        int totalCharacters =
            bodyText.textInfo.characterCount;

        float visibleCharacters = 0f;

        while (bodyText.maxVisibleCharacters < totalCharacters)
        {
            if (isChapterTransitioning || isSkipConfirmationOpen)
            {
                yield return null;
                continue;
            }

            visibleCharacters +=
                charactersPerSecond * Time.unscaledDeltaTime;

            bodyText.maxVisibleCharacters =
                Mathf.Min(
                    Mathf.FloorToInt(visibleCharacters),
                    totalCharacters);

            yield return null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;

        isTyping = false;
        typingCoroutine = null;
        HandleLineFullyDisplayed();
    }

    private void CompleteTyping()
    {
        StopTypingAndRevealText();
        HandleLineFullyDisplayed();
    }

    private void StopTypingAndRevealText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    private void HandleLineFullyDisplayed()
    {
        if (currentLine?.lineId == "chapter1_kayo_014") TriggerKayoFall();
        RevealCurrentSpeakerName();

        if (currentLine != null && currentLine.HasSceneTransition)
        {
            // Scene遷移行はAUTOで先へ進めず、プレイヤーの入力を待つ
            autoAdvanceAt = -1f;
            return;
        }

        if (currentLine != null && currentLine.HasChoices)
        {
            ShowChoices(currentLine.choices);
            return;
        }

        ScheduleAutoAdvance();
    }

    private void ResetKnownCharacterNames()
    {
        knownCharacterIds.Clear();
        knownCharacterIds.Add("doute");

        if (characterDatabase == null)
        {
            return;
        }

        foreach (CharacterData character in characterDatabase.Characters)
        {
            if (character == null ||
                !character.nameKnownInitially ||
                string.IsNullOrWhiteSpace(character.characterId))
            {
                continue;
            }

            knownCharacterIds.Add(character.characterId);
        }

        foreach (string characterId in revealedCharacterIds)
        {
            knownCharacterIds.Add(characterId);
        }
    }

    private void RevealCurrentSpeakerName()
    {
        if (currentLine == null ||
            !currentLine.revealSpeakerName ||
            string.IsNullOrWhiteSpace(currentLine.speakerId))
        {
            return;
        }

        knownCharacterIds.Add(currentLine.speakerId);
        revealedCharacterIds.Add(currentLine.speakerId);

        if (speakerNameText == null ||
            !characterDatabase.TryGetCharacter(
                currentLine.speakerId,
                out CharacterData character))
        {
            return;
        }

        speakerNameText.text = character.displayName;
        speakerNameText.color = character.nameColor;
    }

    private bool TryLoadUpcomingSceneTransition()
    {
        if (currentScenario != null &&
            currentLine != null &&
            currentScenario.TryGetSceneTransitionAtOrAfter(
                currentLine.lineId,
                out DialogueLine currentTransition))
        {
            LoadDialogueScene(
                currentTransition.nextScenePath,
                currentTransition);
            return true;
        }

        int scenarioCount = 1 +
            (followingScenarios?.Count ?? 0);

        for (int nextIndex = currentScenarioIndex + 1;
             nextIndex < scenarioCount;
             nextIndex++)
        {
            DialogueScenarioSO nextScenario =
                followingScenarios[nextIndex - 1];

            if (nextScenario == null ||
                !nextScenario.TryGetFirstSceneTransition(
                    out DialogueLine nextTransition))
            {
                continue;
            }

            LoadDialogueScene(
                nextTransition.nextScenePath,
                nextTransition);
            return true;
        }

        return false;
    }

    private void LoadDialogueScene(
        string scenePath,
        DialogueLine transitionLine)
    {
        if (isSceneLoading || string.IsNullOrWhiteSpace(scenePath))
        {
            return;
        }

        string resumeLineId = transitionLine?.nextLineId;

        if (string.IsNullOrWhiteSpace(resumeLineId) &&
            transitionLine != null &&
            currentScenario != null &&
            currentScenario.TryGetNextLine(
                transitionLine.lineId,
                out DialogueLine nextLine))
        {
            resumeLineId = nextLine.lineId;
        }

        string runtimeSceneName;

        try
        {
            runtimeSceneName = GetRuntimeSceneName(scenePath);

            if (!Application.CanStreamedLevelBeLoaded(runtimeSceneName))
            {
                throw new InvalidOperationException(
                    $"シーン「{runtimeSceneName}」が有効なScene Listにありません。");
            }
            SceneTransitionStarting?.Invoke(transitionLine);
        }
        catch (System.Exception exception)
        {
            HandleSceneLoadFailure(scenePath, exception);
            return;
        }

        QueueResumeLine(resumeLineId);
        isSceneLoading = true;
        isPlaying = false;
        autoAdvanceAt = -1f;

        StopTypingAndRevealText();
        HideChoices();
        StartCoroutine(
            LoadDialogueSceneAfterFade(runtimeSceneName, scenePath));
    }

    private IEnumerator LoadDialogueSceneAfterFade(
        string runtimeSceneName,
        string sourceScenePath)
    {
        Image overlay =
            useSceneTransitionFade
                ? EnsureChapterTransitionOverlay()
                : null;

        if (overlay != null)
        {
            overlay.gameObject.SetActive(true);
            overlay.rectTransform.SetAsLastSibling();

            yield return FadeChapterTransition(
                0f,
                1f,
                sceneFadeOutDuration);

            if (sceneFadeHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    sceneFadeHoldDuration);
            }
        }

        try
        {
            SceneManager.LoadScene(runtimeSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            HandleSceneLoadFailure(sourceScenePath, exception);
        }
    }

    private void HandleSceneLoadFailure(
        string scenePath,
        System.Exception exception)
    {
        QueueResumeLine(null);
        isSceneLoading = false;
        isPlaying = true;

        if (chapterTransitionOverlay != null)
        {
            SetChapterTransitionAlpha(0f);
            chapterTransitionOverlay.gameObject.SetActive(false);
        }

        Debug.LogError(
            $"Unity Scene「{scenePath}」を読み込めませんでした。" +
            "Build ProfilesのScene Listへ追加されているか" +
            "確認してください。\n" +
            exception);
    }

    private static string GetRuntimeSceneName(string scenePath)
    {
        string normalizedPath = scenePath.Trim().Replace('\\', '/');
        int lastSlash = normalizedPath.LastIndexOf('/');

        if (lastSlash >= 0 && lastSlash < normalizedPath.Length - 1)
        {
            normalizedPath = normalizedPath.Substring(lastSlash + 1);
        }

        if (normalizedPath.EndsWith(
                ".unity",
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Substring(
                0,
                normalizedPath.Length - ".unity".Length);
        }

        return normalizedPath;
    }

    private void ShowChoices(IReadOnlyList<DialogueChoice> choices)
    {
        if (choiceOptionsRoot == null || choiceButtonTemplate == null)
        {
            Debug.LogError(
                "選択肢UIが設定されていません。");
            EndDialogue();
            return;
        }

        autoAdvanceAt = -1f;
        isChoiceSelectionOpen = true;

        float totalHeight =
            choices.Count * choiceButtonHeight +
            Mathf.Max(0, choices.Count - 1) * choiceButtonSpacing;

        choiceOptionsRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            totalHeight);

        choiceOptionsRoot.gameObject.SetActive(true);

        for (int index = 0; index < choices.Count; index++)
        {
            DialogueChoice choice = choices[index];
            Button button = Instantiate(
                choiceButtonTemplate,
                choiceOptionsRoot);

            button.gameObject.name = $"ChoiceButton_{index + 1}";

            RectTransform buttonRect =
                (RectTransform)button.transform;

            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition =
                new Vector2(
                    0f,
                    -index *
                    (choiceButtonHeight + choiceButtonSpacing));
            buttonRect.sizeDelta =
                new Vector2(0f, choiceButtonHeight);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = choice.text;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(
                () => SelectChoice(choice));
            button.interactable = false;
            button.gameObject.SetActive(true);

            spawnedChoiceButtons.Add(button);
        }

        choiceActivationCoroutine =
            StartCoroutine(EnableChoiceButtonsAfterInputRelease());
    }

    private IEnumerator EnableChoiceButtonsAfterInputRelease()
    {
        yield return null;

        while (Pointer.current?.press.isPressed == true)
        {
            yield return null;
        }

        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                button.interactable = true;
            }
        }

        choiceActivationCoroutine = null;
    }

    private void SelectChoice(DialogueChoice choice)
    {
        if (!isChoiceSelectionOpen ||
            choice == null ||
            currentScenario == null)
        {
            return;
        }

        HideChoices();

        if (!currentScenario.TryGetLine(
                choice.nextLineId,
                out DialogueLine nextLine))
        {
            Debug.LogError(
                $"選択肢「{choice.text}」の遷移先" +
                $"「{choice.nextLineId}」が存在しません。");
            EndDialogue();
            return;
        }

        ShowLine(nextLine);
    }

    private void HideChoices()
    {
        isChoiceSelectionOpen = false;

        if (choiceActivationCoroutine != null)
        {
            StopCoroutine(choiceActivationCoroutine);
            choiceActivationCoroutine = null;
        }

        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                Destroy(button.gameObject);
            }
        }

        spawnedChoiceButtons.Clear();

        if (choiceOptionsRoot != null)
        {
            choiceOptionsRoot.gameObject.SetActive(false);
        }
    }

    private bool ShouldAutoAdvance()
    {
        return autoPlayEnabled &&
            isPlaying &&
            !isTyping &&
            !isChoiceSelectionOpen &&
            currentLine != null &&
            autoAdvanceAt >= 0f &&
            Time.unscaledTime >= autoAdvanceAt;
    }

    private bool WasDialogueAdvancePressed()
    {
        Pointer pointer = Pointer.current;

        if (pointer == null ||
            !pointer.press.wasPressedThisFrame)
        {
            return false;
        }

        if (playbackControlsRoot == null)
        {
            return true;
        }

        return !RectTransformUtility.RectangleContainsScreenPoint(
            playbackControlsRoot,
            pointer.position.ReadValue());
    }

    private void ScheduleAutoAdvance()
    {
        autoAdvanceAt = autoPlayEnabled &&
            currentLine != null &&
            !currentLine.HasSceneTransition
            ? Time.unscaledTime + autoAdvanceDelaySeconds
            : -1f;
    }

    private void UpdateAutoPlayButton()
    {
        if (autoPlayButtonText == null)
        {
            return;
        }

        autoPlayButtonText.text =
            autoPlayEnabled ? "AUTO: ON" : "AUTO: OFF";

        autoPlayButtonText.color =
            autoPlayEnabled
                ? new Color(0.45f, 1f, 0.55f)
                : Color.white;
    }

    private void CloseSkipConfirmation(bool resumeAutoAdvance)
    {
        if (skipConfirmationRoot != null)
        {
            skipConfirmationRoot.SetActive(false);
        }

        isSkipConfirmationOpen = false;
        Time.timeScale = timeScaleBeforeConfirmation;

        if (resumeAutoAdvance &&
            autoPlayEnabled &&
            isPlaying &&
            !isTyping &&
            autoAdvanceRemainingBeforeConfirmation >= 0f)
        {
            autoAdvanceAt =
                Time.unscaledTime +
                autoAdvanceRemainingBeforeConfirmation;
        }
        else
        {
            autoAdvanceAt = -1f;
        }

        autoAdvanceRemainingBeforeConfirmation = -1f;
    }

    private void EndDialogue()
    {
        if (useSearchPortraitSlide && isSearchPortraitSliding)
        {
            if (!isRoomLookPlaying)
                roomLookCoroutine = StartCoroutine(FadeOutSearchCloseUp());
            return;
        }
        EndDialogueImmediately();
    }

    private IEnumerator FadeOutSearchCloseUp()
    {
        isRoomLookPlaying = true;
        autoAdvanceAt = -1f;
        Image overlay = EnsureChapterTransitionOverlay();
        if (overlay != null)
        {
            overlay.transform.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            SetChapterTransitionAlpha(0f);
            yield return FadeLookReturn(0f, 1f);
        }
        // Restore the room and portraits only under full black, never on the input frame.
        StopSearchPortraitSlide();
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        yield return null;
        while (ArchiveManager.IsOpen) yield return null;
        if (overlay != null)
        {
            yield return FadeLookReturn(1f, 0f);
            overlay.gameObject.SetActive(false);
        }
        isRoomLookPlaying = false;
        roomLookCoroutine = null;
        // Completion enables exploration or its scene transition; notify only after the fade.
        EndDialogueImmediately();
    }

    private void EndDialogueImmediately()
    {
        CancelChapterTransition();
        StopTypingAndRevealText();
        HideChoices();

        isPlaying = false;
        currentLine = null;
        currentScenario = null;
        currentScenarioIndex = -1;
        stopAfterLineId = null;
        autoAdvanceAt = -1f;

        if (!KeepPresentationOnCompletion)
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            SetBackground(null);
        }

        onDialogueCompleted?.Invoke();
        DialogueCompleted?.Invoke();
    }
}
