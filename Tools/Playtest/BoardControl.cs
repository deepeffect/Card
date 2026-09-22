using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using DortGok.Core;

namespace DortGok.Playtest
{
    /// <summary>
    /// 12×12 tahtanın üstten 2D çizimi. Zemin etkileri kareyi doldurur, Hava etkileri çapraz tarama,
    /// Alan etkileri iç çerçeve olarak çizilir (Unity'deki katman gösterimiyle aynı).
    /// </summary>
    internal sealed class BoardControl : Control
    {
        public int Tile = 56;
        public RulesEngine Engine;
        public InteractionController Input;
        public Func<int, bool> IsHuman = _ => false;
        public Coord Hover = Coord.None;

        public event Action<Coord> TileClicked;
        public event Action RightClicked;
        public event Action<Coord> HoverChanged;

        public BoardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void SetTile(int tile)
        {
            Tile = tile;
            Size = new Size(Tile * Board.Size + 1, Tile * Board.Size + 1);
        }

        private Rectangle TileRect(Coord c) => new Rectangle(c.Col * Tile, c.Row * Tile, Tile, Tile);

        private Coord At(Point p)
        {
            var c = new Coord(p.Y / Tile, p.X / Tile);
            return c.IsOnBoard && p.X >= 0 && p.Y >= 0 ? c : Coord.None;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var c = At(e.Location);
            if (c == Hover) return;
            Hover = c;
            HoverChanged?.Invoke(c);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hover = Coord.None;
            HoverChanged?.Invoke(Coord.None);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) RightClicked?.Invoke();
            else if (e.Button == MouseButtons.Left)
            {
                var c = At(e.Location);
                if (c.IsOnBoard) TileClicked?.Invoke(c);
            }
        }

        private static string Initials(string name)
        {
            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1) return words[0].Length >= 2 ? words[0].Substring(0, 2) : words[0];
            return words[0].Substring(0, 1) + words[1].Substring(0, 1);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(WinPalette.Table);
            if (Engine == null) return;
            var s = Engine.State;
            var highlights = Input != null ? Input.Highlights(Hover) : new Dictionary<Coord, Highlight>();
            var meteor = new HashSet<Coord>(s.Delayed.SelectMany(d => d.Tiles));

            using var small = new Font("Segoe UI", Math.Max(7f, Tile * 0.16f), FontStyle.Bold);
            using var tiny = new Font("Segoe UI", Math.Max(6.5f, Tile * 0.14f));
            using var big = new Font("Segoe UI", Math.Max(8f, Tile * 0.2f), FontStyle.Bold);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // Kare zemini ve Zemin katmanı
            foreach (var c in Board.AllTiles())
            {
                int q = Board.QuadrantOf(c);
                var p = s.Players[q];
                var col = WinPalette.Lerp(WinPalette.Table, WinPalette.Faction(p.Faction), ((c.Row + c.Col) & 1) == 0 ? 0.32f : 0.25f);
                if (!p.Alive) col = WinPalette.Lerp(col, WinPalette.Ruin, 0.6f);
                if (Board.IsAltar(c)) col = WinPalette.Lerp(col, WinPalette.Altar, 0.75f);
                if (Engine.Grid.IsRuin(c)) col = WinPalette.Ruin;
                foreach (var fx in Engine.Effects.At(c))
                    if (fx.Def.Layer == EffectLayer.Zemin) col = WinPalette.Lerp(col, WinPalette.Effect(fx.Def.Id), WinPalette.EffectWeight(fx.Def.Id));
                if (meteor.Contains(c)) col = WinPalette.Lerp(col, WinPalette.Meteor, 0.5f);
                using (var b = new SolidBrush(col)) g.FillRectangle(b, TileRect(c));
            }

