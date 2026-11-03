using System;
using System.Collections.Generic;
using UnityEngine;

namespace OceanGuardians.MiniGames.Match3
{
    /// <summary>Tile kinds matching the kiosk bins, plus the GhostNet obstacle.</summary>
    public enum Match3PieceType { Plastic, Metal, Paper, Glass, GhostNet }

    public enum PowerKind { None, SkimmerH, SkimmerV, Compactor }

    /// <summary>One board tile. Id lets the UI track the same piece across falls/refills.</summary>
    public class Match3Piece
    {
        public int Id;
        public Match3PieceType Type;
        public PowerKind Power;
        public bool IsGhostNet => Type == Match3PieceType.GhostNet;
    }

    /// <summary>One cascade worth of animation/info for the UI.</summary>
    public class CascadeStep
    {
        public List<Vector2Int> Cleared = new();
        public List<Vector2Int> FreedGhostNets = new();
        public Vector2Int PowerSpawnAt = new(-1, -1);
        public PowerKind PowerSpawned = PowerKind.None;
        public int Score;
    }

    public class SwapResult
    {
        public bool Success;
        public List<CascadeStep> Steps = new();
        public Dictionary<Match3PieceType, int> ClearedByType = new();
        public int GhostNetsFreed;
        public int Score;
        public int Cascades => Steps.Count;
        public bool SpawnedSkimmer;
        public bool SpawnedCompactor;
        public int TotalCleared(Match3PieceType type) => ClearedByType.TryGetValue(type, out int n) ? n : 0;
        public int TotalMatched()
        {
            int total = 0;
            foreach (var kv in ClearedByType) total += kv.Value;
            return total;
        }
    }

    /// <summary>
    /// Pure-logic 6x6 match-3 board (no MonoBehaviour, fully EditMode-testable, seeded RNG).
    /// Coordinates: x = column 0..5 left→right, y = row 0..5 top→bottom (gravity pulls +y).
    /// Match-4 spawns an Eco-Skimmer (clears its row/column when matched), match-5 or a T/L
    /// intersection spawns an Eco-Compactor (3x3 blast). GhostNets never move or fall; a match
    /// touching one frees it. Resolve returns per-cascade steps so the UI can animate them.
    /// </summary>
    public class Match3Board
    {
        public const int Width = 6;
        public const int Height = 6;
        static readonly Match3PieceType[] NormalTypes =
            { Match3PieceType.Plastic, Match3PieceType.Metal, Match3PieceType.Paper, Match3PieceType.Glass };

        readonly Match3Piece[,] cells = new Match3Piece[Width, Height];
        readonly System.Random rng;
        int nextId = 1;

        public Match3Board(int seed, int ghostNets = 2)
        {
            rng = new System.Random(seed);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    cells[x, y] = NewPiece(RandomTypeAvoidingMatches(x, y));
            for (int i = 0; i < ghostNets; i++)
            {
                int x = rng.Next(Width), y = rng.Next(1, Height);
                if (!cells[x, y].IsGhostNet) cells[x, y] = NewPiece(Match3PieceType.GhostNet);
            }
        }

        Match3Piece NewPiece(Match3PieceType type) => new() { Id = nextId++, Type = type };

