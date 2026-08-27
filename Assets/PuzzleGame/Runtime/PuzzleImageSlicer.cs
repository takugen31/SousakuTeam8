using System;
using UnityEngine;

namespace SousakuTeam8.PuzzleGame
{
    /// <summary>
    /// Maps the nine logical puzzle pieces to a 3 x 3 region of one source image.
    /// Piece IDs run from the top-left to the bottom-right.
    /// </summary>
    public static class PuzzleImageSlicer
    {
        public const int GridSize = 3;

        public static Rect GetUvRect(int pieceId)
        {
            if (pieceId < 0 || pieceId >= GridSize * GridSize)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceId), pieceId, "Piece ID must be between 0 and 8.");
            }

            const float pieceUvSize = 1f / GridSize;
            var rowFromTop = pieceId / GridSize;
            var column = pieceId % GridSize;

            // Texture UV coordinates start at the bottom-left, while piece IDs start at the top-left.
            return new Rect(
                column * pieceUvSize,
                (GridSize - 1 - rowFromTop) * pieceUvSize,
                pieceUvSize,
                pieceUvSize);
        }
    }
}
