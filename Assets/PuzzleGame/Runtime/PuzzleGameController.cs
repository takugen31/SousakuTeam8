using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SousakuTeam8.PuzzleGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PuzzleGameController : MonoBehaviour
    {
        private const string DefaultPuzzleImageResourcePath = "PuzzleGame/PuzzleSource";
        private const float PlayingPieceGap = 10f;
        private const float CompletedBoardScale = 1.025f;
        private const float BoardInset = 18f;
        private const float MoveAnimationDuration = 0.18f;

        private static readonly Color BackgroundColor = FromHex("#101A2D");
        private static readonly Color BackgroundAccentColor = FromHex("#1A2945");
        private static readonly Color BoardColor = FromHex("#F1745B");
        private static readonly Color BoardCompleteColor = FromHex("#FFD26A");
        private static readonly Color SlotColor = FromHex("#243551");
        private static readonly Color PrimaryTextColor = FromHex("#FFF7E8");
        private static readonly Color SecondaryTextColor = FromHex("#AEBAD0");
        private static readonly Color NavyColor = FromHex("#0D2D58");

        [Header("Puzzle image")]
        [Tooltip("One source image that is automatically divided into a 3 x 3 grid. When empty, Resources/PuzzleGame/PuzzleSource is used.")]
        [SerializeField] private Texture2D puzzleImage;

        [Header("Start settings")]
        [SerializeField] private bool startAutomatically = true;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 8;

        [Header("Host integration")]
        [SerializeField] private UnityEvent<int> onCompleted = new UnityEvent<int>();

        private readonly PuzzleBoardState _state = new PuzzleBoardState();
        private readonly PuzzlePieceView[] _pieces = new PuzzlePieceView[PuzzleBoardState.PieceCount];
        private readonly PuzzleSlotView[] _slots = new PuzzleSlotView[PuzzleBoardState.PieceCount];

        private RectTransform _root;
        private RectTransform _safeArea;
        private RectTransform _header;
        private RectTransform _titleRect;
        private RectTransform _subtitleRect;
        private RectTransform _moveBadge;
        private RectTransform _board;
        private RectTransform _boardShadow;
        private RectTransform _slotLayer;
        private RectTransform _pieceLayer;
        private RectTransform _dragLayer;
        private RectTransform _footer;
        private RectTransform _statusRect;
        private RectTransform _completionBadgeRect;
        private Image _boardImage;
        private Text _moveCountText;
        private Text _statusText;
        private Text _completionText;
        private CanvasGroup _completionCanvasGroup;
        private Font _font;
        private Texture2D _activePuzzleImage;

        private PuzzlePieceView _activeDrag;
        private int _activePointerId = int.MinValue;
        private float _boardSize;
        private float _pieceSize;
        private float _currentPieceGap = PlayingPieceGap;
        private bool _isBuilt;
        private bool _isApplyingLayout;
        private bool _hasStarted;
        private bool _isAnimating;
        private bool _isCompleted;
        private bool _hostInteractable = true;
        private int _gameCounter;

        public event Action<int, int, int> PieceMoved;
        public event Action<int> Completed;
        public event Action Restarted;

        public bool IsComplete => _isCompleted;
        public int MoveCount => _state.MoveCount;
        public bool IsInteractable => CanMovePieces;
        public Texture2D PuzzleImage => _activePuzzleImage;
        public UnityEvent<int> OnCompleted => onCompleted;

        private bool CanMovePieces => _hostInteractable && _hasStarted && !_isAnimating && !_isCompleted;

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();

            if (startAutomatically)
            {
                Restart();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_isBuilt && _slots[0] != null && _pieces[0] != null)
            {
                ApplyResponsiveLayout();
            }
        }

        public void Restart()
        {
            Restart(null);
        }

        public void Restart(int? shuffleSeed)
        {
            if (!_isBuilt)
            {
                return;
            }

            var wasStarted = _hasStarted;
            StopAllCoroutines();
            CancelActiveDragImmediately();
            _isAnimating = false;
            _isCompleted = false;
            _currentPieceGap = PlayingPieceGap;
            _gameCounter++;

            var seed = shuffleSeed ?? (useFixedSeed
                ? fixedSeed
                : unchecked(Environment.TickCount * 397 ^ GetInstanceID() ^ _gameCounter));

            _state.Shuffle(seed);
            _hasStarted = true;
            _slotLayer.gameObject.SetActive(true);

            for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
            {
                var pieceId = _state.GetPieceAt(slot);
                var piece = _pieces[pieceId];
                AttachPieceToLayer(piece, false);
                piece.CurrentSlot = slot;
                piece.RectTransform.anchoredPosition = GetSlotPosition(slot);
                piece.RectTransform.localScale = Vector3.one;
                piece.CanvasGroup.alpha = 1f;
                piece.CanvasGroup.blocksRaycasts = true;
            }

            LayoutGrid(true);

            _board.localScale = Vector3.one;
            _boardImage.color = BoardColor;
            _completionCanvasGroup.alpha = 0f;
            _completionBadgeRect.localScale = Vector3.one * 0.88f;
            _completionBadgeRect.gameObject.SetActive(false);
            _statusText.text = "ピースをドラッグして場所を交換";
            UpdateMoveCount();

            if (wasStarted)
            {
                Restarted?.Invoke();
            }
        }

        public void SetInteractable(bool interactable)
        {
            _hostInteractable = interactable;
        }

        public void SetPuzzleImage(Texture2D image, bool restart = true)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            puzzleImage = image;
            _activePuzzleImage = image;
            ValidatePuzzleImage(image);

            if (!_isBuilt)
            {
                return;
            }

            for (var pieceId = 0; pieceId < PuzzleBoardState.PieceCount; pieceId++)
            {
                var piece = _pieces[pieceId];
                piece.SetImage(image, PuzzleImageSlicer.GetUvRect(pieceId));

                var fallbackNumber = piece.RectTransform.Find("Fallback Number");
                if (fallbackNumber != null)
                {
                    fallbackNumber.gameObject.SetActive(false);
                }
            }

            if (restart)
            {
                Restart();
            }
        }

        internal void BeginPieceDrag(PuzzlePieceView piece, PointerEventData eventData)
        {
            if (!CanMovePieces || _activeDrag != null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _activeDrag = piece;
            _activePointerId = eventData.pointerId;
            piece.CanvasGroup.blocksRaycasts = false;
            piece.RectTransform.SetParent(_dragLayer, false);
            SetCenteredAnchors(piece.RectTransform);
            piece.RectTransform.sizeDelta = Vector2.one * _pieceSize;
            piece.RectTransform.SetAsLastSibling();
            piece.RectTransform.localScale = Vector3.one * 1.055f;
            UpdateDragPosition(piece, eventData);
        }

        internal void DragPiece(PuzzlePieceView piece, PointerEventData eventData)
        {
            if (_activeDrag != piece || _activePointerId != eventData.pointerId)
            {
                return;
            }

            UpdateDragPosition(piece, eventData);
        }

        internal void EndPieceDrag(PuzzlePieceView piece, PointerEventData eventData)
        {
            if (_activeDrag != piece || _activePointerId != eventData.pointerId)
            {
                return;
            }

            _activeDrag = null;
            _activePointerId = int.MinValue;
            piece.CanvasGroup.blocksRaycasts = true;
            AttachPieceToLayer(piece, true);
            StartCoroutine(ReturnPieceRoutine(piece));
        }

        internal void DropPieceOnSlot(PuzzlePieceView draggedPiece, int targetSlot, PointerEventData eventData)
        {
            if (_activeDrag != draggedPiece || _activePointerId != eventData.pointerId || !CanMovePieces)
            {
                return;
            }

            var sourceSlot = draggedPiece.CurrentSlot;
            if (sourceSlot == targetSlot || targetSlot < 0 || targetSlot >= PuzzleBoardState.PieceCount)
            {
                return;
            }

            var targetPieceId = _state.GetPieceAt(targetSlot);
            var targetPiece = _pieces[targetPieceId];
            if (!_state.TrySwapSlots(sourceSlot, targetSlot))
            {
                return;
            }

            _activeDrag = null;
            _activePointerId = int.MinValue;
            draggedPiece.CanvasGroup.blocksRaycasts = true;
            AttachPieceToLayer(draggedPiece, true);

            draggedPiece.CurrentSlot = targetSlot;
            targetPiece.CurrentSlot = sourceSlot;
            UpdateMoveCount();
            PieceMoved?.Invoke(sourceSlot, targetSlot, _state.MoveCount);
            StartCoroutine(SwapPiecesRoutine(draggedPiece, targetPiece));
        }

        private void BuildInterface()
        {
            if (_root.parent is RectTransform)
            {
                Stretch(_root);
            }

            var background = CreateImage("Background", _root, BackgroundColor, false);
            Stretch(background.rectTransform);

            var accentTop = CreateImage("Accent Top", _root, BackgroundAccentColor, false);
            var accentTopRect = accentTop.rectTransform;
            accentTopRect.anchorMin = new Vector2(0f, 1f);
            accentTopRect.anchorMax = new Vector2(1f, 1f);
            accentTopRect.pivot = new Vector2(0.5f, 1f);
            accentTopRect.sizeDelta = new Vector2(0f, 8f);

            _safeArea = CreateRect("Safe Area", _root);
            Stretch(_safeArea);

            _header = CreateRect("Header", _safeArea);

            var kickerImage = CreateImage("Kicker", _header, BoardCompleteColor, false);
            var kickerRect = kickerImage.rectTransform;
            kickerRect.anchorMin = kickerRect.anchorMax = new Vector2(0f, 1f);
            kickerRect.pivot = new Vector2(0f, 1f);
            kickerRect.sizeDelta = new Vector2(178f, 32f);
            var kicker = CreateText("Label", kickerRect, "PIECE PUZZLE", 18, FontStyle.Bold, TextAnchor.MiddleCenter, NavyColor);
            Stretch(kicker.rectTransform, 4f);

            var title = CreateText("Title", _header, "3 × 3  PUZZLE", 48, FontStyle.Bold, TextAnchor.MiddleLeft, PrimaryTextColor);
            _titleRect = title.rectTransform;

            var subtitle = CreateText("Subtitle", _header, "9枚を正しい順番に並べて、1枚の絵を完成させよう", 19, FontStyle.Normal, TextAnchor.MiddleLeft, SecondaryTextColor);
            _subtitleRect = subtitle.rectTransform;

            var moveBadgeImage = CreateImage("Move Counter", _header, BackgroundAccentColor, false);
            _moveBadge = moveBadgeImage.rectTransform;
            var moveLabel = CreateText("Label", _moveBadge, "MOVES", 14, FontStyle.Bold, TextAnchor.UpperCenter, SecondaryTextColor);
            moveLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            moveLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            moveLabel.rectTransform.offsetMin = new Vector2(0f, -4f);
            moveLabel.rectTransform.offsetMax = new Vector2(0f, -7f);
            _moveCountText = CreateText("Value", _moveBadge, "00", 30, FontStyle.Bold, TextAnchor.LowerCenter, PrimaryTextColor);
            _moveCountText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _moveCountText.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            _moveCountText.rectTransform.offsetMin = new Vector2(0f, 4f);
            _moveCountText.rectTransform.offsetMax = Vector2.zero;

            var shadowImage = CreateImage("Board Shadow", _safeArea, new Color(0f, 0f, 0f, 0.28f), false);
            _boardShadow = shadowImage.rectTransform;

            _boardImage = CreateImage("Board", _safeArea, BoardColor, false);
            _board = _boardImage.rectTransform;

            var boardInner = CreateImage("Board Inner", _board, BackgroundColor, false);
            Stretch(boardInner.rectTransform, 8f);

            _slotLayer = CreateRect("Slots", _board);
            Stretch(_slotLayer);
            _pieceLayer = CreateRect("Pieces", _board);
            Stretch(_pieceLayer);

            for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
            {
                var slotImage = CreateImage($"Slot {slot + 1}", _slotLayer, SlotColor, true);
                var slotView = slotImage.gameObject.AddComponent<PuzzleSlotView>();
                slotView.Initialize(this, slot);
                _slots[slot] = slotView;
            }

            _activePuzzleImage = ResolvePuzzleImage();
            for (var pieceId = 0; pieceId < PuzzleBoardState.PieceCount; pieceId++)
            {
                var pieceObject = CreateUiObject($"Piece {pieceId + 1}", _pieceLayer, typeof(RawImage), typeof(CanvasGroup), typeof(PuzzlePieceView));
                var rawImage = pieceObject.GetComponent<RawImage>();
                rawImage.color = Color.white;
                rawImage.raycastTarget = true;

                var piece = pieceObject.GetComponent<PuzzlePieceView>();
                piece.Initialize(
                    this,
                    pieceId,
                    _activePuzzleImage == null ? Texture2D.whiteTexture : _activePuzzleImage,
                    PuzzleImageSlicer.GetUvRect(pieceId));
                _pieces[pieceId] = piece;

                if (_activePuzzleImage == null)
                {
                    rawImage.color = FromHex("#FFF4DC");
                    var fallback = CreateText("Fallback Number", piece.RectTransform, (pieceId + 1).ToString(), 96, FontStyle.Bold, TextAnchor.MiddleCenter, NavyColor);
                    Stretch(fallback.rectTransform);
                }
            }

            _dragLayer = CreateRect("Drag Layer", _root);
            Stretch(_dragLayer);

            _footer = CreateRect("Footer", _safeArea);
            _statusText = CreateText("Status", _footer, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleCenter, SecondaryTextColor);
            _statusRect = _statusText.rectTransform;

            var completionImage = CreateImage("Completion Badge", _safeArea, BoardCompleteColor, false);
            _completionBadgeRect = completionImage.rectTransform;
            _completionCanvasGroup = completionImage.gameObject.AddComponent<CanvasGroup>();
            _completionText = CreateText("Text", _completionBadgeRect, "完成！", 25, FontStyle.Bold, TextAnchor.MiddleCenter, NavyColor);
            Stretch(_completionText.rectTransform, 8f);
            _completionBadgeRect.gameObject.SetActive(false);

            _isBuilt = true;
            Canvas.ForceUpdateCanvases();
            ApplyResponsiveLayout();
        }

        private Texture2D ResolvePuzzleImage()
        {
            var resolvedImage = puzzleImage == null
                ? Resources.Load<Texture2D>(DefaultPuzzleImageResourcePath)
                : puzzleImage;

            if (resolvedImage == null)
            {
                Debug.LogWarning(
                    $"Puzzle image was not found at Resources/{DefaultPuzzleImageResourcePath}. Number fallbacks will be used.",
                    this);
                return null;
            }

            ValidatePuzzleImage(resolvedImage);
            return resolvedImage;
        }

        private void ValidatePuzzleImage(Texture2D image)
        {
            if (image.width != image.height)
            {
                Debug.LogWarning(
                    $"Puzzle image '{image.name}' is {image.width} x {image.height}. A square image is recommended because the puzzle board is square.",
                    this);
            }

            if (image.width % PuzzleImageSlicer.GridSize != 0 || image.height % PuzzleImageSlicer.GridSize != 0)
            {
                Debug.LogWarning(
                    $"Puzzle image '{image.name}' dimensions should be divisible by {PuzzleImageSlicer.GridSize} to keep all pieces pixel-aligned.",
                    this);
            }
        }

        private void ApplyResponsiveLayout()
        {
            if (!_isBuilt || _safeArea == null || _slots[0] == null || _pieces[0] == null || _isApplyingLayout)
            {
                return;
            }

            _isApplyingLayout = true;
            ApplySafeAreaAnchors();
            Canvas.ForceUpdateCanvases();

            var width = Mathf.Max(360f, _safeArea.rect.width);
            var height = Mathf.Max(540f, _safeArea.rect.height);
            var horizontalMargin = Mathf.Clamp(width * 0.045f, 28f, 72f);
            var headerWidth = Mathf.Min(width - horizontalMargin * 2f, 980f);
            const float headerHeight = 128f;
            const float headerTop = 24f;
            const float footerHeight = 62f;
            const float footerBottom = 22f;

            SetCenteredTop(_header, new Vector2(headerWidth, headerHeight), headerTop);

            var kicker = (RectTransform)_header.Find("Kicker");
            kicker.anchoredPosition = Vector2.zero;

            _titleRect.anchorMin = _titleRect.anchorMax = new Vector2(0f, 1f);
            _titleRect.pivot = new Vector2(0f, 1f);
            _titleRect.anchoredPosition = new Vector2(0f, -34f);
            _titleRect.sizeDelta = new Vector2(headerWidth - 180f, 55f);

            _subtitleRect.anchorMin = _subtitleRect.anchorMax = new Vector2(0f, 1f);
            _subtitleRect.pivot = new Vector2(0f, 1f);
            _subtitleRect.anchoredPosition = new Vector2(0f, -88f);
            _subtitleRect.sizeDelta = new Vector2(headerWidth - 180f, 34f);

            _moveBadge.anchorMin = _moveBadge.anchorMax = new Vector2(1f, 1f);
            _moveBadge.pivot = new Vector2(1f, 1f);
            _moveBadge.anchoredPosition = Vector2.zero;
            _moveBadge.sizeDelta = new Vector2(142f, 78f);

            var boardTop = height - headerTop - headerHeight - 24f;
            var boardBottom = footerBottom + footerHeight + 24f;
            var availableBoardHeight = Mathf.Max(260f, boardTop - boardBottom);
            _boardSize = Mathf.Min(760f, width - horizontalMargin * 2f, availableBoardHeight);
            var boardCenterFromBottom = boardBottom + _boardSize * 0.5f;
            var boardY = boardCenterFromBottom - height * 0.5f;

            SetCentered(_board, Vector2.one * _boardSize, new Vector2(0f, boardY));
            SetCentered(_boardShadow, Vector2.one * _boardSize, new Vector2(10f, boardY - 12f));

            LayoutGrid(_hasStarted && !_isAnimating);

            var footerWidth = Mathf.Min(width - horizontalMargin * 2f, 760f);
            _footer.anchorMin = _footer.anchorMax = new Vector2(0.5f, 0f);
            _footer.pivot = new Vector2(0.5f, 0f);
            _footer.anchoredPosition = new Vector2(0f, footerBottom);
            _footer.sizeDelta = new Vector2(footerWidth, footerHeight);

            _statusRect.anchorMin = Vector2.zero;
            _statusRect.anchorMax = Vector2.one;
            _statusRect.offsetMin = Vector2.zero;
            _statusRect.offsetMax = Vector2.zero;

            _completionBadgeRect.anchorMin = _completionBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            _completionBadgeRect.pivot = new Vector2(0.5f, 0.5f);
            _completionBadgeRect.anchoredPosition = new Vector2(0f, boardY + _boardSize * 0.5f + 38f);
            _completionBadgeRect.sizeDelta = new Vector2(Mathf.Min(280f, _boardSize * 0.56f), 68f);
            _isApplyingLayout = false;
        }

        private void ApplySafeAreaAnchors()
        {
            var isDirectCanvasChild = _root.parent != null && _root.parent.GetComponent<Canvas>() != null;
            if (!isDirectCanvasChild || Screen.width <= 0 || Screen.height <= 0)
            {
                _safeArea.anchorMin = Vector2.zero;
                _safeArea.anchorMax = Vector2.one;
                _safeArea.offsetMin = Vector2.zero;
                _safeArea.offsetMax = Vector2.zero;
                return;
            }

            var safe = Screen.safeArea;
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        private Vector2 GetSlotPosition(int slotIndex)
        {
            var row = slotIndex / 3;
            var column = slotIndex % 3;
            var firstCenter = -(_pieceSize + _currentPieceGap);
            return new Vector2(
                firstCenter + column * (_pieceSize + _currentPieceGap),
                -firstCenter - row * (_pieceSize + _currentPieceGap));
        }

        private void LayoutGrid(bool includePieces)
        {
            var usableSize = _boardSize - BoardInset * 2f;
            _pieceSize = (usableSize - _currentPieceGap * 2f) / 3f;

            for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
            {
                var slotRect = (RectTransform)_slots[slot].transform;
                SetCentered(slotRect, Vector2.one * _pieceSize, GetSlotPosition(slot));
            }

            if (!includePieces)
            {
                return;
            }

            for (var pieceId = 0; pieceId < PuzzleBoardState.PieceCount; pieceId++)
            {
                var piece = _pieces[pieceId];
                if (piece != _activeDrag)
                {
                    SetCentered(piece.RectTransform, Vector2.one * _pieceSize, GetSlotPosition(piece.CurrentSlot));
                }
            }
        }

        private void UpdateDragPosition(PuzzlePieceView piece, PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                piece.RectTransform.anchoredPosition = localPoint;
            }
        }

        private IEnumerator ReturnPieceRoutine(PuzzlePieceView piece)
        {
            _isAnimating = true;
            var startPosition = piece.RectTransform.anchoredPosition;
            var endPosition = GetSlotPosition(piece.CurrentSlot);
            var elapsed = 0f;

            while (elapsed < MoveAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var amount = EaseOutCubic(Mathf.Clamp01(elapsed / MoveAnimationDuration));
                piece.RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, amount);
                piece.RectTransform.localScale = Vector3.LerpUnclamped(Vector3.one * 1.055f, Vector3.one, amount);
                yield return null;
            }

            piece.RectTransform.anchoredPosition = endPosition;
            piece.RectTransform.localScale = Vector3.one;
            _isAnimating = false;
        }

        private IEnumerator SwapPiecesRoutine(PuzzlePieceView draggedPiece, PuzzlePieceView displacedPiece)
        {
            _isAnimating = true;
            var draggedStart = draggedPiece.RectTransform.anchoredPosition;
            var displacedStart = displacedPiece.RectTransform.anchoredPosition;
            var draggedEnd = GetSlotPosition(draggedPiece.CurrentSlot);
            var displacedEnd = GetSlotPosition(displacedPiece.CurrentSlot);
            var elapsed = 0f;

            while (elapsed < MoveAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var amount = EaseOutCubic(Mathf.Clamp01(elapsed / MoveAnimationDuration));
                draggedPiece.RectTransform.anchoredPosition = Vector2.LerpUnclamped(draggedStart, draggedEnd, amount);
                displacedPiece.RectTransform.anchoredPosition = Vector2.LerpUnclamped(displacedStart, displacedEnd, amount);
                draggedPiece.RectTransform.localScale = Vector3.LerpUnclamped(Vector3.one * 1.055f, Vector3.one, amount);
                yield return null;
            }

            draggedPiece.RectTransform.anchoredPosition = draggedEnd;
            displacedPiece.RectTransform.anchoredPosition = displacedEnd;
            draggedPiece.RectTransform.localScale = Vector3.one;
            _isAnimating = false;

            if (_state.IsComplete && !_isCompleted)
            {
                BeginCompletion();
            }
        }

        private void BeginCompletion()
        {
            var completedMoveCount = _state.MoveCount;
            _isCompleted = true;
            _isAnimating = true;
            _slotLayer.gameObject.SetActive(false);
            _completionText.text = $"完成！  {completedMoveCount}手";
            _statusText.text = "すべてのピースがつながりました";
            _completionBadgeRect.gameObject.SetActive(true);
            StartCoroutine(CompletionAnimationRoutine());
            Completed?.Invoke(completedMoveCount);
            onCompleted.Invoke(completedMoveCount);
        }

        private IEnumerator CompletionAnimationRoutine()
        {
            const float duration = 0.56f;
            var elapsed = 0f;
            var startGap = _currentPieceGap;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(normalized);
                _currentPieceGap = Mathf.Lerp(startGap, 0f, EaseOutCubic(normalized));
                LayoutGrid(true);
                _completionCanvasGroup.alpha = Mathf.Clamp01(normalized * 2.4f);
                _completionBadgeRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, eased);
                _board.localScale = Vector3.one * Mathf.LerpUnclamped(1f, CompletedBoardScale, eased);
                _boardImage.color = Color.Lerp(BoardColor, BoardCompleteColor, Mathf.Clamp01(normalized * 1.7f));
                yield return null;
            }

            _completionCanvasGroup.alpha = 1f;
            _completionBadgeRect.localScale = Vector3.one;
            _board.localScale = Vector3.one * CompletedBoardScale;
            _boardImage.color = BoardCompleteColor;
            _currentPieceGap = 0f;
            LayoutGrid(true);
            _isAnimating = false;
        }

        private void CancelActiveDragImmediately()
        {
            if (_activeDrag == null)
            {
                return;
            }

            _activeDrag.CanvasGroup.blocksRaycasts = true;
            AttachPieceToLayer(_activeDrag, false);
            _activeDrag = null;
            _activePointerId = int.MinValue;
        }

        private void AttachPieceToLayer(PuzzlePieceView piece, bool preserveWorldPosition)
        {
            var worldPosition = piece.RectTransform.position;
            piece.RectTransform.SetParent(_pieceLayer, false);
            SetCenteredAnchors(piece.RectTransform);
            piece.RectTransform.sizeDelta = Vector2.one * _pieceSize;

            if (preserveWorldPosition)
            {
                piece.RectTransform.position = worldPosition;
            }
        }

        private void UpdateMoveCount()
        {
            _moveCountText.text = _state.MoveCount.ToString("00");
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            var textObject = CreateUiObject(name, parent, typeof(Text));
            var text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            var imageObject = CreateUiObject(name, parent, typeof(Image));
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            return CreateUiObject(name, parent).GetComponent<RectTransform>();
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] componentTypes)
        {
            var allTypes = new Type[componentTypes.Length + 1];
            allTypes[0] = typeof(RectTransform);
            Array.Copy(componentTypes, 0, allTypes, 1, componentTypes.Length);
            var gameObject = new GameObject(name, allTypes);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
        {
            SetCenteredAnchors(rect);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetCenteredTop(RectTransform rect, Vector2 size, float top)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private static void SetCenteredAnchors(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private static float EaseOutCubic(float value)
        {
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            var shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private static Color FromHex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.magenta;
        }
    }
}
