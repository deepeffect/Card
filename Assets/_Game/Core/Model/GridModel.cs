using System.Collections.Generic;

namespace DortGok.Core
{
    public sealed class PathStep
    {
        public Coord Tile;
        /// <summary>Bu adımda Yıldız Geçidi'nden geçip bu kareye ışınlandı.</summary>
        public bool Teleport;
    }

    public sealed class PathResult
    {
        public Coord Dest;
        public List<PathStep> Steps = new List<PathStep>();
        public int Length;
        public int EstimatedDamage;
    }

    /// <summary>
    /// GDD "GridManager (GridModel)": 12×12 kare dizisi, doluluk, katmanlı etki indeksi,
    /// şekil ve mesafe sorguları, yol bulma.
    /// </summary>
    public sealed class GridModel
    {
        private readonly int[,] _occupant = new int[Board.Size, Board.Size];
        private readonly bool[,] _ruin = new bool[Board.Size, Board.Size];
        private readonly int[,] _hqOwner = new int[Board.Size, Board.Size];
        private readonly List<int>[,] _effects = new List<int>[Board.Size, Board.Size];

        internal RulesEngine Engine;

        public GridModel()
        {
            for (int r = 0; r < Board.Size; r++)
                for (int c = 0; c < Board.Size; c++)
                {
                    _effects[r, c] = new List<int>();
                    _hqOwner[r, c] = -1;
                }
        }

        // ---------------- Doluluk ----------------

        public int OccupantAt(Coord c) => c.IsOnBoard ? _occupant[c.Row, c.Col] : 0;
        public void SetOccupant(Coord c, int entityId) => _occupant[c.Row, c.Col] = entityId;
        public void ClearOccupant(Coord c, int entityId)
        {
            if (_occupant[c.Row, c.Col] == entityId) _occupant[c.Row, c.Col] = 0;
        }

        public bool IsRuin(Coord c) => c.IsOnBoard && _ruin[c.Row, c.Col];
        public void SetRuin(Coord c) => _ruin[c.Row, c.Col] = true;

        public int HqOwnerAt(Coord c) => c.IsOnBoard ? _hqOwner[c.Row, c.Col] : -1;
        public bool IsHqTile(Coord c) => HqOwnerAt(c) >= 0;
        public void MarkHq(Coord c, int seat) => _hqOwner[c.Row, c.Col] = seat;
        public void UnmarkHq(Coord c) => _hqOwner[c.Row, c.Col] = -1;

        /// <summary>Birim ya da yapının durabileceği boş kare mi (birim, yapı, karargah, Harabe yok).</summary>
        public bool IsEmpty(Coord c) =>
            c.IsOnBoard && _occupant[c.Row, c.Col] == 0 && !_ruin[c.Row, c.Col] && _hqOwner[c.Row, c.Col] < 0;

        /// <summary>Zemin etkisi alabilir mi: karargah kareleri ve Harabeler dokunulmazdır.</summary>
        public bool AcceptsTileEffects(Coord c) => c.IsOnBoard && !IsHqTile(c) && !IsRuin(c);

        // ---------------- GDD API ----------------

        public struct TileInfo
        {
            public Coord Coord;
            public int Occupant;
            public bool Ruin;
            public int HqOwner;
            public bool Altar;
            public int Quadrant;
            public IReadOnlyList<int> EffectIds;
        }

        public TileInfo GetTile(Coord c) => new TileInfo
        {
            Coord = c,
            Occupant = OccupantAt(c),
            Ruin = IsRuin(c),
            HqOwner = HqOwnerAt(c),
            Altar = Board.IsAltar(c),
            Quadrant = Board.QuadrantOf(c),
            EffectIds = c.IsOnBoard ? _effects[c.Row, c.Col] : (IReadOnlyList<int>)new List<int>()
        };

        /// <summary>Şeklin kapladığı kareler; kare şekillerde origin sol üst köşedir, Quadrant'ta origin o çeyrekten bir karedir.</summary>
        public List<Coord> GetShape(ShapeDef shape, Coord origin)
        {
            switch (shape.Kind)
            {
                case ShapeKind.Square:
                case ShapeKind.Rect:
                    return Board.Rect(origin, shape.Height, shape.Width);
                case ShapeKind.Quadrant:
                    return new List<Coord>(Board.QuadrantTiles(Board.QuadrantOf(origin)));
                default:
                    return new List<Coord> { origin };
            }
        }

        // ---------------- Etki indeksi (iki yönlü) ----------------

        public IReadOnlyList<int> EffectIdsAt(Coord c) => _effects[c.Row, c.Col];
        public void IndexEffect(Coord c, int effectId)
        {
            var list = _effects[c.Row, c.Col];
            if (!list.Contains(effectId)) list.Add(effectId);
        }
        public void UnindexEffect(Coord c, int effectId) => _effects[c.Row, c.Col].Remove(effectId);

