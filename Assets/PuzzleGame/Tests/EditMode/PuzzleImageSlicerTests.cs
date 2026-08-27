using System;
using NUnit.Framework;
using UnityEngine;

namespace SousakuTeam8.PuzzleGame.Tests
{
    public sealed class PuzzleImageSlicerTests
    {
        private const float Tolerance = 0.00001f;

        [TestCase(0, 0f, 2f / 3f)]
        [TestCase(2, 2f / 3f, 2f / 3f)]
        [TestCase(4, 1f / 3f, 1f / 3f)]
        [TestCase(6, 0f, 0f)]
        [TestCase(8, 2f / 3f, 0f)]
        public void GetUvRectMapsTopLeftPieceOrderToUnityUvCoordinates(int pieceId, float expectedX, float expectedY)
        {
            var uv = PuzzleImageSlicer.GetUvRect(pieceId);

            Assert.That(uv.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(uv.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(uv.width, Is.EqualTo(1f / 3f).Within(Tolerance));
            Assert.That(uv.height, Is.EqualTo(1f / 3f).Within(Tolerance));
        }

        [Test]
        public void NineUvRectsCoverTheWholeImageWithoutOverlapping()
        {
            var totalArea = 0f;

            for (var firstPieceId = 0; firstPieceId < PuzzleBoardState.PieceCount; firstPieceId++)
            {
                var first = PuzzleImageSlicer.GetUvRect(firstPieceId);
                Assert.That(first.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(first.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(first.xMax, Is.LessThanOrEqualTo(1f + Tolerance));
                Assert.That(first.yMax, Is.LessThanOrEqualTo(1f + Tolerance));
                totalArea += first.width * first.height;

                for (var secondPieceId = firstPieceId + 1; secondPieceId < PuzzleBoardState.PieceCount; secondPieceId++)
                {
                    var second = PuzzleImageSlicer.GetUvRect(secondPieceId);
                    var overlapWidth = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
                    var overlapHeight = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
                    Assert.That(overlapWidth <= Tolerance || overlapHeight <= Tolerance, Is.True);
                }
            }

            Assert.That(totalArea, Is.EqualTo(1f).Within(Tolerance));
        }

        [TestCase(-1)]
        [TestCase(9)]
        public void GetUvRectRejectsInvalidPieceIds(int pieceId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PuzzleImageSlicer.GetUvRect(pieceId));
        }
    }
}
