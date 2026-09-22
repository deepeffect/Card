using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// Fraksiyon büyüleri (GDD Bölüm 3) ve dengeleme sınırları (Bölüm 6): GE maliyeti, bekleme süresi,
    /// turda en fazla 1 ulti, K3 kilidi ve kullanım sınırı, Gök Yankısı, hedef koruması.
    /// </summary>
    public sealed class UltiSystem
    {
        private readonly RulesEngine _e;

        public UltiSystem(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;
        private RulesConfig R => _e.Rules;

        public int Cost(UltiDef d) => R.TierCost[(int)d.Tier];
        public int CooldownOf(UltiDef d) => R.TierCooldown[(int)d.Tier];

        public int RemainingCooldown(int seat, string ultiId) =>
            S.Players[seat].UltiCooldown.TryGetValue(ultiId, out var cd) ? cd : 0;

        /// <summary>Hedeften bağımsız ön koşullar; kullanılabilirse null döner.</summary>
        public string Availability(int seat, string ultiId)
        {
            if (S.IsOver) return "Oyun bitti.";
            if (S.Current != seat || S.Phase != Phase.AnaFaz) return "Sıra sende değil.";
            var p = S.Players[seat];
            if (!UltiCatalog.TryGet(ultiId, out var d) || d.Faction != p.Faction) return "Bu büyü fraksiyonuna ait değil.";
            if (p.UltiUsedThisTurn) return "Bu tur zaten bir ulti kullandın.";
            int cd = RemainingCooldown(seat, ultiId);
            if (cd > 0) return $"Bekleme süresi: {cd} tur.";
            if (p.Ge < Cost(d)) return $"Yeterli GE yok ({Cost(d)} gerekli).";
            if (d.Tier == UltiTier.K3)
            {
                if (S.Round <= R.K3LockedRounds) return $"K3 ultiler ilk {R.K3LockedRounds} raundda kilitli.";
                if (p.K3Uses >= R.K3MaxUses) return $"K3 maçta en fazla {R.K3MaxUses} kez kullanılır.";
                if (p.K3EchoLock) return "Gök Yankısı: başka bir oyuncu K3 kullandı, K3'ün 1 raund kilitli.";
            }
            return null;
        }

        public string Validate(CastUltiCommand c)
        {
            var err = Availability(c.Player, c.UltiId);
            if (err != null) return err;
            var d = UltiCatalog.Get(c.UltiId);
            var t = c.Target ?? TargetSpec.None;
            err = ValidateTarget(d, c.Player, t);
            if (err != null) return err;
            if (d.TargetsPlayer && d.Tier != UltiTier.K1)
            {
                foreach (var target in TargetPlayers(d, c.Player, t))
                    if (S.Players[target].LastTargetedRound == S.Round)
                        return $"Hedef koruması: {S.Players[target].Name} bu raund zaten bir K2/K3 ultiye hedef oldu.";
            }
            return null;
        }

        private bool EnemyAliveSeat(int caster, int seat) =>
            seat >= 0 && seat < Board.Seats && S.Players[seat].Alive && _e.IsEnemy(caster, seat);

        public string ValidateTarget(UltiDef d, int seat, TargetSpec t)
        {
            switch (d.Targeting)
            {
                case UltiTargeting.Self:
                case UltiTargeting.OwnQuadrant:
                    return null;

                case UltiTargeting.EnemyPlayer:
                case UltiTargeting.EnemyQuadrant:
                    return EnemyAliveSeat(seat, t.Player) ? null : "Hayatta bir rakip oyuncu seç.";

                case UltiTargeting.EnemyQuadrantUnits:
                {
                    if (!EnemyAliveSeat(seat, t.Player)) return "Hayatta bir rakibin çeyreğini seç.";
                    var ids = t.EntityIds ?? new List<int>();
                    if (ids.Count < 1 || ids.Count > 2 || ids.Distinct().Count() != ids.Count) return "1 ya da 2 rakip birim seç.";
                    foreach (var id in ids)
                    {
                        if (!S.Units.TryGetValue(id, out var u) || !_e.IsEnemy(seat, u.Owner)) return "Seçilen birim rakip olmalı.";
                        if (Board.QuadrantOf(u.Pos) != t.Player) return "Birimler seçilen çeyrekte olmalı.";
                        if (u.Hp > 5) return "Yalnızca CAN ≤ 5 birimler yönetilebilir.";
                        if (!_e.CanBeTargeted(u, seat, Coord.None, true)) return $"{u.Def.Name} hedef alınamaz.";
                    }
                    return null;
                }

                case UltiTargeting.OwnUnit:
                {
                    if (!S.Units.TryGetValue(t.EntityId, out var u) || u.Owner != seat || u.IsIllusion || u.ControlledBy >= 0)
                        return "Kendi birimlerinden birini seç.";
                    var tiles = IllusionTiles(u, t);
                    if (tiles.Count == 0) return "Birimin yanında boş kare yok.";
                    if (t.Tiles != null && t.Tiles.Any(x => !_e.Grid.IsEmpty(x) || x.Chebyshev(u.Pos) != 1)) return "Kopyalar birime bitişik boş karelere çıkar.";
                    return null;
                }

                case UltiTargeting.OwnUnitToTile:
                {
                    if (!S.Units.TryGetValue(t.EntityId, out var u) || u.Owner != seat || u.IsIllusion || u.ControlledBy >= 0)
                        return "Kendi birimlerinden birini seç.";
                    if (!_e.Grid.IsEmpty(t.Tile)) return "Boş bir kare seç.";
                    foreach (var hq in S.Hqs)
                        if (!hq.IsDead && _e.IsEnemy(seat, hq.Owner) && hq.DistanceTo(t.Tile) < 2)
                            return "Rakip karargaha 2 kareden yakın olamaz.";
                    return null;
                }

                case UltiTargeting.AreaAny:
                    return Board.AllOnBoard(Board.Rect(t.Tile, d.Size, d.Size)) ? null : $"{d.Size}×{d.Size} alan tahtaya sığmalı.";

                case UltiTargeting.AreaEnemyQuadrant:
                {
                    var area = Board.Rect(t.Tile, d.Size, d.Size);
                    if (!Board.AllOnBoard(area)) return $"{d.Size}×{d.Size} alan tahtaya sığmalı.";
                    int q = Board.CommonQuadrant(area);
                    if (!EnemyAliveSeat(seat, q)) return "Alan tamamen bir rakibin çeyreğinde olmalı.";
                    if (d.Id == UltiCatalog.Hortum && t.Direction != Coord.None && !Coord.Dirs4.Contains(t.Direction))
                        return "Hortum yönü yukarı, aşağı, sağ ya da sol olmalı.";
                    return null;
                }

                case UltiTargeting.TilesEnemyQuadrant:
                {
                    var tiles = t.Tiles ?? new List<Coord>();
                    if (!IsValidFireShape(tiles, d.Size)) return "En fazla 6 karelik düz çizgi ya da 2×3 alan seç.";
                    if (tiles.Any(x => _e.Grid.IsHqTile(x))) return "Karargah kareleri ateşe verilemez.";
                    int q = Board.CommonQuadrant(tiles);
                    if (!EnemyAliveSeat(seat, q)) return "Kareler tamamen bir rakibin çeyreğinde olmalı.";
                    return null;
                }

                case UltiTargeting.CenterAny:
                    return t.Tile.IsOnBoard ? null : "Merkez kareyi seç.";

                case UltiTargeting.EnemyEntity:
                {
                    var ent = S.GetEntity(t.EntityId);
                    if (ent == null || ent.IsDead || !_e.IsEnemy(seat, _e.Actions.OwnerOf(ent))) return "Rakip bir birim, yapı ya da karargah seç.";
                    if (ent is Unit u && !_e.CanBeTargeted(u, seat, Coord.None, true)) return "Bu birim ultiyle hedef alınamaz.";
                    return null;
                }

                case UltiTargeting.TwoTiles:
                    if (!_e.Grid.IsEmpty(t.Tile) || !_e.Grid.IsEmpty(t.Tile2) || t.Tile == t.Tile2) return "İki farklı boş kare seç.";
                    return null;

                case UltiTargeting.OwnDeployTile:
                    return _e.Cards.IsDeployable(seat, t.Tile) ? null : "Konuşlanma alanındaki boş bir kare seç.";
            }
            return "Geçersiz hedef.";
        }

        /// <summary>En fazla N karelik düz çizgi ya da tam 2×3 / 3×2 dikdörtgen.</summary>
        public static bool IsValidFireShape(List<Coord> tiles, int maxLine)
        {
            int n = tiles.Count;
            if (n == 0 || n > maxLine || tiles.Distinct().Count() != n || !Board.AllOnBoard(tiles)) return false;
            var rows = tiles.Select(x => x.Row).Distinct().ToList();
            var cols = tiles.Select(x => x.Col).Distinct().ToList();
            if (rows.Count == 1 && cols.Max() - cols.Min() + 1 == n) return true;
            if (cols.Count == 1 && rows.Max() - rows.Min() + 1 == n) return true;
            if (n == 6 && ((rows.Count == 2 && cols.Count == 3) || (rows.Count == 3 && cols.Count == 2)))
                return rows.Max() - rows.Min() + 1 == rows.Count && cols.Max() - cols.Min() + 1 == cols.Count;
            return false;
        }

        /// <summary>Ultinin kapladığı kareler (önizleme ve olay için).</summary>
        public List<Coord> AreaTiles(UltiDef d, int seat, TargetSpec t)
        {
            switch (d.Targeting)
            {
                case UltiTargeting.AreaAny:
                case UltiTargeting.AreaEnemyQuadrant:
                    return Board.Rect(t.Tile, d.Size, d.Size).Where(x => x.IsOnBoard).ToList();
                case UltiTargeting.CenterAny:
                    return Board.Around(t.Tile, 2, true);
                case UltiTargeting.TilesEnemyQuadrant:
                    return t.Tiles != null ? t.Tiles.ToList() : new List<Coord>();
                case UltiTargeting.EnemyQuadrant:
                case UltiTargeting.EnemyQuadrantUnits:
                case UltiTargeting.EnemyPlayer:
                    return t.Player >= 0 ? Board.QuadrantTiles(t.Player).ToList() : new List<Coord>();
                case UltiTargeting.OwnQuadrant:
                    return Board.QuadrantTiles(seat).ToList();
                case UltiTargeting.TwoTiles:
                    return new List<Coord> { t.Tile, t.Tile2 };
                case UltiTargeting.OwnDeployTile:
                case UltiTargeting.OwnUnitToTile:
                    return new List<Coord> { t.Tile };
                case UltiTargeting.EnemyEntity:
                {
                    var ent = S.GetEntity(t.EntityId);
                    return ent == null ? new List<Coord>() : new List<Coord> { _e.Actions.PosOf(ent, new Coord(6, 6)) };
                }
                default:
                    return new List<Coord>();
            }
        }

        /// <summary>Hedef korumasına konu olan rakip oyuncular.</summary>
        public List<int> TargetPlayers(UltiDef d, int seat, TargetSpec t)
        {
            var list = new List<int>();
            switch (d.Targeting)
            {
                case UltiTargeting.EnemyPlayer:
                case UltiTargeting.EnemyQuadrant:
                case UltiTargeting.EnemyQuadrantUnits:
                    if (t.Player >= 0) list.Add(t.Player);
                    break;
                case UltiTargeting.AreaEnemyQuadrant:
                case UltiTargeting.TilesEnemyQuadrant:
                {
                    int q = Board.CommonQuadrant(AreaTiles(d, seat, t));
                    if (q >= 0) list.Add(q);
                    break;
                }
                case UltiTargeting.AreaAny:
                case UltiTargeting.CenterAny:
                    foreach (var tile in AreaTiles(d, seat, t))
                    {
                        int q = Board.QuadrantOf(tile);
                        if (q >= 0 && !list.Contains(q) && S.Players[q].Alive && _e.IsEnemy(seat, q)) list.Add(q);
                    }
                    break;
            }
            return list;
        }

        // ---------------- Uygulama ----------------

        public void Apply(CastUltiCommand c)
        {
            int seat = c.Player;
            var p = S.Players[seat];
            var d = UltiCatalog.Get(c.UltiId);
            var t = c.Target ?? TargetSpec.None;

            _e.GainGe(seat, -Cost(d), d.Name);
            p.UltiCooldown[d.Id] = CooldownOf(d);
            p.UltiUsedThisTurn = true;
            if (d.Tier == UltiTier.K3)
            {
                p.K3Uses++;
                if (S.Stats.FirstK3Round < 0) S.Stats.FirstK3Round = S.Round;
                foreach (var other in S.Players)
                    if (other.Seat != seat && other.Alive) other.K3EchoLock = true;
            }
            if (d.TargetsPlayer && d.Tier != UltiTier.K1)
                foreach (var target in TargetPlayers(d, seat, t)) S.Players[target].LastTargetedRound = S.Round;
            int castId = S.NextCastId++;
            S.Stats.UltisCast[seat]++;
            _e.Emit(new GameEvent
            {
                Type = GameEventType.UltiCast, Player = seat, Id = d.Id, Amount = Cost(d), Tiles = AreaTiles(d, seat, t),
                Message = $"{p.Name} ulti kullandı: {d.Name} ({Names.Of(d.Tier)}, {Cost(d)} GE)!"
            });

            var src = new DamageSource { Player = seat, Kind = DamageKind.Ulti, IsUlti = true, IsArea = d.IsArea, CastId = castId, Label = d.Name };

            switch (d.Id)
            {
                case UltiCatalog.GeceYarisi:
                    AddPlayerEffect(d, seat, t.Player, 2,
                        StatModifier.Add(Stat.Mnz, -1, Affects.All), StatModifier.Add(Stat.Hiz, -1, Affects.All), StatModifier.Add(Stat.YapiMnz, -1, Affects.All));
                    break;

                case UltiCatalog.SafakVakti:
                    AddPlayerEffect(d, seat, seat, 2, StatModifier.Add(Stat.Sal, 1, Affects.All), StatModifier.Add(Stat.Hiz, 1, Affects.All));
                    break;

                case UltiCatalog.Illuzyon:
                    CastIllusion(seat, S.Units[t.EntityId], t);
                    break;

                case UltiCatalog.ZehirliSis:
                    _e.Effects.Apply(EffectCatalog.ZehirliSis, seat, Board.Rect(t.Tile, 4, 4), 3, castId, true, Coord.None);
                    break;

                case UltiCatalog.Tutulma:
                    foreach (var id in t.EntityIds)
                    {
                        var u = S.Units[id];
                        u.ControlledBy = seat;
                        u.ControlMoveLeft = true;
                        u.ControlAttackLeft = true;
                        u.ControlCastId = castId;
                        u.Statuses.RemoveAll(st => st.Kind == StatusKind.Defending);
                        _e.Emit(new GameEvent { Type = GameEventType.ControlStarted, Player = seat, EntityId = u.Id, To = u.Pos, Message = $"Tutulma: {u.Def.Name} bu tur {p.Name} tarafından yönetiliyor." });
                    }
                    break;

                case UltiCatalog.GunesPatlamasi:
                {
                    var area = Board.Rect(t.Tile, 3, 3);
                    foreach (var u in S.Units.Values.Where(x => area.Contains(x.Pos) && _e.IsEnemy(seat, x.Owner)).ToList())
                    {
                        u.Statuses.RemoveAll(st => st.Kind == StatusKind.Blinded);
                        u.Statuses.Add(new Status { Kind = StatusKind.Blinded, SourcePlayer = seat, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = seat });
                        _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = u.Id, Id = "blinded", Message = u.Def.Name + " Kör oldu." });
                    }
                    if (_e.Effects.DispelTag(area, EffectTag.Sis) > 0) _e.Say("Güneş Patlaması sisi dağıttı.", seat);
                    break;
                }

                case UltiCatalog.KavurucuAtes:
                    _e.Effects.Apply(EffectCatalog.Yanan, seat, t.Tiles, 3, castId, true, Coord.None);
                    break;

                case UltiCatalog.AnkaYukselisi:
                    _e.Cards.SpawnUnit(seat, CardCatalog.AnkaKusu, t.Tile, -1);
                    break;

                case UltiCatalog.SimsekCarpmasi:
                    CastLightning(seat, S.GetEntity(t.EntityId), src);
                    break;

                case UltiCatalog.IyilestiriciYagmur:
                {
                    var quad = Board.QuadrantTiles(seat).ToList();
                    _e.Effects.Apply(EffectCatalog.Yagmur, seat, quad, 3, castId, true, Coord.None);
                    _e.Effects.Apply(EffectCatalog.Islak, seat, quad, 3, castId, true, Coord.None);
                    break;
                }

                case UltiCatalog.Hortum:
                {
                    var area = Board.Rect(t.Tile, 3, 3);
                    var dir = t.Direction != Coord.None ? t.Direction : DefaultTornadoDirection(area);
                    _e.Effects.Apply(EffectCatalog.Hortum, seat, area, 2, castId, true, dir);
                    break;
                }

                case UltiCatalog.SuperHucre:
                    _e.Effects.Apply(EffectCatalog.SuperHucre, seat, Board.QuadrantTiles(t.Player), 2, castId, true, Coord.None);
                    break;

                case UltiCatalog.KayanYildiz:
                {
                    var u = S.Units[t.EntityId];
                    _e.Forced.Teleport(u, t.Tile, "Kayan Yıldız ile ışınlandı");
                    if (u.Hp > 0) u.En = Math.Min(R.MaxEn, u.En + 1);
                    foreach (var tile in Board.Around(t.Tile, 1, false))
                    {
                        var ent = _e.EntityAt(tile);
                        if (ent != null && !(ent is Headquarters) && _e.IsEnemy(seat, _e.Actions.OwnerOf(ent)))
                            _e.Damage.Deal(ent, 1, src);
                    }
                    break;
                }

                case UltiCatalog.YildizGecidi:
                    _e.Effects.Apply(EffectCatalog.Gecit, seat, new[] { t.Tile, t.Tile2 }, 3, castId, true, Coord.None);
                    break;

                case UltiCatalog.KaraDelik:
                    CastBlackHole(seat, t.Tile, src);
                    break;

                case UltiCatalog.MeteorYagmuru:
                {
                    var area = Board.Rect(t.Tile, 4, 4);
                    S.Delayed.Add(new DelayedSpell { Id = _e.NewEntityId(), UltiId = d.Id, Caster = seat, CastId = castId, Tiles = area });
                    _e.Emit(new GameEvent { Type = GameEventType.MeteorMarked, Player = seat, Id = d.Id, Tiles = area, Message = "Meteor alanı kırmızı işaretlendi: sonraki Yıldız turunun başında düşecek!" });
                    break;
                }
            }
        }

        private void AddPlayerEffect(UltiDef d, int caster, int target, int turns, params StatModifier[] mods)
        {
            var pe = new PlayerEffect { Id = _e.NewEntityId(), UltiId = d.Id, Caster = caster, TargetPlayer = target, RemainingTurns = turns, Modifiers = mods };
            S.PlayerEffects.Add(pe);
            _e.Emit(new GameEvent { Type = GameEventType.EffectAdded, Player = caster, EntityId = pe.Id, Id = d.Id, TargetId = target, Amount = turns, Message = $"{d.Name}: {S.Players[target].Name} etkilendi ({turns} tur)." });
        }

        public List<Coord> IllusionTiles(Unit u, TargetSpec t)
        {
            if (t.Tiles != null && t.Tiles.Count > 0) return t.Tiles.Take(2).ToList();
            return Board.Around(u.Pos, 1, false).Where(c => _e.Grid.IsEmpty(c)).Take(2).ToList();
        }

        private void CastIllusion(int seat, Unit original, TargetSpec t)
        {
            foreach (var tile in IllusionTiles(original, t))
            {
                var copy = new Unit
                {
                    Id = _e.NewEntityId(),
                    Owner = seat,
                    CardId = original.CardId,
                    Pos = tile,
                    BaseAtk = 0,
                    BaseMaxHp = 1,
                    Hp = 1,
                    BaseSpeed = original.BaseSpeed,
                    BaseRange = original.BaseRange,
                    Flying = original.Flying,
                    En = R.SummonEn,
                    IsIllusion = true,
                    IllusionRemaining = 3,
                    IllusionOfId = original.Id,
                    WeaponCardId = original.WeaponCardId,
                    ArmorCardId = original.ArmorCardId,
                    SummonedTurnIndex = S.TurnIndex
                };
                S.Units[copy.Id] = copy;
                _e.Grid.SetOccupant(tile, copy.Id);
                _e.Emit(new GameEvent { Type = GameEventType.UnitSummoned, Player = seat, EntityId = copy.Id, To = tile, Id = copy.CardId, Message = $"{original.Def.Name} {tile} karesinde belirdi." });
            }
        }

        private void CastLightning(int seat, Entity target, DamageSource src)
        {
            // Paratoner: 3 kare içindeki tek hedefli rakip ultiler bu yapıya yönlenir.
            var targetPos = _e.Actions.PosOf(target, target is Headquarters hq0 ? hq0.Tiles[0] : Coord.None);
            Structure rod = null;
            int rodDist = int.MaxValue;
            foreach (var s in S.Structures.Values)
            {
                if (s.Def.Passive != CardPassive.Paratoner || !_e.IsEnemy(seat, s.Owner)) continue;
                int dist = _e.DistanceTo(target, s.Pos);
                if (dist > 3) continue;
                if (dist < rodDist || (dist == rodDist && s.Id < rod.Id)) { rod = s; rodDist = dist; }
            }
            if (rod != null && rod != target)
            {
                _e.Say($"Paratoner şimşeği çekti!", rod.Owner);
                target = rod;
                targetPos = rod.Pos;
            }

            bool wet = !(target is Headquarters) && _e.Effects.TileHas(targetPos, EffectTag.Su);
            if (target is Headquarters hq) _e.Damage.DealToHq(hq, 2, src);
            else _e.Damage.Deal(target, 4, src);

            if (!wet) return;
            // Islak zincir: bitişik Islak karelerdeki rakiplere 2 hasar.
            foreach (var tile in Board.Around(targetPos, 1, false))
            {
                if (!_e.Effects.TileHas(tile, EffectTag.Su)) continue;
                var ent = _e.EntityAt(tile);
                if (ent == null || ent is Headquarters || !_e.IsEnemy(seat, _e.Actions.OwnerOf(ent))) continue;
                _e.Say("Şimşek ıslak zeminde zincirlendi!", seat);
                _e.Damage.Deal(ent, 2, src);
            }
        }

        private void CastBlackHole(int seat, Coord center, DamageSource src)
        {
            var area = Board.Around(center, 2, true);
            var pulled = S.Units.Values
                .Where(u => area.Contains(u.Pos) && _e.IsEnemy(seat, u.Owner))
                .OrderBy(u => u.Pos.Chebyshev(center)).ThenBy(u => u.Id).ToList();
            var collision = src;
            collision.Kind = DamageKind.Collision;
            foreach (var u in pulled)
            {
                if (u.IsDead || u.Hp <= 0) continue;
                int moved = 0;
                for (int i = 0; i < 4; i++)
                {
                    var dir = (center - u.Pos).Sign();
                    if (dir.Row == 0 && dir.Col == 0) break;
                    var next = u.Pos + dir;
                    if (!_e.Grid.IsEmpty(next))
                    {
                        if (_e.EntityAt(next) is Unit blocker)
                        {
                            _e.Say($"{u.Def.Name} ile {blocker.Def.Name} çarpıştı.", seat);
                            _e.Damage.DealToUnit(u, 1, collision);
                            if (_e.IsEnemy(seat, blocker.Owner)) _e.Damage.DealToUnit(blocker, 1, collision);
                        }
                        break;
                    }
                    _e.Forced.StepUnit(u, next);
                    moved++;
                    if (u.Hp <= 0) break;
                }
                if (moved > 0) _e.Forced.SettleFlyer(u);
            }
            _e.Effects.Apply(EffectCatalog.Cekim, seat, Board.Around(center, 1, true), 2, src.CastId, true, Coord.None);
        }

        private Coord DefaultTornadoDirection(List<Coord> area)
        {
            int q = Board.CommonQuadrant(area);
            var center = area[area.Count / 2];
            var hq = Board.HqOrigin(q < 0 ? 0 : q);
            var delta = hq - center;
            if (Math.Abs(delta.Row) >= Math.Abs(delta.Col)) return delta.Row >= 0 ? Coord.Down : Coord.Up;
            return delta.Col >= 0 ? Coord.Right : Coord.Left;
        }

        // ---------------- Zamanlı işler ----------------

        /// <summary>Şafak 1. adım: gecikmeli büyüler (Meteor) düşer.</summary>
        public void ResolveDelayed(int seat)
        {
            foreach (var d in S.Delayed.Where(x => x.Caster == seat).ToList())
            {
                S.Delayed.Remove(d);
                var def = UltiCatalog.Get(d.UltiId);
                var src = new DamageSource { Player = seat, Kind = DamageKind.Ulti, IsUlti = true, IsArea = true, CastId = d.CastId, Label = def.Name };
                _e.Emit(new GameEvent { Type = GameEventType.Log, Player = seat, Id = "meteor_landed", Tiles = d.Tiles, Message = "Meteorlar düşüyor!" });
                var hitHq = new HashSet<int>();
                foreach (var tile in d.Tiles)
                {
                    var ent = _e.EntityAt(tile);
                    if (ent == null || !_e.IsEnemy(seat, _e.Actions.OwnerOf(ent))) continue;
                    switch (ent)
                    {
                        case Unit u: _e.Damage.DealToUnit(u, 3, src); break;
                        case Structure s: _e.Damage.DealToStructure(s, 2, src); break;
                        case Headquarters hq:
                            if (hitHq.Add(hq.Owner)) _e.Damage.DealToHq(hq, 3, src);
                            break;
                    }
                }
            }
        }

        /// <summary>Hortum: uygulandığında süpürür (steps = 0), yapanın tur başında 2 kare ilerleyip yeniden süpürür.</summary>
        public void AdvanceTornado(AreaEffectInstance fx, int steps, int structureDamage)
        {
            if (steps <= 0)
            {
                Sweep(fx, fx.Tiles.ToList(), structureDamage);
                return;
            }
            for (int i = 0; i < steps; i++)
            {
                if (fx.Direction == Coord.None || !S.Effects.ContainsKey(fx.Id)) return;
                var moved = fx.Tiles.Select(x => x + fx.Direction).ToList();
                if (!Board.AllOnBoard(moved)) return;
                _e.Effects.MoveTo(fx, moved);
                Sweep(fx, moved, structureDamage);
            }
        }

        private void Sweep(AreaEffectInstance fx, List<Coord> tiles, int structureDamage)
        {
            if (_e.Effects.DispelTag(tiles, EffectTag.Sis) > 0) _e.Say("Hortum sisi dağıttı.", fx.Caster);
            var exclude = new HashSet<Coord>(fx.Tiles);
            var src = new DamageSource { Player = fx.Caster, Kind = DamageKind.Ulti, IsUlti = true, IsArea = true, CastId = fx.CastId, Label = "Hortum" };
            var victims = tiles.Select(x => _e.EntityAt(x)).OfType<Unit>()
                .Where(u => _e.IsEnemy(fx.Caster, u.Owner)).Distinct().OrderBy(u => u.Id).ToList();
            foreach (var u in victims)
            {
                if (u.IsDead || u.Hp <= 0) continue;
                if (_e.Effects.TileHas(u.Pos, EffectTag.Cekim)) continue; // Hortum + Çekim: Kara Delik baskın gelir
                if (!_e.Effects.MarkHit(fx, u.Id)) continue;
                bool onFire = _e.Effects.TileHasEffect(u.Pos, EffectCatalog.Yanan); // Hortum + Yanan: Ateş Hortumu
                _e.Forced.Scatter(u, 3, exclude);
                if (u.Hp > 0) _e.Damage.DealToUnit(u, onFire ? 2 : 1, src);
            }
            foreach (var s in tiles.Select(x => _e.EntityAt(x)).OfType<Structure>().Where(s => _e.IsEnemy(fx.Caster, s.Owner)).Distinct().ToList())
                if (_e.Effects.MarkHit(fx, s.Id)) _e.Damage.DealToStructure(s, structureDamage, src);
        }

        /// <summary>Tutulma: tur sonunda yönetilen birimler 2 hasar alıp sahibine döner.</summary>
        public void EndControl(int seat)
        {
            foreach (var u in S.Units.Values.Where(x => x.ControlledBy == seat).ToList())
            {
                u.ControlledBy = -1;
                u.ControlMoveLeft = u.ControlAttackLeft = false;
                _e.Emit(new GameEvent { Type = GameEventType.ControlEnded, Player = u.Owner, EntityId = u.Id, To = u.Pos, Message = $"{u.Def.Name} sahibine döndü." });
                _e.Damage.DealToUnit(u, 2, new DamageSource { Player = seat, Kind = DamageKind.Ulti, IsUlti = true, CastId = u.ControlCastId, Label = "Tutulma" });
            }
        }

        public string ValidateSteer(SteerTornadoCommand c)
        {
            if (!S.Effects.TryGetValue(c.EffectId, out var fx) || fx.Def.Id != EffectCatalog.Hortum || fx.Caster != c.Player) return "Yönetebileceğin bir hortum yok.";
            if (!Coord.Dirs4.Contains(c.Direction)) return "Yön yukarı, aşağı, sağ ya da sol olmalı.";
            return null;
        }

        public void ApplySteer(SteerTornadoCommand c)
        {
            S.Effects[c.EffectId].Direction = c.Direction;
            _e.Say("Hortumun yönü değişti.", c.Player);
        }
    }
}
