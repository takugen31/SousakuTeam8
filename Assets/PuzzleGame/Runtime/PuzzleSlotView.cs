using UnityEngine;
using UnityEngine.EventSystems;

namespace SousakuTeam8.PuzzleGame
{
    [DisallowMultipleComponent]
    public sealed class PuzzleSlotView : MonoBehaviour, IDropHandler
    {
        private PuzzleGameController _controller;

        public int SlotIndex { get; private set; }

        internal void Initialize(PuzzleGameController controller, int slotIndex)
        {
            _controller = controller;
            SlotIndex = slotIndex;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var draggedPiece = eventData.pointerDrag == null
                ? null
                : eventData.pointerDrag.GetComponent<PuzzlePieceView>();

            if (draggedPiece != null)
            {
                _controller.DropPieceOnSlot(draggedPiece, SlotIndex, eventData);
            }
        }
    }
}