        // ---------------- Yol bulma ----------------

        /// <summary>
        /// Birimin bu Hareket ile ulaşabileceği kareler. Her kare için en az tahmini hasarlı,
        /// sonra en kısa yol seçilir (GDD: yol önizlemesi zemin hasarını gösterir).
        /// </summary>
        public Dictionary<Coord, PathResult> Reachable(Unit u, int maxSteps)
        {
            var result = new Dictionary<Coord, PathResult>();
            if (maxSteps <= 0) return result;
            var e = Engine;
            int side = u.ActingPlayer;
            const int N = Board.Size * Board.Size;
            int layers = maxSteps + 1;
            var dmg = new int[layers * N];
            var parent = new int[layers * N];
            var teleported = new bool[layers * N];
            var bestDmg = new int[N];
            for (int i = 0; i < dmg.Length; i++) { dmg[i] = int.MaxValue; parent[i] = -1; }
            for (int i = 0; i < N; i++) bestDmg[i] = int.MaxValue;

            int Key(int layer, Coord c) => layer * N + c.Row * Board.Size + c.Col;
            Coord FromIdx(int idx) { int k = idx % N; return new Coord(k / Board.Size, k % Board.Size); }

            var frontier = new List<Coord> { u.Pos };
            dmg[Key(0, u.Pos)] = 0;
            bestDmg[u.Pos.Row * Board.Size + u.Pos.Col] = 0;

            for (int s = 0; s < maxSteps; s++)
            {
                var next = new List<Coord>();
                foreach (var cur in frontier)
                {
                    int curDmg = dmg[Key(s, cur)];
                    foreach (var d in Coord.Dirs4)
                    {
                        var n = cur + d;
                        if (!CanPassThrough(u, side, n)) continue;
                        int stepDmg = u.Flying ? 0 : e.Effects.EstimateEnterDamage(u, n);
                        var landing = n;
                        bool tele = false;
                        var exit = e.Effects.PortalExit(n);
                        if (exit != Coord.None && (IsEmpty(exit) || OccupantAt(exit) == u.Id))
                        {
                            landing = exit;
                            tele = true;
                            if (!u.Flying) stepDmg += e.Effects.EstimateEnterDamage(u, exit);
                        }
                        int total = curDmg + stepDmg;
                        int li = landing.Row * Board.Size + landing.Col;
                        if (total >= bestDmg[li]) continue;
                        bestDmg[li] = total;
                        int k = Key(s + 1, landing);
                        dmg[k] = total;
                        parent[k] = Key(s, cur);
                        teleported[k] = tele;
                        next.Add(landing);
                    }
                }
                frontier = next;
                if (frontier.Count == 0) break;
            }

            for (int s = 1; s <= maxSteps; s++)
            {
                for (int k = s * N; k < (s + 1) * N; k++)
                {
                    if (dmg[k] == int.MaxValue) continue;
                    var c = FromIdx(k);
                    if (c == u.Pos || !IsEmpty(c)) continue;
                    int est = dmg[k] + (u.Flying ? e.Effects.EstimateEnterDamage(u, c) : 0);
                    if (result.TryGetValue(c, out var existing) &&
                        (existing.EstimatedDamage < est || (existing.EstimatedDamage == est && existing.Length <= s)))
                        continue;
                    var path = new PathResult { Dest = c, Length = s, EstimatedDamage = est };
                    int walk = k;
                    var rev = new List<PathStep>();
                    while (parent[walk] >= 0)
                    {
                        rev.Add(new PathStep { Tile = FromIdx(walk), Teleport = teleported[walk] });
                        walk = parent[walk];
                    }
                    rev.Reverse();
                    path.Steps = rev;
                    result[c] = path;
                }
            }
            return result;
        }

        /// <summary>
        /// Dost birimlerin içinden geçilebilir; düşman birimler, yapılar, karargahlar ve Harabeler yolu keser.
        /// Uçan birimler her birimin ve yapının üzerinden geçer.
        /// </summary>
        public bool CanPassThrough(Unit u, int side, Coord c)
        {
            if (!c.IsOnBoard) return false;
            if (u.Flying) return true;
            if (IsRuin(c) || IsHqTile(c)) return false;
            int occ = OccupantAt(c);
            if (occ == 0 || occ == u.Id) return true;
            var e = Engine;
            if (e.State.Structures.ContainsKey(occ)) return false;
            if (e.State.Units.TryGetValue(occ, out var other))
                return !e.IsEnemy(side, other.ActingPlayer);
            return false;
        }
    }
}
