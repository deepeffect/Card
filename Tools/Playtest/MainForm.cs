using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DortGok.Core;

namespace DortGok.Playtest
{
    /// <summary>
    /// Masaüstü test istemcisi: aynı ekranda sırayla oynanır, insan olmayan koltuklara bot oturur (GDD Faz 1 hot-seat).
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const int Col = 340;

        private RulesEngine _engine;
        private InteractionController _input;
        private SimpleBot[] _bots;
        private readonly bool[] _human = { true, false, false, false };
        private int _seed;

        private readonly BoardControl _board = new BoardControl();
        private readonly Label _top = new Label();
        private readonly Label _hoverInfo = new Label();
        private readonly Label _status = new Label();
        private readonly FlowLayoutPanel _players = Column();
        private readonly FlowLayoutPanel _selection = Column();
        private readonly FlowLayoutPanel _options = Column();
        private readonly FlowLayoutPanel _ultis = Column();
        private readonly ListBox _log = new ListBox();
        private readonly FlowLayoutPanel _hand = new FlowLayoutPanel();
        private readonly Button _endTurn = new Button();
        private readonly Button _drawExtra = new Button();
        private readonly Button _newMatch = new Button();
        private readonly Timer _botTimer = new Timer { Interval = 700 };
        private readonly ToolTip _tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        private readonly List<string> _pendingLog = new List<string>();