        Match3PieceType RandomTypeAvoidingMatches(int x, int y)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                var t = NormalTypes[rng.Next(NormalTypes.Length)];
                bool h = x >= 2 && cells[x - 1, y]?.Type == t && cells[x - 2, y]?.Type == t;
                bool v = y >= 2 && cells[x, y - 1]?.Type == t && cells[x, y - 2]?.Type == t;
                if (!h && !v) return t;
            }
            return NormalTypes[rng.Next(NormalTypes.Length)];
        }

        public Match3Piece CellAt(int x, int y) => InBounds(x, y) ? cells[x, y] : null;
        public static bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>Test/builder setup: overwrite a cell with a fresh piece.</summary>
        public void SetCell(int x, int y, Match3PieceType type, PowerKind power = PowerKind.None)
        {
            var p = NewPiece(type);
            p.Power = power;
            cells[x, y] = p;
        }

        // ---------- match detection ----------

        class MatchGroup
        {
            public HashSet<Vector2Int> Cells = new();
            public Match3PieceType Type;
            public int LongestRun;
            public bool HasIntersection;
        }

        List<MatchGroup> FindMatchGroups()
        {
            var runs = new List<List<Vector2Int>>();
            void Scan(bool horizontal)
            {
                for (int a = 0; a < (horizontal ? Height : Width); a++)
                {
                    int runStart = 0;
                    for (int b = 1; b <= (horizontal ? Width : Height); b++)
                    {
                        bool same = b < (horizontal ? Width : Height) && SameType(horizontal, a, b, runStart);
                        if (!same)
                        {
                            if (b - runStart >= 3)
                            {
                                var run = new List<Vector2Int>();
                                for (int i = runStart; i < b; i++) run.Add(horizontal ? new Vector2Int(i, a) : new Vector2Int(a, i));
                                runs.Add(run);
                            }
                            runStart = b;
                        }
                    }
                }
            }
            bool SameType(bool horizontal, int a, int b, int start)
            {
                var p = horizontal ? cells[b, a] : cells[a, b];
                var q = horizontal ? cells[start, a] : cells[a, start];
                return p != null && q != null && !p.IsGhostNet && p.Type == q.Type;
            }
            Scan(true); Scan(false);

            // Merge runs that share a cell (T/L/+ intersections become one group).
            var groups = new List<MatchGroup>();
            foreach (var run in runs)
            {
                MatchGroup target = null;
                foreach (var g in groups)
                {
                    bool shares = false;
                    foreach (var c in run) if (g.Cells.Contains(c)) { shares = true; break; }
                    if (shares) { target = g; break; }
                }
                if (target == null)
                {
                    target = new MatchGroup { Type = cells[run[0].x, run[0].y].Type };
                    groups.Add(target);
                }
                else target.HasIntersection = true;
                foreach (var c in run) target.Cells.Add(c);
                target.LongestRun = Math.Max(target.LongestRun, run.Count);
            }
            return groups;
        }

        /// <summary>All currently matched cell positions (used by tests/UI hints).</summary>
        public List<Vector2Int> FindMatches()
        {
            var all = new List<Vector2Int>();
            foreach (var g in FindMatchGroups()) all.AddRange(g.Cells);
            return all;
        }

        // ---------- swapping & resolution ----------

        public SwapResult TrySwap(Vector2Int a, Vector2Int b)
        {
            var result = new SwapResult();
            if (!InBounds(a.x, a.y) || !InBounds(b.x, b.y)) return result;
            if (Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) != 1) return result;
            var pa = cells[a.x, a.y]; var pb = cells[b.x, b.y];
            if (pa == null || pb == null || pa.IsGhostNet || pb.IsGhostNet) return result;

            (cells[a.x, a.y], cells[b.x, b.y]) = (pb, pa);
            if (FindMatchGroups().Count == 0)
            {
                (cells[a.x, a.y], cells[b.x, b.y]) = (pa, pb); // snap back
                return result;
            }
            result.Success = true;
            ResolveAll(result, preferSpawnAt: b);
            return result;
        }

        void ResolveAll(SwapResult result, Vector2Int preferSpawnAt)
        {
            int cascade = 0;
            while (true)
            {
                var groups = FindMatchGroups();
                if (groups.Count == 0) break;
                cascade++;
                var step = new CascadeStep();
                var toClear = new HashSet<Vector2Int>();
                Vector2Int spawnAt = new(-1, -1);
                var spawnPower = PowerKind.None;
                Match3PieceType spawnType = default;

                foreach (var g in groups)
                {
                    var power = g.HasIntersection || g.Cells.Count >= 5 ? PowerKind.Compactor
                        : g.LongestRun == 4 ? (IsHorizontal(g) ? PowerKind.SkimmerH : PowerKind.SkimmerV)
                        : PowerKind.None;
                    if (power != PowerKind.None && spawnPower == PowerKind.None)
                    {
                        spawnPower = power;
                        spawnType = g.Type;
                        spawnAt = g.Cells.Contains(preferSpawnAt) ? preferSpawnAt : FirstCell(g);
                    }
                    foreach (var c in g.Cells) toClear.Add(c);
                }

                ExpandPowerTriggers(toClear);

                // Free ghost nets touching anything cleared.
                foreach (var c in new List<Vector2Int>(toClear))
                    foreach (var d in Dirs)
                    {
                        var n = c + d;
                        if (InBounds(n.x, n.y) && cells[n.x, n.y]?.IsGhostNet == true && toClear.Add(n))
                            step.FreedGhostNets.Add(n);
                    }

                foreach (var c in toClear)
                {
                    var piece = cells[c.x, c.y];
                    if (piece == null) continue;
                    step.Cleared.Add(c);
                    result.Score += (piece.IsGhostNet ? 30 : 10) * cascade;
                    step.Score += (piece.IsGhostNet ? 30 : 10) * cascade;
                    if (piece.IsGhostNet) result.GhostNetsFreed++;
                    else result.ClearedByType[piece.Type] = result.TotalCleared(piece.Type) + 1;
                    cells[c.x, c.y] = null;
                }

                if (spawnPower != PowerKind.None)
                {
                    var spawned = NewPiece(spawnType);
                    spawned.Power = spawnPower;
                    cells[spawnAt.x, spawnAt.y] = spawned;
                    step.PowerSpawnAt = spawnAt;
                    step.PowerSpawned = spawnPower;
                    if (spawnPower == PowerKind.Compactor) result.SpawnedCompactor = true; else result.SpawnedSkimmer = true;
                }

                ApplyGravityAndRefill();
                result.Steps.Add(step);
                if (cascade > 20) break; // ponytail: safety valve against pathological refills
            }
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        static bool IsHorizontal(MatchGroup g)
        {
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in g.Cells) { minY = Math.Min(minY, c.y); maxY = Math.Max(maxY, c.y); }
            return minY == maxY;
        }

        static Vector2Int FirstCell(MatchGroup g)
        {
            foreach (var c in g.Cells) return c;
            return Vector2Int.zero;
        }

        /// <summary>Matched power pieces detonate: skimmers sweep a line, compactors blast 3x3 (chain-reacting).</summary>
        void ExpandPowerTriggers(HashSet<Vector2Int> toClear)
        {
            var queue = new Queue<Vector2Int>(toClear);
            var triggered = new HashSet<Vector2Int>();
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                var piece = InBounds(c.x, c.y) ? cells[c.x, c.y] : null;
                if (piece == null || piece.Power == PowerKind.None || !triggered.Add(c)) continue;
                void Add(int x, int y)
                {
                    var p = new Vector2Int(x, y);
                    if (InBounds(x, y) && cells[x, y] != null && !cells[x, y].IsGhostNet && toClear.Add(p)) queue.Enqueue(p);
                }
                switch (piece.Power)
                {
                    case PowerKind.SkimmerH: for (int x = 0; x < Width; x++) Add(x, c.y); break;
                    case PowerKind.SkimmerV: for (int y = 0; y < Height; y++) Add(c.x, y); break;
                    case PowerKind.Compactor:
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) Add(c.x + dx, c.y + dy);
                        break;
                }
            }
        }

        /// <summary>Pieces fall within each column segment (ghost nets are fixed dams); tops refill randomly.</summary>
        void ApplyGravityAndRefill()
        {
            for (int x = 0; x < Width; x++)
            {
                int segmentBottom = Height - 1;
                while (segmentBottom >= 0)
                {
                    int segmentTop = segmentBottom;
                    while (segmentTop >= 0 && cells[x, segmentTop]?.IsGhostNet != true) segmentTop--;
                    segmentTop++; // first row of this segment
                    int write = segmentBottom;
                    for (int y = segmentBottom; y >= segmentTop; y--)
                        if (cells[x, y] != null) { cells[x, write] = cells[x, y]; if (write != y) cells[x, y] = null; write--; }
                    for (int y = write; y >= segmentTop; y--) cells[x, y] = NewPiece(NormalTypes[rng.Next(NormalTypes.Length)]);
                    segmentBottom = segmentTop - 2; // skip the ghost net row itself
                }
            }
        }
    }
}
