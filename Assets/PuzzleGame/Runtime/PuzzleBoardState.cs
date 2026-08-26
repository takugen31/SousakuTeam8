using System;

namespace SousakuTeam8.PuzzleGame
{
    /// <summary>
    /// Pure C# model for the 3 x 3 puzzle. Slot and piece indices are both zero based.
    /// </summary>
    public sealed class PuzzleBoardState
    {
        public const int PieceCount = 9;

        private readonly int[] _slotToPiece = new int[PieceCount];

        public PuzzleBoardState()
        {
            ResetSolved();
        }

        public int MoveCount { get; private set; }

        public bool IsComplete
        {
            get
            {
                for (var slot = 0; slot < PieceCount; slot++)
                {
                    if (_slotToPiece[slot] != slot)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public int GetPieceAt(int slotIndex)
        {
            ValidateIndex(slotIndex, nameof(slotIndex));
            return _slotToPiece[slotIndex];
        }

        public int FindSlotForPiece(int pieceId)
        {
            ValidateIndex(pieceId, nameof(pieceId));

            for (var slot = 0; slot < PieceCount; slot++)
            {
                if (_slotToPiece[slot] == pieceId)
                {
                    return slot;
                }
            }

            throw new InvalidOperationException($"Piece {pieceId} is missing from the board.");
        }

        public void Shuffle()
        {
            Shuffle(Environment.TickCount);
        }

        public void Shuffle(int seed)
        {
            ResetSolved();
            var random = new Random(seed);

            for (var index = PieceCount - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                SwapValues(index, swapIndex);
            }

            // A puzzle must never begin already solved, including fixed-seed tests.
            if (IsComplete)
            {
                SwapValues(0, 1);
            }

            MoveCount = 0;
        }

        public bool TrySwapSlots(int firstSlot, int secondSlot)
        {
            if (!IsValidIndex(firstSlot) || !IsValidIndex(secondSlot) || firstSlot == secondSlot)
            {
                return false;
            }

            SwapValues(firstSlot, secondSlot);
            MoveCount++;
            return true;
        }

        public void ResetSolved()
        {
            for (var index = 0; index < PieceCount; index++)
            {
                _slotToPiece[index] = index;
            }

            MoveCount = 0;
        }

        private static bool IsValidIndex(int index)
        {
            return index >= 0 && index < PieceCount;
        }

        private static void ValidateIndex(int index, string parameterName)
        {
            if (!IsValidIndex(index))
            {
                throw new ArgumentOutOfRangeException(parameterName, index, $"Index must be between 0 and {PieceCount - 1}.");
            }
        }

        private void SwapValues(int firstSlot, int secondSlot)
        {
            var firstPiece = _slotToPiece[firstSlot];
            _slotToPiece[firstSlot] = _slotToPiece[secondSlot];
            _slotToPiece[secondSlot] = firstPiece;
        }
    }
}
