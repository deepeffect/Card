using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>Arayüzün tahtada vurguladığı kare türleri.</summary>
    public enum Highlight { Reach, ReachDanger, Target, Deploy, Ally, PreviewOk, PreviewBad, Selected }

    public enum InputMode { None, PlayCard, Move, Attack, Retreat, Floop, Ulti }

    /// <summary>
    /// Oyuncu girdisini komutlara çeviren, motordan bağımsız arayüz mantığı: seçim, kart oynama,
    /// birim eylemleri, Floop ve ulti hedefleme, tahta vurguları. Unity HUD'u ve masaüstü test istemcisi
    /// aynı sınıfı kullanır; yalnızca çizim farklıdır.
    /// </summary>
    public sealed class InteractionController
    {
        private readonly Func<Command, CommandResult> _submit;
        private readonly Func<int, bool> _isHuman;

        public RulesEngine Engine;

        public InputMode Mode { get; private set; }
        public int Selected = -1;
        public int CardInstance { get; private set; } = -1;
        public int FloopSource { get; private set; } = -1;
        public string UltiId { get; private set; }
        public int PendingEntity { get; private set; } = -1;
        public Coord PendingTile { get; private set; } = Coord.None;
        public readonly List<int> TutulmaUnits = new List<int>();

        // Hedefleme seçenekleri
        public bool Pull = true;
        public int ForceDistance = 2;
        public Coord Direction = Coord.Right;
        public bool FireRect;
        public bool FireVertical;
        public int FireLength = 6;

        public string Status { get; private set; }
        public event Action<string> StatusChanged;

        public InteractionController(RulesEngine engine, Func<Command, CommandResult> submit, Func<int, bool> isHuman)
        {
            Engine = engine;
            _submit = submit;
            _isHuman = isHuman;
        }

        private GameState S => Engine.State;
        public int Me => S.Current;

        /// <summary>Sıradaki oyuncu insan ve Ana Faz'da mı.</summary>
        public bool MyTurn => Engine != null && _isHuman(S.Current) && !S.IsOver && S.Phase == Phase.AnaFaz;

        public void Reset()
        {
            Cancel();
            Selected = -1;
            SetStatus(null);
        }

        public void Cancel()
        {
            Mode = InputMode.None;
            CardInstance = -1;
            FloopSource = -1;
            UltiId = null;
            PendingEntity = -1;
            PendingTile = Coord.None;
            TutulmaUnits.Clear();
        }

        private void SetStatus(string message)
        {
            Status = message;
            StatusChanged?.Invoke(message);
        }

        private bool Send(Command c, bool keepMode)
        {
            c.Player = S.Current;
            var r = _submit(c);
            if (!r.Ok)
            {
                SetStatus(r.Error);
                return false;
            }
            SetStatus(null);
            if (!keepMode) Cancel();
            if (Selected >= 0 && S.GetEntity(Selected) == null) Selected = -1;
            return true;
        }

        // ---------------- Sorgular ----------------

        public Unit SelectedUnit => S.Units.TryGetValue(Selected, out var u) ? u : null;

        public bool CanMoveNow(Unit u)
        {
            if (u.ControlledBy >= 0 && u.Owner != u.ControlledBy) return u.ControlMoveLeft;
            return !u.Moved && (u.En >= 1 || Engine.Actions.NextMoveIsFree(u));
        }

        public bool CanAttackNow(Unit u)
        {
            if (u.ControlledBy >= 0 && u.Owner != u.ControlledBy) return u.ControlAttackLeft;
            return !u.Attacked && u.En >= 1;
        }

        public bool CanDefendNow(Unit u) => u.ControlledBy < 0 && !u.Defended && u.En >= 1;

        public bool IsMine(Unit u) => u != null && u.ActingPlayer == Me && MyTurn;

        public FloopKind? FloopKindOf(int sourceId)
        {
            var src = S.GetEntity(sourceId);
            if (src is Unit u && u.Def.HasFloop) return u.Def.Floop.Kind;
            if (src is Structure st && st.Def.HasFloop) return st.Def.Floop.Kind;
            return null;
        }

        public UltiDef ActiveUlti => UltiId != null ? UltiCatalog.Get(UltiId) : null;

        // ---------------- Eylemler ----------------

        public void SelectCard(int instanceId)
        {
            if (!MyTurn) return;
            var card = S.Players[Me].Hand.FirstOrDefault(h => h.InstanceId == instanceId);
            if (card == null) return;
            if (card.Def.Cost > S.Players[Me].Ap)
            {
                SetStatus($"Yeterli AP yok ({card.Def.Cost} gerekli).");
                return;
            }
            Cancel();
            Mode = InputMode.PlayCard;
            CardInstance = instanceId;
            SetStatus(card.Def.Type == CardType.Gelistirme ? "Geliştirmeyi takacağın birimi seç." : "Vurgulu karelerden birini seç.");
        }

        public void StartMove() { if (SelectedUnit != null) { Cancel(); Mode = InputMode.Move; } }
        public void StartAttack() { if (SelectedUnit != null) { Cancel(); Mode = InputMode.Attack; } }
        public void StartRetreat() { if (SelectedUnit != null) { Cancel(); Mode = InputMode.Retreat; } }

        public void Defend()
        {
            var u = SelectedUnit;
            if (u != null) Send(new DefendCommand { UnitId = u.Id }, keepMode: true);
        }

        public void StartFloop(int sourceId)
        {
            if (!MyTurn) return;
            var kind = FloopKindOf(sourceId);
            if (kind == null) return;
            Cancel();
            switch (kind.Value)
            {
                case FloopKind.Gizlen:
                case FloopKind.Hucum:
                case FloopKind.OkYagmuru:
                case FloopKind.VurKac:
                case FloopKind.Feda:
                case FloopKind.TakimyildizZirhi:
                case FloopKind.SisOrtusu:
                    Send(new FloopCommand { SourceId = sourceId }, keepMode: true);
                    return;
                default:
                    Mode = InputMode.Floop;
                    FloopSource = sourceId;
                    SetStatus(kind.Value == FloopKind.Gozlem ? "Destenin üstündeki kartlardan birini seç." :
                              kind.Value == FloopKind.RuzgarlaSavur ? "Önce yapıya bitişik dost birimi seç." : "Hedefi seç.");
                    return;
            }
        }

        public void ChooseGozlem(int index)
        {
            if (Mode == InputMode.Floop && FloopSource >= 0)
                Send(new FloopCommand { SourceId = FloopSource, Target = new TargetSpec { Choice = index } }, keepMode: false);
        }

        public void StartUlti(string ultiId)
        {
            if (!MyTurn) return;
            var d = UltiCatalog.Get(ultiId);
            Cancel();
            if (d.Targeting == UltiTargeting.Self || d.Targeting == UltiTargeting.OwnQuadrant)
            {
                Send(new CastUltiCommand { UltiId = d.Id, Target = TargetSpec.None }, keepMode: false);
                return;
            }
            Mode = InputMode.Ulti;
            UltiId = ultiId;
            SetStatus(d.Text);
        }

        public void ConfirmTutulma()
        {
            if (Mode != InputMode.Ulti || UltiId != UltiCatalog.Tutulma || TutulmaUnits.Count == 0) return;
            if (!S.Units.TryGetValue(TutulmaUnits[0], out var first)) return;
            Send(new CastUltiCommand
            {
                UltiId = UltiId,
                Target = new TargetSpec { Player = Board.QuadrantOf(first.Pos), EntityIds = TutulmaUnits.ToList() }
            }, keepMode: false);
        }

        public void Steer(int effectId, Coord dir) => Send(new SteerTornadoCommand { EffectId = effectId, Direction = dir }, keepMode: true);

        public void EndTurn()
        {
            if (!MyTurn) return;
            if (Send(new EndTurnCommand(), keepMode: false)) Selected = -1;
        }

        public void DrawExtra()
        {
            if (MyTurn) Send(new DrawExtraCommand(), keepMode: true);
        }

        // ---------------- Tahta tıklaması ----------------

        public void Click(Coord c)
        {
            if (Engine == null || !c.IsOnBoard) return;
            var ent = Engine.EntityAt(c);

            if (!MyTurn || Mode == InputMode.None)
            {
                var sel = SelectedUnit;
                if (MyTurn && sel != null && sel.ActingPlayer == Me)
                {
                    if (ent != null && ent.Id != sel.Id && CanAttackNow(sel) && Engine.Actions.AttackTargets(sel).Contains(ent))
                    {
                        Send(new AttackCommand { UnitId = sel.Id, TargetId = ent.Id }, keepMode: true);
                        return;
                    }
                    if (ent == null && CanMoveNow(sel) && Engine.Actions.ReachableFor(sel).ContainsKey(c))
                    {
                        Send(new MoveCommand { UnitId = sel.Id, To = c }, keepMode: true);
                        return;
                    }
                }
                Selected = ent != null ? ent.Id : -1;
                return;
            }

            switch (Mode)
            {
                case InputMode.Move:
                    Send(new MoveCommand { UnitId = Selected, To = c }, keepMode: false);
                    break;
                case InputMode.Retreat:
                    Send(new RetreatCommand { UnitId = Selected, To = c }, keepMode: false);
                    break;
                case InputMode.Attack:
                    if (ent != null) Send(new AttackCommand { UnitId = Selected, TargetId = ent.Id }, keepMode: false);
                    break;
                case InputMode.PlayCard:
                {
                    var card = S.Players[Me].Hand.FirstOrDefault(h => h.InstanceId == CardInstance);
                    if (card == null) { Cancel(); break; }
                    var target = card.Def.Type == CardType.Gelistirme ? TargetSpec.AtEntity(ent != null ? ent.Id : -1) : TargetSpec.AtTile(c);
                    Send(new PlayCardCommand { CardInstanceId = CardInstance, Target = target }, keepMode: false);
                    break;
                }
                case InputMode.Floop:
                {
                    var kind = FloopKindOf(FloopSource);
                    if (kind == null) { Cancel(); break; }
                    if (kind.Value == FloopKind.RuzgarlaSavur && PendingEntity < 0)
                    {
                        if (ent is Unit) { PendingEntity = ent.Id; SetStatus("Şimdi 4 kare içinde boş bir kare seç."); }
                        break;
                    }
                    var spec = FloopSpecFor(kind.Value, c, preview: false);
                    if (spec != null) Send(new FloopCommand { SourceId = FloopSource, Target = spec }, keepMode: false);
                    break;
                }
                case InputMode.Ulti:
                    UltiClick(c, ent);
                    break;
            }
        }

        private void UltiClick(Coord c, Entity ent)
        {
            var d = ActiveUlti;
            switch (d.Targeting)
            {
                case UltiTargeting.EnemyQuadrantUnits:
                    if (ent is Unit u)
                    {
                        if (TutulmaUnits.Contains(u.Id)) TutulmaUnits.Remove(u.Id);
                        else if (TutulmaUnits.Count < 2) TutulmaUnits.Add(u.Id);
                        SetStatus($"Seçilen birim: {TutulmaUnits.Count}/2. Onayla ile büyüyü yap.");
                    }
                    return;
                case UltiTargeting.OwnUnitToTile:
                    if (PendingEntity < 0)
                    {
                        if (ent is Unit) { PendingEntity = ent.Id; SetStatus("Şimdi ışınlanacak boş kareyi seç."); }
                        return;
                    }
                    break;
                case UltiTargeting.TwoTiles:
                    if (!PendingTile.IsOnBoard) { PendingTile = c; SetStatus("İkinci geçidin karesini seç."); return; }
                    break;
            }
            var spec = UltiSpecFor(d, c, preview: false);
            if (spec != null) Send(new CastUltiCommand { UltiId = d.Id, Target = spec }, keepMode: false);
        }

        // ---------------- Hedef tanımları ----------------

        public TargetSpec FloopSpecFor(FloopKind kind, Coord tile, bool preview)
        {
            var ent = Engine.EntityAt(tile);
            switch (kind)
            {
                case FloopKind.CekIt:
                    return ent != null ? new TargetSpec { EntityId = ent.Id, Pull = Pull, Amount = ForceDistance } : null;
                case FloopKind.Isaretle:
                case FloopKind.Sifa:
                case FloopKind.IsikOku:
                    return ent != null ? TargetSpec.AtEntity(ent.Id) : null;
                case FloopKind.MancinikAtisi:
                    return TargetSpec.AtTile(tile);
                case FloopKind.RuzgarlaSavur:
                    if (PendingEntity < 0)
                    {
                        if (!(ent is Unit)) return null;
                        var probe = Board.Around(tile, 1, false).FirstOrDefault(c => Engine.Grid.IsEmpty(c));
                        return probe.IsOnBoard || !preview ? new TargetSpec { EntityId = ent.Id, Tile = probe } : null;
                    }
                    return new TargetSpec { EntityId = PendingEntity, Tile = tile };
                default:
                    return null;
            }
        }

        public static Coord TopLeftFor(Coord c, int size) => new Coord(c.Row - (size - 1) / 2, c.Col - (size - 1) / 2);

        public List<Coord> FireTiles(Coord c)
        {
            if (FireRect) return Board.Rect(c, FireVertical ? 3 : 2, FireVertical ? 2 : 3);
            var list = new List<Coord>();
            for (int i = 0; i < FireLength; i++) list.Add(FireVertical ? new Coord(c.Row + i, c.Col) : new Coord(c.Row, c.Col + i));
            return list;
        }

        public TargetSpec UltiSpecFor(UltiDef d, Coord tile, bool preview)
        {
            var ent = Engine.EntityAt(tile);
            switch (d.Targeting)
            {
                case UltiTargeting.EnemyPlayer:
                case UltiTargeting.EnemyQuadrant:
                    return TargetSpec.AtPlayer(Board.QuadrantOf(tile));
                case UltiTargeting.EnemyQuadrantUnits:
                case UltiTargeting.OwnUnit:
                    return ent is Unit ? TargetSpec.AtEntity(ent.Id) : null;
                case UltiTargeting.OwnUnitToTile:
                    if (PendingEntity < 0) return ent is Unit ? new TargetSpec { EntityId = ent.Id, Tile = FirstLegalTeleportTile(ent.Id) } : null;
                    return new TargetSpec { EntityId = PendingEntity, Tile = tile };
                case UltiTargeting.AreaAny:
                case UltiTargeting.AreaEnemyQuadrant:
                    return new TargetSpec { Tile = TopLeftFor(tile, d.Size), Direction = Direction };
                case UltiTargeting.TilesEnemyQuadrant:
                    return new TargetSpec { Tiles = FireTiles(tile) };
                case UltiTargeting.CenterAny:
                case UltiTargeting.OwnDeployTile:
                    return TargetSpec.AtTile(tile);
                case UltiTargeting.EnemyEntity:
                    return ent != null ? TargetSpec.AtEntity(ent.Id) : null;
                case UltiTargeting.TwoTiles:
                    return PendingTile.IsOnBoard
                        ? new TargetSpec { Tile = PendingTile, Tile2 = tile }
                        : new TargetSpec { Tile = tile, Tile2 = Board.AllTiles().FirstOrDefault(c => c != tile && Engine.Grid.IsEmpty(c)) };
                default:
                    return TargetSpec.None;
            }
        }

        private Coord FirstLegalTeleportTile(int unitId)
        {
            var d = UltiCatalog.Get(UltiCatalog.KayanYildiz);
            foreach (var c in Board.AllTiles())
                if (Engine.Ultis.ValidateTarget(d, Me, new TargetSpec { EntityId = unitId, Tile = c }) == null) return c;
            return Coord.None;
        }

        // ---------------- Vurgular ----------------

        public Dictionary<Coord, Highlight> Highlights(Coord hover)
        {
            var map = new Dictionary<Coord, Highlight>();
            if (Engine == null) return map;
            var sel = S.GetEntity(Selected);
            if (sel is Unit su0) map[su0.Pos] = Highlight.Selected;
            else if (sel is Structure ss0) map[ss0.Pos] = Highlight.Selected;
            if (!MyTurn) return map;

            switch (Mode)
            {
                case InputMode.None:
                case InputMode.Move:
                case InputMode.Attack:
                {
                    var u = SelectedUnit;
                    if (u == null || u.ActingPlayer != Me) break;
                    if (Mode != InputMode.Attack && CanMoveNow(u))
                        foreach (var kv in Engine.Actions.ReachableFor(u))
                            map[kv.Key] = kv.Value.EstimatedDamage > 0 ? Highlight.ReachDanger : Highlight.Reach;
                    if (Mode != InputMode.Move && CanAttackNow(u))
                        foreach (var t in Engine.Actions.AttackTargets(u)) Mark(map, t, Highlight.Target);
                    break;
                }
                case InputMode.Retreat:
                {
                    var u = SelectedUnit;
                    if (u == null) break;
                    foreach (var kv in Engine.Grid.Reachable(u, Math.Min(2, Engine.Stats.Speed(u)))) map[kv.Key] = Highlight.Reach;
                    break;
                }
                case InputMode.PlayCard:
                {
                    var card = S.Players[Me].Hand.FirstOrDefault(h => h.InstanceId == CardInstance);
                    if (card == null) break;
                    if (card.Def.Type == CardType.Birim) foreach (var c in Engine.Cards.DeployTiles(Me)) map[c] = Highlight.Deploy;
                    else if (card.Def.Type == CardType.Yapi) foreach (var c in Engine.Cards.BuildTiles(Me)) map[c] = Highlight.Deploy;
                    else foreach (var u in S.UnitsOf(Me).Where(x => Engine.Cards.CanAttachTo(Me, card.Def, x))) map[u.Pos] = Highlight.Deploy;
                    break;
                }
                case InputMode.Floop:
                {
                    var kind = FloopKindOf(FloopSource);
                    if (kind == null) break;
                    foreach (var tile in Board.AllTiles())
                    {
                        var spec = FloopSpecFor(kind.Value, tile, preview: true);
                        if (spec == null) continue;
                        if (Engine.Validate(new FloopCommand { Player = Me, SourceId = FloopSource, Target = spec }) == null)
                            map[tile] = kind.Value == FloopKind.Sifa || (kind.Value == FloopKind.RuzgarlaSavur && PendingEntity < 0) ? Highlight.Ally : Highlight.Target;
                    }
                    break;
                }
                case InputMode.Ulti:
                    UltiHighlights(map, hover);
                    break;
            }
            return map;
        }

        private static void Mark(Dictionary<Coord, Highlight> map, Entity ent, Highlight h)
        {
            switch (ent)
            {
                case Unit u: map[u.Pos] = h; break;
                case Structure st: map[st.Pos] = h; break;
                case Headquarters hq: foreach (var t in hq.Tiles) map[t] = h; break;
            }
        }

        private void UltiHighlights(Dictionary<Coord, Highlight> map, Coord hover)
        {
            var d = ActiveUlti;
            if (d == null) return;
            bool entityTargeted = d.Targeting == UltiTargeting.EnemyEntity || d.Targeting == UltiTargeting.OwnUnit ||
                                  (d.Targeting == UltiTargeting.OwnUnitToTile && PendingEntity < 0) || d.Targeting == UltiTargeting.EnemyQuadrantUnits;
            if (entityTargeted)
            {
                foreach (var tile in Board.AllTiles())
                {
                    var spec = UltiSpecFor(d, tile, preview: true);
                    if (spec != null && ValidUltiTarget(d, spec)) map[tile] = Highlight.PreviewOk;
                }
                foreach (var id in TutulmaUnits)
                    if (S.Units.TryGetValue(id, out var tu)) map[tu.Pos] = Highlight.Selected;
                return;
            }
            if (!hover.IsOnBoard) return;
            var hoverSpec = UltiSpecFor(d, hover, preview: true);
            if (hoverSpec == null) return;
            bool ok = Engine.Validate(new CastUltiCommand { Player = Me, UltiId = d.Id, Target = hoverSpec }) == null;
            foreach (var t in Engine.Ultis.AreaTiles(d, Me, hoverSpec))
                if (t.IsOnBoard) map[t] = ok ? Highlight.PreviewOk : Highlight.PreviewBad;
            if (PendingTile.IsOnBoard) map[PendingTile] = Highlight.Selected;
        }

        private bool ValidUltiTarget(UltiDef d, TargetSpec spec)
        {
            if (d.Targeting == UltiTargeting.EnemyQuadrantUnits)
            {
                if (!S.Units.TryGetValue(spec.EntityId, out var u)) return false;
                var probe = new TargetSpec { Player = Board.QuadrantOf(u.Pos), EntityIds = new List<int> { u.Id } };
                return Engine.Ultis.ValidateTarget(d, Me, probe) == null;
            }
            return Engine.Ultis.ValidateTarget(d, Me, spec) == null;
        }

        // ---------------- Bilgi metinleri ----------------

        public string Tooltip(Coord c)
        {
            if (Engine == null || !c.IsOnBoard) return "";
            var parts = new List<string> { $"{c} · {Names.Of(S.Players[Board.QuadrantOf(c)].Faction)} çeyreği" + (Board.IsAltar(c) ? " · Gök Sunağı" : "") };
            var ent = Engine.EntityAt(c);
            if (ent != null) parts.Add(Engine.EntityName(ent));
            foreach (var fx in Engine.Effects.At(c))
                parts.Add($"{fx.Def.Name} ({fx.Def.Layer}, {fx.RemainingTurns} tur, {S.Players[fx.Caster].Name})");
            if (S.Delayed.Any(d => d.Tiles.Contains(c))) parts.Add("Meteor düşecek!");
            var sel = SelectedUnit;
            if (Mode == InputMode.None && sel != null && sel.ActingPlayer == Me && MyTurn)
            {
                if (Engine.Actions.ReachableFor(sel).TryGetValue(c, out var path) && path.EstimatedDamage > 0)
                    parts.Add($"Yol hasarı: {path.EstimatedDamage}");
            }
            return string.Join("\n", parts);
        }

        /// <summary>Birim için özet satırı; illüzyonlar sahibinden başkasına gerçek gibi görünür.</summary>
        public string UnitSummary(Unit u)
        {
            bool ownerView = u.Owner == Me && _isHuman(Me);
            if (u.IsIllusion && !ownerView)
                return $"SAL {u.Def.Atk} · CAN {u.Def.Hp} · HIZ {u.Def.Speed} · MNZ {u.Def.Range}";
            var st = Engine.Stats;
            return $"SAL {st.Atk(u)} · CAN {u.Hp}/{st.MaxHp(u)} · HIZ {st.Speed(u)} · MNZ {st.Range(u)} · EN {u.En} · Zırh {st.Armor(u)}";
        }

        public List<string> UnitTags(Unit u)
        {
            var tags = new List<string>();
            if (u.Flying) tags.Add("Uçan");
            if (u.IsIllusion && u.Owner == Me && _isHuman(Me)) tags.Add("İllüzyon (yalnızca sen görüyorsun)");
            if (Engine.IsShadowed(u)) tags.Add("Gölge");
            foreach (var st in u.Statuses)
                if (st.Kind != StatusKind.Shadow) tags.Add(Names.Of(st.Kind));
            if (u.ControlledBy >= 0) tags.Add("Tutulma: " + S.Players[u.ControlledBy].Name);
            if (u.WeaponCardId != null) tags.Add("Silah: " + CardCatalog.Get(u.WeaponCardId).Name);
            if (u.ArmorCardId != null) tags.Add("Zırh: " + CardCatalog.Get(u.ArmorCardId).Name);
            return tags;
        }
    }
}
