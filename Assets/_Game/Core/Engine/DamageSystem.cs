using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    public struct DamageSource
    {
        public int Player;
        public int EntityId;
        public DamageKind Kind;
        public bool IsUlti;
        /// <summary>Gök Kalkanı'nın yarıya indirdiği "alan büyüsü" hasarı mı.</summary>
        public bool IsArea;
        /// <summary>Kümülüs Devi'nin azalttığı menzilli hasar mı.</summary>
        public bool IsRanged;
        public bool FromUnit;
        public int CastId;
        public string Label;

        public static DamageSource None => new DamageSource { Player = -1, EntityId = -1 };
    }

    /// <summary>Hasar, iyileşme, ölüm ve karargah kuralları (GDD Bölüm 2, 4 ve 6).</summary>
    public sealed class DamageSystem
    {
        private readonly RulesEngine _e;
        private readonly List<(Entity entity, DamageSource source)> _pending = new List<(Entity, DamageSource)>();
        private readonly HashSet<int> _pendingIds = new HashSet<int>();

        public DamageSystem(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;

        public int Deal(Entity target, int amount, DamageSource src)
        {
            switch (target)
            {
                case Unit u: return DealToUnit(u, amount, src);
                case Structure s: return DealToStructure(s, amount, src);
                case Headquarters h: return DealToHq(h, amount, src);
                default: return 0;
            }
        }

        /// <summary>Gök Kalkanı: çevresindeki 3×3 alana düşen rakip alan büyülerinin hasarını yarıya indirir.</summary>
        private bool ShieldedAt(Coord c, int caster)
        {
            foreach (var s in S.Structures.Values)
                if (s.Def.Passive == CardPassive.GokKalkani && !s.IsDead && s.Pos.Chebyshev(c) <= 1 && _e.IsEnemy(caster, s.Owner))
                    return true;
            return false;
        }

        private int ApplyShield(int amount, bool shielded, string targetName, int caster)
        {
            if (!shielded) return amount;
            int halved = amount / 2;
            _e.Say($"Gök Kalkanı {targetName} üzerindeki büyü hasarını {amount} → {halved} indirdi.", caster);
            return halved;
        }

        private bool KumulusProtects(Unit u)
        {
            foreach (var other in S.Units.Values)
                if (other != u && !other.IsDead && !other.IsIllusion && other.Def.Passive == CardPassive.KumulusKalkani &&
                    _e.IsAlly(other.Owner, u.Owner) && other.Pos.Chebyshev(u.Pos) == 1)
                    return true;
            return false;
        }

        public int DealToUnit(Unit u, int amount, DamageSource src)
        {
            if (u == null || u.IsDead || amount <= 0 || !S.Units.ContainsKey(u.Id)) return 0;
            int d = amount;
            if (src.IsUlti && src.IsArea) d = ApplyShield(d, ShieldedAt(u.Pos, src.Player), u.Def.Name, src.Player);
            if (d <= 0) return 0;

            if (u.IsIllusion)
            {
                RevealIllusion(u, src.Player);
                return 1;
            }

            bool ignoreArmor = false;
            if (src.Kind == DamageKind.Attack && u.Has(StatusKind.Marked))
            {
                d += 2;
                ignoreArmor = true;
                u.Statuses.RemoveAll(s => s.Kind == StatusKind.Marked);
                _e.Say($"İşaret tetiklendi: {u.Def.Name} +2 hasar alıyor ve Zırh yok sayılıyor.", src.Player);
            }
            int reduction = ignoreArmor ? 0 : _e.Stats.Armor(u);
            if (src.IsRanged && KumulusProtects(u)) reduction += 1;
            d = Math.Max(1, d - reduction);

            u.Hp -= d;
            if (src.Player >= 0) u.LastDamagedByPlayer = src.Player;
            if (u.Has(StatusKind.Hidden))
            {
                u.Statuses.RemoveAll(s => s.Kind == StatusKind.Hidden);
                _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = u.Id, Id = "hidden_off", Message = u.Def.Name + " hasar aldı, Gizli bozuldu." });
            }
            _e.Emit(new GameEvent
            {
                Type = GameEventType.Damaged, Player = src.Player, EntityId = u.Id, Amount = d, To = u.Pos,
                Message = $"{u.Def.Name} {d} hasar aldı" + (src.Label != null ? $" ({src.Label})" : "") + $". CAN {Math.Max(0, u.Hp)}."
            });
            if (u.Hp <= 0) Queue(u, src);
            return d;
        }

        public int DealToStructure(Structure s, int amount, DamageSource src)
        {
            if (s == null || s.IsDead || amount <= 0 || !S.Structures.ContainsKey(s.Id)) return 0;
            int d = amount;
            if (src.IsUlti && src.IsArea) d = ApplyShield(d, ShieldedAt(s.Pos, src.Player), s.Def.Name, src.Player);
            if (d <= 0) return 0;
            d = Math.Max(1, d);
            s.Hp -= d;
            if (src.Player >= 0) s.LastDamagedByPlayer = src.Player;
            _e.Emit(new GameEvent
            {
                Type = GameEventType.Damaged, Player = src.Player, EntityId = s.Id, Amount = d, To = s.Pos,
                Message = $"{s.Def.Name} {d} hasar aldı" + (src.Label != null ? $" ({src.Label})" : "") + $". CAN {Math.Max(0, s.Hp)}."
            });
            if (s.Hp <= 0) Queue(s, src);
            return d;
        }

        /// <summary>
        /// Karargah hasarı. Ulti hasarı: büyü başına en fazla 3, raund başına en fazla 5, Son Siper'de 0.
        /// Deste bitimi ve Gök Çöküşü Zırh'tan etkilenmez (darbe değildirler).
        /// </summary>
        public int DealToHq(Headquarters hq, int amount, DamageSource src)
        {
            if (hq == null || hq.IsDead || amount <= 0) return 0;
            var owner = S.Players[hq.Owner];
            int d = amount;
            if (src.IsUlti && src.IsArea)
            {
                bool shielded = hq.Tiles.Any(t => ShieldedAt(t, src.Player));
                d = ApplyShield(d, shielded, "karargah", src.Player);
                if (d <= 0) return 0;
            }
            if (src.Kind != DamageKind.Fatigue && src.Kind != DamageKind.Collapse)
                d = Math.Max(1, d - _e.Stats.HqArmor(hq.Owner));

            if (src.IsUlti)
            {
                if (hq.LastStandActive)
                {
                    _e.Say($"Son Siper: {owner.Name} karargahı ulti hasarına bağışık.", hq.Owner);
                    return 0;
                }
                if (hq.UltiDamageRound != S.Round)
                {
                    hq.UltiDamageRound = S.Round;
                    hq.UltiDamageThisRound = 0;
                }
                hq.UltiDamageByCast.TryGetValue(src.CastId, out var castDealt);
                int capCast = _e.Rules.UltiHqCapPerCast - castDealt;
                int capRound = _e.Rules.UltiHqCapPerRound - hq.UltiDamageThisRound;
                int capped = Math.Min(d, Math.Min(capCast, capRound));
                if (capped < d)
                    _e.Say($"Karargah hasar tavanı: {d} → {Math.Max(0, capped)}.", hq.Owner);
                d = capped;
                if (d <= 0) return 0;
                hq.UltiDamageByCast[src.CastId] = castDealt + d;
                hq.UltiDamageThisRound += d;
                S.Stats.UltiHqDamage += d;
            }

            hq.Hp -= d;
            if (src.Player >= 0) hq.LastDamagedByPlayer = src.Player;
            S.Stats.TotalHqDamage += d;
            S.Stats.AddHqDamage(hq.Owner, S.Round, d);
            _e.Emit(new GameEvent
            {
                Type = GameEventType.Damaged, Player = src.Player, EntityId = hq.Id, Amount = d, To = hq.Tiles[0], Id = src.Kind.ToString(),
                Message = $"{owner.Name} karargahı {d} hasar aldı" + (src.Label != null ? $" ({src.Label})" : "") + $". CAN {Math.Max(0, hq.Hp)}."
            });
            _e.GainGe(hq.Owner, d * _e.Rules.GePerHqDamage, "karargah hasarı");

            if (hq.Hp <= 0)
            {
                Queue(hq, src);
            }
            else if (!hq.LastStandTriggered && hq.Hp < _e.Rules.LastStandThreshold)
            {
                hq.LastStandTriggered = true;
                hq.LastStandEndsAfterOwnerTurn = owner.TurnsTaken + 1;
                _e.Emit(new GameEvent { Type = GameEventType.LastStand, Player = hq.Owner, EntityId = hq.Id, Message = $"Son Siper! {owner.Name} karargahı sonraki turunun sonuna kadar ulti hasarına bağışık; 1 kart çekiyor." });
                _e.Cards.Draw(hq.Owner, 1);
            }
            return d;
        }

        public void HealUnit(Unit u, int amount, string reason)
        {
            if (u == null || u.IsDead || u.IsIllusion || amount <= 0) return;
            int max = _e.Stats.MaxHp(u);
            int before = u.Hp;
            u.Hp = Math.Min(max, u.Hp + amount);
            if (u.Hp > before)
                _e.Emit(new GameEvent { Type = GameEventType.Healed, Player = u.Owner, EntityId = u.Id, Amount = u.Hp - before, To = u.Pos, Message = $"{u.Def.Name} {u.Hp - before} CAN iyileşti ({reason})." });
        }

        public void HealStructure(Structure s, int amount, string reason)
        {
            if (s == null || s.IsDead || amount <= 0) return;
            int before = s.Hp;
            s.Hp = Math.Min(s.MaxHp, s.Hp + amount);
            if (s.Hp > before)
                _e.Emit(new GameEvent { Type = GameEventType.Healed, Player = s.Owner, EntityId = s.Id, Amount = s.Hp - before, To = s.Pos, Message = $"{s.Def.Name} {s.Hp - before} CAN iyileşti ({reason})." });
        }

        /// <summary>İllüzyon kopyası vurulunca kaybolur.</summary>
        public void RevealIllusion(Unit u, int byPlayer)
        {
            if (u.IsDead) return;
            _e.Emit(new GameEvent { Type = GameEventType.IllusionRevealed, Player = byPlayer, EntityId = u.Id, To = u.Pos, Message = $"{u.Def.Name} bir İllüzyon'du ve dağıldı!" });
            u.Hp = 0;
            Queue(u, new DamageSource { Player = byPlayer, EntityId = -1, Kind = DamageKind.Attack });
        }

        /// <summary>Kıvılcım Koşucusu gibi kendini feda eden birimler.</summary>
        public void Sacrifice(Unit u)
        {
            u.Hp = 0;
            Queue(u, new DamageSource { Player = u.Owner, EntityId = u.Id, Kind = DamageKind.Sacrifice, FromUnit = true });
            ResolveDeaths();
        }

        private void Queue(Entity e, DamageSource src)
        {
            if (_pendingIds.Add(e.Id)) _pending.Add((e, src));
        }

        public void ResolveDeaths()
        {
            int guard = 0;
            while (_pending.Count > 0 && guard++ < 1000)
            {
                var batch = _pending.ToList();
                _pending.Clear();
                _pendingIds.Clear();
                foreach (var (entity, src) in batch)
                {
                    switch (entity)
                    {
                        case Unit u: HandleUnitDeath(u, src); break;
                        case Structure s: HandleStructureDeath(s, src); break;
                        case Headquarters h: _e.Turns.Eliminate(h.Owner, src.Player); break;
                    }
                }
            }
        }

        private void HandleStructureDeath(Structure s, DamageSource src)
        {
            if (s.IsDead || !S.Structures.ContainsKey(s.Id)) return;
            s.IsDead = true;
            S.Structures.Remove(s.Id);
            _e.Grid.ClearOccupant(s.Pos, s.Id);
            var owner = S.Players[s.Owner];
            if (s.CardInstanceId >= 0) owner.Discard.Add(new CardInstance { InstanceId = s.CardInstanceId, CardId = s.CardId });
            _e.Emit(new GameEvent { Type = GameEventType.EntityDestroyed, Player = src.Player, EntityId = s.Id, To = s.Pos, Id = s.CardId, Message = $"{s.Def.Name} yıkıldı." });
        }

        private void HandleUnitDeath(Unit u, DamageSource src)
        {
            if (u.IsDead || !S.Units.ContainsKey(u.Id)) return;
            if (u.Hp > 0) return; // bu arada iyileşmiş olabilir

            if (u.IsIllusion)
            {
                RemoveUnit(u, src, silent: true);
                return;
            }

            int killer = src.Player >= 0 ? src.Player : u.LastDamagedByPlayer;
            bool enemyKill = killer >= 0 && _e.IsEnemy(killer, u.Owner);

            // Ölüm ödülleri (Bölüm 2): rakip birim yok etmek +8, kendi biriminin yok edilmesi +6.
            if (enemyKill) _e.GainGe(killer, _e.Rules.GeKill, "rakip birim yok edildi");
            _e.GainGe(u.Owner, _e.Rules.GeOwnLoss, "birim kaybı");

            // Anka: ilk ölümünde aynı karede 3 CAN ile yeniden doğar.
            if (u.CardId == CardCatalog.AnkaKusu && !u.Reborn)
            {
                u.Reborn = true;
                u.Hp = 3;
                u.Statuses.Clear();
                _e.Emit(new GameEvent { Type = GameEventType.UnitReborn, Player = u.Owner, EntityId = u.Id, To = u.Pos, Message = "Anka Kuşu küllerinden yeniden doğdu (3 CAN)!" });
                return;
            }

            var pos = u.Pos;
            RemoveUnit(u, src, silent: false);

            // Kor İzi (Güneş pasifi): rakip birimi yok eden Güneş birimi o kareye 2 tur süren Kor bırakır.
            if (enemyKill && src.FromUnit && S.Players[killer].Faction == Faction.Gunes)
                _e.Effects.Apply(EffectCatalog.Kor, killer, new[] { pos }, EffectCatalog.Get(EffectCatalog.Kor).DurationTurns, 0, false, Coord.None);

            // Nova Golemi: yok olduğunda çevresindeki 8 kareye 2 hasar (dost ve düşman).
            if (u.Def.Passive == CardPassive.NovaPatlamasi)
            {
                _e.Say("Nova Golemi süpernova oldu!", u.Owner);
                var nova = new DamageSource { Player = u.Owner, EntityId = u.Id, Kind = DamageKind.Passive, FromUnit = true, Label = "Nova" };
                var hitHqs = new HashSet<int>();
                foreach (var t in Board.Around(pos, 1, false))
                {
                    var ent = _e.EntityAt(t);
                    if (ent is Headquarters hq)
                    {
                        if (hitHqs.Add(hq.Owner)) DealToHq(hq, 2, nova);
                    }
                    else if (ent != null) Deal(ent, 2, nova);
                }
            }
        }

        private void RemoveUnit(Unit u, DamageSource src, bool silent)
        {
            u.IsDead = true;
            S.Units.Remove(u.Id);
            _e.Grid.ClearOccupant(u.Pos, u.Id);
            var owner = S.Players[u.Owner];
            if (u.CardInstanceId >= 0) owner.Discard.Add(new CardInstance { InstanceId = u.CardInstanceId, CardId = u.CardId });
            if (u.WeaponInstanceId >= 0) owner.Discard.Add(new CardInstance { InstanceId = u.WeaponInstanceId, CardId = u.WeaponCardId });
            if (u.ArmorInstanceId >= 0) owner.Discard.Add(new CardInstance { InstanceId = u.ArmorInstanceId, CardId = u.ArmorCardId });
            if (!silent)
                _e.Emit(new GameEvent { Type = GameEventType.EntityDestroyed, Player = src.Player, EntityId = u.Id, To = u.Pos, Id = u.CardId, Message = $"{u.Def.Name} ({owner.Name}) yok oldu." });
            else
                _e.Emit(new GameEvent { Type = GameEventType.EntityDestroyed, Player = src.Player, EntityId = u.Id, To = u.Pos, Id = u.CardId });
        }
    }
}
