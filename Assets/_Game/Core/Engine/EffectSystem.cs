using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// Alan etkilerinin yaşam döngüsü, tetikleri ve etkileşimleri (GDD Bölüm 4 ve 5).
    /// Kareler etki kimliklerini, etkiler de kapladıkları kareleri tutar (iki yönlü indeks).
    /// </summary>
    public sealed class EffectSystem
    {
        private readonly RulesEngine _e;

        public EffectSystem(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;

        // ---------------- Sorgular ----------------

        /// <summary>Karedeki etkiler: Zemin, Hava, Alan sırasıyla.</summary>
        public List<AreaEffectInstance> At(Coord c)
        {
            var list = new List<AreaEffectInstance>();
            if (!c.IsOnBoard) return list;
            foreach (var id in _e.Grid.EffectIdsAt(c))
                if (S.Effects.TryGetValue(id, out var fx)) list.Add(fx);
            list.Sort((a, b) => a.Def.Layer != b.Def.Layer ? a.Def.Layer.CompareTo(b.Def.Layer) : a.Id.CompareTo(b.Id));
            return list;
        }

        public bool TileHas(Coord c, EffectTag tag)
        {
            if (!c.IsOnBoard) return false;
            foreach (var id in _e.Grid.EffectIdsAt(c))
                if (S.Effects.TryGetValue(id, out var fx) && fx.Has(tag)) return true;
            return false;
        }

        public bool TileHasEffect(Coord c, string defId)
        {
            if (!c.IsOnBoard) return false;
            foreach (var id in _e.Grid.EffectIdsAt(c))
                if (S.Effects.TryGetValue(id, out var fx) && fx.Def.Id == defId) return true;
            return false;
        }

        /// <summary>Kare bir Yıldız Geçidi ise eş geçidin karesi, değilse Coord.None.</summary>
        public Coord PortalExit(Coord c)
        {
            if (!c.IsOnBoard) return Coord.None;
            foreach (var id in _e.Grid.EffectIdsAt(c))
            {
                if (!S.Effects.TryGetValue(id, out var fx) || !fx.Has(EffectTag.Gecit) || fx.Tiles.Count != 2) continue;
                foreach (var t in fx.Tiles)
                    if (t != c) return t;
            }
            return Coord.None;
        }

        /// <summary>Birim bu kareye girerse alacağı tahmini hasar (yol önizlemesi için).</summary>
        public int EstimateEnterDamage(Unit u, Coord c)
        {
            int total = 0;
            foreach (var fx in At(c))
            {
                if (fx.LastHitTurn.TryGetValue(u.Id, out var t) && t == S.TurnIndex) continue;
                foreach (var a in fx.Def.Actions)
                    if (a.Trigger == Trigger.OnEnter && a.Kind == ActionKind.Damage && _e.AffectsOwner(a.Affects, fx.Caster, u.Owner))
                        total += a.UnitAmount;
            }
            return total;
        }

        // ---------------- Uygulama ve etkileşimler ----------------

        /// <summary>
        /// Etkiyi karelere uygular. Karargah kareleri ve Harabeler etki almaz. Etkileşim tablosu:
        /// Islak + Yanan/Kor → ikisi de kalkar, kare 1 tur Buhar olur. Aynı etki aynı kareye gelirse süresi yenilenir.
        /// </summary>
        public AreaEffectInstance Apply(string defId, int caster, IEnumerable<Coord> tiles, int duration, int castId, bool fromUlti, Coord direction)
        {
            var def = EffectCatalog.Get(defId);
            var accepted = new List<Coord>();
            var steamTiles = new List<Coord>();
            foreach (var t in tiles.Distinct())
            {
                if (!_e.Grid.AcceptsTileEffects(t)) continue;
                if (def.Has(EffectTag.Ates) && TileHas(t, EffectTag.Su))
                {
                    RemoveTagAt(t, EffectTag.Su);
                    steamTiles.Add(t);
                    continue;
                }
                if (def.Has(EffectTag.Su) && TileHas(t, EffectTag.Ates))
                {
                    RemoveTagAt(t, EffectTag.Ates);
                    steamTiles.Add(t);
                    continue;
                }
                foreach (var old in At(t))
                    if (old.Def.Id == def.Id) RemoveTile(old, t);
                accepted.Add(t);
            }

            if (steamTiles.Count > 0)
            {
                Apply(EffectCatalog.Buhar, caster, steamTiles, EffectCatalog.Get(EffectCatalog.Buhar).DurationTurns, castId, false, Coord.None);
                _e.Say("Islak zemin ateşle karşılaştı: ikisi de söndü, Buhar yükseldi.", caster);
            }

            if (accepted.Count == 0) return null;

            var fx = new AreaEffectInstance
            {
                Id = _e.NewEntityId(),
                Def = def,
                Caster = caster,
                RemainingTurns = duration,
                CastId = castId,
                FromUlti = fromUlti,
                Direction = direction
            };
            foreach (var t in accepted)
            {
                fx.Tiles.Add(t);
                _e.Grid.IndexEffect(t, fx.Id);
            }
            S.Effects[fx.Id] = fx;
            _e.Emit(new GameEvent
            {
                Type = GameEventType.EffectAdded, Player = caster, EntityId = fx.Id, Id = def.Id,
                Tiles = accepted, Amount = duration,
                Message = $"{def.Name} {accepted.Count} kareye yayıldı ({duration} tur)."
            });

            foreach (var a in def.Actions)
                if (a.Trigger == Trigger.OnApply) RunAction(fx, a, null);
            return fx;
        }

        public void RemoveTile(AreaEffectInstance fx, Coord t)
        {
            if (!fx.Tiles.Remove(t)) return;
            _e.Grid.UnindexEffect(t, fx.Id);
            if (fx.Tiles.Count == 0 || fx.Has(EffectTag.Gecit)) Remove(fx);
            else _e.Emit(new GameEvent { Type = GameEventType.EffectMoved, EntityId = fx.Id, Id = fx.Def.Id, Tiles = fx.Tiles.ToList() });
        }

        public void Remove(AreaEffectInstance fx)
        {
            if (!S.Effects.Remove(fx.Id)) return;
            foreach (var t in fx.Tiles) _e.Grid.UnindexEffect(t, fx.Id);
            _e.Emit(new GameEvent
            {
                Type = GameEventType.EffectRemoved, Player = fx.Caster, EntityId = fx.Id, Id = fx.Def.Id,
                Tiles = fx.Tiles.ToList(), Message = fx.Def.Name + " sona erdi."
            });
        }

        public void RemoveTagAt(Coord t, EffectTag tag)
        {
            foreach (var fx in At(t))
                if (fx.Has(tag)) RemoveTile(fx, t);
        }

        /// <summary>Alandaki belirli etiketli etkileri dağıtır (Güneş Patlaması / Hortum + Sis).</summary>
        public int DispelTag(IEnumerable<Coord> tiles, EffectTag tag)
        {
            int n = 0;
            foreach (var t in tiles)
            {
                if (!t.IsOnBoard) continue;
                foreach (var fx in At(t))
                    if (fx.Has(tag)) { RemoveTile(fx, t); n++; }
            }
            return n;
        }

        /// <summary>Etkiyi yeni karelere taşır (Hortum).</summary>
        public void MoveTo(AreaEffectInstance fx, List<Coord> newTiles)
        {
            foreach (var t in fx.Tiles) _e.Grid.UnindexEffect(t, fx.Id);
            fx.Tiles.Clear();
            foreach (var t in newTiles)
            {
                fx.Tiles.Add(t);
                _e.Grid.IndexEffect(t, fx.Id);
            }
            _e.Emit(new GameEvent { Type = GameEventType.EffectMoved, Player = fx.Caster, EntityId = fx.Id, Id = fx.Def.Id, Tiles = newTiles, Message = fx.Def.Name + " ilerledi." });
        }

        // ---------------- Tetikler ----------------

        /// <summary>Giriş tetiği: birim kareye girdiğinde (hareket, itme, savrulma, ışınlanma).</summary>
        public void RunOnEnter(Unit u, Coord c)
        {
            foreach (var fx in At(c))
            {
                if (u.IsDead || u.Hp <= 0) return;
                foreach (var a in fx.Def.Actions)
                    if (a.Trigger == Trigger.OnEnter) RunAction(fx, a, u);
            }
        }

        /// <summary>Şafak 3. adım: oyuncunun birim ve yapılarına "tur başı" zemin etkileri işler.</summary>
        public void RunOwnerTurnStart(int seat)
        {
            foreach (var u in S.UnitsOf(seat).ToList())
            {
                foreach (var fx in At(u.Pos))
                {
                    if (u.IsDead || u.Hp <= 0) break;
                    foreach (var a in fx.Def.Actions)
                        if (a.Trigger == Trigger.OwnerTurnStart) RunAction(fx, a, u);
                }
            }
            foreach (var s in S.StructuresOf(seat).ToList())
            {
                foreach (var fx in At(s.Pos))
                {
                    if (s.IsDead || s.Hp <= 0) break;
                    foreach (var a in fx.Def.Actions)
                        if (a.Trigger == Trigger.OwnerTurnStart) RunStructureAction(fx, a, s);
                }
            }
        }

        /// <summary>Şafak 1. adım: yapanın tur başı tetikleri (Hortum ilerler, Süper Hücre vurur).</summary>
        public void RunCasterTurnStart(int seat)
        {
            foreach (var fx in S.Effects.Values.Where(f => f.Caster == seat).OrderBy(f => f.Id).ToList())
            {
                if (!S.Effects.ContainsKey(fx.Id)) continue;
                foreach (var a in fx.Def.Actions)
                    if (a.Trigger == Trigger.CasterTurnStart) RunAction(fx, a, null);
            }
        }

        /// <summary>Şafak 5. adım: yapanın etkilerinin süresi 1 azalır, dolanlar kalkar.</summary>
        public void DecrementAndExpire(int caster)
        {
            foreach (var fx in S.Effects.Values.Where(f => f.Caster == caster).ToList())
            {
                fx.RemainingTurns--;
                if (fx.RemainingTurns <= 0) Remove(fx);
            }
            foreach (var pe in S.PlayerEffects.Where(p => p.Caster == caster).ToList())
            {
                pe.RemainingTurns--;
                if (pe.RemainingTurns <= 0)
                {
                    S.PlayerEffects.Remove(pe);
                    _e.Emit(new GameEvent { Type = GameEventType.EffectRemoved, Player = caster, EntityId = pe.Id, Id = pe.UltiId, Message = UltiCatalog.Get(pe.UltiId).Name + " sona erdi." });
                }
            }
        }

        /// <summary>Elenen oyuncunun etkileri masadan kalkar.</summary>
        public void RemoveAllOf(int caster)
        {
            foreach (var fx in S.Effects.Values.Where(f => f.Caster == caster).ToList()) Remove(fx);
            S.PlayerEffects.RemoveAll(p => p.Caster == caster || p.TargetPlayer == caster);
            S.Delayed.RemoveAll(d => d.Caster == caster);
        }

        private bool AlreadyHit(AreaEffectInstance fx, int entityId) =>
            fx.LastHitTurn.TryGetValue(entityId, out var t) && t == S.TurnIndex;

        private DamageSource SourceOf(AreaEffectInstance fx) => new DamageSource
        {
            Player = fx.Caster,
            Kind = fx.FromUlti ? DamageKind.Ulti : DamageKind.TileEffect,
            IsUlti = fx.FromUlti,
            IsArea = fx.FromUlti,
            CastId = fx.CastId,
            Label = fx.Def.Name
        };

        private void RunAction(AreaEffectInstance fx, TriggeredAction a, Unit u)
        {
            switch (a.Kind)
            {
                case ActionKind.Damage:
                    if (u == null || !_e.AffectsOwner(a.Affects, fx.Caster, u.Owner) || a.UnitAmount <= 0) return;
                    if (a.OncePerTurn && AlreadyHit(fx, u.Id)) return;
                    fx.LastHitTurn[u.Id] = S.TurnIndex;
                    _e.Damage.DealToUnit(u, a.UnitAmount, SourceOf(fx));
                    break;
                case ActionKind.Heal:
                    if (u == null || !_e.AffectsOwner(a.Affects, fx.Caster, u.Owner)) return;
                    if (a.OncePerTurn && AlreadyHit(fx, u.Id)) return;
                    fx.LastHitTurn[u.Id] = S.TurnIndex;
                    _e.Damage.HealUnit(u, a.UnitAmount, fx.Def.Name);
                    break;
                case ActionKind.StrikeHighestHp:
                {
                    Unit best = null;
                    foreach (var t in fx.Tiles)
                    {
                        if (!S.Units.TryGetValue(_e.Grid.OccupantAt(t), out var cand)) continue;
                        if (!_e.AffectsOwner(a.Affects, fx.Caster, cand.Owner)) continue;
                        // Alan büyüsünün kendiliğinden vuruşu: Gölge Örtüsü korumaz, Gizli birim seçilemez.
                        if (cand.Has(StatusKind.Hidden)) continue;
                        if (best == null || cand.Hp > best.Hp || (cand.Hp == best.Hp && cand.Id < best.Id)) best = cand;
                    }
                    if (best != null)
                    {
                        _e.Say($"{fx.Def.Name}: şimşek {best.Def.Name} birimine düştü.", fx.Caster);
                        _e.Damage.DealToUnit(best, a.UnitAmount, SourceOf(fx));
                    }
                    break;
                }
                case ActionKind.TornadoAdvance:
                    _e.Ultis.AdvanceTornado(fx, a.UnitAmount, a.StructureAmount);
                    break;
            }
        }

        private void RunStructureAction(AreaEffectInstance fx, TriggeredAction a, Structure s)
        {
            if (a.StructureAmount <= 0 || !_e.AffectsOwner(a.Affects, fx.Caster, s.Owner)) return;
            if (a.OncePerTurn && AlreadyHit(fx, s.Id)) return;
            fx.LastHitTurn[s.Id] = S.TurnIndex;
            if (a.Kind == ActionKind.Damage) _e.Damage.DealToStructure(s, a.StructureAmount, SourceOf(fx));
            else if (a.Kind == ActionKind.Heal) _e.Damage.HealStructure(s, a.StructureAmount, fx.Def.Name);
        }

        /// <summary>Hortum'un süpürmesi sırasında yapı hasarı için "tur başına 1 kez" işaretleme.</summary>
        internal bool MarkHit(AreaEffectInstance fx, int entityId)
        {
            if (AlreadyHit(fx, entityId)) return false;
            fx.LastHitTurn[entityId] = S.TurnIndex;
            return true;
        }
    }
}
