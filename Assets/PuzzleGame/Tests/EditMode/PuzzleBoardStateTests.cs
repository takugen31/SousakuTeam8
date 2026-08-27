using System.Collections.Generic;
using NUnit.Framework;

namespace SousakuTeam8.PuzzleGame.Tests
{
    public sealed class PuzzleBoardStateTests
    {
        [Test]
        public void ShuffleProducesCompletePermutationAndNeverStartsSolved()
        {
            for (var seed = 0; seed < 128; seed++)
            {
                var state = new PuzzleBoardState();
                state.Shuffle(seed);
                var pieces = new HashSet<int>();

                for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
                {
                    pieces.Add(state.GetPieceAt(slot));
                }

                Assert.That(pieces.Count, Is.EqualTo(PuzzleBoardState.PieceCount), $"seed: {seed}");
                Assert.That(state.IsComplete, Is.False, $"seed: {seed}");
                Assert.That(state.MoveCount, Is.Zero, $"seed: {seed}");
            }
        }

        [Test]
        public void FixedSeedProducesTheSameArrangement()
        {
            var first = new PuzzleBoardState();
            var second = new PuzzleBoardState();
            first.Shuffle(20260814);
            second.Shuffle(20260814);

            for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
            {
                Assert.That(first.GetPieceAt(slot), Is.EqualTo(second.GetPieceAt(slot)));
            }
        }

        [Test]
        public void ValidSwapExchangesPiecesAndCountsOneMove()
        {
            var state = new PuzzleBoardState();
            var firstPiece = state.GetPieceAt(0);
            var secondPiece = state.GetPieceAt(8);

            var changed = state.TrySwapSlots(0, 8);

            Assert.That(changed, Is.True);
            Assert.That(state.GetPieceAt(0), Is.EqualTo(secondPiece));
            Assert.That(state.GetPieceAt(8), Is.EqualTo(firstPiece));
            Assert.That(state.MoveCount, Is.EqualTo(1));
            Assert.That(state.IsComplete, Is.False);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(9, 0)]
        [TestCase(0, 9)]
        [TestCase(4, 4)]
        public void InvalidSwapLeavesBoardAndMoveCountUnchanged(int firstSlot, int secondSlot)
        {
            var state = new PuzzleBoardState();

            Assert.That(state.TrySwapSlots(firstSlot, secondSlot), Is.False);
            Assert.That(state.MoveCount, Is.Zero);
            Assert.That(state.IsComplete, Is.True);
        }

        [Test]
        public void SwappingTheSamePairTwiceReturnsToCompletedState()
        {
            var state = new PuzzleBoardState();

            state.TrySwapSlots(1, 7);
            Assert.That(state.IsComplete, Is.False);

            state.TrySwapSlots(1, 7);
            Assert.That(state.IsComplete, Is.True);
            Assert.That(state.MoveCount, Is.EqualTo(2));
        }

        [Test]
        public void FindSlotForPieceMatchesEveryBoardEntry()
        {
            var state = new PuzzleBoardState();
            state.Shuffle(42);

            for (var slot = 0; slot < PuzzleBoardState.PieceCount; slot++)
            {
                Assert.That(state.FindSlotForPiece(state.GetPieceAt(slot)), Is.EqualTo(slot));
            }
        }
    }
}
