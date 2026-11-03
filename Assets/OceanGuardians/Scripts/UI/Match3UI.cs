using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.MiniGames.Match3;

namespace OceanGuardians.UI
{
    /// <summary>One on-screen tile. Reports presses/drag-swipes to Match3UI.</summary>
    public class Match3Tile : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        public int PieceId;
        public Vector2Int Cell;
        public Image Background;
        public TMP_Text Label;
        public Image PowerMark;
        internal Match3UI Owner;
        Vector2 dragStart;
        bool dragged;

        public void OnPointerDown(PointerEventData e) { dragStart = e.position; dragged = false; Owner?.OnTilePressed(this); }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (dragged) return;
            var delta = e.position - dragStart;
            if (delta.magnitude < 36f) return;
            dragged = true;
            var dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2Int((int)Mathf.Sign(delta.x), 0)
                : new Vector2Int(0, (int)-Mathf.Sign(delta.y)); // screen up = board up (row -1)
            Owner?.OnTileSwiped(this, dir);
        }
    }

    /// <summary>
    /// The Recycle Sorter overlay: 6x6 tactile board over the pure Match3Board model.
    /// Animates swaps, snap-backs, pops, bouncy falls and power sweeps step by cascade step,
    /// then settles the round (kiosk bonus deposit on victory, gentle conversion otherwise).
    /// </summary>
    public class Match3UI : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform boardRoot;
        [SerializeField] TMP_Text movesText;
        [SerializeField] TMP_Text objectivesText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button quitButton;
        [SerializeField] float cellSize = 82f;

        static readonly Dictionary<Match3PieceType, (Color color, string label, string quotaName)> Looks = new()
        {
            { Match3PieceType.Plastic, (new Color(0.25f, 0.55f, 0.98f), "PET", "Plastics") },
            { Match3PieceType.Metal, (new Color(0.3f, 0.8f, 0.35f), "CAN", "Metals") },
            { Match3PieceType.Paper, (new Color(1f, 0.82f, 0.2f), "BOX", "Paper") },
            { Match3PieceType.Glass, (new Color(0.95f, 0.6f, 0.2f), "GLS", "Glass") },
            { Match3PieceType.GhostNet, (new Color(0.15f, 0.45f, 0.42f), "NET", "Ghost Nets") },
        };

        Match3Board board;
        SorterRound round;
        RecyclingKiosk kiosk;
        readonly Dictionary<int, Match3Tile> tiles = new();
        Match3Tile selected;
        bool busy, confirmQuit;
        int seedCounter;

        Vector2 CellPos(Vector2Int c) => new((c.x - (Match3Board.Width - 1) / 2f) * cellSize, ((Match3Board.Height - 1) / 2f - c.y) * cellSize);

        void OnEnable() => EventManager.Match3Requested += OpenRound;
        void OnDisable() => EventManager.Match3Requested -= OpenRound;

        void Start()
        {
            if (panel) panel.SetActive(false);
            quitButton?.onClick.AddListener(OnQuitClicked);
        }

        public void OpenRound()
        {
            if (panel == null || panel.activeSelf) return;
            kiosk = Object.FindAnyObjectByType<RecyclingKiosk>();
            board = new Match3Board(1234 + (++seedCounter * 7919) + System.DateTime.Now.Second);
            round = new SorterRound(RecycleSorterGame.BuildQuotas(BagSystem.Instance));
            confirmQuit = false;
            if (quitButton) quitButton.GetComponentInChildren<TMP_Text>().text = "Quit to Beach";
            panel.SetActive(true);
            RebuildTiles(instant: true);
            RefreshHeader();
            SetMessage("Sort the recycling! Match 3 or more.");
        }

        void CloseRound()
        {
            if (panel) panel.SetActive(false);
            foreach (var t in tiles.Values) Destroy(t.gameObject);
            tiles.Clear();
            selected = null;
        }

        void OnQuitClicked()
        {
            if (busy) return;
            if (!confirmQuit && round != null && round.State == SorterState.Playing)
            {
                confirmQuit = true;
                quitButton.GetComponentInChildren<TMP_Text>().text = "Tap again to quit";
                SetMessage("Leave the sorter? Your bag stays as-is.");
                return;
            }
            CloseRound();
        }

        // ---------- input ----------

        internal void OnTilePressed(Match3Tile tile)
        {
            if (busy || round == null || round.State != SorterState.Playing) return;
            Audio.AudioManager.Instance?.PlayTileSelect();
            if (selected != null && selected != tile && Adjacent(selected.Cell, tile.Cell))
            {
                StartCoroutine(SwapFlow(selected.Cell, tile.Cell));
                Select(null);
                return;
            }
            Select(tile);
        }

        internal void OnTileSwiped(Match3Tile tile, Vector2Int dir)
        {
            if (busy || round == null || round.State != SorterState.Playing) return;
            var to = tile.Cell + dir;
            if (!Match3Board.InBounds(to.x, to.y)) return;
            Select(null);
            StartCoroutine(SwapFlow(tile.Cell, to));
        }

        static bool Adjacent(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

        void Select(Match3Tile tile)
        {
            if (selected) selected.transform.localScale = Vector3.one;
            selected = tile;
            if (selected) selected.transform.localScale = Vector3.one * 1.12f;
        }

        // ---------- flow ----------

        IEnumerator SwapFlow(Vector2Int a, Vector2Int b)
        {
            busy = true;
            var pieceA = board.CellAt(a.x, a.y); var pieceB = board.CellAt(b.x, b.y);
            yield return TweenTiles(new[] { (pieceA, CellPos(b)), (pieceB, CellPos(a)) }, 0.16f);

            var result = board.TrySwap(a, b);
            if (!result.Success)
            {
                SetMessage("No match there - try another swap!");
                yield return TweenTiles(new[] { (pieceA, CellPos(a)), (pieceB, CellPos(b)) }, 0.16f); // snap back
                busy = false;
                yield break;
            }

            int cascade = 0;
            foreach (var step in result.Steps)
            {
                cascade++;
                Audio.AudioManager.Instance?.PlayMatchChime(cascade);
                if (step.PowerSpawned is PowerKind.SkimmerH or PowerKind.SkimmerV) Audio.AudioManager.Instance?.PlaySplash();
                if (cascade > 1) SetMessage($"Combo x{cascade}! Great Sort!");
                // Pop cleared tiles (the power-spawn cell survives as the new power piece).
                var popping = new List<Match3Tile>();
                foreach (var t in tiles.Values)
                    if (step.Cleared.Contains(t.Cell) && t.Cell != step.PowerSpawnAt) popping.Add(t);
                yield return PopTiles(popping);
                foreach (var t in popping) { tiles.Remove(t.PieceId); Destroy(t.gameObject); }
                SyncTilesFromBoard();
                yield return TweenAllToCells(0.34f, bouncy: true);
            }

            round.ApplyMove(result.ClearedByType, result.Cascades, result.Score);
            RefreshHeader();
            if (round.State == SorterState.Won) yield return FinishVictory();
            else if (round.State == SorterState.OutOfMoves) yield return FinishOutOfMoves();
            busy = false;
        }

        IEnumerator FinishVictory()
        {
            int credits = 0;
            if (kiosk != null)
            {
                // Standard deposit, then the exact +25% bonus on top (RecycleSorterGame.BonusCredits).
                int basePaid = Mathf.Max(kiosk.DepositAllCore(1f, RecycleSorterGame.VictoryEcoTokens, out _), 0);
                int bonus = RecycleSorterGame.BonusCredits(basePaid);
                if (bonus > 0) EconomyManager.Instance?.Add(CurrencyType.RecyclingCredits, bonus);
                credits = basePaid + bonus;
            }
            Audio.AudioManager.Instance?.PlaySorterVictory();
            RecycleSorterGame.Instance?.RecordResult(true);
            EventManager.RaiseMatch3Completed(true, credits);
            SpeechBubbleManager.Instance?.SayCoral("Sorted to perfection! Bonus credits, yay!");
            SetMessage($"You did it! Bag sorted for {credits} Credits (+25% bonus) and +{RecycleSorterGame.VictoryEcoTokens} Eco!");
            quitButton.GetComponentInChildren<TMP_Text>().text = "Back to Beach";
            confirmQuit = true; // next tap leaves without the are-you-sure step
            yield return new WaitForSeconds(1.4f);
        }

        IEnumerator FinishOutOfMoves()
        {
            // Gentle resolution: pieces matched so far become base credits; the bag is untouched.
            int credits = round.TotalMatched;
            int eco = round.TotalMatched / 10;
            EconomyManager.Instance?.Add(CurrencyType.RecyclingCredits, credits);
            EconomyManager.Instance?.Add(CurrencyType.EcoTokens, eco);
            RecycleSorterGame.Instance?.RecordResult(false);
            EventManager.RaiseMatch3Completed(false, credits);
            EventManager.RaiseCheckpoint();
            SetMessage($"Out of moves - nice sorting! Matched pieces became {credits} Credits and {eco} Eco. Your bag is unchanged.");
            quitButton.GetComponentInChildren<TMP_Text>().text = "Back to Beach";
            confirmQuit = true;
            yield return new WaitForSeconds(1.4f);
        }

        // ---------- rendering ----------

        void RebuildTiles(bool instant)
        {
            foreach (var t in tiles.Values) Destroy(t.gameObject);
            tiles.Clear();
            SyncTilesFromBoard();
            foreach (var t in tiles.Values) ((RectTransform)t.transform).anchoredPosition = CellPos(t.Cell);
        }

        void SyncTilesFromBoard()
        {
            var seen = new HashSet<int>();
            for (int y = 0; y < Match3Board.Height; y++)
                for (int x = 0; x < Match3Board.Width; x++)
                {
                    var piece = board.CellAt(x, y);
                    if (piece == null) continue;
                    seen.Add(piece.Id);
                    if (tiles.TryGetValue(piece.Id, out var tile)) tile.Cell = new Vector2Int(x, y);
                    else tiles[piece.Id] = CreateTile(piece, new Vector2Int(x, y), spawnAbove: true);
                }
            var stale = new List<int>();
            foreach (var kv in tiles) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (int id in stale) { Destroy(tiles[id].gameObject); tiles.Remove(id); }
            foreach (var t in tiles.Values) PaintTile(t);
        }

        Match3Tile CreateTile(Match3Piece piece, Vector2Int cell, bool spawnAbove)
        {
            var go = new GameObject("Tile", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(boardRoot, false);
            rt.sizeDelta = new Vector2(cellSize - 8f, cellSize - 8f);
            rt.anchoredPosition = CellPos(cell) + (spawnAbove ? Vector2.up * cellSize * 1.5f : Vector2.zero);
            var bg = go.AddComponent<Image>();
            var tile = go.AddComponent<Match3Tile>();
            tile.Owner = this; tile.PieceId = piece.Id; tile.Cell = cell; tile.Background = bg;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            tile.Label = labelGo.AddComponent<TextMeshProUGUI>();
            tile.Label.fontSize = 24f; tile.Label.fontStyle = FontStyles.Bold;
            tile.Label.alignment = TextAlignmentOptions.Center; tile.Label.raycastTarget = false; tile.Label.color = Color.white;

            var markGo = new GameObject("PowerMark", typeof(RectTransform));
            var mrt = (RectTransform)markGo.transform;
            mrt.SetParent(rt, false);
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = new Vector2(4f, 30f); mrt.offsetMax = new Vector2(-4f, -30f);
            tile.PowerMark = markGo.AddComponent<Image>();
            tile.PowerMark.color = new Color(1f, 1f, 1f, 0.85f);
            tile.PowerMark.raycastTarget = false;
            PaintTile(tile);
            return tile;
        }

        void PaintTile(Match3Tile tile)
        {
            var piece = board.CellAt(tile.Cell.x, tile.Cell.y);
            if (piece == null || piece.Id != tile.PieceId)
            {
                // Find by id (cell may lag during syncs).
                for (int y = 0; y < Match3Board.Height && (piece == null || piece.Id != tile.PieceId); y++)
                    for (int x = 0; x < Match3Board.Width; x++)
                        if (board.CellAt(x, y)?.Id == tile.PieceId) piece = board.CellAt(x, y);
            }
            if (piece == null) return;
            var look = Looks[piece.Type];
            tile.Background.color = look.color;
            tile.Label.text = piece.Power == PowerKind.Compactor ? "BOOM" : look.label;
            bool skimmer = piece.Power is PowerKind.SkimmerH or PowerKind.SkimmerV;
            tile.PowerMark.gameObject.SetActive(skimmer);
            if (skimmer)
            {
                var mrt = (RectTransform)tile.PowerMark.transform;
                bool horiz = piece.Power == PowerKind.SkimmerH;
                mrt.offsetMin = horiz ? new Vector2(4f, 30f) : new Vector2(30f, 4f);
                mrt.offsetMax = horiz ? new Vector2(-4f, -30f) : new Vector2(-30f, -4f);
            }
        }

        IEnumerator TweenTiles(IEnumerable<(Match3Piece piece, Vector2 to)> moves, float seconds)
        {
            var list = new List<(RectTransform rt, Vector2 from, Vector2 to)>();
            foreach (var (piece, to) in moves)
                if (piece != null && tiles.TryGetValue(piece.Id, out var tile))
                    list.Add(((RectTransform)tile.transform, ((RectTransform)tile.transform).anchoredPosition, to));
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                foreach (var (rt, from, to) in list) rt.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }
            foreach (var (rt, _, to) in list) rt.anchoredPosition = to;
        }

        IEnumerator TweenAllToCells(float seconds, bool bouncy)
        {
            var list = new List<(RectTransform rt, Vector2 from, Vector2 to)>();
            foreach (var t in tiles.Values)
                list.Add(((RectTransform)t.transform, ((RectTransform)t.transform).anchoredPosition, CellPos(t.Cell)));
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float eased = bouncy ? 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.06f : Mathf.SmoothStep(0f, 1f, k);
                foreach (var (rt, from, to) in list) rt.anchoredPosition = Vector2.Lerp(from, to, eased);
                yield return null;
            }
            foreach (var (rt, _, to) in list) rt.anchoredPosition = to;
        }

        IEnumerator PopTiles(List<Match3Tile> popping)
        {
            const float seconds = 0.22f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float s = 1f + Mathf.Sin(t / seconds * Mathf.PI) * 0.25f - (t / seconds) * 0.9f;
                foreach (var tile in popping) tile.transform.localScale = Vector3.one * Mathf.Max(s, 0.05f);
                yield return null;
            }
        }

        void RefreshHeader()
        {
            if (movesText) movesText.text = round != null ? $"Moves\n{round.MovesLeft}" : "";
            if (scoreText) scoreText.text = round != null ? $"Score: {round.Score}" : "";
            if (objectivesText && round != null)
            {
                var sb = new StringBuilder();
                foreach (var kv in round.Quotas)
                {
                    int have = round.Progress.TryGetValue(kv.Key, out int h) ? h : 0;
                    string done = have >= kv.Value ? " <color=#2E9E4F>(Done!)</color>" : "";
                    var look = Looks[kv.Key];
                    sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(look.color)}>●</color> {look.quotaName}: {Mathf.Min(have, kv.Value)}/{kv.Value}{done}");
                }
                objectivesText.text = sb.ToString();
            }
        }

        void SetMessage(string text) { if (messageText) messageText.text = text; }
    }
}
