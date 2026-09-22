using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>
    /// Tahtadaki alan etkileri (GDD Bölüm 4 zemin etkileri tablosu ve Bölüm 5 veri örnekleri).
    /// Etkinin kime zarar verdiği "Affects" ile büyüyü yapana göre belirlenir.
    /// </summary>
    public static class EffectCatalog
    {
        public const string Yanan = "yanan";
        public const string Kor = "kor";
        public const string Islak = "islak";
        public const string ZehirliSis = "zehirli_sis";
        public const string FenerSisi = "fener_sisi";
        public const string Buhar = "buhar";
        public const string Yagmur = "yagmur";
        public const string Cekim = "cekim";
        public const string Gecit = "gecit";
        public const string Hortum = "hortum";
        public const string SuperHucre = "super_hucre";

        private static readonly Dictionary<string, AreaEffectDef> _all = new Dictionary<string, AreaEffectDef>();

        public static IEnumerable<AreaEffectDef> All => _all.Values;

        public static AreaEffectDef Get(string id) => _all[id];

        private static TriggeredAction Act(Trigger t, ActionKind k, int unit, int structure, Affects a, bool once) =>
            new TriggeredAction { Trigger = t, Kind = k, UnitAmount = unit, StructureAmount = structure, Affects = a, OncePerTurn = once };

        private static void Add(AreaEffectDef d) => _all.Add(d.Id, d);

        static EffectCatalog()
        {
            // Yanan: giren ya da orada tur başlatan birim 2 hasar alır (tur başına 1 kez); yapı 1.
            Add(new AreaEffectDef
            {
                Id = Yanan, Name = "Yanan", Layer = EffectLayer.Zemin, Tags = EffectTag.Ates,
                Shape = ShapeDef.Line(6), Zone = TargetZone.EnemyQuadrant, DurationTurns = 3,
                Actions = new[]
                {
                    Act(Trigger.OnEnter, ActionKind.Damage, 2, 0, Affects.Enemies, true),
                    Act(Trigger.OwnerTurnStart, ActionKind.Damage, 2, 1, Affects.Enemies, true)
                }
            });

            // Kor: Yanan'ın 1 hasarlık sürümü.
            Add(new AreaEffectDef
            {
                Id = Kor, Name = "Kor", Layer = EffectLayer.Zemin, Tags = EffectTag.Ates,
                Shape = ShapeDef.Single(), Zone = TargetZone.Any, DurationTurns = 2,
                Actions = new[]
                {
                    Act(Trigger.OnEnter, ActionKind.Damage, 1, 0, Affects.Enemies, true),
                    Act(Trigger.OwnerTurnStart, ActionKind.Damage, 1, 1, Affects.Enemies, true)
                }
            });

            // Islak: Şimşek bitişik Islak karelere zincirlenir; ateşle karşılaşınca ikisi de kalkar.
            Add(new AreaEffectDef
            {
                Id = Islak, Name = "Islak", Layer = EffectLayer.Zemin, Tags = EffectTag.Su,
                Shape = ShapeDef.Single(), Zone = TargetZone.Any, DurationTurns = 2
            });

            // Zehirli Sis (Ay K2): tur başı 1 hasar, sisteki rakiplerin MNZ'si 1.
            Add(new AreaEffectDef
            {
                Id = ZehirliSis, Name = "Zehirli Sis", Layer = EffectLayer.Hava, Tags = EffectTag.Sis | EffectTag.Zehir,
                Shape = ShapeDef.Square(4), Zone = TargetZone.EnemyQuadrant, DurationTurns = 3,
                Modifiers = new[] { StatModifier.Set(Stat.Mnz, 1, Affects.Enemies) },
                Actions = new[] { Act(Trigger.OwnerTurnStart, ActionKind.Damage, 1, 1, Affects.Enemies, true) }
            });

            // Sis Feneri Floop'u: hasarsız Sis.
            Add(new AreaEffectDef
            {
                Id = FenerSisi, Name = "Sis", Layer = EffectLayer.Hava, Tags = EffectTag.Sis,
                Shape = ShapeDef.Square(3), Zone = TargetZone.Any, DurationTurns = 1,
                Modifiers = new[] { StatModifier.Set(Stat.Mnz, 1, Affects.Enemies) }
            });

            // Buhar: Islak + ateş karşılaşmasından doğan hasarsız Sis. Kimin olduğu belirsiz olduğu için herkesi etkiler.
            Add(new AreaEffectDef
            {
                Id = Buhar, Name = "Buhar", Layer = EffectLayer.Hava, Tags = EffectTag.Sis | EffectTag.Buhar,
                Shape = ShapeDef.Single(), Zone = TargetZone.Any, DurationTurns = 1,
                Modifiers = new[] { StatModifier.Set(Stat.Mnz, 1, Affects.All) }
            });

            // İyileştirici Yağmur (Bulut K1): kendi birim ve yapıları her tur başında +1 CAN.
            Add(new AreaEffectDef
            {
                Id = Yagmur, Name = "İyileştirici Yağmur", Layer = EffectLayer.Hava, Tags = EffectTag.Yagmur,
                Shape = ShapeDef.Quadrant(), Zone = TargetZone.OwnQuadrant, DurationTurns = 3,
                Actions = new[] { Act(Trigger.OwnerTurnStart, ActionKind.Heal, 1, 1, Affects.Allies, true) }
            });

            // Kara Delik çekim alanı: birimlerin HIZ'ı 0 (saldırabilirler).
            Add(new AreaEffectDef
            {
                Id = Cekim, Name = "Çekim", Layer = EffectLayer.Alan, Tags = EffectTag.Cekim,
                Shape = ShapeDef.Square(3), Zone = TargetZone.Any, DurationTurns = 2,
                Modifiers = new[] { StatModifier.Set(Stat.Hiz, 0, Affects.Enemies) }
            });

            // Yıldız Geçidi: giren birim eş geçitten çıkar.
            Add(new AreaEffectDef
            {
                Id = Gecit, Name = "Yıldız Geçidi", Layer = EffectLayer.Alan, Tags = EffectTag.Gecit,
                Shape = ShapeDef.Pair(), Zone = TargetZone.Any, DurationTurns = 3
            });

            // Hortum (Bulut K2): uygulandığında savurur, yapanın her tur başında 2 kare ilerler.
            Add(new AreaEffectDef
            {
                Id = Hortum, Name = "Hortum", Layer = EffectLayer.Alan, Tags = EffectTag.Hortum,
                Shape = ShapeDef.Square(3), Zone = TargetZone.EnemyQuadrant, DurationTurns = 2,
                Actions = new[]
                {
                    Act(Trigger.OnApply, ActionKind.TornadoAdvance, 0, 1, Affects.Enemies, true),
                    Act(Trigger.CasterTurnStart, ActionKind.TornadoAdvance, 2, 1, Affects.Enemies, true)
                }
            });

            // Süper Hücre (Bulut K3): HIZ 1, menzilli saldırı yok, her tur en yüksek CAN'lı rakibe 3 hasar.
            Add(new AreaEffectDef
            {
                Id = SuperHucre, Name = "Süper Hücre", Layer = EffectLayer.Hava, Tags = EffectTag.Firtina,
                Shape = ShapeDef.Quadrant(), Zone = TargetZone.EnemyQuadrant, DurationTurns = 2,
                Modifiers = new[]
                {
                    StatModifier.Set(Stat.Hiz, 1, Affects.Enemies),
                    StatModifier.Set(Stat.Mnz, 1, Affects.Enemies)
                },
                Actions = new[] { Act(Trigger.CasterTurnStart, ActionKind.StrikeHighestHp, 3, 0, Affects.Enemies, false) }
            });
        }
    }
}
