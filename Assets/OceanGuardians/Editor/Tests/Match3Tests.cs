using NUnit.Framework;
using UnityEngine;
using OceanGuardians.MiniGames.Match3;

namespace OceanGuardians.EditorTools.Tests
{
    /// <summary>Pure-logic Edit Mode tests for the Recycle Sorter board and round rules.</summary>
    public class Match3Tests
    {
        const Match3PieceType P = Match3PieceType.Plastic;
        const Match3PieceType M = Match3PieceType.Metal;
        const Match3PieceType A = Match3PieceType.Paper;
        const Match3PieceType G = Match3PieceType.Glass;
        const Match3PieceType N = Match3PieceType.GhostNet;

        /// <summary>6x6 pattern with no matches: type = (x + 2y) % 4 never repeats 3 in a line.</summary>
        static Match3Board PatternBoard(int seed = 1)
        {
            var board = new Match3Board(seed, ghostNets: 0);
            var types = new[] { P, M, A, G };
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    board.SetCell(x, y, types[(x + 2 * y) % 4]);
            return board;
        }

        [Test]
        public void NewBoardIsFullWithNoPreExistingMatches()
        {
            var board = new Match3Board(42);
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    Assert.IsNotNull(board.CellAt(x, y), $"cell {x},{y} filled");
            Assert.AreEqual(0, board.FindMatches().Count);
        }

        [Test]
        public void SwapFormingHorizontalMatchSucceedsAndClears()
        {
            var board = PatternBoard();
            // Row 5 (bottom): make P P M P pattern pieces we can swap into PPP.
            board.SetCell(0, 5, P); board.SetCell(1, 5, P); board.SetCell(2, 5, M); board.SetCell(3, 5, P);
            board.SetCell(2, 4, A); // piece above the M is not P, so vertical alternative won't pre-match
            var result = board.TrySwap(new Vector2Int(2, 5), new Vector2Int(3, 5)); // wait: swapping M with P gives P P P M? cells 0,1 = P, cell2 becomes P -> PPP
            Assert.IsTrue(result.Success);
            Assert.Greater(result.TotalCleared(P), 2, "at least the 3 plastics cleared");
            AssertNoEmpties(board);
        }

        [Test]
        public void SwapWithoutMatchSnapsBack()
        {
            var board = PatternBoard();
            var beforeA = board.CellAt(0, 0).Type;
            var beforeB = board.CellAt(1, 0).Type;
            var result = board.TrySwap(new Vector2Int(0, 0), new Vector2Int(1, 0));
            Assert.IsFalse(result.Success);
            Assert.AreEqual(beforeA, board.CellAt(0, 0).Type);
            Assert.AreEqual(beforeB, board.CellAt(1, 0).Type);
        }

        [Test]
        public void VerticalMatchClears()
        {
            var board = PatternBoard();
            board.SetCell(4, 0, G); board.SetCell(4, 1, G); board.SetCell(4, 2, M); board.SetCell(5, 2, G);
            board.SetCell(5, 0, P); board.SetCell(5, 1, A); // avoid extra matches in column 5
            var result = board.TrySwap(new Vector2Int(4, 2), new Vector2Int(5, 2)); // G drops into column 4 -> GGG vertical
            Assert.IsTrue(result.Success);
            Assert.GreaterOrEqual(result.TotalCleared(G), 3);
            AssertNoEmpties(board);
        }

        [Test]
        public void MatchFourSpawnsSkimmer()
        {
            var board = PatternBoard();
            board.SetCell(0, 5, M); board.SetCell(1, 5, M); board.SetCell(2, 5, M); board.SetCell(3, 5, P); board.SetCell(4, 5, A);
            board.SetCell(3, 4, M); board.SetCell(4, 4, G); board.SetCell(2, 4, A); board.SetCell(1, 4, P); board.SetCell(0, 4, G);
            var result = board.TrySwap(new Vector2Int(3, 5), new Vector2Int(3, 4)); // M falls in: M M M M horizontal
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.SpawnedSkimmer, "match-4 spawns an Eco-Skimmer");
            bool anySkimmer = false;
            for (int y = 0; y < 6 && !anySkimmer; y++)
                for (int x = 0; x < 6; x++)
                    if (board.CellAt(x, y)?.Power is PowerKind.SkimmerH or PowerKind.SkimmerV) { anySkimmer = true; break; }
            Assert.IsTrue(anySkimmer, "skimmer piece sits on the board");
        }

