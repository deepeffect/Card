using System.Collections.Generic;
using System.Linq;
using DortGok.Core;
using UnityEngine;

namespace DortGok.Presentation
{
    /// <summary>
    /// Gri kutu arayüzü (IMGUI): tur çubuğu, oyuncular, el, seçili birim eylemleri, Floop ve ulti hedefleme,
    /// olay günlüğü. Ulti seçilince şekil tahtada fareyle gezdirilir; geçersiz kareler kırmızı yanar (GDD Bölüm 4).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private enum Mode { None, PlayCard, Move, Attack, Retreat, Floop, Ulti }

        private sealed class FloatText
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
        }

        private GameBootstrap _g;
        private Mode _mode;
        private int _card = -1;
        private int _selected = -1;
        private int _floopSource = -1;
        private string _ulti;
        private int _pendingEntity = -1;
        private Coord _pendingTile = Coord.None;
        private readonly List<int> _tutulma = new List<int>();

        // Hedefleme seçenekleri
        private bool _pull = true;
        private int _forceDistance = 2;
        private Coord _direction = Coord.Right;
        private bool _fireRect;
        private bool _fireVertical;
        private int _fireLength = 6;

        private Coord _hover = Coord.None;
        private string _status;
        private float _statusUntil;
        private string _banner;
        private float _bannerUntil;
        private Color _bannerColor = Color.white;
        private readonly List<string> _log = new List<string>();
        private readonly List<FloatText> _floats = new List<FloatText>();
        private readonly List<Rect> _panels = new List<Rect>();
        private Vector2 _handScroll;
        private Vector2 _logScroll;

        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _cardStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _labelStyle;

        private RulesEngine E => _g != null ? _g.Engine : null;

        public void Bind(GameBootstrap g)
        {
            _g = g;
            g.Queue.Played += OnPlayed;
            g.MatchStarted += ResetUi;
        }

        private void ResetUi()
        {
            CancelMode();
            _selected = -1;
            _log.Clear();
            _floats.Clear();
            _banner = null;
        }

        private void CancelMode()
        {
            _mode = Mode.None;
            _card = -1;
            _floopSource = -1;
            _ulti = null;
            _pendingEntity = -1;
            _pendingTile = Coord.None;
            _tutulma.Clear();
        }

        private void Flash(string message, float seconds = 3f)
        {
            _status = message;
            _statusUntil = Time.time + seconds;
        }

        private void Submit(Command c, bool keepMode = false)
        {
            var r = _g.Submit(c);
            if (!r.Ok) Flash(r.Error);
            else if (!keepMode) CancelMode();
        }

        // ---------------- Olaylar ----------------

        private void OnPlayed(GameEvent e)
        {
            if (!string.IsNullOrEmpty(e.Message) && e.Type != GameEventType.ApChanged)
            {
                _log.Add(e.Message);
                if (_log.Count > 80) _log.RemoveAt(0);
                _logScroll.y = float.MaxValue;
            }
            var s = E?.State;
            if (s == null) return;
            switch (e.Type)
            {
                case GameEventType.Damaged:
                    AddFloat(e.EntityId, e.To, "-" + e.Amount, new Color(1f, 0.35f, 0.3f));
                    break;
                case GameEventType.Healed:
                    AddFloat(e.EntityId, e.To, "+" + e.Amount, new Color(0.4f, 1f, 0.5f));
                    break;
                case GameEventType.UltiCast:
                    ShowBanner($"{s.Players[e.Player].Name}: {UltiCatalog.Get(e.Id).Name}!", Palette.Faction(s.Players[e.Player].Faction), 1.8f);
                    break;
                case GameEventType.TurnStarted:
                    ShowBanner($"{s.Players[e.Player].Name} turu", Palette.Faction(s.Players[e.Player].Faction), 1.0f);
                    break;
                case GameEventType.PlayerEliminated:
                    ShowBanner(e.Message, Color.white, 2.5f);
                    break;
                case GameEventType.LastStand:
                    ShowBanner("Son Siper!", Palette.Altar, 1.6f);
                    break;
                case GameEventType.IllusionRevealed:
                    AddFloat(-1, e.To, "İllüzyon!", Color.magenta);
                    break;
                case GameEventType.FloopUsed:
                    AddFloat(e.EntityId, Coord.None, "Floop!", Palette.Altar);
                    break;
            }
        }

        private void AddFloat(int entityId, Coord tile, string text, Color color)
        {
            Vector3 world = tile.IsOnBoard ? BoardSpace.ToWorld(tile, 1.1f) : _g.Board.WorldOfEntity(E.State, entityId) + Vector3.up;
            _floats.Add(new FloatText { World = world, Text = text, Color = color });
        }

        private void ShowBanner(string text, Color color, float seconds)
        {
            _banner = text;
            _bannerColor = color;
            _bannerUntil = Time.time + seconds;
        }

        // ---------------- Vurgular (tahta dokusu) ----------------

        public void FillOverlay(BoardView board)
        {
            board.Overlay.Clear();
            board.Hover = _hover;
            board.SelectedEntity = _selected;
            var e = E;
            if (e == null) return;
            var s = e.State;
            int me = s.Current;
            board.Viewer = _g.IsHuman(me) ? me : -1;
            if (s.IsOver || !_g.IsHuman(me) || s.Phase != Phase.AnaFaz) return;

            switch (_mode)
            {
                case Mode.None:
                case Mode.Move:
                case Mode.Attack:
                {
                    if (!s.Units.TryGetValue(_selected, out var u) || u.ActingPlayer != me) break;
                    if (_mode != Mode.Attack && CanMoveNow(u))
                        foreach (var kv in e.Actions.ReachableFor(u))
                            board.Overlay[kv.Key] = kv.Value.EstimatedDamage > 0 ? Color.Lerp(Palette.Reach, Palette.Target, 0.45f) : Palette.Reach;
                    if (_mode != Mode.Move && CanAttackNow(u))
                        foreach (var t in e.Actions.AttackTargets(u)) Mark(board, t, Palette.Target);
                    break;
                }
                case Mode.Retreat:
                {
                    if (!s.Units.TryGetValue(_selected, out var u)) break;
                    foreach (var kv in e.Grid.Reachable(u, Mathf.Min(2, e.Stats.Speed(u)))) board.Overlay[kv.Key] = Palette.Reach;
                    break;
                }
                case Mode.PlayCard:
                {
                    var card = s.Players[me].Hand.FirstOrDefault(h => h.InstanceId == _card);
                    if (card == null) break;
                    if (card.Def.Type == CardType.Birim) foreach (var c in e.Cards.DeployTiles(me)) board.Overlay[c] = Palette.Deploy;
                    else if (card.Def.Type == CardType.Yapi) foreach (var c in e.Cards.BuildTiles(me)) board.Overlay[c] = Palette.Deploy;
                    else foreach (var u in s.UnitsOf(me).Where(x => e.Cards.CanAttachTo(me, card.Def, x))) board.Overlay[u.Pos] = Palette.Deploy;
                    break;
                }
                case Mode.Floop:
                    FloopOverlay(board, me);
                    break;
                case Mode.Ulti:
                    UltiOverlay(board, me);
                    break;
            }
        }

        private void Mark(BoardView board, Entity ent, Color color)
        {
            switch (ent)
            {
                case Unit u: board.Overlay[u.Pos] = color; break;
                case Structure st: board.Overlay[st.Pos] = color; break;
                case Headquarters hq: foreach (var t in hq.Tiles) board.Overlay[t] = color; break;
            }
        }

        private bool CanMoveNow(Unit u)
        {
            if (u.ControlledBy >= 0 && u.Owner != u.ControlledBy) return u.ControlMoveLeft;
            return !u.Moved && (u.En >= 1 || E.Actions.NextMoveIsFree(u));
        }

        private bool CanAttackNow(Unit u)
        {
            if (u.ControlledBy >= 0 && u.Owner != u.ControlledBy) return u.ControlAttackLeft;
            return !u.Attacked && u.En >= 1;
        }

        private void FloopOverlay(BoardView board, int me)
        {
            var e = E;
            var src = e.State.GetEntity(_floopSource);
            if (src == null) return;
            var def = src is Unit su ? su.Def : ((Structure)src).Def;
            foreach (var tile in Board.AllTiles())
            {
                var spec = FloopSpecFor(def.Floop.Kind, tile, preview: true);
                if (spec == null) continue;
                if (e.Validate(new FloopCommand { Player = me, SourceId = _floopSource, Target = spec }) == null)
                    board.Overlay[tile] = def.Floop.Kind == FloopKind.Sifa || (def.Floop.Kind == FloopKind.RuzgarlaSavur && _pendingEntity < 0) ? Palette.Reach : Palette.Target;
            }
        }

        private void UltiOverlay(BoardView board, int me)
        {
            var e = E;
            var d = UltiCatalog.Get(_ulti);
            // Tıklanabilir hedefler (varlık hedefli ultiler)
            if (d.Targeting == UltiTargeting.EnemyEntity || d.Targeting == UltiTargeting.OwnUnit ||
                (d.Targeting == UltiTargeting.OwnUnitToTile && _pendingEntity < 0) || d.Targeting == UltiTargeting.EnemyQuadrantUnits)
            {
                foreach (var tile in Board.AllTiles())
                {
                    var spec = UltiSpecFor(d, tile, preview: true);
                    if (spec != null && ValidTarget(d, me, spec)) board.Overlay[tile] = Palette.PreviewOk;
                }
                foreach (var id in _tutulma)
                    if (e.State.Units.TryGetValue(id, out var tu)) board.Overlay[tu.Pos] = Palette.Selected;
                return;
            }
            if (!_hover.IsOnBoard) return;
            var hoverSpec = UltiSpecFor(d, _hover, preview: true);
            if (hoverSpec == null) return;
            bool ok = e.Validate(new CastUltiCommand { Player = me, UltiId = d.Id, Target = hoverSpec }) == null;
            foreach (var t in e.Ultis.AreaTiles(d, me, hoverSpec))
                if (t.IsOnBoard) board.Overlay[t] = ok ? Palette.PreviewOk : Palette.PreviewBad;
            if (_pendingTile.IsOnBoard) board.Overlay[_pendingTile] = Palette.Selected;
        }

        private bool ValidTarget(UltiDef d, int me, TargetSpec spec)
        {
            if (d.Targeting == UltiTargeting.EnemyQuadrantUnits)
            {
                if (!E.State.Units.TryGetValue(spec.EntityId, out var u)) return false;
                var probe = new TargetSpec { Player = Board.QuadrantOf(u.Pos), EntityIds = new List<int> { u.Id } };
                return E.Ultis.ValidateTarget(d, me, probe) == null;
            }
            return E.Ultis.ValidateTarget(d, me, spec) == null;
        }

        // ---------------- Hedef tanımları ----------------

        private TargetSpec FloopSpecFor(FloopKind kind, Coord tile, bool preview)
        {
            var ent = E.EntityAt(tile);
            switch (kind)
            {
                case FloopKind.CekIt:
                    return ent != null ? new TargetSpec { EntityId = ent.Id, Pull = _pull, Amount = _forceDistance } : null;
                case FloopKind.Isaretle:
                case FloopKind.Sifa:
                case FloopKind.IsikOku:
                    return ent != null ? TargetSpec.AtEntity(ent.Id) : null;
                case FloopKind.MancinikAtisi:
                    return TargetSpec.AtTile(tile);
                case FloopKind.RuzgarlaSavur:
                    if (_pendingEntity < 0)
                    {
                        if (!(ent is Unit)) return null;
                        // İlk adım: bitişik dost birim; hedef kare olarak kendi karesinin yanındaki boş kareyi deneriz.
                        var probe = Board.Around(tile, 1, false).FirstOrDefault(c => E.Grid.IsEmpty(c));
                        return probe.IsOnBoard || !preview ? new TargetSpec { EntityId = ent.Id, Tile = probe } : null;
                    }
                    return new TargetSpec { EntityId = _pendingEntity, Tile = tile };
                default:
                    return null;
            }
        }

        private static Coord TopLeftFor(Coord c, int size) => new Coord(c.Row - (size - 1) / 2, c.Col - (size - 1) / 2);

        private List<Coord> FireTiles(Coord c)
        {
            if (_fireRect) return Board.Rect(c, _fireVertical ? 3 : 2, _fireVertical ? 2 : 3);
            var list = new List<Coord>();
            for (int i = 0; i < _fireLength; i++) list.Add(_fireVertical ? new Coord(c.Row + i, c.Col) : new Coord(c.Row, c.Col + i));
            return list;
        }

        private TargetSpec UltiSpecFor(UltiDef d, Coord tile, bool preview)
        {
            var ent = E.EntityAt(tile);
            switch (d.Targeting)
            {
                case UltiTargeting.EnemyPlayer:
                case UltiTargeting.EnemyQuadrant:
                    return TargetSpec.AtPlayer(Board.QuadrantOf(tile));
                case UltiTargeting.EnemyQuadrantUnits:
                    return ent is Unit ? TargetSpec.AtEntity(ent.Id) : null;
                case UltiTargeting.OwnUnit:
                    return ent is Unit ? TargetSpec.AtEntity(ent.Id) : null;
                case UltiTargeting.OwnUnitToTile:
                    if (_pendingEntity < 0) return ent is Unit ? new TargetSpec { EntityId = ent.Id, Tile = FirstLegalTeleportTile(ent.Id) } : null;
                    return new TargetSpec { EntityId = _pendingEntity, Tile = tile };
                case UltiTargeting.AreaAny:
                case UltiTargeting.AreaEnemyQuadrant:
                    return new TargetSpec { Tile = TopLeftFor(tile, d.Size), Direction = _direction };
                case UltiTargeting.TilesEnemyQuadrant:
                    return new TargetSpec { Tiles = FireTiles(tile) };
                case UltiTargeting.CenterAny:
                case UltiTargeting.OwnDeployTile:
                    return TargetSpec.AtTile(tile);
                case UltiTargeting.EnemyEntity:
                    return ent != null ? TargetSpec.AtEntity(ent.Id) : null;
                case UltiTargeting.TwoTiles:
                    return _pendingTile.IsOnBoard ? new TargetSpec { Tile = _pendingTile, Tile2 = tile } : new TargetSpec { Tile = tile, Tile2 = FirstEmptyOtherThan(tile) };
                default:
                    return TargetSpec.None;
            }
        }

        private Coord FirstLegalTeleportTile(int unitId)
        {
            var d = UltiCatalog.Get(UltiCatalog.KayanYildiz);
            foreach (var c in Board.AllTiles())
                if (E.Ultis.ValidateTarget(d, E.State.Current, new TargetSpec { EntityId = unitId, Tile = c }) == null) return c;
            return Coord.None;
        }

        private Coord FirstEmptyOtherThan(Coord tile) => Board.AllTiles().FirstOrDefault(c => c != tile && E.Grid.IsEmpty(c));

        // ---------------- Tahta tıklaması ----------------

        private void OnBoardClick(Coord c)
        {
            var e = E;
            var s = e.State;
            int me = s.Current;
            var ent = e.EntityAt(c);
            bool myTurn = _g.IsHuman(me) && !s.IsOver && s.Phase == Phase.AnaFaz;

            if (!myTurn || _mode == Mode.None)
            {
                if (myTurn && s.Units.TryGetValue(_selected, out var sel) && sel.ActingPlayer == me)
                {
                    if (ent != null && ent.Id != sel.Id && CanAttackNow(sel) && e.Actions.AttackTargets(sel).Contains(ent))
                    {
                        Submit(new AttackCommand { UnitId = sel.Id, TargetId = ent.Id }, keepMode: true);
                        return;
                    }
                    if (ent == null && CanMoveNow(sel) && e.Actions.ReachableFor(sel).ContainsKey(c))
                    {
                        Submit(new MoveCommand { UnitId = sel.Id, To = c }, keepMode: true);
                        return;
                    }
                }
                _selected = ent != null ? ent.Id : -1;
                return;
            }

            switch (_mode)
            {
                case Mode.Move:
                    Submit(new MoveCommand { UnitId = _selected, To = c });
                    break;
                case Mode.Retreat:
                    Submit(new RetreatCommand { UnitId = _selected, To = c });
                    break;
                case Mode.Attack:
                    if (ent != null) Submit(new AttackCommand { UnitId = _selected, TargetId = ent.Id });
                    break;
                case Mode.PlayCard:
                {
                    var card = s.Players[me].Hand.FirstOrDefault(h => h.InstanceId == _card);
                    if (card == null) { CancelMode(); break; }
                    var target = card.Def.Type == CardType.Gelistirme ? TargetSpec.AtEntity(ent != null ? ent.Id : -1) : TargetSpec.AtTile(c);
                    Submit(new PlayCardCommand { CardInstanceId = _card, Target = target });
                    break;
                }
                case Mode.Floop:
                {
                    var src = s.GetEntity(_floopSource);
                    if (src == null) { CancelMode(); break; }
                    var kind = src is Unit su ? su.Def.Floop.Kind : ((Structure)src).Def.Floop.Kind;
                    if (kind == FloopKind.RuzgarlaSavur && _pendingEntity < 0)
                    {
                        if (ent is Unit) _pendingEntity = ent.Id;
                        Flash("Şimdi 4 kare içinde boş bir kare seç.");
                        break;
                    }
                    var spec = FloopSpecFor(kind, c, preview: false);
                    if (spec != null) Submit(new FloopCommand { SourceId = _floopSource, Target = spec });
                    break;
                }
                case Mode.Ulti:
                    UltiClick(c, ent);
                    break;
            }
        }

        private void UltiClick(Coord c, Entity ent)
        {
            var d = UltiCatalog.Get(_ulti);
            switch (d.Targeting)
            {
                case UltiTargeting.EnemyQuadrantUnits:
                    if (ent is Unit u)
                    {
                        if (_tutulma.Contains(u.Id)) _tutulma.Remove(u.Id);
                        else if (_tutulma.Count < 2) _tutulma.Add(u.Id);
                    }
                    return;
                case UltiTargeting.OwnUnitToTile:
                    if (_pendingEntity < 0)
                    {
                        if (ent is Unit) { _pendingEntity = ent.Id; Flash("Şimdi ışınlanacak boş kareyi seç."); }
                        return;
                    }
                    break;
                case UltiTargeting.TwoTiles:
                    if (!_pendingTile.IsOnBoard) { _pendingTile = c; Flash("İkinci geçidin karesini seç."); return; }
                    break;
            }
            var spec = UltiSpecFor(d, c, preview: false);
            if (spec != null) Submit(new CastUltiCommand { UltiId = d.Id, Target = spec });
        }

        // ---------------- Çizim ----------------

        private void EnsureStyles()
        {
            if (_text != null) return;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, richText = true };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            _cardStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true, padding = new RectOffset(6, 6, 5, 5) };
            _bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        private void OnGUI()
        {
            var e = E;
            if (e == null) return;
            EnsureStyles();
            var s = e.State;
            _panels.Clear();

            UpdateHover();
            DrawWorldLabels(s);
            DrawTopBar(s);
            DrawPlayers(s);
            DrawRightPanel(s);
            DrawHand(s);
            DrawBanner();
            DrawGameOver(s);
            HandleInput();
        }

        private void UpdateHover()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector2 mp = Event.current.mousePosition;
            var ray = cam.ScreenPointToRay(new Vector3(mp.x, Screen.height - mp.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                var c = BoardSpace.FromWorld(ray.GetPoint(enter));
                _hover = c.IsOnBoard ? c : Coord.None;
            }
            else _hover = Coord.None;
        }

        private bool OverPanel(Vector2 mp)
        {
            foreach (var r in _panels)
                if (r.Contains(mp)) return true;
            return false;
        }

        private void HandleInput()
        {
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                CancelMode();
                ev.Use();
                return;
            }
            if (ev.type == EventType.ScrollWheel && !OverPanel(ev.mousePosition))
            {
                var cam = Camera.main;
                if (cam != null) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + ev.delta.y * 1.5f, 25f, 70f);
                ev.Use();
                return;
            }
            if (ev.type != EventType.MouseDown || OverPanel(ev.mousePosition)) return;
            if (ev.button == 1)
            {
                CancelMode();
                ev.Use();
                return;
            }
            if (ev.button == 0 && _hover.IsOnBoard)
            {
                OnBoardClick(_hover);
                ev.Use();
            }
        }

        private void DrawWorldLabels(GameState s)
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var u in s.Units.Values)
            {
                var sp = cam.WorldToScreenPoint(_g.Board.WorldOfEntity(s, u.Id) + Vector3.up * 1.15f);
                if (sp.z < 0) continue;
                string hint = u.IsIllusion && _g.IsHuman(u.Owner) && s.Current == u.Owner ? " ?" : "";
                string text = u.IsIllusion ? $"{u.Def.Atk}/{u.Def.Hp}{hint}" : $"{E.Stats.Atk(u)}/{u.Hp}";
                Label(new Vector2(sp.x, Screen.height - sp.y), text, Palette.FactionAccent(s.Players[u.Owner].Faction));
            }
            foreach (var st in s.Structures.Values)
            {
                var sp = cam.WorldToScreenPoint(_g.Board.WorldOfEntity(s, st.Id) + Vector3.up * 1.2f);
                if (sp.z < 0) continue;
                Label(new Vector2(sp.x, Screen.height - sp.y), $"{st.Hp}", Color.white);
            }
            foreach (var hq in s.Hqs)
            {
                if (hq == null || hq.IsDead) continue;
                var sp = cam.WorldToScreenPoint(BoardSpace.CenterOf(hq.Tiles, 2.1f));
                if (sp.z < 0) continue;
                Label(new Vector2(sp.x, Screen.height - sp.y), $"{hq.Hp}/{hq.MaxHp}" + (hq.LastStandActive ? " SS" : ""), Color.white);
            }
            float dt = Time.deltaTime;
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var f = _floats[i];
                f.Age += dt;
                if (f.Age > 1.3f) { _floats.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(f.World + Vector3.up * (f.Age * 0.8f));
                if (sp.z < 0) continue;
                var c = f.Color;
                c.a = 1f - f.Age / 1.3f;
                var old = GUI.color;
                GUI.color = c;
                GUI.Label(new Rect(sp.x - 40, Screen.height - sp.y - 12, 80, 24), f.Text, _labelStyle);
                GUI.color = old;
            }
            if (_hover.IsOnBoard)
            {
                var tip = TileTooltip(_hover);
                if (!string.IsNullOrEmpty(tip))
                {
                    var mp = Event.current.mousePosition;
                    var size = _small.CalcSize(new GUIContent(tip));
                    var rect = new Rect(mp.x + 16, mp.y + 12, Mathf.Min(260, size.x + 12), _small.CalcHeight(new GUIContent(tip), 248) + 8);
                    GUI.Box(rect, GUIContent.none);
                    GUI.Label(new Rect(rect.x + 6, rect.y + 4, rect.width - 8, rect.height), tip, _small);
                }
            }
        }

        private void Label(Vector2 p, string text, Color color)
        {
            var rect = new Rect(p.x - 34, p.y - 10, 68, 20);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(p.x - 22, p.y - 8, 44, 16), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(rect, text, _labelStyle);
            GUI.color = old;
        }

        private string TileTooltip(Coord c)
        {
            var e = E;
            var parts = new List<string> { $"<b>{c}</b> · çeyrek: {Names.Of(e.State.Players[Board.QuadrantOf(c)].Faction)}" + (Board.IsAltar(c) ? " · Gök Sunağı" : "") };
            foreach (var fx in e.Effects.At(c))
                parts.Add($"{fx.Def.Name} ({fx.Def.Layer}, {fx.RemainingTurns} tur, {e.State.Players[fx.Caster].Name})");
            if (e.State.Delayed.Any(d => d.Tiles.Contains(c))) parts.Add("<color=#ff5040>Meteor düşecek!</color>");
            var ent = e.EntityAt(c);
            if (ent != null) parts.Add(e.EntityName(ent));
            if (_mode == Mode.None && e.State.Units.TryGetValue(_selected, out var sel) && sel.ActingPlayer == e.State.Current)
            {
                var reach = e.Actions.ReachableFor(sel);
                if (reach.TryGetValue(c, out var path) && path.EstimatedDamage > 0)
                    parts.Add($"<color=#ff8060>Yol hasarı: {path.EstimatedDamage}</color>");
            }
            return string.Join("\n", parts);
        }

        private void DrawTopBar(GameState s)
        {
            var rect = new Rect(0, 0, Screen.width, 40);
            _panels.Add(rect);
            GUI.Box(rect, GUIContent.none);
            var p = s.CurrentPlayer;
            var fc = Palette.Faction(p.Faction);
            string timer = _g.TurnTimer && _g.IsHuman(s.Current) ? $" · Süre {Mathf.CeilToInt(_g.TurnTimeLeft)} sn" : "";
            GUI.Label(new Rect(10, 8, 620, 26),
                $"<b>Dört Gök</b> · Raund {s.Round} · Sıra: <color=#{Hex(fc)}><b>{p.Name}</b></color> ({(_g.IsHuman(s.Current) ? "insan" : "bot")}) · {Names.Of(s.Phase)} · <b>AP {p.Ap}</b> · <b>GE {p.Ge}</b>{timer}",
                _text);

            float x = Screen.width - 10;
            bool myTurn = _g.IsHuman(s.Current) && !s.IsOver && s.Phase == Phase.AnaFaz;
            GUI.enabled = myTurn;
            x -= 120;
            if (GUI.Button(new Rect(x, 6, 115, 28), "Turu Bitir")) { Submit(new EndTurnCommand()); _selected = -1; }
            x -= 150;
            if (GUI.Button(new Rect(x, 6, 145, 28), $"Ek Kart Çek ({E.Rules.ExtraDrawCost} AP)")) Submit(new DrawExtraCommand(), keepMode: true);
            GUI.enabled = true;
            x -= 105;
            if (GUI.Button(new Rect(x, 6, 100, 28), "Yeni Maç")) _g.NewMatch(_g.Seed);
            x -= 150;
            GUI.Label(new Rect(x, 10, 60, 20), "Hız", _small);
            _g.EventSpeed = GUI.HorizontalSlider(new Rect(x + 30, 15, 110, 20), _g.EventSpeed, 0.5f, 5f);
            x -= 120;
            _g.TurnTimer = GUI.Toggle(new Rect(x, 10, 115, 20), _g.TurnTimer, " 90 sn süre");
        }

        private void DrawPlayers(GameState s)
        {
            var rect = new Rect(0, 40, 250, Screen.height - 40 - 170);
            _panels.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            GUILayout.Label("Oyuncular", _title);
            foreach (var p in s.Players.OrderBy(x => x.OrderPos))
            {
                var hq = s.Hqs[p.Seat];
                var fc = Palette.Faction(p.Faction);
                string turn = s.Current == p.Seat ? "  ← sırada" : "";
                string team = E.Rules.TeamMode ? $" · Takım {p.Team + 1}" : "";
                GUILayout.Label($"<color=#{Hex(fc)}><b>{p.Name}</b></color>{turn}{team}", _text);
                if (!p.Alive)
                {
                    GUILayout.Label($"<color=#888888>Elendi (raund {p.EliminatedRound})</color>", _small);
                    GUILayout.Space(6);
                    continue;
                }
                Bar(GUILayoutUtility.GetRect(200, 12), hq.Hp / (float)hq.MaxHp, new Color(0.9f, 0.3f, 0.3f), $"Karargah {hq.Hp}/{hq.MaxHp}");
                Bar(GUILayoutUtility.GetRect(200, 12), p.Ge / (float)E.Rules.GeMax, new Color(0.4f, 0.7f, 1f), $"GE {p.Ge}");
                GUILayout.Label($"El {p.Hand.Count} · Deste {p.Deck.Count} · Birim {s.UnitsOf(p.Seat).Count()} · Yapı {s.StructuresOf(p.Seat).Count()}", _small);
                bool human = _g.IsHuman(p.Seat);
                bool now = GUILayout.Toggle(human, human ? " İnsan" : " Bot");
                if (now != human) _g.SetHuman(p.Seat, now);
                GUILayout.Space(6);
            }
            GUILayout.EndArea();
        }

        private void Bar(Rect r, float frac, Color color, string text)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 4, r.y - 3, r.width, r.height + 6), text, _small);
            GUI.color = old;
        }

        private void DrawRightPanel(GameState s)
        {
            var rect = new Rect(Screen.width - 330, 40, 330, Screen.height - 40 - 170);
            _panels.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            int me = s.Current;
            bool myTurn = _g.IsHuman(me) && !s.IsOver && s.Phase == Phase.AnaFaz;

            DrawSelection(s, me, myTurn);
            GUILayout.Space(6);
            if (myTurn) DrawUltis(s, me);
            if (myTurn) DrawModeOptions(s, me);

            if (!string.IsNullOrEmpty(_status) && Time.time < _statusUntil)
                GUILayout.Label($"<color=#ffb070>{_status}</color>", _text);

            GUILayout.Label("Olay günlüğü", _title);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            foreach (var line in _log) GUILayout.Label(line, _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelection(GameState s, int me, bool myTurn)
        {
            var ent = s.GetEntity(_selected);
            if (ent == null)
            {
                GUILayout.Label(myTurn ? "Bir birim seç ya da elden kart oyna. Esc / sağ tık: iptal." : "Bot oynuyor…", _small);
                return;
            }
            var e = E;
            switch (ent)
            {
                case Unit u:
                {
                    var fc = Palette.Faction(s.Players[u.Owner].Faction);
                    GUILayout.Label($"<color=#{Hex(fc)}><b>{u.Def.Name}</b></color> ({s.Players[u.Owner].Name})", _title);
                    bool own = u.ActingPlayer == me && myTurn;
                    string stats = u.IsIllusion && !(own && u.Owner == me)
                        ? $"SAL {u.Def.Atk} · CAN {u.Def.Hp} · HIZ {u.Def.Speed} · MNZ {u.Def.Range}"
                        : $"SAL {e.Stats.Atk(u)} · CAN {u.Hp}/{e.Stats.MaxHp(u)} · HIZ {e.Stats.Speed(u)} · MNZ {e.Stats.Range(u)} · EN {u.En} · Zırh {e.Stats.Armor(u)}";
                    GUILayout.Label(stats, _text);
                    var tags = new List<string>();
                    if (u.Flying) tags.Add("Uçan");
                    if (u.IsIllusion && u.Owner == me) tags.Add("<color=#ff66ff>İllüzyon (yalnızca sen görüyorsun)</color>");
                    if (e.IsShadowed(u)) tags.Add("Gölge");
                    foreach (var st in u.Statuses) if (st.Kind != StatusKind.Shadow) tags.Add(Names.Of(st.Kind));
                    if (u.ControlledBy >= 0) tags.Add("Tutulma: " + s.Players[u.ControlledBy].Name);
                    if (u.WeaponCardId != null) tags.Add("Silah: " + CardCatalog.Get(u.WeaponCardId).Name);
                    if (u.ArmorCardId != null) tags.Add("Zırh: " + CardCatalog.Get(u.ArmorCardId).Name);
                    if (tags.Count > 0) GUILayout.Label(string.Join(" · ", tags), _small);
                    if (!string.IsNullOrEmpty(u.Def.Text)) GUILayout.Label(u.Def.Text, _small);
                    if (u.Def.HasFloop) GUILayout.Label($"<b>Floop ({u.Def.Floop.Cost} AP): {u.Def.Floop.Name}</b> — {u.Def.Floop.Text}", _small);
                    if (!own) break;

                    GUILayout.BeginHorizontal();
                    GUI.enabled = CanMoveNow(u);
                    if (GUILayout.Button(e.Actions.NextMoveIsFree(u) ? "Hareket (bedava)" : "Hareket")) { CancelMode(); _mode = Mode.Move; }
                    GUI.enabled = CanAttackNow(u);
                    if (GUILayout.Button("Saldırı")) { CancelMode(); _mode = Mode.Attack; }
                    GUI.enabled = u.ControlledBy < 0 && !u.Defended && u.En >= 1;
                    if (GUILayout.Button("Savun")) Submit(new DefendCommand { UnitId = u.Id }, keepMode: true);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (u.Def.HasFloop)
                    {
                        string why = e.Floops.CanUse(me, u.Id);
                        GUI.enabled = why == null;
                        if (GUILayout.Button($"Floop: {u.Def.Floop.Name}")) StartFloop(u.Id, u.Def.Floop.Kind);
                        GUI.enabled = true;
                    }
                    if (u.RetreatAvailable && GUILayout.Button("Geri Çekil (2)")) { CancelMode(); _mode = Mode.Retreat; }
                    GUILayout.EndHorizontal();
                    break;
                }
                case Structure st:
                {
                    var fc = Palette.Faction(s.Players[st.Owner].Faction);
                    GUILayout.Label($"<color=#{Hex(fc)}><b>{st.Def.Name}</b></color> ({s.Players[st.Owner].Name})", _title);
                    GUILayout.Label($"CAN {st.Hp}/{st.MaxHp}" + (st.Def.Range > 0 ? $" · MNZ {e.Stats.StructureRange(st)}" : ""), _text);
                    if (!string.IsNullOrEmpty(st.Def.Text)) GUILayout.Label(st.Def.Text, _small);
                    if (st.Def.HasFloop)
                    {
                        GUILayout.Label($"<b>Floop ({st.Def.Floop.Cost} AP): {st.Def.Floop.Name}</b> — {st.Def.Floop.Text}", _small);
                        if (myTurn && st.Owner == me)
                        {
                            GUI.enabled = e.Floops.CanUse(me, st.Id) == null;
                            if (GUILayout.Button($"Floop: {st.Def.Floop.Name}")) StartFloop(st.Id, st.Def.Floop.Kind);
                            GUI.enabled = true;
                        }
                    }
                    break;
                }
                case Headquarters hq:
                    GUILayout.Label($"<b>{s.Players[hq.Owner].Name} karargahı</b>", _title);
                    GUILayout.Label($"CAN {hq.Hp}/{hq.MaxHp} · Zırh {e.Stats.HqArmor(hq.Owner)}" + (hq.LastStandActive ? " · Son Siper" : ""), _text);
                    break;
            }
        }

        private void StartFloop(int sourceId, FloopKind kind)
        {
            CancelMode();
            switch (kind)
            {
                case FloopKind.Gizlen:
                case FloopKind.Hucum:
                case FloopKind.OkYagmuru:
                case FloopKind.VurKac:
                case FloopKind.Feda:
                case FloopKind.TakimyildizZirhi:
                case FloopKind.SisOrtusu:
                    Submit(new FloopCommand { SourceId = sourceId }, keepMode: true);
                    return;
                default:
                    _mode = Mode.Floop;
                    _floopSource = sourceId;
                    return;
            }
        }

        private void DrawUltis(GameState s, int me)
        {
            var p = s.Players[me];
            GUILayout.Label($"Ultiler · GE {p.Ge}", _title);
            foreach (var d in UltiCatalog.ForFaction(p.Faction))
            {
                string why = E.Ultis.Availability(me, d.Id);
                int cd = E.Ultis.RemainingCooldown(me, d.Id);
                string label = $"{d.Name} · {Names.Of(d.Tier)} {E.Ultis.Cost(d)} GE" + (cd > 0 ? $" · bekleme {cd}" : "") + (d.IsNew ? " · yeni" : "");
                GUI.enabled = why == null;
                bool active = _mode == Mode.Ulti && _ulti == d.Id;
                if (GUILayout.Button(active ? "> " + label : label))
                {
                    CancelMode();
                    if (d.Targeting == UltiTargeting.Self || d.Targeting == UltiTargeting.OwnQuadrant)
                        Submit(new CastUltiCommand { UltiId = d.Id, Target = TargetSpec.None });
                    else
                    {
                        _mode = Mode.Ulti;
                        _ulti = d.Id;
                    }
                }
                GUI.enabled = true;
                if (active) GUILayout.Label(d.Text, _small);
                else if (why != null) GUILayout.Label($"<color=#999999>{why}</color>", _small);
            }
        }

        private void DrawModeOptions(GameState s, int me)
        {
            if (_mode == Mode.Ulti && _ulti != null)
            {
                var d = UltiCatalog.Get(_ulti);
                if (d.Id == UltiCatalog.Hortum)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Yön:", _small, GUILayout.Width(36));
                    if (GUILayout.Toggle(_direction == Coord.Up, "↑", GUI.skin.button)) _direction = Coord.Up;
                    if (GUILayout.Toggle(_direction == Coord.Down, "↓", GUI.skin.button)) _direction = Coord.Down;
                    if (GUILayout.Toggle(_direction == Coord.Left, "←", GUI.skin.button)) _direction = Coord.Left;
                    if (GUILayout.Toggle(_direction == Coord.Right, "→", GUI.skin.button)) _direction = Coord.Right;
                    GUILayout.EndHorizontal();
                }
                if (d.Targeting == UltiTargeting.TilesEnemyQuadrant)
                {
                    GUILayout.BeginHorizontal();
                    _fireRect = GUILayout.Toggle(_fireRect, _fireRect ? "2×3 alan" : "Düz çizgi", GUI.skin.button);
                    _fireVertical = GUILayout.Toggle(_fireVertical, _fireVertical ? "Dikey" : "Yatay", GUI.skin.button);
                    GUILayout.EndHorizontal();
                    if (!_fireRect)
                    {
                        GUILayout.Label($"Uzunluk: {_fireLength}", _small);
                        _fireLength = Mathf.RoundToInt(GUILayout.HorizontalSlider(_fireLength, 1, 6));
                    }
                }
                if (d.Targeting == UltiTargeting.EnemyQuadrantUnits)
                {
                    GUILayout.Label($"Seçilen birim: {_tutulma.Count}/2 (CAN ≤ 5, aynı çeyrek)", _small);
                    GUI.enabled = _tutulma.Count > 0;
                    if (GUILayout.Button("Tutulma'yı onayla"))
                    {
                        var first = s.Units.TryGetValue(_tutulma[0], out var fu) ? fu : null;
                        if (first != null)
                            Submit(new CastUltiCommand { UltiId = d.Id, Target = new TargetSpec { Player = Board.QuadrantOf(first.Pos), EntityIds = _tutulma.ToList() } });
                    }
                    GUI.enabled = true;
                }
                if (GUILayout.Button("İptal (Esc)")) CancelMode();
            }
            else if (_mode == Mode.Floop)
            {
                var src = s.GetEntity(_floopSource);
                var kind = src is Unit u ? u.Def.Floop.Kind : src is Structure st ? st.Def.Floop.Kind : FloopKind.Gizlen;
                if (kind == FloopKind.CekIt)
                {
                    GUILayout.BeginHorizontal();
                    _pull = GUILayout.Toggle(_pull, _pull ? "Çek" : "İt", GUI.skin.button);
                    if (GUILayout.Toggle(_forceDistance == 1, "1 kare", GUI.skin.button)) _forceDistance = 1;
                    if (GUILayout.Toggle(_forceDistance == 2, "2 kare", GUI.skin.button)) _forceDistance = 2;
                    GUILayout.EndHorizontal();
                }
                if (kind == FloopKind.Gozlem)
                {
                    var top = E.Floops.PeekTop(me, 3);
                    GUILayout.Label("Destenin üstündeki kartlardan birini seç:", _small);
                    for (int i = 0; i < top.Count; i++)
                        if (GUILayout.Button($"{top[i].Def.Name} ({top[i].Def.Cost} AP)"))
                            Submit(new FloopCommand { SourceId = _floopSource, Target = new TargetSpec { Choice = i } });
                }
                if (GUILayout.Button("İptal (Esc)")) CancelMode();
            }
            else if (_mode != Mode.None)
            {
                if (GUILayout.Button("İptal (Esc)")) CancelMode();
            }

            foreach (var fx in s.Effects.Values.Where(f => f.Caster == me && f.Def.Id == EffectCatalog.Hortum).ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hortum yönü:", _small, GUILayout.Width(84));
                foreach (var (dir, arrow) in new[] { (Coord.Up, "↑"), (Coord.Down, "↓"), (Coord.Left, "←"), (Coord.Right, "→") })
                {
                    bool on = fx.Direction == dir;
                    if (GUILayout.Toggle(on, arrow, GUI.skin.button) && !on)
                        Submit(new SteerTornadoCommand { EffectId = fx.Id, Direction = dir }, keepMode: true);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawHand(GameState s)
        {
            var rect = new Rect(0, Screen.height - 170, Screen.width, 170);
            _panels.Add(rect);
            GUI.Box(rect, GUIContent.none);
            int me = s.Current;
            var p = s.Players[me];
            bool human = _g.IsHuman(me);
            GUI.Label(new Rect(10, rect.y + 4, 600, 22), human
                ? $"<b>{p.Name}</b> eli · {p.Hand.Count} kart · Deste {p.Deck.Count}"
                : $"<b>{p.Name}</b> (bot) düşünüyor…", _text);
            if (!human) return;

            float cardW = 170, cardH = 132;
            var view = new Rect(8, rect.y + 28, rect.width - 16, cardH + 6);
            var content = new Rect(0, 0, Mathf.Max(view.width - 20, p.Hand.Count * (cardW + 6)), cardH);
            _handScroll = GUI.BeginScrollView(view, _handScroll, content);
            for (int i = 0; i < p.Hand.Count; i++)
            {
                var card = p.Hand[i];
                var d = card.Def;
                var fc = Palette.Faction(d.Faction);
                string body = $"<b>{d.Name}</b>  <color=#{Hex(Palette.Altar)}>{d.Cost} AP</color>\n" +
                              $"<color=#{Hex(fc)}>{Names.Of(d.Faction)} · {Names.Of(d.Type)}</color>\n" +
                              (d.Type != CardType.Gelistirme ? d.StatLine + "\n" : "") +
                              (string.IsNullOrEmpty(d.Text) ? "" : d.Text + "\n") +
                              (d.Floop != null ? $"Floop ({d.Floop.Cost}): {d.Floop.Name}" : "");
                bool affordable = d.Cost <= p.Ap && s.Phase == Phase.AnaFaz;
                bool active = _mode == Mode.PlayCard && _card == card.InstanceId;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = active ? Palette.PreviewOk : affordable ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(i * (cardW + 6), 0, cardW, cardH), body, _cardStyle) && affordable)
                {
                    CancelMode();
                    _mode = Mode.PlayCard;
                    _card = card.InstanceId;
                }
                GUI.backgroundColor = old;
            }
            GUI.EndScrollView();
        }

        private void DrawBanner()
        {
            if (_banner == null || Time.time > _bannerUntil) return;
            var rect = new Rect(0, Screen.height * 0.22f, Screen.width, 60);
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(Screen.width * 0.25f, rect.y, Screen.width * 0.5f, rect.height), Texture2D.whiteTexture);
            GUI.color = _bannerColor;
            GUI.Label(rect, _banner, _bannerStyle);
            GUI.color = old;
        }

        private void DrawGameOver(GameState s)
        {
            if (!s.IsOver) return;
            var rect = new Rect(Screen.width / 2f - 220, Screen.height / 2f - 90, 440, 180);
            _panels.Add(rect);
            GUI.Box(rect, GUIContent.none);
            string winner = s.WinnerSeat >= 0
                ? string.Join(" + ", s.Players.Where(p => p.Team == s.WinnerTeam).Select(p => p.Name))
                : "yok";
            GUI.Label(new Rect(rect.x + 20, rect.y + 20, rect.width - 40, 40), $"<b>Oyun bitti</b>", _title);
            GUI.Label(new Rect(rect.x + 20, rect.y + 55, rect.width - 40, 60), $"Kazanan: <b>{winner}</b> · {s.Round} raund · tohum {_g.CurrentSeed}", _text);
            if (GUI.Button(new Rect(rect.x + 20, rect.y + 120, 180, 36), "Yeni Maç")) _g.NewMatch(_g.Seed);
        }
    }
}
