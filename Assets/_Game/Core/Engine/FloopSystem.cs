using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// Floop (aktif yetenek, GDD Bölüm 4): tahtadaki kart başına turda 1 kez; birimde AP + 1 EN,
    /// yapıda yalnızca AP. Galaksi Asası taşıyan birimde turda 2 kez.
    /// </summary>
    public sealed class FloopSystem
    {
        private readonly RulesEngine _e;

        public FloopSystem(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;

        public int MaxFloopsPerTurn(Unit u) => _e.Stats.HasUpgradePassive(u, CardPassive.GalaksiAsasi) ? 2 : 1;

        /// <summary>Floop kullanılabilir mi (hedeften bağımsız ön koşullar).</summary>
        public string CanUse(int player, int sourceId)
        {
            var p = S.Players[player];
            if (S.Units.TryGetValue(sourceId, out var u))
            {
                if (u.Owner != player || u.IsIllusion) return "Bu birim senin değil.";
                if (u.ControlledBy >= 0) return "Yönetilen birimler Floop kullanamaz.";
                if (!u.Def.HasFloop) return "Bu kartın Floop'u yok.";
                if (u.FloopsUsed >= MaxFloopsPerTurn(u)) return "Floop bu tur zaten kullanıldı.";
                if (u.En < 1) return "Yeterli EN yok.";
                if (p.Ap < u.Def.Floop.Cost) return $"Yeterli AP yok ({u.Def.Floop.Cost} gerekli).";
                return null;
            }
            if (S.Structures.TryGetValue(sourceId, out var s))
            {
                if (s.Owner != player) return "Bu yapı senin değil.";
                if (!s.Def.HasFloop) return "Bu kartın Floop'u yok.";
                if (s.FloopsUsed >= 1) return "Floop bu tur zaten kullanıldı.";
                if (p.Ap < s.Def.Floop.Cost) return $"Yeterli AP yok ({s.Def.Floop.Cost} gerekli).";
                return null;
            }
            return "Kaynak bulunamadı.";
        }

        public string Validate(FloopCommand c)
        {
            var err = CanUse(c.Player, c.SourceId);
            if (err != null) return err;
            var t = c.Target ?? TargetSpec.None;
            var src = S.GetEntity(c.SourceId);
            var def = src is Unit uu ? uu.Def : ((Structure)src).Def;
            switch (def.Floop.Kind)
            {
                case FloopKind.Gizlen:
                case FloopKind.Hucum:
                case FloopKind.OkYagmuru:
                case FloopKind.VurKac:
                case FloopKind.TakimyildizZirhi:
                case FloopKind.Feda:
                case FloopKind.SisOrtusu:
                    return null;
                case FloopKind.CekIt:
                {
                    var u = (Unit)src;
                    if (!S.Units.TryGetValue(t.EntityId, out var target) || !_e.IsEnemy(c.Player, target.ActingPlayer)) return "Bir rakip birim seç.";
                    if (target.Pos.Chebyshev(u.Pos) > 3) return "Hedef 3 kare içinde olmalı.";
                    if (!_e.CanBeTargeted(target, c.Player, u.Pos, false)) return "Hedef alınamaz.";
                    if (t.Amount < 0 || t.Amount > 2) return "En fazla 2 kare.";
                    return null;
                }
                case FloopKind.Isaretle:
                {
                    var u = (Unit)src;
                    if (!S.Units.TryGetValue(t.EntityId, out var target) || !_e.IsEnemy(c.Player, target.ActingPlayer)) return "Bir rakip birim seç.";
                    if (target.Pos.Chebyshev(u.Pos) > 4) return "Hedef 4 kare içinde olmalı.";
                    if (!_e.CanBeTargeted(target, c.Player, u.Pos, false)) return "Hedef alınamaz.";
                    return null;
                }
                case FloopKind.MancinikAtisi:
                {
                    var s = (Structure)src;
                    if (!t.Tile.IsOnBoard) return "Bir kare seç.";
                    if (t.Tile.Chebyshev(s.Pos) > _e.Stats.StructureRange(s)) return "Kare menzil dışında.";
                    return null;
                }
                case FloopKind.Sifa:
                {
                    var u = (Unit)src;
                    if (!S.Units.TryGetValue(t.EntityId, out var target) || target.IsIllusion || !_e.IsAlly(c.Player, target.Owner)) return "Bir dost birim seç.";
                    if (target.Pos.Chebyshev(u.Pos) > 3) return "Hedef 3 kare içinde olmalı.";
                    return null;
                }
                case FloopKind.RuzgarlaSavur:
                {
                    var s = (Structure)src;
                    if (!S.Units.TryGetValue(t.EntityId, out var target) || !_e.IsAlly(c.Player, target.Owner) || target.ControlledBy >= 0) return "Bitişik bir dost birim seç.";
                    if (target.Pos.Chebyshev(s.Pos) != 1) return "Birim yapıya bitişik olmalı.";
                    if (!_e.Grid.IsEmpty(t.Tile) || t.Tile.Chebyshev(target.Pos) > 4) return "4 kare içinde boş bir kare seç.";
                    return null;
                }
                case FloopKind.IsikOku:
                {
                    var u = (Unit)src;
                    var target = S.GetEntity(t.EntityId);
                    if (target == null || target.IsDead || !_e.IsEnemy(c.Player, _e.Actions.OwnerOf(target))) return "Bir rakip hedef seç.";
                    if (_e.DistanceTo(target, u.Pos) > _e.Stats.Range(u)) return "Hedef menzil dışında.";
                    if (target is Unit tu && !_e.CanBeTargeted(tu, c.Player, u.Pos, false)) return "Hedef alınamaz.";
                    return null;
                }
                case FloopKind.Gozlem:
                    return null;
                default:
                    return "Bilinmeyen Floop.";
            }
        }

        public void Apply(FloopCommand c)
        {
            var p = S.Players[c.Player];
            var src = S.GetEntity(c.SourceId);
            var t = c.Target ?? TargetSpec.None;
            CardDef def;
            if (src is Unit su)
            {
                def = su.Def;
                su.En--;
                su.FloopsUsed++;
            }
            else
            {
                var st = (Structure)src;
                def = st.Def;
                st.FloopsUsed++;
            }
            p.Ap -= def.Floop.Cost;
            _e.Cards.EmitAp(p);
            _e.Emit(new GameEvent { Type = GameEventType.FloopUsed, Player = c.Player, EntityId = src.Id, Id = def.Floop.Kind.ToString(), Message = $"Floop! {def.Name}: {def.Floop.Name}." });

            // Kozmik Rezonans (Yıldız pasifi): her Floop +3 GE, tur başına en fazla +9.
            if (p.Faction == Faction.Yildiz)
            {
                int gain = Math.Min(3, 9 - p.ResonanceThisTurn);
                if (gain > 0)
                {
                    p.ResonanceThisTurn += gain;
                    _e.GainGe(c.Player, gain, "Kozmik Rezonans");
                }
            }

            switch (def.Floop.Kind)
            {
                case FloopKind.Gizlen:
                {
                    var u = (Unit)src;
                    if (!u.Has(StatusKind.Hidden))
                        u.Statuses.Add(new Status { Kind = StatusKind.Hidden, SourcePlayer = c.Player, Expiry = StatusExpiry.Manual });
                    _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = u.Id, Id = "hidden", Message = u.Def.Name + " Gizli oldu." });
                    break;
                }
                case FloopKind.CekIt:
                {
                    var u = (Unit)src;
                    var target = S.Units[t.EntityId];
                    int dist = t.Amount <= 0 ? 2 : t.Amount;
                    int moved = t.Pull
                        ? _e.Forced.PullToward(target, u.Pos, dist)
                        : _e.Forced.Push(target, (target.Pos - u.Pos).Sign(), dist, DamageSource.None, 0);
                    _e.Say($"{target.Def.Name} {moved} kare {(t.Pull ? "çekildi" : "itildi")}.", c.Player);
                    break;
                }
                case FloopKind.Isaretle:
                {
                    var target = S.Units[t.EntityId];
                    if (!target.Has(StatusKind.Marked))
                        target.Statuses.Add(new Status { Kind = StatusKind.Marked, SourcePlayer = c.Player, Expiry = StatusExpiry.Manual });
                    _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = target.Id, Id = "marked", Message = target.Def.Name + " İşaretlendi." });
                    break;
                }
                case FloopKind.SisOrtusu:
                {
                    var s = (Structure)src;
                    _e.Effects.Apply(EffectCatalog.FenerSisi, c.Player, Board.Around(s.Pos, 1, true), 1, 0, false, Coord.None);
                    break;
                }
                case FloopKind.Hucum:
                    AddTurnStatus((Unit)src, StatusKind.Charge, c.Player);
                    break;
                case FloopKind.OkYagmuru:
                    AddTurnStatus((Unit)src, StatusKind.ArrowRain, c.Player);
                    break;
                case FloopKind.VurKac:
                    AddTurnStatus((Unit)src, StatusKind.HitAndRun, c.Player);
                    break;
                case FloopKind.Feda:
                {
                    var u = (Unit)src;
                    var center = u.Pos;
                    _e.Say($"{u.Def.Name} kendini feda etti!", c.Player);
                    _e.Damage.Sacrifice(u);
                    var dmg = new DamageSource { Player = c.Player, EntityId = u.Id, Kind = DamageKind.Floop, FromUnit = true, Label = "Feda" };
                    var hitHq = new HashSet<int>();
                    var ring = Board.Around(center, 1, false);
                    foreach (var tile in ring)
                    {
                        var ent = _e.EntityAt(tile);
                        if (ent == null || !_e.IsEnemy(c.Player, _e.Actions.OwnerOf(ent))) continue;
                        if (ent is Headquarters hq)
                        {
                            if (hitHq.Add(hq.Owner)) _e.Damage.DealToHq(hq, 2, dmg);
                        }
                        else _e.Damage.Deal(ent, 2, dmg);
                    }
                    _e.Effects.Apply(EffectCatalog.Kor, c.Player, ring, EffectCatalog.Get(EffectCatalog.Kor).DurationTurns, 0, false, Coord.None);
                    break;
                }
                case FloopKind.MancinikAtisi:
                {
                    var s = (Structure)src;
                    var ent = _e.EntityAt(t.Tile);
                    // Yapılar karargahları hedef alamaz (Bölüm 4).
                    if (ent != null && !(ent is Headquarters) && _e.IsEnemy(c.Player, _e.Actions.OwnerOf(ent)))
                        _e.Damage.Deal(ent, 2, new DamageSource { Player = c.Player, EntityId = s.Id, Kind = DamageKind.Structure, IsRanged = true, Label = "Mancınık" });
                    _e.Effects.Apply(EffectCatalog.Yanan, c.Player, new[] { t.Tile }, 1, 0, false, Coord.None);
                    break;
                }
                case FloopKind.Sifa:
                {
                    var target = S.Units[t.EntityId];
                    _e.Damage.HealUnit(target, 2, "Şifa");
                    _e.Effects.Apply(EffectCatalog.Islak, c.Player, new[] { target.Pos }, 2, 0, false, Coord.None);
                    break;
                }
                case FloopKind.RuzgarlaSavur:
                {
                    var target = S.Units[t.EntityId];
                    _e.Forced.Teleport(target, t.Tile, "rüzgârla savruldu");
                    break;
                }
                case FloopKind.IsikOku:
                {
                    var u = (Unit)src;
                    var target = S.GetEntity(t.EntityId);
                    _e.Damage.Deal(target, 2, new DamageSource { Player = c.Player, EntityId = u.Id, Kind = DamageKind.Floop, IsRanged = true, FromUnit = true, Label = "Işık Oku" });
                    break;
                }
                case FloopKind.TakimyildizZirhi:
                {
                    var u = (Unit)src;
                    foreach (var ally in S.Units.Values.Where(x => x != u && !x.IsIllusion && _e.IsAlly(c.Player, x.Owner) && x.Pos.Chebyshev(u.Pos) == 1))
                    {
                        ally.Statuses.RemoveAll(st => st.Kind == StatusKind.GuardArmor);
                        ally.Statuses.Add(new Status { Kind = StatusKind.GuardArmor, Value = 1, SourcePlayer = c.Player, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = c.Player });
                        _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = ally.Id, Id = "guard", Message = ally.Def.Name + " +1 Zırh kazandı." });
                    }
                    break;
                }
                case FloopKind.Gozlem:
                {
                    var top = p.Deck.Take(3).ToList();
                    if (top.Count == 0) break;
                    int choice = t.Choice >= 0 && t.Choice < top.Count ? t.Choice : 0;
                    var picked = top[choice];
                    foreach (var card in top) p.Deck.Remove(card);
                    p.Hand.Add(picked);
                    foreach (var card in top)
                        if (card != picked) p.Deck.Add(card);
                    _e.Emit(new GameEvent { Type = GameEventType.CardDrawn, Player = c.Player, EntityId = picked.InstanceId, Id = picked.CardId, Message = $"Gözlem: {picked.Def.Name} ele alındı, diğerleri destenin altına kondu." });
                    break;
                }
            }
        }

        private void AddTurnStatus(Unit u, StatusKind kind, int player)
        {
            if (!u.Has(kind))
                u.Statuses.Add(new Status { Kind = kind, SourcePlayer = player, Expiry = StatusExpiry.EndOfTurn, ExpiryPlayer = player });
            _e.Emit(new GameEvent { Type = GameEventType.StatusChanged, EntityId = u.Id, Id = kind.ToString(), Message = $"{u.Def.Name}: {Names.Of(kind)}." });
        }

        /// <summary>Gözlemevi Floop'u için destenin üstündeki kartlar (arayüz seçimi için).</summary>
        public List<CardInstance> PeekTop(int player, int n) => S.Players[player].Deck.Take(n).ToList();
    }
}
