using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>Tahtadaki bir alan etkisinin çalışma zamanı örneği (GDD Bölüm 5).</summary>
    public sealed class AreaEffectInstance
    {
        public int Id;
        public AreaEffectDef Def;
        public int Caster;
        public HashSet<Coord> Tiles = new HashSet<Coord>();   // hareketli etkilerde güncellenir
        public int RemainingTurns;
        public int CastId;
        public bool FromUlti;
        public Coord Direction = Coord.None;                   // Hortum
        /// <summary>"Tur başına 1 kez" kuralı: varlık kimliği → son etkilendiği tur sırası.</summary>
        public readonly Dictionary<int, int> LastHitTurn = new Dictionary<int, int>();

        public bool Has(EffectTag tag) => Def.Has(tag);
    }

    /// <summary>Oyuncu çapında değiştirici (Gece Yarısı, Şafak Vakti).</summary>
    public sealed class PlayerEffect
    {
        public int Id;
        public string UltiId;
        public int Caster;
        public int TargetPlayer;
        public int RemainingTurns;
        public StatModifier[] Modifiers;
    }

    /// <summary>Gecikmeli büyü (Meteor): yapanın sonraki tur başında çözülür.</summary>
    public sealed class DelayedSpell
    {
        public int Id;
        public string UltiId;
        public int Caster;
        public int CastId;
        public List<Coord> Tiles;
    }
}
