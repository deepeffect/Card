using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// Birim eylemleri (GDD Bölüm 2 ve 4): Hareket, Saldırı, Savun; her biri 1 EN ve tur başına en fazla 1 kez.
    /// Ayrıca Alacakaranlık'taki otomatik yapı atışları, karargah karşılığı ve Anka yanması.
    /// </summary>
    public sealed class UnitActionSystem
    {
        private readonly RulesEngine _e;

        public UnitActionSystem(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;

        /// <summary>Komutu veren oyuncu bu birimi yönetebilir mi (kendi birimi ya da Tutulma ile yönettiği).</summary>
        private string CheckCommandable(int player, Unit u)
        {
            if (u == null || u.IsDead) return "Birim bulunamadı.";
            if (u.ControlledBy >= 0) return u.ControlledBy == player ? null : "Bu birimi şu an başkası yönetiyor.";
            if (u.Owner != player) return "Bu birim senin değil.";
            return null;
        }

        private bool IsControlled(Unit u, int player) => u.ControlledBy == player && u.Owner != player;

        // ---------------- Hareket ----------------

        /// <summary>Bulut pasifi "Arka Rüzgâr": tur başına bir Bulut biriminin Hareket'i EN harcamaz.</summary>
        public bool NextMoveIsFree(Unit u)
        {
            var p = S.Players[u.Owner];
            return p.Faction == Faction.Bulut && !p.FreeMoveUsed && u.ControlledBy < 0;
        }

        public Dictionary<Coord, PathResult> ReachableFor(Unit u) => _e.Grid.Reachable(u, _e.Stats.Speed(u));

        public string ValidateMove(MoveCommand c)
        {
            S.Units.TryGetValue(c.UnitId, out var u);
            var err = CheckCommandable(c.Player, u);
            if (err != null) return err;
            if (IsControlled(u, c.Player))
            {
                if (!u.ControlMoveLeft) return "Yönetilen birim bu tur zaten hareket etti.";
            }
            else
            {
                if (u.Moved) return "Birim bu tur zaten hareket etti.";
                if (!NextMoveIsFree(u) && u.En < 1) return "Yeterli EN yok.";
            }
            int speed = _e.Stats.Speed(u);
            if (speed <= 0) return "Birim hareket edemiyor (HIZ 0).";
            if (!ReachableFor(u).ContainsKey(c.To)) return "Bu kareye ulaşılamaz.";
            return null;
        }

        public void ApplyMove(MoveCommand c)
        {
            var u = S.Units[c.UnitId];
            var path = ReachableFor(u)[c.To];
            if (IsControlled(u, c.Player)) u.ControlMoveLeft = false;
            else
            {
                if (NextMoveIsFree(u)) S.Players[u.Owner].FreeMoveUsed = true;
                else u.En--;
                u.Moved = true;
            }
            Walk(u, path, "ilerledi");
        }

        public string ValidateRetreat(RetreatCommand c)
        {
            S.Units.TryGetValue(c.UnitId, out var u);
            var err = CheckCommandable(c.Player, u);
            if (err != null) return err;
            if (!u.RetreatAvailable) return "Vur-Kaç geri çekilmesi kullanılamaz.";
            int steps = Math.Min(2, _e.Stats.Speed(u));
            if (steps <= 0) return "Birim hareket edemiyor.";
            if (!_e.Grid.Reachable(u, steps).ContainsKey(c.To)) return "Geri çekilme için en fazla 2 kare.";
            return null;
        }

        public void ApplyRetreat(RetreatCommand c)
        {
            var u = S.Units[c.UnitId];
            int steps = Math.Min(2, _e.Stats.Speed(u));
            var path = _e.Grid.Reachable(u, steps)[c.To];
            u.RetreatAvailable = false;
            Walk(u, path, "geri çekildi");
        }

        /// <summary>
        /// Yol boyunca ilerler. Dost birimlerin içinden geçerken kareyi doldurmaz.
        /// Uçan olmayan birimler her girdiği karenin giriş etkisini alır; Uçan yalnızca vardığı karenin.
        /// </summary>
        private void Walk(Unit u, PathResult path, string verb)
        {
            var from = u.Pos;
            _e.Grid.ClearOccupant(from, u.Id);
            var tiles = new List<Coord>();
            foreach (var step in path.Steps)
            {
                if (step.Teleport)
                    _e.Emit(new GameEvent { Type = GameEventType.UnitTeleported, Player = u.Owner, EntityId = u.Id, From = u.Pos, To = step.Tile, Message = $"{u.Def.Name} Yıldız Geçidi'nden geçti." });
                u.Pos = step.Tile;
                tiles.Add(step.Tile);
                if (!u.Flying) _e.Effects.RunOnEnter(u, step.Tile);
                if (u.Hp <= 0) break;
            }
            if (u.Hp > 0 && u.Flying) _e.Effects.RunOnEnter(u, u.Pos);
            if (u.Hp > 0 || _e.Grid.OccupantAt(u.Pos) == 0) _e.Grid.SetOccupant(u.Pos, u.Id);
            _e.Emit(new GameEvent { Type = GameEventType.UnitMoved, Player = u.ActingPlayer, EntityId = u.Id, From = from, To = u.Pos, Tiles = tiles, Message = $"{u.Def.Name} {verb}: {from} → {u.Pos}." });
        }

        // ---------------- Saldırı ----------------

        /// <summary>Birimin şu an saldırabileceği hedefler.</summary>
        public List<Entity> AttackTargets(Unit u)
        {
            var list = new List<Entity>();
            int side = u.ActingPlayer;
            int range = _e.Stats.Range(u);
            foreach (var other in S.Units.Values)
                if (_e.IsEnemy(side, other.ActingPlayer) && other.Pos.Chebyshev(u.Pos) <= range && _e.CanBeTargeted(other, side, u.Pos, false))
                    list.Add(other);
            foreach (var s in S.Structures.Values)
                if (_e.IsEnemy(side, s.Owner) && s.Pos.Chebyshev(u.Pos) <= range)
                    list.Add(s);
            if (u.ControlledBy < 0)
                foreach (var hq in S.Hqs)
                    if (!hq.IsDead && _e.IsEnemy(side, hq.Owner) && hq.DistanceTo(u.Pos) <= range)
                        list.Add(hq);
            return list;
        }

        public string ValidateAttack(AttackCommand c)
        {
            S.Units.TryGetValue(c.UnitId, out var u);
            var err = CheckCommandable(c.Player, u);
            if (err != null) return err;
            if (IsControlled(u, c.Player))
            {
                if (!u.ControlAttackLeft) return "Yönetilen birim bu tur zaten saldırdı.";
            }
            else
            {
                if (u.Attacked) return "Birim bu tur zaten saldırdı.";
                if (u.En < 1) return "Yeterli EN yok.";
            }
            var target = S.GetEntity(c.TargetId);
            if (target == null || target.IsDead) return "Hedef bulunamadı.";
            if (target is Headquarters && u.ControlledBy >= 0) return "Yönetilen birimler karargahlara saldıramaz.";
            if (!AttackTargets(u).Contains(target)) return "Hedef menzil dışında ya da hedef alınamaz.";
            return null;
        }

        public void ApplyAttack(AttackCommand c)
        {
            var u = S.Units[c.UnitId];
            var target = S.GetEntity(c.TargetId);
            int side = u.ActingPlayer;
            bool controlled = IsControlled(u, c.Player);
            if (controlled) u.ControlAttackLeft = false;
            else
            {
                u.En--;
                u.Attacked = true;
            }

            bool wasHidden = u.Has(StatusKind.Hidden);
            if (wasHidden) u.Statuses.RemoveAll(s => s.Kind == StatusKind.Hidden);
            int range = _e.Stats.Range(u);
            bool melee = range <= 1;
            _e.Emit(new GameEvent { Type = GameEventType.AttackPerformed, Player = side, EntityId = u.Id, TargetId = target.Id, From = u.Pos, To = PosOf(target, u.Pos), Message = $"{u.Def.Name} → {_e.EntityName(target)} saldırısı." });

            // İllüzyona saldıran birimin EN'si 0 olur.
            if (target is Unit tu && tu.IsIllusion)
            {
                _e.Damage.RevealIllusion(tu, side);
                if (!controlled) u.En = 0;
                _e.Say($"{u.Def.Name} bir kopyaya saldırdı: EN'si 0'a düştü.", side);
                return;
            }

            int atk = _e.Stats.Atk(u);
            if (wasHidden && _e.Stats.HasUpgradePassive(u, CardPassive.HilalHancer)) atk += 2;
            if (u.Has(StatusKind.Blinded)) { atk = 0; _e.Say($"{u.Def.Name} Kör: saldırı ıskaladı.", side); }

            var src = new DamageSource { Player = side, EntityId = u.Id, Kind = DamageKind.Attack, IsRanged = !melee, FromUnit = true, Label = u.Def.Name };
            if (atk > 0) _e.Damage.Deal(target, atk, src);

            // Karşı saldırı: yakın dövüşte hayatta kalan hedef, EN harcamadan kendi SAL'i kadar karşılık verir.
            if (melee && target is Unit defender && !defender.IsDead && defender.Hp > 0 && !defender.IsIllusion)
            {
                int counter = defender.Has(StatusKind.Blinded) ? 0 : _e.Stats.Atk(defender);
                if (counter > 0)
                {
                    _e.Say($"{defender.Def.Name} karşı saldırı yapıyor.", defender.ActingPlayer);
                    _e.Damage.DealToUnit(u, counter, new DamageSource { Player = defender.ActingPlayer, EntityId = defender.Id, Kind = DamageKind.Counter, FromUnit = true, Label = "karşı saldırı" });
                }
            }

            if (atk > 0) ApplyOnHitEffects(u, target, side);

            if (u.Has(StatusKind.HitAndRun) && u.Hp > 0) u.RetreatAvailable = true;
        }

        private void ApplyOnHitEffects(Unit attacker, Entity target, int side)
        {
            var dir = (PosOf(target, attacker.Pos) - attacker.Pos).Sign();

            if (target is Unit tu && tu.Hp > 0 && !tu.IsDead)
            {
                // Güneş Kılıcı: vurduğu birim Yanık olur.
                if (_e.Stats.HasUpgradePassive(attacker, CardPassive.GunesKilici) && !tu.Has(StatusKind.Burning))
                {
                    tu.Statuses.Add(new Status { Kind = StatusKind.Burning, Value = 1, SourcePlayer = side, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = tu.Owner });
                    _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = tu.Id, Id = "burning", Message = tu.Def.Name + " Yanık oldu." });
                }
                // Fırtına Mızrağı: vurduğu birimi 1 kare geri iter, engele çarparsa 1 hasar.
                if (_e.Stats.HasUpgradePassive(attacker, CardPassive.FirtinaMizragi))
                    _e.Forced.Push(tu, dir, 1, new DamageSource { Player = side, EntityId = attacker.Id, Kind = DamageKind.Collision, FromUnit = true, Label = "çarpma" }, 1);
            }

            // Hücum: hedefin arkasındaki kareye 1 hasar.
            if (attacker.Has(StatusKind.Charge))
            {
                var behind = PosOf(target, attacker.Pos) + dir;
                var ent = _e.EntityAt(behind);
                if (ent != null && ent != target && _e.IsEnemy(side, OwnerOf(ent)))
                    _e.Damage.Deal(ent, 1, new DamageSource { Player = side, EntityId = attacker.Id, Kind = DamageKind.Floop, FromUnit = true, Label = "Hücum" });
            }

            // Ok Yağmuru: hedefe bitişik ikinci bir rakibe 1 hasar.
            if (attacker.Has(StatusKind.ArrowRain))
            {
                var second = PickArrowRainTarget(attacker, target, side);
                if (second != null)
                    _e.Damage.Deal(second, 1, new DamageSource { Player = side, EntityId = attacker.Id, Kind = DamageKind.Floop, IsRanged = true, FromUnit = true, Label = "Ok Yağmuru" });
            }
        }

        internal Entity PickArrowRainTarget(Unit attacker, Entity primary, int side, int preferredId = -1)
        {
            var center = PosOf(primary, attacker.Pos);
            Entity best = null;
            foreach (var t in Board.Around(center, 1, false))
            {
                var ent = _e.EntityAt(t);
                if (ent == null || ent == primary || ent is Headquarters || !_e.IsEnemy(side, OwnerOf(ent))) continue;
                if (ent is Unit eu && !_e.CanBeTargeted(eu, side, attacker.Pos, false)) continue;
                if (ent.Id == preferredId) return ent;
                if (best == null || ent.Hp < best.Hp || (ent.Hp == best.Hp && ent.Id < best.Id)) best = ent;
            }
            return best;
        }

        public int OwnerOf(Entity e) => e is Unit u ? u.ActingPlayer : e.Owner;

        /// <summary>Varlığın saldırana en yakın karesi.</summary>
        public Coord PosOf(Entity e, Coord from)
        {
            switch (e)
            {
                case Unit u: return u.Pos;
                case Structure s: return s.Pos;
                case Headquarters h:
                    return h.Tiles.OrderBy(t => t.Chebyshev(from)).ThenBy(t => t.Manhattan(from)).First();
                default: return Coord.None;
            }
        }

        // ---------------- Savun ----------------

        public string ValidateDefend(DefendCommand c)
        {
            S.Units.TryGetValue(c.UnitId, out var u);
            if (u == null || u.IsDead || u.Owner != c.Player || u.ControlledBy >= 0) return "Birim bulunamadı.";
            if (u.Defended) return "Birim bu tur zaten savundu.";
            if (u.En < 1) return "Yeterli EN yok.";
            return null;
        }

        public void ApplyDefend(DefendCommand c)
        {
            var u = S.Units[c.UnitId];
            u.En--;
            u.Defended = true;
            u.Statuses.RemoveAll(s => s.Kind == StatusKind.Defending);
            u.Statuses.Add(new Status { Kind = StatusKind.Defending, Value = _e.Rules.DefendArmor, SourcePlayer = u.Owner, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = u.Owner });
            _e.Emit(new GameEvent { Type = GameEventType.Defended, Player = u.Owner, EntityId = u.Id, To = u.Pos, Message = $"{u.Def.Name} savunmaya geçti (+{_e.Rules.DefendArmor} Zırh)." });
        }

        // ---------------- Alacakaranlık ----------------

        /// <summary>Okçu Kulesi: tur sonunda menzildeki en düşük CAN'lı rakip birime 2 hasar.</summary>
        public void StructureAutoAttacks(int seat)
        {
            foreach (var s in S.StructuresOf(seat).OrderBy(x => x.Id).ToList())
            {
                if (s.IsDead || s.Def.Passive != CardPassive.OkcuKulesi) continue;
                int range = _e.Stats.StructureRange(s);
                Unit best = null;
                foreach (var u in S.Units.Values)
                {
                    if (!_e.IsEnemy(seat, u.ActingPlayer) || u.Pos.Chebyshev(s.Pos) > range) continue;
                    if (!_e.CanBeTargeted(u, seat, s.Pos, false)) continue;
                    if (best == null || u.Hp < best.Hp || (u.Hp == best.Hp && u.Id < best.Id)) best = u;
                }
                if (best == null) continue;
                _e.Emit(new GameEvent { Type = GameEventType.AttackPerformed, Player = seat, EntityId = s.Id, TargetId = best.Id, From = s.Pos, To = best.Pos, Message = $"{s.Def.Name} {best.Def.Name} birimine ok attı." });
                _e.Damage.DealToUnit(best, 2, new DamageSource { Player = seat, EntityId = s.Id, Kind = DamageKind.Structure, IsRanged = true, Label = s.Def.Name });
            }
        }

        /// <summary>Karargah, bitişiğindeki her rakip birime 1 hasar verir.</summary>
        public void HqRetaliation(int seat)
        {
            var hq = S.Hqs[seat];
            if (hq.IsDead) return;
            foreach (var u in S.Units.Values.Where(x => _e.IsEnemy(seat, x.ActingPlayer) && hq.DistanceTo(x.Pos) == 1).OrderBy(x => x.Id).ToList())
                _e.Damage.DealToUnit(u, 1, new DamageSource { Player = seat, EntityId = hq.Id, Kind = DamageKind.HqRetaliation, Label = "karargah savunması" });
        }

        /// <summary>Anka Kuşu: her kendi tur sonunda çevresindeki 8 kareye 1 hasar.</summary>
        public void AnkaBurn(int seat)
        {
            foreach (var anka in S.UnitsOf(seat).Where(u => u.CardId == CardCatalog.AnkaKusu).ToList())
            {
                if (anka.IsDead) continue;
                var src = new DamageSource { Player = seat, EntityId = anka.Id, Kind = DamageKind.Passive, FromUnit = true, Label = "Anka alevi" };
                var hitHq = new HashSet<int>();
                foreach (var t in Board.Around(anka.Pos, 1, false))
                {
                    var ent = _e.EntityAt(t);
                    if (ent == null || !_e.IsEnemy(seat, OwnerOf(ent))) continue;
                    if (ent is Headquarters hq)
                    {
                        if (hitHq.Add(hq.Owner)) _e.Damage.DealToHq(hq, 1, src);
                    }
                    else _e.Damage.Deal(ent, 1, src);
                }
            }
        }
    }
}