        public MainForm(int tile = 0)
        {
            Text = "Dört Gök — test istemcisi (.NET 10)";
            BackColor = Color.FromArgb(24, 26, 34);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;

            if (tile <= 0)
            {
                var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 1000);
                tile = Math.Max(36, Math.Min(60, Math.Min((area.Height - 290) / 12, (area.Width - 2 * Col - 60) / 12)));
            }
            BuildLayout(tile);
            _botTimer.Tick += (_, __) => BotStep();
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { _input?.Cancel(); RefreshUi(); } };
            NewMatch(0);
            _botTimer.Start();
        }

        private static FlowLayoutPanel Column() => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(32, 35, 46),
            Padding = new Padding(6)
        };

        private void BuildLayout(int tile)
        {
            SuspendLayout();
            _board.SetTile(tile);
            int boardSize = _board.Width;
            int x0 = 10, y0 = 50;

            _top.SetBounds(x0, 8, boardSize, 36);
            _top.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _top.TextAlign = ContentAlignment.MiddleLeft;

            int bx = x0 + boardSize + 10;
            _newMatch.Text = "Yeni Maç";
            _drawExtra.Text = "Ek Kart Çek (1 AP)";
            _endTurn.Text = "Turu Bitir";
            foreach (var (btn, w) in new[] { (_newMatch, 100), (_drawExtra, 140), (_endTurn, 110) })
            {
                btn.SetBounds(bx, 10, w, 32);
                StyleButton(btn);
                bx += w + 8;
            }
            _endTurn.BackColor = Color.FromArgb(70, 110, 70);
            _newMatch.Click += (_, __) => NewMatch(0);
            _drawExtra.Click += (_, __) => { _input.DrawExtra(); RefreshUi(); };
            _endTurn.Click += (_, __) => { _input.EndTurn(); RefreshUi(); };

            _board.Location = new Point(x0, y0);
            _board.TileClicked += c => { _input.Click(c); RefreshUi(); };
            _board.RightClicked += () => { _input.Cancel(); RefreshUi(); };
            _board.HoverChanged += c => _hoverInfo.Text = c.IsOnBoard ? _input.Tooltip(c) : "";
            _board.IsHuman = seat => _human[seat];

            _hoverInfo.SetBounds(x0, y0 + boardSize + 4, boardSize, 64);
            _hoverInfo.ForeColor = Color.LightGray;

            int cx = x0 + boardSize + 10;
            int colHeight = boardSize + 68;
            _players.SetBounds(cx, y0, Col, 250);
            _selection.SetBounds(cx, y0 + 256, Col, 250);
            _options.SetBounds(cx, y0 + 512, Col, colHeight - 512);
            _ultis.SetBounds(cx + Col + 8, y0, Col, 250);
            _status.SetBounds(cx + Col + 8, y0 + 256, Col, 46);
            _status.ForeColor = Color.FromArgb(255, 176, 112);
            _log.SetBounds(cx + Col + 8, y0 + 306, Col, colHeight - 306);
            _log.BackColor = Color.FromArgb(20, 22, 30);
            _log.ForeColor = Color.Gainsboro;
            _log.BorderStyle = BorderStyle.None;
            _log.HorizontalScrollbar = true;

            _hand.SetBounds(x0, y0 + colHeight + 6, boardSize + 20 + 2 * Col, 150);
            _hand.FlowDirection = FlowDirection.LeftToRight;
            _hand.WrapContents = false;
            _hand.AutoScroll = true;
            _hand.BackColor = Color.FromArgb(32, 35, 46);

            Controls.AddRange(new Control[] { _top, _newMatch, _drawExtra, _endTurn, _board, _hoverInfo, _players, _selection, _options, _ultis, _status, _log, _hand });
            ClientSize = new Size(_hand.Right + 10, _hand.Bottom + 10);
            ResumeLayout();
        }

        private static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(52, 56, 72);
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = Color.FromArgb(90, 95, 120);
        }

        // ---------------- Maç ----------------

        private void NewMatch(int seed)
        {
            _seed = seed != 0 ? seed : new Random().Next(1, int.MaxValue);
            _pendingLog.Clear();
            _log.Items.Clear();
            _engine = RulesEngine.CreateMatch(new GameSetup { Seed = _seed }, e =>
            {
                if (!string.IsNullOrEmpty(e.Message) && e.Type != GameEventType.ApChanged) _pendingLog.Add(e.Message);
            });
            _input = new InteractionController(_engine, c => _engine.Execute(c), seat => _human[seat]);
            _input.StatusChanged += m => _status.Text = m ?? "";
            _bots = Enumerable.Range(0, Board.Seats).Select(i => new SimpleBot(_engine, i)).ToArray();
            _board.Engine = _engine;
            _board.Input = _input;
            Text = $"Dört Gök — test istemcisi (.NET 10) · tohum {_seed}";
            RefreshUi();
        }

        /// <summary>Sıradaki koltuk bot ise bir turunu oynatır (tur başına bir zamanlayıcı adımı).</summary>
        public void BotStep()
        {
            var s = _engine.State;
            if (s.IsOver || _human[s.Current]) return;
            _bots[s.Current].PlayTurn();
            RefreshUi();
        }

        /// <summary>Testler ve ekran görüntüsü için: tüm koltukları bot olarak n tur oynatır.</summary>
        public void AutoPlay(int turns)
        {
            for (int i = 0; i < turns && !_engine.State.IsOver; i++) _bots[_engine.State.Current].PlayTurn();
            RefreshUi();
        }

        public void SetHuman(int seat, bool human)
        {
            _human[seat] = human;
            RefreshUi();
        }

        public void SelectFirstUnitOfCurrent()
        {
            var u = _engine.State.UnitsOf(_engine.State.Current).FirstOrDefault(x => !x.IsIllusion);
            if (u != null) _input.Selected = u.Id;
            RefreshUi();
        }

        public void StopTimer() => _botTimer.Stop();

        public int CurrentSeat => _engine.State.Current;

        // ---------------- Arayüz ----------------

        private void RefreshUi()
        {
            if (_engine == null) return;
            SuspendLayout();
            FlushLog();
            var s = _engine.State;
            var p = s.CurrentPlayer;
            bool human = _human[s.Current];
            _top.Text = $"Raund {s.Round} · Sıra: {p.Name} ({(human ? "insan" : "bot")}) · {Names.Of(s.Phase)} · AP {p.Ap} · GE {p.Ge}";
            _top.ForeColor = WinPalette.Faction(p.Faction);
            _endTurn.Enabled = _input.MyTurn;
            _drawExtra.Enabled = _input.MyTurn;
            BuildPlayers();
            BuildSelection();
            BuildUltis();
            BuildOptions();
            BuildHand();
            var hover = _board.Hover;
            if (hover.IsOnBoard) _hoverInfo.Text = _input.Tooltip(hover);
            _board.Invalidate();
            ResumeLayout();
        }

        private void FlushLog()
        {
            if (_pendingLog.Count == 0) return;
            _log.BeginUpdate();
            foreach (var line in _pendingLog) _log.Items.Add(line);
            while (_log.Items.Count > 500) _log.Items.RemoveAt(0);
            _log.EndUpdate();
            _pendingLog.Clear();
            _log.TopIndex = Math.Max(0, _log.Items.Count - 1);
        }

        private static void Clear(Control panel)
        {
            var old = panel.Controls.Cast<Control>().ToList();
            panel.Controls.Clear();
            foreach (var c in old) c.Dispose();
        }

        private Label Text_(string text, Color? color = null, bool bold = false, int width = Col - 30)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                ForeColor = color ?? Color.Gainsboro,
                Margin = new Padding(2, 2, 2, 2)
            };
            if (bold) l.Font = new Font(Font, FontStyle.Bold);
            return l;
        }

        private Button Btn(string text, Action onClick, bool enabled = true, int width = Col - 34, string tip = null)
        {
            var b = new Button { Text = text, Width = width, Height = 28, Enabled = enabled, Margin = new Padding(2) };
            StyleButton(b);
            b.Click += (_, __) => { onClick(); RefreshUi(); };
            if (tip != null) _tip.SetToolTip(b, tip);
            return b;
        }

        private FlowLayoutPanel Row() => new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };

        private void BuildPlayers()
        {
            Clear(_players);
            var s = _engine.State;
            _players.Controls.Add(Text_("Oyuncular (tur sırasıyla)", Color.White, bold: true));
            foreach (var p in s.Players.OrderBy(x => x.OrderPos))
            {
                var hq = s.Hqs[p.Seat];
                var row = Row();
                row.Controls.Add(Text_(p.Name + (s.Current == p.Seat ? "  ← sırada" : ""), WinPalette.Faction(p.Faction), bold: true, width: 220));
                var cb = new CheckBox { Text = "İnsan", Checked = _human[p.Seat], AutoSize = true, ForeColor = Color.Gainsboro, Margin = new Padding(8, 0, 0, 0) };
                int seat = p.Seat;
                cb.CheckedChanged += (_, __) => SetHuman(seat, cb.Checked);
                row.Controls.Add(cb);
                _players.Controls.Add(row);
                _players.Controls.Add(p.Alive
                    ? Text_($"Karargah {hq.Hp} · GE {p.Ge} · El {p.Hand.Count} · Deste {p.Deck.Count} · Birim {s.UnitsOf(p.Seat).Count()}", Color.LightGray)
                    : Text_($"Elendi (raund {p.EliminatedRound})", Color.Gray));
            }
        }

        private void BuildSelection()
        {
            Clear(_selection);
            var s = _engine.State;
            int me = s.Current;
            var ent = s.GetEntity(_input.Selected);
            if (ent == null)
            {
                _selection.Controls.Add(Text_(_input.MyTurn
                    ? "Bir birim seç ya da aşağıdan kart oyna.\nSeçili birimle: yeşil kareye tıkla = hareket, kırmızıya = saldırı.\nEsc / sağ tık: iptal."
                    : "Bot oynuyor…", Color.LightGray));
                return;
            }
            switch (ent)
            {
                case Unit u:
                {
                    _selection.Controls.Add(Text_($"{u.Def.Name} ({s.Players[u.Owner].Name})", WinPalette.Faction(s.Players[u.Owner].Faction), bold: true));
                    _selection.Controls.Add(Text_(_input.UnitSummary(u)));
                    var tags = _input.UnitTags(u);
                    if (tags.Count > 0) _selection.Controls.Add(Text_(string.Join(" · ", tags), Color.LightGray));
                    if (!string.IsNullOrEmpty(u.Def.Text)) _selection.Controls.Add(Text_(u.Def.Text, Color.LightGray));
                    if (u.Def.HasFloop) _selection.Controls.Add(Text_($"Floop ({u.Def.Floop.Cost} AP): {u.Def.Floop.Name} — {u.Def.Floop.Text}", Color.LightGray));
                    if (!_input.IsMine(u)) break;
                    var row = Row();
                    row.Controls.Add(Btn(_engine.Actions.NextMoveIsFree(u) ? "Hareket (bedava)" : "Hareket", _input.StartMove, _input.CanMoveNow(u), 100));
                    row.Controls.Add(Btn("Saldırı", _input.StartAttack, _input.CanAttackNow(u), 90));
                    row.Controls.Add(Btn("Savun", _input.Defend, _input.CanDefendNow(u), 90));
                    _selection.Controls.Add(row);
                    if (u.Def.HasFloop && u.ControlledBy < 0)
                    {
                        string why = _engine.Floops.CanUse(me, u.Id);
                        _selection.Controls.Add(Btn($"Floop: {u.Def.Floop.Name} ({u.Def.Floop.Cost} AP)", () => _input.StartFloop(u.Id), why == null, tip: why ?? u.Def.Floop.Text));
                    }
                    if (u.RetreatAvailable) _selection.Controls.Add(Btn("Geri Çekil (2 kare)", _input.StartRetreat));
                    break;
                }
                case Structure st:
                {
                    _selection.Controls.Add(Text_($"{st.Def.Name} ({s.Players[st.Owner].Name})", WinPalette.Faction(s.Players[st.Owner].Faction), bold: true));
                    _selection.Controls.Add(Text_($"CAN {st.Hp}/{st.MaxHp}" + (st.Def.Range > 0 ? $" · MNZ {_engine.Stats.StructureRange(st)}" : "")));
                    if (!string.IsNullOrEmpty(st.Def.Text)) _selection.Controls.Add(Text_(st.Def.Text, Color.LightGray));
                    if (st.Def.HasFloop)
                    {
                        _selection.Controls.Add(Text_($"Floop ({st.Def.Floop.Cost} AP): {st.Def.Floop.Name} — {st.Def.Floop.Text}", Color.LightGray));
                        if (_input.MyTurn && st.Owner == me)
                        {
                            string why = _engine.Floops.CanUse(me, st.Id);
                            _selection.Controls.Add(Btn($"Floop: {st.Def.Floop.Name}", () => _input.StartFloop(st.Id), why == null, tip: why));
                        }
                    }
                    break;
                }
                case Headquarters hq:
                    _selection.Controls.Add(Text_($"{s.Players[hq.Owner].Name} karargahı", WinPalette.Faction(s.Players[hq.Owner].Faction), bold: true));
                    _selection.Controls.Add(Text_($"CAN {hq.Hp}/{hq.MaxHp} · Zırh {_engine.Stats.HqArmor(hq.Owner)}" + (hq.LastStandActive ? " · Son Siper" : "")));
                    break;
            }
        }

        private void BuildUltis()
        {
            Clear(_ultis);
            var s = _engine.State;
            int me = s.Current;
            var p = s.Players[me];
            _ultis.Controls.Add(Text_($"{Names.Of(p.Faction)} ultileri · GE {p.Ge}", WinPalette.Faction(p.Faction), bold: true));
            foreach (var d in UltiCatalog.ForFaction(p.Faction))
            {
                string why = _input.MyTurn ? _engine.Ultis.Availability(me, d.Id) : "Sıra sende değil.";
                int cd = _engine.Ultis.RemainingCooldown(me, d.Id);
                bool active = _input.Mode == InputMode.Ulti && _input.UltiId == d.Id;
                string label = (active ? "> " : "") + $"{d.Name} · {Names.Of(d.Tier)} {_engine.Ultis.Cost(d)} GE" + (cd > 0 ? $" · bekleme {cd}" : "") + (d.IsNew ? " · yeni" : "");
                _ultis.Controls.Add(Btn(label, () => _input.StartUlti(d.Id), why == null, tip: d.Text));
                if (why != null && _input.MyTurn) _ultis.Controls.Add(Text_(why, Color.Gray));
            }
        }

        private void BuildOptions()
        {
            Clear(_options);
            var s = _engine.State;
            int me = s.Current;
            if (!_input.MyTurn) return;
            var d = _input.ActiveUlti;
            if (_input.Mode == InputMode.Ulti && d != null)
            {
                _options.Controls.Add(Text_($"{d.Name}: {d.Text}", Color.White));
                if (d.Id == UltiCatalog.Hortum) _options.Controls.Add(DirectionRow(_input.Direction, dir => _input.Direction = dir));
                if (d.Targeting == UltiTargeting.TilesEnemyQuadrant)
                {
                    var row = Row();
                    row.Controls.Add(Btn(_input.FireRect ? "Şekil: 2×3 alan" : "Şekil: düz çizgi", () => _input.FireRect = !_input.FireRect, width: 130));
                    row.Controls.Add(Btn(_input.FireVertical ? "Dikey" : "Yatay", () => _input.FireVertical = !_input.FireVertical, width: 70));
                    if (!_input.FireRect)
                    {
                        row.Controls.Add(Btn("−", () => _input.FireLength = Math.Max(1, _input.FireLength - 1), width: 28));
                        row.Controls.Add(Text_($"{_input.FireLength} kare", width: 60));
                        row.Controls.Add(Btn("+", () => _input.FireLength = Math.Min(6, _input.FireLength + 1), width: 28));
                    }
                    _options.Controls.Add(row);
                }
                if (d.Targeting == UltiTargeting.EnemyQuadrantUnits)
                {
                    _options.Controls.Add(Text_($"Seçilen birim: {_input.TutulmaUnits.Count}/2 (CAN ≤ 5, aynı çeyrek)"));
                    _options.Controls.Add(Btn("Tutulma'yı onayla", _input.ConfirmTutulma, _input.TutulmaUnits.Count > 0));
                }
                _options.Controls.Add(Btn("İptal (Esc)", _input.Cancel));
            }
            else if (_input.Mode == InputMode.Floop)
            {
                var kind = _input.FloopKindOf(_input.FloopSource);
                if (kind == FloopKind.CekIt)
                {
                    var row = Row();
                    row.Controls.Add(Btn(_input.Pull ? "Çek" : "İt", () => _input.Pull = !_input.Pull, width: 60));
                    row.Controls.Add(Btn(_input.ForceDistance == 1 ? "[1 kare]" : "1 kare", () => _input.ForceDistance = 1, width: 70));
                    row.Controls.Add(Btn(_input.ForceDistance == 2 ? "[2 kare]" : "2 kare", () => _input.ForceDistance = 2, width: 70));
                    _options.Controls.Add(row);
                }
                if (kind == FloopKind.Gozlem)
                {
                    var top = _engine.Floops.PeekTop(me, 3);
                    for (int i = 0; i < top.Count; i++)
                    {
                        int idx = i;
                        _options.Controls.Add(Btn($"{top[i].Def.Name} ({top[i].Def.Cost} AP)", () => _input.ChooseGozlem(idx)));
                    }
                }
                _options.Controls.Add(Btn("İptal (Esc)", _input.Cancel));
            }
            else if (_input.Mode != InputMode.None)
            {
                _options.Controls.Add(Btn("İptal (Esc)", _input.Cancel));
            }

            foreach (var fx in s.Effects.Values.Where(f => f.Caster == me && f.Def.Id == EffectCatalog.Hortum).ToList())
            {
                _options.Controls.Add(Text_("Hortumun sonraki yönü:"));
                int id = fx.Id;
                _options.Controls.Add(DirectionRow(fx.Direction, dir => _input.Steer(id, dir)));
            }
        }

        private Control DirectionRow(Coord current, Action<Coord> set)
        {
            var row = Row();
            foreach (var (dir, arrow) in new[] { (Coord.Up, "↑"), (Coord.Down, "↓"), (Coord.Left, "←"), (Coord.Right, "→") })
            {
                var d = dir;
                row.Controls.Add(Btn(current == d ? $"[{arrow}]" : arrow, () => set(d), width: 44));
            }
            return row;
        }

        private void BuildHand()
        {
            Clear(_hand);
            var s = _engine.State;
            var p = s.CurrentPlayer;
            if (!_human[s.Current])
            {
                _hand.Controls.Add(Text_($"{p.Name} (bot) oynuyor… El: {p.Hand.Count} kart", Color.LightGray, width: 600));
                return;
            }
            foreach (var card in p.Hand)
            {
                var d = card.Def;
                bool affordable = d.Cost <= p.Ap && _input.MyTurn;
                bool active = _input.Mode == InputMode.PlayCard && _input.CardInstance == card.InstanceId;
                string body = $"{d.Name} — {d.Cost} AP\n{Names.Of(d.Faction)} · {Names.Of(d.Type)}" +
                              (d.Type == CardType.Gelistirme ? " · " + Names.Of(d.Slot) : "") + "\n" +
                              (d.Type != CardType.Gelistirme ? d.StatLine + "\n" : "") +
                              (d.Floop != null ? $"Floop ({d.Floop.Cost}): {d.Floop.Name}" : "");
                var b = new Button
                {
                    Text = body,
                    Width = 175,
                    Height = 122,
                    TextAlign = ContentAlignment.TopLeft,
                    Margin = new Padding(4)
                };
                StyleButton(b);
                var tint = d.Faction == Faction.Notr ? Color.FromArgb(120, 140, 150) : WinPalette.Faction(d.Faction);
                b.BackColor = active ? Color.FromArgb(120, 110, 40) : affordable ? WinPalette.Dark(tint, 0.5f) : Color.FromArgb(28, 28, 32);
                b.ForeColor = affordable || active ? Color.White : Color.FromArgb(110, 110, 110);
                if (!affordable && !active) b.Text += "\n(AP yetersiz)";
                int id = card.InstanceId;
                b.Click += (_, __) => { _input.SelectCard(id); RefreshUi(); };
                string tip = (string.IsNullOrEmpty(d.Text) ? "" : d.Text + "\n") + (d.Floop != null ? $"Floop ({d.Floop.Cost} AP): {d.Floop.Text}\n" : "") + (d.Visual ?? "");
                if (tip.Length > 0) _tip.SetToolTip(b, tip.Trim());
                _hand.Controls.Add(b);
            }
        }
    }
}
