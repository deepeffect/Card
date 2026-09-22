using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>GDD "CardManager": deste, el, atık; kart oynama doğrulaması ve uygulaması.</summary>
    public sealed class CardManager
    {
        private readonly RulesEngine _e;

        public CardManager(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;

        internal void BuildDeck(PlayerState p, List<string> custom, bool shuffle)
        {
            var ids = custom ?? CardCatalog.BuildStarterDeck(p.Faction, _e.Rules.CopiesPerCoreCard);
            foreach (var id in ids)
                p.Deck.Add(new CardInstance { InstanceId = S.NextCardInstanceId++, CardId = id });
            if (shuffle && custom == null) S.Rng.Shuffle(p.Deck);
        }

        /// <summary>Kart çeker; deste boşsa her çekme girişiminde karargah artan hasar alır (1, 2, 3…).</summary>
        public void Draw(int seat, int count)
        {
            var p = S.Players[seat];
            for (int i = 0; i < count; i++)
            {
                if (!p.Alive) return;
                if (p.Deck.Count == 0)
                {
                    p.Fatigue++;
                    _e.Say($"{p.Name} destesi bitti: karargah {p.Fatigue} hasar alıyor.", seat);
                    _e.Damage.DealToHq(S.Hqs[seat], p.Fatigue, new DamageSource { Player = -1, Kind = DamageKind.Fatigue, Label = "deste bitti" });
                    continue;
                }
                var card = p.Deck[0];
                p.Deck.RemoveAt(0);
                p.Hand.Add(card);
                _e.Emit(new GameEvent { Type = GameEventType.CardDrawn, Player = seat, EntityId = card.InstanceId, Id = card.CardId, Message = $"{p.Name} bir kart çekti." });
            }
        }

        public string ValidateExtraDraw(DrawExtraCommand c)
        {
            var p = S.Players[c.Player];
            if (p.ExtraDrawsThisTurn >= _e.Rules.ExtraDrawsPerTurn) return "Ek kart bu tur zaten çekildi.";
            if (p.Ap < _e.Rules.ExtraDrawCost) return "Yeterli AP yok.";
            return null;
        }

        public void ApplyExtraDraw(DrawExtraCommand c)
        {
            var p = S.Players[c.Player];
            p.Ap -= _e.Rules.ExtraDrawCost;
            p.ExtraDrawsThisTurn++;
            EmitAp(p);
            Draw(c.Player, 1);
        }

        // ---------------- Konuşlanma ----------------

        /// <summary>Konuşlanma alanı: karargahın 3 kare çevresi + kendi yapılarına bitişik kareler.</summary>
        public bool IsDeployable(int seat, Coord c)
        {
            if (!_e.Grid.IsEmpty(c)) return false;
            var hq = S.Hqs[seat];
            if (!hq.IsDead && hq.DistanceTo(c) <= _e.Rules.DeployRadius) return true;
            foreach (var s in S.Structures.Values)
            {
                if (s.Owner != seat || s.Pos.Chebyshev(c) != 1) continue;
                if (_e.Rules.AllStructuresExtendDeployment || s.Def.Passive == CardPassive.IleriKarakol) return true;
            }
            return false;
        }

        public List<Coord> DeployTiles(int seat) => Board.AllTiles().Where(c => IsDeployable(seat, c)).ToList();

        /// <summary>Yapılar yalnızca kendi çeyreğine kurulur. Sunak karelerine yapı kurulamaz (Sunak çekişmeli kalsın diye).</summary>
        public bool IsBuildable(int seat, Coord c) =>
            _e.Grid.IsEmpty(c) && Board.QuadrantOf(c) == seat && !Board.IsAltar(c);

        public List<Coord> BuildTiles(int seat) => Board.AllTiles().Where(c => IsBuildable(seat, c)).ToList();

        public bool CanAttachTo(int seat, CardDef upgrade, Unit u) =>
            u != null && !u.IsDead && u.Owner == seat && !u.IsIllusion && u.ControlledBy < 0;

        // ---------------- Kart oynama ----------------

        public string ValidatePlay(PlayCardCommand c)
        {
            var p = S.Players[c.Player];
            var card = p.Hand.FirstOrDefault(h => h.InstanceId == c.CardInstanceId);
            if (card == null) return "Kart elinde değil.";
            var def = card.Def;
            if (p.Ap < def.Cost) return $"Yeterli AP yok ({def.Cost} gerekli).";
            var t = c.Target ?? TargetSpec.None;
            switch (def.Type)
            {
                case CardType.Birim:
                    if (!IsDeployable(c.Player, t.Tile)) return "Birim yalnızca konuşlanma alanındaki boş bir kareye iner.";
                    return null;
                case CardType.Yapi:
                    if (S.StructuresOf(c.Player).Count() >= _e.Rules.MaxStructures) return $"En fazla {_e.Rules.MaxStructures} yapı kurulabilir.";
                    if (!IsBuildable(c.Player, t.Tile)) return "Yapı yalnızca kendi çeyreğindeki boş bir kareye kurulur.";
                    return null;
                default:
                    S.Units.TryGetValue(t.EntityId, out var u);
                    if (!CanAttachTo(c.Player, def, u)) return "Geliştirme kendi birimlerinden birine takılmalı.";
                    return null;
            }
        }

        public void ApplyPlay(PlayCardCommand c)
        {
            var p = S.Players[c.Player];
            var card = p.Hand.First(h => h.InstanceId == c.CardInstanceId);
            var def = card.Def;
            p.Hand.Remove(card);
            p.Ap -= def.Cost;
            EmitAp(p);
            _e.Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = c.Player, EntityId = card.InstanceId, Id = def.Id, Message = $"{p.Name} {def.Name} oynadı." });
            var t = c.Target;
            switch (def.Type)
            {
                case CardType.Birim:
                    SpawnUnit(c.Player, def.Id, t.Tile, card.InstanceId);
                    break;
                case CardType.Yapi:
                    SpawnStructure(c.Player, def.Id, t.Tile, card.InstanceId);
                    break;
                default:
                    Attach(S.Units[t.EntityId], card);
                    break;
            }
        }

        public Unit SpawnUnit(int seat, string cardId, Coord at, int cardInstanceId)
        {
            var def = CardCatalog.Get(cardId);
            var u = new Unit
            {
                Id = _e.NewEntityId(),
                Owner = seat,
                CardId = cardId,
                CardInstanceId = cardInstanceId,
                Pos = at,
                BaseAtk = def.Atk,
                BaseMaxHp = def.Hp,
                Hp = def.Hp,
                BaseSpeed = def.Speed,
                BaseRange = def.Range,
                Flying = def.Flying,
                En = _e.Rules.SummonEn,
                IsToken = def.IsToken,
                SummonedTurnIndex = S.TurnIndex
            };
            S.Units[u.Id] = u;
            _e.Grid.SetOccupant(at, u.Id);
            _e.Emit(new GameEvent { Type = GameEventType.UnitSummoned, Player = seat, EntityId = u.Id, To = at, Id = cardId, Message = $"{def.Name} {at} karesine indi." });
            return u;
        }

        public Structure SpawnStructure(int seat, string cardId, Coord at, int cardInstanceId)
        {
            var def = CardCatalog.Get(cardId);
            var s = new Structure
            {
                Id = _e.NewEntityId(),
                Owner = seat,
                CardId = cardId,
                CardInstanceId = cardInstanceId,
                Pos = at,
                Hp = def.Hp,
                MaxHp = def.Hp
            };
            S.Structures[s.Id] = s;
            _e.Grid.SetOccupant(at, s.Id);
            _e.Emit(new GameEvent { Type = GameEventType.StructureBuilt, Player = seat, EntityId = s.Id, To = at, Id = cardId, Message = $"{def.Name} {at} karesine kuruldu." });
            return s;
        }

        /// <summary>Geliştirme takılır; aynı yuvadaki eski geliştirme atığa gider.</summary>
        public void Attach(Unit u, CardInstance card)
        {
            var def = card.Def;
            var p = S.Players[u.Owner];
            int maxBefore = _e.Stats.MaxHp(u);
            if (def.Slot == UpgradeSlot.Silah)
            {
                if (u.WeaponInstanceId >= 0) p.Discard.Add(new CardInstance { InstanceId = u.WeaponInstanceId, CardId = u.WeaponCardId });
                u.WeaponCardId = def.Id;
                u.WeaponInstanceId = card.InstanceId;
            }
            else
            {
                if (u.ArmorInstanceId >= 0) p.Discard.Add(new CardInstance { InstanceId = u.ArmorInstanceId, CardId = u.ArmorCardId });
                u.ArmorCardId = def.Id;
                u.ArmorInstanceId = card.InstanceId;
            }
            int maxAfter = _e.Stats.MaxHp(u);
            if (maxAfter > maxBefore) u.Hp += maxAfter - maxBefore;
            else if (u.Hp > maxAfter) u.Hp = maxAfter;
            _e.Emit(new GameEvent
            {
                Type = GameEventType.UpgradeAttached, Player = u.Owner, EntityId = u.Id, Id = def.Id, To = u.Pos,
                Message = $"{u.Def.Name} {def.Name} kuşandı. {def.Visual}"
            });
        }

        /// <summary>Alacakaranlık 4. adım: el 8 karta indirilir.</summary>
        internal void EnforceHandLimit(int seat, List<int> preferred)
        {
            var p = S.Players[seat];
            int excess = p.Hand.Count - _e.Rules.HandLimit;
            if (excess <= 0) return;
            var toDrop = new List<CardInstance>();
            if (preferred != null)
                foreach (var id in preferred)
                {
                    if (toDrop.Count >= excess) break;
                    var card = p.Hand.FirstOrDefault(h => h.InstanceId == id);
                    if (card != null && !toDrop.Contains(card)) toDrop.Add(card);
                }
            for (int i = p.Hand.Count - 1; i >= 0 && toDrop.Count < excess; i--)
                if (!toDrop.Contains(p.Hand[i])) toDrop.Add(p.Hand[i]);
            foreach (var card in toDrop)
            {
                p.Hand.Remove(card);
                p.Discard.Add(card);
                _e.Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = seat, EntityId = card.InstanceId, Id = card.CardId, Message = $"{p.Name} el sınırı için {card.Def.Name} attı." });
            }
        }

        internal void EmitAp(PlayerState p) =>
            _e.Emit(new GameEvent { Type = GameEventType.ApChanged, Player = p.Seat, Amount = p.Ap });
    }
}
