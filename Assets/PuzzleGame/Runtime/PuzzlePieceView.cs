using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SousakuTeam8.PuzzleGame
{
    [DisallowMultipleComponent]
    public sealed class PuzzlePieceView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private PuzzleGameController _controller;

        public int PieceId { get; private set; }
        public int CurrentSlot { get; internal set; }
        public RectTransform RectTransform { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }
        public RawImage Image { get; private set; }

        internal void Initialize(PuzzleGameController controller, int pieceId, Texture texture, Rect uvRect)
        {
            _controller = controller;
            PieceId = pieceId;
            RectTransform = (RectTransform)transform;
            CanvasGroup = GetComponent<CanvasGroup>();
            Image = GetComponent<RawImage>();
            SetImage(texture, uvRect);
        }

        internal void SetImage(Texture texture, Rect uvRect)
        {
            Image.texture = texture;
            Image.uvRect = uvRect;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _controller.BeginPieceDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _controller.DragPiece(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _controller.EndPieceDrag(this, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var draggedPiece = eventData.pointerDrag == null
                ? null
                : eventData.pointerDrag.GetComponent<PuzzlePieceView>();

            if (draggedPiece != null && draggedPiece != this)
            {
                _controller.DropPieceOnSlot(draggedPiece, CurrentSlot, eventData);
            }
        }
    }
}
