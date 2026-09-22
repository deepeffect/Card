using System.Drawing;
using DortGok.Core;

namespace DortGok.Playtest
{
    /// <summary>Unity'deki Palette ile aynı renkler (GDD Bölüm 1 fraksiyon renkleri).</summary>
    internal static class WinPalette
    {
        public static readonly Color Table = Color.FromArgb(18, 20, 28);
        public static readonly Color Altar = Color.FromArgb(250, 209, 92);
        public static readonly Color Ruin = Color.FromArgb(46, 43, 43);
        public static readonly Color Grid = Color.FromArgb(5, 10, 20);
        public static readonly Color Reach = Color.FromArgb(89, 242, 140);
        public static readonly Color Target = Color.FromArgb(255, 77, 77);
        public static readonly Color Deploy = Color.FromArgb(102, 230, 255);
        public static readonly Color PreviewOk = Color.FromArgb(255, 242, 77);
        public static readonly Color PreviewBad = Color.FromArgb(242, 38, 38);
        public static readonly Color Meteor = Color.FromArgb(255, 26, 13);

        public static Color Faction(Faction f)
        {
            switch (f)
            {
                case Core.Faction.Ay: return Color.FromArgb(153, 115, 217);
                case Core.Faction.Gunes: return Color.FromArgb(245, 158, 46);
                case Core.Faction.Bulut: return Color.FromArgb(158, 214, 247);
                case Core.Faction.Yildiz: return Color.FromArgb(56, 92, 199);
                default: return Color.FromArgb(178, 178, 178);
            }
        }

        public static Color Accent(Faction f)
        {
            switch (f)
            {
                case Core.Faction.Ay: return Color.FromArgb(209, 214, 230);
                case Core.Faction.Gunes: return Color.FromArgb(255, 214, 89);
                case Core.Faction.Bulut: return Color.FromArgb(247, 250, 255);
                case Core.Faction.Yildiz: return Color.FromArgb(77, 219, 235);
                default: return Color.White;
            }
        }

        public static Color Effect(string id)
        {
            switch (id)
            {
                case EffectCatalog.Yanan: return Color.FromArgb(255, 89, 13);
                case EffectCatalog.Kor: return Color.FromArgb(204, 77, 26);
                case EffectCatalog.Islak: return Color.FromArgb(51, 128, 255);
                case EffectCatalog.ZehirliSis: return Color.FromArgb(140, 204, 89);
                case EffectCatalog.FenerSisi: return Color.FromArgb(204, 204, 230);
                case EffectCatalog.Buhar: return Color.FromArgb(235, 235, 235);
                case EffectCatalog.Yagmur: return Color.FromArgb(128, 191, 255);
                case EffectCatalog.Cekim: return Color.FromArgb(120, 40, 170);
                case EffectCatalog.Gecit: return Color.FromArgb(51, 255, 242);
                case EffectCatalog.Hortum: return Color.FromArgb(217, 242, 255);
                case EffectCatalog.SuperHucre: return Color.FromArgb(60, 80, 160);
                default: return Color.Gray;
            }
        }

        public static float EffectWeight(string id)
        {
            switch (id)
            {
                case EffectCatalog.Yagmur: return 0.25f;
                case EffectCatalog.SuperHucre: return 0.35f;
                case EffectCatalog.Islak: return 0.40f;
                default: return 0.55f;
            }
        }

        public static Color Highlight(Highlight h)
        {
            switch (h)
            {
                case Core.Highlight.Reach: return Reach;
                case Core.Highlight.ReachDanger: return Lerp(Reach, Target, 0.45f);
                case Core.Highlight.Target: return Target;
                case Core.Highlight.Deploy: return Deploy;
                case Core.Highlight.Ally: return Reach;
                case Core.Highlight.PreviewOk: return PreviewOk;
                case Core.Highlight.PreviewBad: return PreviewBad;
                default: return Color.White;
            }
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Dark(Color c, float k) => Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));

        public static Color Alpha(Color c, int a) => Color.FromArgb(a, c.R, c.G, c.B);
    }
}
