using DortGok.Core;
using UnityEngine;

namespace DortGok.Presentation
{
    /// <summary>
    /// Fraksiyon renkleri (GDD Bölüm 1): Ay mor-gümüş, Güneş turuncu-altın, Bulut beyaz-açık mavi,
    /// Yıldız lacivert-camgöbeği. Zemin etkisi renkleri tahta dokusuna karıştırılır.
    /// </summary>
    public static class Palette
    {
        public static Color Faction(Faction f)
        {
            switch (f)
            {
                case Core.Faction.Ay: return new Color(0.60f, 0.45f, 0.85f);
                case Core.Faction.Gunes: return new Color(0.96f, 0.62f, 0.18f);
                case Core.Faction.Bulut: return new Color(0.62f, 0.84f, 0.97f);
                case Core.Faction.Yildiz: return new Color(0.22f, 0.36f, 0.78f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        /// <summary>İkincil renk: Ay gümüş, Güneş altın, Bulut beyaz, Yıldız camgöbeği.</summary>
        public static Color FactionAccent(Faction f)
        {
            switch (f)
            {
                case Core.Faction.Ay: return new Color(0.82f, 0.84f, 0.90f);
                case Core.Faction.Gunes: return new Color(1.00f, 0.84f, 0.35f);
                case Core.Faction.Bulut: return new Color(0.97f, 0.98f, 1.00f);
                case Core.Faction.Yildiz: return new Color(0.30f, 0.86f, 0.92f);
                default: return Color.white;
            }
        }

        public static Color Dark(Color c, float k = 0.55f) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static readonly Color Table = new Color(0.07f, 0.08f, 0.11f);
        public static readonly Color Altar = new Color(0.98f, 0.82f, 0.36f);
        public static readonly Color Ruin = new Color(0.18f, 0.17f, 0.17f);
        public static readonly Color GridLine = new Color(0.02f, 0.04f, 0.08f);
        public static readonly Color Reach = new Color(0.35f, 0.95f, 0.55f);
        public static readonly Color Target = new Color(1.00f, 0.30f, 0.30f);
        public static readonly Color Deploy = new Color(0.40f, 0.90f, 1.00f);
        public static readonly Color PreviewOk = new Color(1.00f, 0.95f, 0.30f);
        public static readonly Color PreviewBad = new Color(0.95f, 0.15f, 0.15f);
        public static readonly Color Meteor = new Color(1.00f, 0.10f, 0.05f);
        public static readonly Color Selected = Color.white;

        public static Color Effect(string effectId)
        {
            switch (effectId)
            {
                case EffectCatalog.Yanan: return new Color(1.00f, 0.35f, 0.05f);
                case EffectCatalog.Kor: return new Color(0.80f, 0.30f, 0.10f);
                case EffectCatalog.Islak: return new Color(0.20f, 0.50f, 1.00f);
                case EffectCatalog.ZehirliSis: return new Color(0.55f, 0.80f, 0.35f);
                case EffectCatalog.FenerSisi: return new Color(0.80f, 0.80f, 0.90f);
                case EffectCatalog.Buhar: return new Color(0.92f, 0.92f, 0.92f);
                case EffectCatalog.Yagmur: return new Color(0.50f, 0.75f, 1.00f);
                case EffectCatalog.Cekim: return new Color(0.25f, 0.05f, 0.35f);
                case EffectCatalog.Gecit: return new Color(0.20f, 1.00f, 0.95f);
                case EffectCatalog.Hortum: return new Color(0.85f, 0.95f, 1.00f);
                case EffectCatalog.SuperHucre: return new Color(0.15f, 0.20f, 0.45f);
                default: return Color.gray;
            }
        }

        /// <summary>Etkinin tahta rengine karışma oranı.</summary>
        public static float EffectWeight(string effectId)
        {
            switch (effectId)
            {
                case EffectCatalog.Yagmur: return 0.25f;
                case EffectCatalog.SuperHucre: return 0.35f;
                case EffectCatalog.Islak: return 0.40f;
                default: return 0.55f;
            }
        }
    }

    /// <summary>Kare ↔ dünya dönüşümü: satır 0 kuzeyde (+z), sütun 0 batıda (−x); her kare 1 birim.</summary>
    public static class BoardSpace
    {
        public static Vector3 ToWorld(Coord c, float y = 0f) => new Vector3(c.Col - 5.5f, y, 5.5f - c.Row);

        public static Coord FromWorld(Vector3 p) => new Coord(Mathf.FloorToInt(6f - p.z), Mathf.FloorToInt(p.x + 6f));

        public static Vector3 CenterOf(Coord[] tiles, float y = 0f)
        {
            var sum = Vector3.zero;
            foreach (var t in tiles) sum += ToWorld(t, y);
            return sum / tiles.Length;
        }
    }
}
