using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// GDD yol haritası Faz 2'deki "basit bot": açgözlü sezgilerle bir turu baştan sona oynar.
    /// Aynı ekranda boş koltukları doldurur ve denge simülasyonlarında kullanılır.
    /// </summary>
    public sealed class SimpleBot
    {
        private readonly RulesEngine _e;
        public readonly int Seat;
        private int _turnIndexAtStart;

        public SimpleBot(RulesEngine engine, int seat)
        {
            _e = engine;
            Seat = seat;
        }

        private GameState S => _e.State;
        private PlayerState Me => S.Players[Seat];

        private bool StillMyTurn => !S.IsOver && S.Current == Seat && S.Phase == Phase.AnaFaz && S.TurnIndex == _turnIndexAtStart;

        private bool Try(Command c)
        {
            if (!StillMyTurn) return false;
            c.Player = Seat;
            return _e.Execute(c).Ok;
        }

        /// <summary>Botun bu turdaki tüm kararlarını verir ve turu bitirir.</summary>
        public void PlayTurn()
        {
            if (S.IsOver || S.Current != Seat || S.Phase != Phase.AnaFaz) return;
            _turnIndexAtStart = S.TurnIndex;

            TryUlti();
            PlayCards();
            ActWithUnits();
            StructureFloops();
            if (!Me.UltiUsedThisTurn) TryUlti();
            // Ek kartı yalnızca el boşaldığında ve deste uzunken çek; aksi halde deste erken biter.
            if (Me.Ap >= _e.Rules.ExtraDrawCost && Me.Hand.Count == 0 && Me.Deck.Count > 8) Try(new DrawExtraCommand());
            PlayCards();
            SteerTornadoes();
            if (StillMyTurn) Try(new EndTurnCommand());
        }

        // ---------------- Hedefler ----------------

        private IEnumerable<Unit> EnemyUnits => S.Units.Values.Where(u => _e.IsEnemy(Seat, u.ActingPlayer));
        private IEnumerable<Headquarters> EnemyHqs => S.Hqs.Where(h => !h.IsDead && _e.IsEnemy(Seat, h.Owner));

        private Headquarters FocusHq(Coord from)
        {
            Headquarters best = null;
            double bestScore = double.MaxValue;
            foreach (var hq in EnemyHqs)
            {
                double score = hq.DistanceTo(from) + hq.Hp / 5.0;
                if (score < bestScore) { bestScore = score; best = hq; }
            }
            return best;
        }

        /// <summary>
        /// Hedef: yola çıkan yakın tehdit, Sunak'ı tutacak tek bir birim, yoksa en zayıf/en yakın rakip karargah.
        /// </summary>
        private Coord GoalFor(Unit u)
        {
            // Karargaha yürürken yalnızca yolu kesen (bitişik) rakiplerle oyalan.
            var hqNear = FocusHq(u.Pos);
            var threat = EnemyUnits.Where(x => x.Pos.Chebyshev(u.Pos) <= 1).OrderBy(x => x.Hp).FirstOrDefault();
            if (threat != null && (hqNear == null || hqNear.DistanceTo(u.Pos) > 2)) return threat.Pos;
            bool altarHeld = S.UnitsOf(Seat).Any(x => !x.IsIllusion && Board.IsAltar(x.Pos));
            if (!altarHeld)
            {
                var keeper = S.UnitsOf(Seat).Where(x => !x.IsIllusion && x.ControlledBy < 0)
                    .OrderBy(x => Board.AltarTiles.Min(a => a.Manhattan(x.Pos))).FirstOrDefault();
                if (keeper == u) return Board.AltarTiles.OrderBy(a => a.Manhattan(u.Pos)).First();
            }
            var hq = FocusHq(u.Pos);
            return hq != null ? _e.Actions.PosOf(hq, u.Pos) : new Coord(6, 6);
        }

        /// <summary>Rakiplerden biri bu kareye gelecek turda saldırabilir mi.</summary>
        private bool Threatened(Coord c) =>
            EnemyUnits.Any(x => x.Pos.Manhattan(c) <= _e.Stats.Speed(x) + _e.Stats.Range(x));

        // ---------------- Kartlar ----------------

        private void PlayCards()
        {
            for (int guard = 0; guard < 12 && StillMyTurn; guard++)
            {
                var playable = Me.Hand.Where(c => c.Def.Cost <= Me.Ap).OrderBy(c => Priority(c.Def)).ToList();
                bool played = false;
                foreach (var card in playable)
                {
                    var target = ChooseCardTarget(card.Def);
                    if (target == null) continue;
                    if (Try(new PlayCardCommand { CardInstanceId = card.InstanceId, Target = target })) { played = true; break; }
                }
                if (!played) break;
            }
        }

        private int Priority(CardDef d)
        {
            if (d.Type == CardType.Birim) return 10 - d.Cost;
            if (d.Type == CardType.Gelistirme) return 20 - d.Cost;
            return 30 - d.Cost;
        }

        private TargetSpec ChooseCardTarget(CardDef d)
        {
            switch (d.Type)
            {
                case CardType.Birim:
                {
                    var tiles = _e.Cards.DeployTiles(Seat);
                    if (tiles.Count == 0) return null;
                    var best = tiles.OrderBy(t => DistToFront(t) + (HasHarm(t) ? 5 : 0)).First();
                    return TargetSpec.AtTile(best);
                }
                case CardType.Yapi:
                {
                    if (S.StructuresOf(Seat).Count() >= _e.Rules.MaxStructures) return null;
                    if (d.Passive == CardPassive.KaragahSurlari && S.StructuresOf(Seat).Any(s => s.Def.Passive == CardPassive.KaragahSurlari)) return null;
                    var tiles = _e.Cards.BuildTiles(Seat);
                    if (tiles.Count == 0) return null;
                    var hq = S.Hqs[Seat];
                    Coord best;
                    switch (d.Passive)
                    {
                        case CardPassive.DinlenmeKampi:
                        case CardPassive.BulutDuragi:
                            best = tiles.OrderByDescending(t => S.UnitsOf(Seat).Count(u => u.Pos.Chebyshev(t) == 1)).ThenBy(t => hq.DistanceTo(t)).First();
                            break;
                        case CardPassive.IleriKarakol:
                            best = tiles.OrderBy(t => t.Chebyshev(new Coord(6, 6)) * 1.0 - 0).First();
                            break;
                        case CardPassive.Gozlemevi:
                            best = tiles.OrderBy(t => hq.DistanceTo(t) == 1 ? 0 : hq.DistanceTo(t)).First();
                            break;
                        default:
                            best = tiles.Where(t => hq.DistanceTo(t) >= 2 && hq.DistanceTo(t) <= 4)
                                .DefaultIfEmpty(tiles[0]).OrderBy(t => DistToFront(t)).First();
                            break;
                    }
                    return TargetSpec.AtTile(best);
                }
                default:
                {
                    var candidates = S.UnitsOf(Seat).Where(u => !u.IsIllusion && u.ControlledBy < 0).ToList();
                    if (candidates.Count == 0) return null;
                    Func<Unit, bool> slotFree = u => d.Slot == UpgradeSlot.Silah ? u.WeaponCardId == null : u.ArmorCardId == null;
                    var best = candidates.OrderByDescending(u => slotFree(u) ? 1 : 0)
                        .ThenByDescending(u => _e.Stats.Atk(u) + u.Hp)
                        .ThenBy(u => DistToFront(u.Pos)).First();
                    if (!slotFree(best)) return null;
                    return TargetSpec.AtEntity(best.Id);
                }
            }
        }

        private int DistToFront(Coord c)
        {
            var near = EnemyUnits.Select(u => u.Pos.Chebyshev(c)).DefaultIfEmpty(99).Min();
            var hq = FocusHq(c);
            int hqDist = hq != null ? hq.DistanceTo(c) : 99;
            return Math.Min(near + 2, hqDist);
        }

        private bool HasHarm(Coord c) => _e.Effects.At(c).Any(fx => _e.IsEnemy(Seat, fx.Caster) && fx.Def.Actions.Any(a => a.Kind == ActionKind.Damage));

        // ---------------- Birimler ----------------

        private void ActWithUnits()
        {
            var units = S.Units.Values.Where(u => (u.Owner == Seat && u.ControlledBy < 0) || u.ControlledBy == Seat)
                .OrderBy(u => DistToFront(u.Pos)).ToList();
            foreach (var u in units)
            {
                if (!StillMyTurn) return;
                if (u.IsDead || !S.Units.ContainsKey(u.Id)) continue;
                bool controlled = u.ControlledBy == Seat && u.Owner != Seat;

                if (!controlled) PreAttackFloops(u);
                if (!TryAttack(u))
                {
                    MoveToward(u, GoalFor(u));
                    TryAttack(u);
                }
                if (u.IsDead || !S.Units.ContainsKey(u.Id) || controlled) continue;
                PostFloops(u);
                if (u.RetreatAvailable)
                {
                    var hq = S.Hqs[Seat];
                    var back = _e.Grid.Reachable(u, Math.Min(2, _e.Stats.Speed(u))).Keys.OrderBy(t => hq.DistanceTo(t)).FirstOrDefault();
                    if (back.IsOnBoard) Try(new RetreatCommand { UnitId = u.Id, To = back });
                }
                if (S.Units.ContainsKey(u.Id) && !u.Defended && u.En >= 1 && Threatened(u.Pos))
                    Try(new DefendCommand { UnitId = u.Id });
            }
        }

        private bool TryAttack(Unit u)
        {
            if (!S.Units.ContainsKey(u.Id)) return false;
            bool controlled = u.ControlledBy == Seat && u.Owner != Seat;
            if (controlled ? !u.ControlAttackLeft : (u.Attacked || u.En < 1)) return false;
            var targets = _e.Actions.AttackTargets(u);
            if (targets.Count == 0) return false;
            int atk = _e.Stats.Atk(u);
            Entity best = null;
            double bestScore = double.MinValue;
            foreach (var t in targets)
            {
                double score;
                switch (t)
                {
                    case Headquarters hq:
                        score = 60 + (30 - hq.Hp);
                        break;
                    case Structure s:
                        score = 25 + (s.Hp <= atk ? 30 : 0) + (s.Def.Passive == CardPassive.OkcuKulesi ? 10 : 0);
                        break;
                    case Unit tu:
                        int dmg = Math.Max(1, atk - _e.Stats.Armor(tu));
                        bool kills = dmg >= tu.Hp;
                        score = 30 + (kills ? 50 + tu.Def.Cost * 8 : dmg * 4);
                        bool melee = _e.Stats.Range(u) <= 1;
                        if (melee && !kills && _e.Stats.Atk(tu) >= u.Hp + _e.Stats.Armor(u)) score -= 40;
                        break;
                    default:
                        score = 0;
                        break;
                }
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best == null || bestScore < 0) return false;
            return Try(new AttackCommand { UnitId = u.Id, TargetId = best.Id });
        }

        private void MoveToward(Unit u, Coord goal)
        {
            if (!S.Units.ContainsKey(u.Id)) return;
            bool controlled = u.ControlledBy == Seat && u.Owner != Seat;
            if (controlled ? !u.ControlMoveLeft : (u.Moved || (u.En < 1 && !_e.Actions.NextMoveIsFree(u)))) return;
            // Saldırıya EN kalmayacaksa ve zaten hedefe yakınsa bekle.
            var reach = _e.Actions.ReachableFor(u);
            if (reach.Count == 0) return;
            int range = _e.Stats.Range(u);
            int current = u.Pos.Chebyshev(goal);
            Coord best = Coord.None;
            double bestScore = double.MaxValue;
            foreach (var kv in reach)
            {
                double score = kv.Key.Chebyshev(goal) * 2 + kv.Value.EstimatedDamage * 3 + kv.Value.Length * 0.1;
                bool canHit = EnemyUnits.Any(x => x.Pos.Chebyshev(kv.Key) <= range) || EnemyHqs.Any(h => h.DistanceTo(kv.Key) <= range);
                if (canHit) score -= 4;
                if (kv.Value.EstimatedDamage >= u.Hp) score += 100;
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            if (best == Coord.None || best.Chebyshev(goal) >= current && !EnemyHqs.Any()) return;
            Try(new MoveCommand { UnitId = u.Id, To = best });
        }

        private bool CanFloop(Unit u) => u.Def.HasFloop && _e.Floops.CanUse(Seat, u.Id) == null;

        private void PreAttackFloops(Unit u)
        {
            if (!CanFloop(u)) return;
            var kind = u.Def.Floop.Kind;
            int range = _e.Stats.Range(u);
            switch (kind)
            {
                case FloopKind.Hucum:
                    if (u.En >= 3 && EnemyUnits.Concat<Entity>(EnemyHqs).Any(x => _e.DistanceTo(x, u.Pos) <= _e.Stats.Speed(u) + 2))
                        Try(new FloopCommand { SourceId = u.Id });
                    break;
                case FloopKind.OkYagmuru:
                    if (u.En >= 2 && EnemyUnits.Any(x => x.Pos.Chebyshev(u.Pos) <= range && EnemyUnits.Any(y => y != x && y.Pos.Chebyshev(x.Pos) == 1)))
                        Try(new FloopCommand { SourceId = u.Id });
                    break;
                case FloopKind.VurKac:
                    if (u.En >= 2 && EnemyUnits.Concat<Entity>(EnemyHqs).Any(x => _e.DistanceTo(x, u.Pos) <= _e.Stats.Speed(u) + 1))
                        Try(new FloopCommand { SourceId = u.Id });
                    break;
                case FloopKind.Isaretle:
                {
                    var target = EnemyUnits.Where(x => x.Pos.Chebyshev(u.Pos) <= 4 && !x.Has(StatusKind.Marked) && _e.CanBeTargeted(x, Seat, u.Pos, false))
                        .OrderByDescending(x => x.Hp).FirstOrDefault();
                    if (target != null && S.UnitsOf(Seat).Any(o => o != u && o.Pos.Chebyshev(target.Pos) <= 3))
                        Try(new FloopCommand { SourceId = u.Id, Target = TargetSpec.AtEntity(target.Id) });
                    break;
                }
                case FloopKind.Feda:
                {
                    int around = Board.Around(u.Pos, 1, false).Select(t => _e.EntityAt(t)).Count(x => x != null && _e.IsEnemy(Seat, _e.Actions.OwnerOf(x)));
                    if (around >= 2) Try(new FloopCommand { SourceId = u.Id });
                    break;
                }
                case FloopKind.CekIt:
                {
                    var hq = S.Hqs[Seat];
                    var target = EnemyUnits.Where(x => x.Pos.Chebyshev(u.Pos) <= 3 && hq.DistanceTo(x.Pos) <= 3 && _e.CanBeTargeted(x, Seat, u.Pos, false)).FirstOrDefault();
                    if (target != null) Try(new FloopCommand { SourceId = u.Id, Target = new TargetSpec { EntityId = target.Id, Pull = false, Amount = 2 } });
                    break;
                }
                case FloopKind.TakimyildizZirhi:
                    if (S.UnitsOf(Seat).Count(o => o != u && o.Pos.Chebyshev(u.Pos) == 1) >= 2 && EnemyUnits.Any(x => x.Pos.Chebyshev(u.Pos) <= 4))
                        Try(new FloopCommand { SourceId = u.Id });
                    break;
            }
        }

        private void PostFloops(Unit u)
        {
            if (!S.Units.ContainsKey(u.Id)) return;
            for (int i = 0; i < 2 && CanFloop(u); i++)
            {
                var kind = u.Def.Floop.Kind;
                bool used = false;
                switch (kind)
                {
                    case FloopKind.IsikOku:
                    {
                        int range = _e.Stats.Range(u);
                        Entity target = EnemyUnits.Where(x => x.Pos.Chebyshev(u.Pos) <= range && _e.CanBeTargeted(x, Seat, u.Pos, false)).OrderBy(x => x.Hp).FirstOrDefault();
                        if (target == null) target = EnemyHqs.FirstOrDefault(h => h.DistanceTo(u.Pos) <= range);
                        if (target == null) target = S.Structures.Values.FirstOrDefault(s => _e.IsEnemy(Seat, s.Owner) && s.Pos.Chebyshev(u.Pos) <= range);
                        if (target != null) used = Try(new FloopCommand { SourceId = u.Id, Target = TargetSpec.AtEntity(target.Id) });
                        break;
                    }
                    case FloopKind.Sifa:
                    {
                        var hurt = S.Units.Values.Where(x => !x.IsIllusion && _e.IsAlly(Seat, x.Owner) && x.Pos.Chebyshev(u.Pos) <= 3 && _e.Stats.MaxHp(x) - x.Hp >= 2)
                            .OrderByDescending(x => _e.Stats.MaxHp(x) - x.Hp).FirstOrDefault();
                        if (hurt != null) used = Try(new FloopCommand { SourceId = u.Id, Target = TargetSpec.AtEntity(hurt.Id) });
                        break;
                    }
                    case FloopKind.Gizlen:
                        if (!u.Has(StatusKind.Hidden) && EnemyUnits.Any(x => x.Pos.Chebyshev(u.Pos) <= 4))
                            used = Try(new FloopCommand { SourceId = u.Id });
                        break;
                }
                if (!used) break;
            }
        }

        private void StructureFloops()
        {
            foreach (var s in S.StructuresOf(Seat).ToList())
            {
                if (!StillMyTurn) return;
                if (!s.Def.HasFloop || _e.Floops.CanUse(Seat, s.Id) != null) continue;
                switch (s.Def.Floop.Kind)
                {
                    case FloopKind.MancinikAtisi:
                    {
                        int range = _e.Stats.StructureRange(s);
                        Entity target = EnemyUnits.Where(u => u.Pos.Chebyshev(s.Pos) <= range).OrderBy(u => u.Hp).FirstOrDefault();
                        if (target == null) target = S.Structures.Values.FirstOrDefault(x => _e.IsEnemy(Seat, x.Owner) && x.Pos.Chebyshev(s.Pos) <= range);
                        if (target != null) Try(new FloopCommand { SourceId = s.Id, Target = TargetSpec.AtTile(_e.Actions.PosOf(target, s.Pos)) });
                        break;
                    }
                    case FloopKind.SisOrtusu:
                        if (EnemyUnits.Any(u => u.Pos.Chebyshev(s.Pos) <= 4)) Try(new FloopCommand { SourceId = s.Id });
                        break;
                    case FloopKind.Gozlem:
                        if (Me.Deck.Count > 8 && Me.Hand.Count < 3) Try(new FloopCommand { SourceId = s.Id, Target = new TargetSpec { Choice = 0 } });
                        break;
                }
            }
        }

        private void SteerTornadoes()
        {
            foreach (var fx in S.Effects.Values.Where(f => f.Caster == Seat && f.Def.Id == EffectCatalog.Hortum).ToList())
            {
                var center = fx.Tiles.OrderBy(t => t.Row).ThenBy(t => t.Col).ElementAt(fx.Tiles.Count / 2);
                Coord best = fx.Direction;
                int bestCount = -1;
                foreach (var d in Coord.Dirs4)
                {
                    int count = EnemyUnits.Count(u => (u.Pos - (center + d + d)).Chebyshev(new Coord(0, 0)) <= 1);
                    if (count > bestCount) { bestCount = count; best = d; }
                }
                if (best != fx.Direction) Try(new SteerTornadoCommand { EffectId = fx.Id, Direction = best });
            }
        }

        // ---------------- Ultiler ----------------

        private void TryUlti()
        {
            if (!StillMyTurn || Me.UltiUsedThisTurn) return;
            CastUltiCommand best = null;
            double bestScore = 0;
            // K3 açıldıysa ve GE ona yaklaştıysa küçük büyülere harcama (yalnızca çok değerli fırsatlar).
            var k3 = UltiCatalog.ForFaction(Me.Faction).FirstOrDefault(x => x.Tier == UltiTier.K3);
            bool savingForK3 = k3 != null && S.Round >= _e.Rules.K3LockedRounds && Me.K3Uses < _e.Rules.K3MaxUses
                               && Me.Ge >= 60 && Me.Ge < _e.Ultis.Cost(k3);
            foreach (var d in UltiCatalog.ForFaction(Me.Faction))
            {
                if (_e.Ultis.Availability(Seat, d.Id) != null) continue;
                if (savingForK3 && d.Tier != UltiTier.K3 && bestScore < 40) bestScore = 40;
                foreach (var (target, score) in Candidates(d))
                {
                    if (score <= bestScore) continue;
                    var cmd = new CastUltiCommand { Player = Seat, UltiId = d.Id, Target = target };
                    if (_e.Validate(cmd) != null) continue;
                    best = cmd;
                    bestScore = score;
                }
            }
            if (best != null) Try(best);
        }

        private int EnemyUnitsIn(IEnumerable<Coord> tiles) => tiles.Count(t => _e.EntityAt(t) is Unit u && _e.IsEnemy(Seat, u.Owner));
        private int EnemyStructuresIn(IEnumerable<Coord> tiles) => tiles.Count(t => _e.EntityAt(t) is Structure s && _e.IsEnemy(Seat, s.Owner));

        private IEnumerable<(TargetSpec, double)> Candidates(UltiDef d)
        {
            switch (d.Id)
            {
                case UltiCatalog.GeceYarisi:
                    foreach (var p in S.Players.Where(p => p.Alive && _e.IsEnemy(Seat, p.Seat)))
                    {
                        int near = S.UnitsOf(p.Seat).Count(u => S.UnitsOf(Seat).Any(m => m.Pos.Chebyshev(u.Pos) <= 4));
                        if (near >= 2) yield return (TargetSpec.AtPlayer(p.Seat), near * 8);
                    }
                    break;
                case UltiCatalog.Illuzyon:
                {
                    var u = S.UnitsOf(Seat).Where(x => !x.IsIllusion && x.ControlledBy < 0).OrderBy(x => DistToFront(x.Pos)).FirstOrDefault();
                    if (u != null && DistToFront(u.Pos) <= 4) yield return (TargetSpec.AtEntity(u.Id), 12);
                    break;
                }
                case UltiCatalog.ZehirliSis:
                case UltiCatalog.Hortum:
                {
                    int n = d.Size;
                    foreach (var p in S.Players.Where(p => p.Alive && _e.IsEnemy(Seat, p.Seat)))
                        foreach (var q in Board.QuadrantTiles(p.Seat))
                        {
                            var area = Board.Rect(q, n, n);
                            if (Board.CommonQuadrant(area) != p.Seat) continue;
                            double score = EnemyUnitsIn(area) * 12 + EnemyStructuresIn(area) * 6;
                            if (score >= 24) yield return (new TargetSpec { Tile = q }, score);
                        }
                    break;
                }
                case UltiCatalog.GunesPatlamasi:
                case UltiCatalog.MeteorYagmuru:
                {
                    int n = d.Size;
                    foreach (var q in Board.AllTiles())
                    {
                        var area = Board.Rect(q, n, n);
                        if (!Board.AllOnBoard(area)) continue;
                        double score = EnemyUnitsIn(area) * (d.Tier == UltiTier.K3 ? 15 : 10) + EnemyStructuresIn(area) * 6;
                        if (d.Id == UltiCatalog.MeteorYagmuru && EnemyHqs.Any(h => h.Tiles.Any(area.Contains))) score += 20;
                        if (score >= (d.Tier == UltiTier.K3 ? 30 : 20)) yield return (new TargetSpec { Tile = q }, score);
                    }
                    break;
                }
                case UltiCatalog.KavurucuAtes:
                    foreach (var p in S.Players.Where(p => p.Alive && _e.IsEnemy(Seat, p.Seat)))
                        foreach (var shape in FireShapes(p.Seat))
                        {
                            double score = EnemyUnitsIn(shape) * 12 + EnemyStructuresIn(shape) * 6;
                            if (score >= 18) yield return (new TargetSpec { Tiles = shape }, score);
                        }
                    break;
                case UltiCatalog.Tutulma:
                    foreach (var p in S.Players.Where(p => p.Alive && _e.IsEnemy(Seat, p.Seat)))
                    {
                        var eligible = S.Units.Values.Where(u => _e.IsEnemy(Seat, u.Owner) && Board.QuadrantOf(u.Pos) == p.Seat && u.Hp <= 5 && _e.CanBeTargeted(u, Seat, Coord.None, true))
                            .OrderByDescending(u => _e.Stats.Atk(u)).Take(2).Select(u => u.Id).ToList();
                        if (eligible.Count > 0) yield return (new TargetSpec { Player = p.Seat, EntityIds = eligible }, eligible.Count * 20);
                    }
                    break;
                case UltiCatalog.SafakVakti:
                {
                    int engaged = S.UnitsOf(Seat).Count(u => DistToFront(u.Pos) <= 4);
                    if (engaged >= 2) yield return (TargetSpec.None, engaged * 9);
                    break;
                }
                case UltiCatalog.AnkaYukselisi:
                {
                    var tiles = _e.Cards.DeployTiles(Seat);
                    if (tiles.Count > 0) yield return (TargetSpec.AtTile(tiles.OrderBy(DistToFront).First()), 60);
                    break;
                }
                case UltiCatalog.SimsekCarpmasi:
                {
                    foreach (var u in EnemyUnits.Where(x => _e.CanBeTargeted(x, Seat, Coord.None, true)))
                    {
                        bool kills = Math.Max(1, 4 - _e.Stats.Armor(u)) >= u.Hp;
                        yield return (TargetSpec.AtEntity(u.Id), (kills ? 25 : 8) + u.Def.Cost * 4);
                    }
                    foreach (var hq in EnemyHqs.Where(h => h.Hp <= 2))
                        yield return (TargetSpec.AtEntity(hq.Id), 100);
                    break;
                }
                case UltiCatalog.IyilestiriciYagmur:
                {
                    var quad = Board.QuadrantTiles(Seat).ToList();
                    int missing = S.UnitsOf(Seat).Where(u => quad.Contains(u.Pos) && !u.IsIllusion).Sum(u => _e.Stats.MaxHp(u) - u.Hp)
                                  + S.StructuresOf(Seat).Sum(s => s.MaxHp - s.Hp);
                    int fire = quad.Count(t => _e.Effects.TileHas(t, EffectTag.Ates));
                    double score = missing * 2 + fire * 4;
                    if (score >= 10) yield return (TargetSpec.None, score);
                    break;
                }
                case UltiCatalog.SuperHucre:
                    foreach (var p in S.Players.Where(p => p.Alive && _e.IsEnemy(Seat, p.Seat)))
                    {
                        int count = EnemyUnitsIn(Board.QuadrantTiles(p.Seat));
                        if (count >= 2) yield return (TargetSpec.AtPlayer(p.Seat), count * 15);
                    }
                    break;
                case UltiCatalog.KayanYildiz:
                {
                    var u = S.UnitsOf(Seat).Where(x => !x.IsIllusion && x.ControlledBy < 0).OrderByDescending(x => _e.Stats.Atk(x) + x.Hp).FirstOrDefault();
                    var hq = u == null ? null : EnemyHqs.OrderBy(h => h.Hp).FirstOrDefault();
                    if (u == null || hq == null || hq.DistanceTo(u.Pos) <= 4) break;
                    var tile = Board.AllTiles().Where(t => _e.Grid.IsEmpty(t) && EnemyHqs.All(h => h.DistanceTo(t) >= 2))
                        .OrderBy(t => hq.DistanceTo(t)).FirstOrDefault();
                    if (tile.IsOnBoard) yield return (new TargetSpec { EntityId = u.Id, Tile = tile }, 22);
                    break;
                }
                case UltiCatalog.KaraDelik:
                    foreach (var c in Board.AllTiles())
                    {
                        int count = EnemyUnitsIn(Board.Around(c, 2, true));
                        if (count >= 3) yield return (TargetSpec.AtTile(c), count * 10);
                    }
                    break;
                case UltiCatalog.YildizGecidi:
                    break;
            }
        }

        private IEnumerable<List<Coord>> FireShapes(int quadrant)
        {
            var tiles = Board.QuadrantTiles(quadrant).ToList();
            int r0 = tiles.Min(t => t.Row), c0 = tiles.Min(t => t.Col);
            for (int i = 0; i < Board.QuadrantSize; i++)
            {
                yield return Enumerable.Range(0, 6).Select(k => new Coord(r0 + i, c0 + k)).Where(t => !_e.Grid.IsHqTile(t)).ToList();
                yield return Enumerable.Range(0, 6).Select(k => new Coord(r0 + k, c0 + i)).Where(t => !_e.Grid.IsHqTile(t)).ToList();
            }
            for (int r = 0; r <= 4; r++)
                for (int c = 0; c <= 3; c++)
                {
                    var rect = Board.Rect(new Coord(r0 + r, c0 + c), 2, 3);
                    if (!rect.Any(_e.Grid.IsHqTile)) yield return rect;
                }
        }
    }
}