        [Test]
        public void MatchFiveSpawnsCompactor()
        {
            var board = PatternBoard();
            board.SetCell(0, 5, P); board.SetCell(1, 5, P); board.SetCell(2, 5, P); board.SetCell(3, 5, M); board.SetCell(4, 5, P); board.SetCell(5, 5, A);
            board.SetCell(3, 4, P); board.SetCell(0, 4, A); board.SetCell(1, 4, G); board.SetCell(2, 4, M); board.SetCell(4, 4, G); board.SetCell(5, 4, M);
            var result = board.TrySwap(new Vector2Int(3, 5), new Vector2Int(3, 4)); // P lands: P P P P P
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.SpawnedCompactor, "match-5 spawns an Eco-Compactor");
        }

        [Test]
        public void GhostNetCannotMoveButAdjacentMatchFreesIt()
        {
            var board = PatternBoard();
            board.SetCell(2, 2, N);
            Assert.IsFalse(board.TrySwap(new Vector2Int(2, 2), new Vector2Int(3, 2)).Success, "ghost nets are immovable");
            board.SetCell(2, 5, P); board.SetCell(2, 4, P); board.SetCell(2, 3, M); board.SetCell(1, 3, P);
            board.SetCell(1, 5, A); board.SetCell(1, 4, G);
            var result = board.TrySwap(new Vector2Int(2, 3), new Vector2Int(1, 3)); // P into column 2 -> PPP at rows 3-5, adjacent to net at (2,2)
            Assert.IsTrue(result.Success);
            Assert.GreaterOrEqual(result.GhostNetsFreed, 1, "adjacent match frees the net");
            Assert.AreNotEqual(N, board.CellAt(2, 2).Type);
        }

        static void AssertNoEmpties(Match3Board board)
        {
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    Assert.IsNotNull(board.CellAt(x, y), $"gravity+refill left {x},{y} empty");
        }

        [Test]
        public void RoundVictoryAndOutOfMovesResolution()
        {
            var round = new SorterRound(new System.Collections.Generic.Dictionary<Match3PieceType, int> { { P, 3 } }, moves: 2);
            Assert.AreEqual(SorterState.Playing, round.State);
            round.ApplyMove(new System.Collections.Generic.Dictionary<Match3PieceType, int> { { P, 2 } }, cascades: 1);
            Assert.AreEqual(SorterState.Playing, round.State);
            Assert.AreEqual(1, round.MovesLeft);
            round.ApplyMove(new System.Collections.Generic.Dictionary<Match3PieceType, int> { { P, 1 } }, cascades: 1);
            Assert.AreEqual(SorterState.Won, round.State, "quota met on the last move still wins");

            var lost = new SorterRound(new System.Collections.Generic.Dictionary<Match3PieceType, int> { { P, 99 } }, moves: 1);
            lost.ApplyMove(new System.Collections.Generic.Dictionary<Match3PieceType, int> { { P, 4 }, { M, 3 } }, cascades: 2);
            Assert.AreEqual(SorterState.OutOfMoves, lost.State);
            Assert.AreEqual(7, lost.TotalMatched, "matched pieces convert to base credits");
        }

        [Test]
        public void BonusMathIsTwentyFivePercent()
        {
            Assert.AreEqual(25, RecycleSorterGame.BonusCredits(100));
            Assert.AreEqual(3, RecycleSorterGame.BonusCredits(10), "rounds up so the bonus never vanishes");
            Assert.AreEqual(0, RecycleSorterGame.BonusCredits(0));
        }
    }
}