            // Hava (çapraz tarama) ve Alan (iç çerçeve) katmanları
            foreach (var fx in s.Effects.Values)
            {
                var ec = WinPalette.Effect(fx.Def.Id);
                foreach (var t in fx.Tiles)
                {
                    var r = TileRect(t);
                    if (fx.Def.Layer == EffectLayer.Hava)
                    {
                        using var hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, WinPalette.Alpha(ec, 170), Color.Transparent);
                        g.FillRectangle(hb, r);
                    }
                    else if (fx.Def.Layer == EffectLayer.Alan)
                    {
                        using var pen = new Pen(ec, Math.Max(2f, Tile * 0.07f));
                        r.Inflate(-4, -4);
                        g.DrawRectangle(pen, r);
                    }
                }
            }

            // Vurgular
            foreach (var kv in highlights)
            {
                var r = TileRect(kv.Key);
                if (kv.Value == Highlight.Selected)
                {
                    using var pen = new Pen(Color.White, 3f);
                    r.Inflate(-2, -2);
                    g.DrawRectangle(pen, r);
                    continue;
                }
                using var b = new SolidBrush(WinPalette.Alpha(WinPalette.Highlight(kv.Value), 120));
                g.FillRectangle(b, r);
            }
            if (Hover.IsOnBoard)
                using (var b = new SolidBrush(Color.FromArgb(45, 255, 255, 255))) g.FillRectangle(b, TileRect(Hover));

            // Izgara
            using (var gridPen = new Pen(WinPalette.Grid))
            {
                for (int i = 0; i <= Board.Size; i++)
                {
                    g.DrawLine(gridPen, i * Tile, 0, i * Tile, Board.Size * Tile);
                    g.DrawLine(gridPen, 0, i * Tile, Board.Size * Tile, i * Tile);
                }
            }
            using (var quadPen = new Pen(Color.FromArgb(120, 255, 255, 255), 2f))
            {
                g.DrawLine(quadPen, 6 * Tile, 0, 6 * Tile, 12 * Tile);
                g.DrawLine(quadPen, 0, 6 * Tile, 12 * Tile, 6 * Tile);
            }

            // Karargahlar
            foreach (var hq in s.Hqs)
            {
                var o = Board.HqOrigin(hq.Owner);
                var r = new Rectangle(o.Col * Tile + 4, o.Row * Tile + 4, Tile * 2 - 8, Tile * 2 - 8);
                var faction = s.Players[hq.Owner].Faction;
                if (hq.IsDead)
                {
                    using var rb = new SolidBrush(WinPalette.Ruin);
                    g.FillRectangle(rb, r);
                    g.DrawString("Harabe", small, Brushes.Gray, r, center);
                    continue;
                }
                using (var hb = new SolidBrush(WinPalette.Dark(WinPalette.Faction(faction), 0.6f))) g.FillRectangle(hb, r);
                using (var pen = new Pen(hq.LastStandActive ? WinPalette.Altar : WinPalette.Accent(faction), hq.LastStandActive ? 4f : 2f)) g.DrawRectangle(pen, r);
                float frac = hq.Hp / (float)hq.MaxHp;
                var bar = new Rectangle(r.X + 6, r.Bottom - 12, (int)((r.Width - 12) * frac), 6);
                using (var bb = new SolidBrush(Color.FromArgb(230, 90, 80))) g.FillRectangle(bb, bar);
                g.DrawString("Karargah", small, Brushes.White, new Rectangle(r.X, r.Y + 4, r.Width, r.Height / 3), center);
                g.DrawString($"{hq.Hp}/{hq.MaxHp}" + (hq.LastStandActive ? "\nSon Siper" : ""), big, Brushes.White, new Rectangle(r.X, r.Y + r.Height / 3, r.Width, r.Height * 2 / 3 - 10), center);
            }

            // Yapılar
            foreach (var st in s.Structures.Values)
            {
                var r = TileRect(st.Pos);
                r.Inflate(-7, -7);
                var fc = WinPalette.Faction(s.Players[st.Owner].Faction);
                var fill = WinPalette.Lerp(WinPalette.Dark(fc, 0.45f), WinPalette.Lerp(fc, Color.Silver, 0.35f), st.Hp / (float)Math.Max(1, st.MaxHp));
                using (var b = new SolidBrush(fill)) g.FillRectangle(b, r);
                using (var pen = new Pen(Input != null && Input.Selected == st.Id ? Color.White : WinPalette.Accent(s.Players[st.Owner].Faction), 2f)) g.DrawRectangle(pen, r);
                if (st.Def.Passive == CardPassive.OkcuKulesi || st.CardId == CardCatalog.KizginMancinik)
                    g.FillPolygon(Brushes.White, new[] { new Point(r.X + r.Width / 2, r.Y + 2), new Point(r.X + r.Width / 2 - 5, r.Y + 9), new Point(r.X + r.Width / 2 + 5, r.Y + 9) });
                g.DrawString(Initials(st.Def.Name) + "\n" + st.Hp, small, Brushes.Black, r, center);
            }

            // Birimler
            int viewer = IsHuman(s.Current) ? s.Current : -1;
            foreach (var u in s.Units.Values)
            {
                var r = TileRect(u.Pos);
                r.Inflate(-6, -6);
                var owner = s.Players[u.Owner];
                var fc = u.CardId == CardCatalog.AnkaKusu ? Color.FromArgb(255, 115, 26) : WinPalette.Faction(owner.Faction);
                if (u.Has(StatusKind.Hidden)) fc = WinPalette.Lerp(fc, Color.Black, 0.6f);
                if (u.Has(StatusKind.Blinded)) fc = WinPalette.Lerp(fc, Color.Gray, 0.6f);
                using (var b = new SolidBrush(fc)) g.FillEllipse(b, r);

                var ring = WinPalette.Accent(owner.Faction);
                float width = 2f;
                if (Engine.IsShadowed(u)) ring = WinPalette.Dark(ring, 0.45f);
                if (u.ControlledBy >= 0) { ring = Color.Magenta; width = 3f; }
                if (Input != null && Input.Selected == u.Id) { ring = Color.White; width = 3.5f; }
                using (var pen = new Pen(ring, width))
                {
                    if (u.Has(StatusKind.Hidden)) pen.DashStyle = DashStyle.Dash;
                    g.DrawEllipse(pen, r);
                    if (u.Flying)
                    {
                        var r2 = r;
                        r2.Inflate(3, 3);
                        using var wing = new Pen(WinPalette.Alpha(ring, 150), 1.5f);
                        g.DrawEllipse(wing, r2);
                    }
                }

                bool ownerView = u.IsIllusion && viewer == u.Owner;
                string stats = u.IsIllusion ? $"{u.Def.Atk}/{u.Def.Hp}" : $"{Engine.Stats.Atk(u)}/{u.Hp}";
                var textBrush = owner.Faction == Faction.Yildiz || owner.Faction == Faction.Ay ? Brushes.White : Brushes.Black;
                g.DrawString(Initials(u.Def.Name) + (ownerView ? "?" : "") + "\n" + stats, small, textBrush, r, center);

                // Durum işaretleri
                int dot = Math.Max(6, Tile / 7);
                if (u.Has(StatusKind.Defending))
                    using (var b = new SolidBrush(Color.FromArgb(90, 160, 255))) g.FillRectangle(b, r.Right - dot, r.Y, dot, dot);
                if (u.Has(StatusKind.Marked)) g.FillEllipse(Brushes.Red, r.X, r.Y, dot, dot);
                if (u.Has(StatusKind.Burning)) g.FillEllipse(Brushes.OrangeRed, r.X, r.Bottom - dot, dot, dot);
                if (u.WeaponCardId != null)
                    using (var b = new SolidBrush(WeaponColor(u.WeaponCardId))) g.FillRectangle(b, r.Right - dot / 2, r.Y + r.Height / 4, dot / 2 + 2, r.Height / 2);
                if (u.ArmorCardId != null) g.FillRectangle(Brushes.LightSlateGray, r.X - 1, r.Y + r.Height / 3, dot / 2 + 2, r.Height / 3);
            }

            // Oyun sonu
            if (s.IsOver)
            {
                using var shade = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
                g.FillRectangle(shade, ClientRectangle);
                string winner = s.WinnerSeat >= 0 ? string.Join(" + ", s.Players.Where(p => p.Team == s.WinnerTeam).Select(p => p.Name)) : "yok";
                using var huge = new Font("Segoe UI", Math.Max(14f, Tile * 0.4f), FontStyle.Bold);
                g.DrawString($"Oyun bitti\nKazanan: {winner}\n{s.Round} raund", huge, Brushes.White, ClientRectangle, center);
            }
        }

        private static Color WeaponColor(string id)
        {
            switch (id)
            {
                case CardCatalog.DahaIyiKilic: return Color.FromArgb(255, 247, 190);
                case CardCatalog.GunesKilici: return Color.FromArgb(255, 128, 26);
                case CardCatalog.HilalHancer: return Color.FromArgb(217, 217, 255);
                case CardCatalog.FirtinaMizragi: return Color.FromArgb(89, 242, 255);
                case CardCatalog.GalaksiAsasi: return Color.FromArgb(166, 102, 255);
                default: return Color.Silver;
            }
        }
    }
}
