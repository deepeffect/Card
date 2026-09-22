using System.Collections.Generic;
using System.Linq;
using DortGok.Core;
using UnityEngine;

namespace DortGok.Presentation
{
    /// <summary>
    /// Gri kutu arayüzü (IMGUI): tur çubuğu, oyuncular, el, seçili birim eylemleri, Floop ve ulti hedefleme,
    /// olay günlüğü. Girdi mantığı InteractionController'dadır; bu sınıf yalnızca çizer ve tıklamaları iletir.
    /// Ulti seçilince şekil tahtada fareyle gezdirilir; geçersiz kareler kırmızı yanar (GDD Bölüm 4).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private sealed class FloatText
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
        }

        private GameBootstrap _g;
        private InteractionController _input;

        private Coord _hover = Coord.None;
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
            _input = new InteractionController(null, c => _g.Submit(c), seat => _g.IsHuman(seat));
            _input.StatusChanged += m => _statusUntil = Time.time + 4f;
            g.Queue.Played += OnPlayed;
            g.MatchStarted += ResetUi;
        }

        private void ResetUi()
        {
            _input.Engine = _g.Engine;
            _input.Reset();
            _log.Clear();
            _floats.Clear();
            _banner = null;
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
            var e = E;
            if (e == null) return;
            if (_input.Engine != e) _input.Engine = e;
            board.SelectedEntity = _input.Selected;
            board.Viewer = _g.IsHuman(e.State.Current) ? e.State.Current : -1;
            foreach (var kv in _input.Highlights(_hover))
                board.Overlay[kv.Key] = ColorOf(kv.Value);
        }

        private static Color ColorOf(Highlight h)
        {
            switch (h)
            {
                case Highlight.Reach: return Palette.Reach;
                case Highlight.ReachDanger: return Color.Lerp(Palette.Reach, Palette.Target, 0.45f);
                case Highlight.Target: return Palette.Target;
                case Highlight.Deploy: return Palette.Deploy;
                case Highlight.Ally: return Palette.Reach;
                case Highlight.PreviewOk: return Palette.PreviewOk;
                case Highlight.PreviewBad: return Palette.PreviewBad;
                default: return Palette.Selected;
            }
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
            if (_input.Engine != e) _input.Engine = e;
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
                _input.Cancel();
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
                _input.Cancel();
                ev.Use();
                return;
            }
            if (ev.button == 0 && _hover.IsOnBoard)
            {
                _input.Click(_hover);
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
                var tip = _input.Tooltip(_hover);
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
            GUI.enabled = _input.MyTurn;
            x -= 120;
            if (GUI.Button(new Rect(x, 6, 115, 28), "Turu Bitir")) _input.EndTurn();
            x -= 150;
            if (GUI.Button(new Rect(x, 6, 145, 28), $"Ek Kart Çek ({E.Rules.ExtraDrawCost} AP)")) _input.DrawExtra();
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
            bool myTurn = _input.MyTurn;

            DrawSelection(s, me, myTurn);
            GUILayout.Space(6);
            if (myTurn) DrawUltis(s, me);
            if (myTurn) DrawModeOptions(s, me);

            if (!string.IsNullOrEmpty(_input.Status) && Time.time < _statusUntil)
                GUILayout.Label($"<color=#ffb070>{_input.Status}</color>", _text);

            GUILayout.Label("Olay günlüğü", _title);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            foreach (var line in _log) GUILayout.Label(line, _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelection(GameState s, int me, bool myTurn)
        {
            var ent = s.GetEntity(_input.Selected);
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
                    GUILayout.Label(_input.UnitSummary(u), _text);
                    var tags = _input.UnitTags(u);
                    if (tags.Count > 0) GUILayout.Label(string.Join(" · ", tags), _small);
                    if (!string.IsNullOrEmpty(u.Def.Text)) GUILayout.Label(u.Def.Text, _small);
                    if (u.Def.HasFloop) GUILayout.Label($"<b>Floop ({u.Def.Floop.Cost} AP): {u.Def.Floop.Name}</b> — {u.Def.Floop.Text}", _small);
                    if (!_input.IsMine(u)) break;

                    GUILayout.BeginHorizontal();
                    GUI.enabled = _input.CanMoveNow(u);
                    if (GUILayout.Button(e.Actions.NextMoveIsFree(u) ? "Hareket (bedava)" : "Hareket")) _input.StartMove();
                    GUI.enabled = _input.CanAttackNow(u);
                    if (GUILayout.Button("Saldırı")) _input.StartAttack();
                    GUI.enabled = _input.CanDefendNow(u);
                    if (GUILayout.Button("Savun")) _input.Defend();
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (u.Def.HasFloop && u.ControlledBy < 0)
                    {
                        GUI.enabled = e.Floops.CanUse(me, u.Id) == null;
                        if (GUILayout.Button($"Floop: {u.Def.Floop.Name}")) _input.StartFloop(u.Id);
                        GUI.enabled = true;
                    }
                    if (u.RetreatAvailable && GUILayout.Button("Geri Çekil (2)")) _input.StartRetreat();
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
                            if (GUILayout.Button($"Floop: {st.Def.Floop.Name}")) _input.StartFloop(st.Id);
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
                bool active = _input.Mode == InputMode.Ulti && _input.UltiId == d.Id;
                if (GUILayout.Button(active ? "> " + label : label)) _input.StartUlti(d.Id);
                GUI.enabled = true;
                if (active) GUILayout.Label(d.Text, _small);
                else if (why != null) GUILayout.Label($"<color=#999999>{why}</color>", _small);
            }
        }

        private void DrawModeOptions(GameState s, int me)
        {
            var d = _input.ActiveUlti;
            if (_input.Mode == InputMode.Ulti && d != null)
            {
                if (d.Id == UltiCatalog.Hortum)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Yön:", _small, GUILayout.Width(36));
                    if (GUILayout.Toggle(_input.Direction == Coord.Up, "↑", GUI.skin.button)) _input.Direction = Coord.Up;
                    if (GUILayout.Toggle(_input.Direction == Coord.Down, "↓", GUI.skin.button)) _input.Direction = Coord.Down;
                    if (GUILayout.Toggle(_input.Direction == Coord.Left, "←", GUI.skin.button)) _input.Direction = Coord.Left;
                    if (GUILayout.Toggle(_input.Direction == Coord.Right, "→", GUI.skin.button)) _input.Direction = Coord.Right;
                    GUILayout.EndHorizontal();
                }
                if (d.Targeting == UltiTargeting.TilesEnemyQuadrant)
                {
                    GUILayout.BeginHorizontal();
                    _input.FireRect = GUILayout.Toggle(_input.FireRect, _input.FireRect ? "2×3 alan" : "Düz çizgi", GUI.skin.button);
                    _input.FireVertical = GUILayout.Toggle(_input.FireVertical, _input.FireVertical ? "Dikey" : "Yatay", GUI.skin.button);
                    GUILayout.EndHorizontal();
                    if (!_input.FireRect)
                    {
                        GUILayout.Label($"Uzunluk: {_input.FireLength}", _small);
                        _input.FireLength = Mathf.RoundToInt(GUILayout.HorizontalSlider(_input.FireLength, 1, 6));
                    }
                }
                if (d.Targeting == UltiTargeting.EnemyQuadrantUnits)
                {
                    GUILayout.Label($"Seçilen birim: {_input.TutulmaUnits.Count}/2 (CAN ≤ 5, aynı çeyrek)", _small);
                    GUI.enabled = _input.TutulmaUnits.Count > 0;
                    if (GUILayout.Button("Tutulma'yı onayla")) _input.ConfirmTutulma();
                    GUI.enabled = true;
                }
                if (GUILayout.Button("İptal (Esc)")) _input.Cancel();
            }
            else if (_input.Mode == InputMode.Floop)
            {
                var kind = _input.FloopKindOf(_input.FloopSource);
                if (kind == FloopKind.CekIt)
                {
                    GUILayout.BeginHorizontal();
                    _input.Pull = GUILayout.Toggle(_input.Pull, _input.Pull ? "Çek" : "İt", GUI.skin.button);
                    if (GUILayout.Toggle(_input.ForceDistance == 1, "1 kare", GUI.skin.button)) _input.ForceDistance = 1;
                    if (GUILayout.Toggle(_input.ForceDistance == 2, "2 kare", GUI.skin.button)) _input.ForceDistance = 2;
                    GUILayout.EndHorizontal();
                }
                if (kind == FloopKind.Gozlem)
                {
                    var top = E.Floops.PeekTop(me, 3);
                    for (int i = 0; i < top.Count; i++)
                        if (GUILayout.Button($"{top[i].Def.Name} ({top[i].Def.Cost} AP)")) _input.ChooseGozlem(i);
                }
                if (GUILayout.Button("İptal (Esc)")) _input.Cancel();
            }
            else if (_input.Mode != InputMode.None)
            {
                if (GUILayout.Button("İptal (Esc)")) _input.Cancel();
            }

            foreach (var fx in s.Effects.Values.Where(f => f.Caster == me && f.Def.Id == EffectCatalog.Hortum).ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hortum yönü:", _small, GUILayout.Width(84));
                foreach (var (dir, arrow) in new[] { (Coord.Up, "↑"), (Coord.Down, "↓"), (Coord.Left, "←"), (Coord.Right, "→") })
                {
                    bool on = fx.Direction == dir;
                    if (GUILayout.Toggle(on, arrow, GUI.skin.button) && !on) _input.Steer(fx.Id, dir);
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
                bool active = _input.Mode == InputMode.PlayCard && _input.CardInstance == card.InstanceId;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = active ? Palette.PreviewOk : affordable ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(new Rect(i * (cardW + 6), 0, cardW, cardH), body, _cardStyle)) _input.SelectCard(card.InstanceId);
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
            GUI.Label(new Rect(rect.x + 20, rect.y + 20, rect.width - 40, 40), "<b>Oyun bitti</b>", _title);
            GUI.Label(new Rect(rect.x + 20, rect.y + 55, rect.width - 40, 60), $"Kazanan: <b>{winner}</b> · {s.Round} raund · tohum {_g.CurrentSeed}", _text);
            if (GUI.Button(new Rect(rect.x + 20, rect.y + 120, 180, 36), "Yeni Maç")) _g.NewMatch(_g.Seed);
        }
    }
}
