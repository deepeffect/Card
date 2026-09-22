using System.Collections.Generic;

namespace DortGok.Core
{
    public enum UltiTargeting
    {
        Self,               // Şafak Vakti
        OwnQuadrant,        // İyileştirici Yağmur
        EnemyPlayer,        // Gece Yarısı
        EnemyQuadrant,      // Süper Hücre
        EnemyQuadrantUnits, // Tutulma
        OwnUnit,            // İllüzyon
        OwnUnitToTile,      // Kayan Yıldız
        AreaAny,            // Güneş Patlaması, Meteor Yağmuru (sol üst köşe)
        AreaEnemyQuadrant,  // Zehirli Sis, Hortum (sol üst köşe)
        TilesEnemyQuadrant, // Kavurucu Ateş (çizgi ya da 2×3)
        CenterAny,          // Kara Delik
        EnemyEntity,        // Şimşek Çarpması
        TwoTiles,           // Yıldız Geçidi
        OwnDeployTile       // Anka Yükselişi
    }

    /// <summary>Fraksiyon büyüsü (ulti) tanımı; GDD Bölüm 3 tabloları.</summary>
    public sealed class UltiDef
    {
        public string Id;
        public string Name;
        public Faction Faction;
        public UltiTier Tier;
        public UltiTargeting Targeting;
        public int Size;          // alan kenarı (3, 4, 5) ya da en uzun çizgi (6)
        public bool IsArea;       // Gök Kalkanı'nın yarıya indirdiği "alan büyüsü" mü
        public bool TargetsPlayer; // Hedef korumasına (K2/K3) konu olabilir mi
        public string Duration;
        public string Text;
        public bool IsNew;        // GDD'de "(yeni)" olarak eklenenler
    }

    public static class UltiCatalog
    {
        public const string GeceYarisi = "gece_yarisi";
        public const string Illuzyon = "illuzyon";
        public const string ZehirliSis = "zehirli_sis";
        public const string Tutulma = "tutulma";
        public const string SafakVakti = "safak_vakti";
        public const string GunesPatlamasi = "gunes_patlamasi";
        public const string KavurucuAtes = "kavurucu_ates";
        public const string AnkaYukselisi = "anka_yukselisi";
        public const string SimsekCarpmasi = "simsek_carpmasi";
        public const string IyilestiriciYagmur = "iyilestirici_yagmur";
        public const string Hortum = "hortum";
        public const string SuperHucre = "super_hucre";
        public const string KayanYildiz = "kayan_yildiz";
        public const string YildizGecidi = "yildiz_gecidi";
        public const string KaraDelik = "kara_delik";
        public const string MeteorYagmuru = "meteor_yagmuru";

        private static readonly Dictionary<string, UltiDef> _all = new Dictionary<string, UltiDef>();
        private static readonly Dictionary<Faction, List<UltiDef>> _byFaction = new Dictionary<Faction, List<UltiDef>>();

        public static UltiDef Get(string id) => _all[id];
        public static bool TryGet(string id, out UltiDef def) => _all.TryGetValue(id ?? "", out def);
        public static IEnumerable<UltiDef> All => _all.Values;

        public static IReadOnlyList<UltiDef> ForFaction(Faction f) =>
            _byFaction.TryGetValue(f, out var list) ? list : new List<UltiDef>();

        private static void Add(UltiDef d)
        {
            _all.Add(d.Id, d);
            if (!_byFaction.TryGetValue(d.Faction, out var list))
                _byFaction[d.Faction] = list = new List<UltiDef>();
            list.Add(d);
        }

        static UltiCatalog()
        {
            // ---------- Ay ----------
            Add(new UltiDef { Id = GeceYarisi, Name = "Gece Yarısı", Faction = Faction.Ay, Tier = UltiTier.K1,
                Targeting = UltiTargeting.EnemyPlayer, Duration = "2 tur",
                Text = "Tüm birim ve kulelerinin MNZ'si −1, HIZ'ı −1 (en az 1)." });
            Add(new UltiDef { Id = Illuzyon, Name = "İllüzyon", Faction = Faction.Ay, Tier = UltiTier.K1,
                Targeting = UltiTargeting.OwnUnit, Duration = "3 tur ya da vurulana kadar",
                Text = "Birime bitişik 2 boş kareye kopyası çıkar. Rakibe gerçek görünür; 1 CAN, 0 SAL, hareket edebilir. Kopyaya saldıran birimin EN'si 0 olur." });
            Add(new UltiDef { Id = ZehirliSis, Name = "Zehirli Sis", Faction = Faction.Ay, Tier = UltiTier.K2,
                Targeting = UltiTargeting.AreaEnemyQuadrant, Size = 4, IsArea = true, TargetsPlayer = true, Duration = "3 tur",
                Text = "Sis içinde tur başlatan rakip birim ve yapılar 1 hasar alır. Sisteki rakiplerin MNZ'si 1'e düşer. Ay birimleri etkilenmez." });
            Add(new UltiDef { Id = Tutulma, Name = "Tutulma", Faction = Faction.Ay, Tier = UltiTier.K3, IsNew = true,
                Targeting = UltiTargeting.EnemyQuadrantUnits, TargetsPlayer = true, Duration = "1 tur",
                Text = "O çeyrekteki en fazla 2 rakip birimi (CAN ≤ 5) bir tur yönet: hareket ettir, saldırt. Karargahlara saldıramaz, Floop kullanamazlar. Tur sonunda 2 hasar alıp sahibine dönerler." });

            // ---------- Güneş ----------
            Add(new UltiDef { Id = SafakVakti, Name = "Şafak Vakti", Faction = Faction.Gunes, Tier = UltiTier.K1,
                Targeting = UltiTargeting.Self, Duration = "2 tur",
                Text = "Kendi tüm birimleri +1 SAL, +1 HIZ." });
            Add(new UltiDef { Id = GunesPatlamasi, Name = "Güneş Patlaması", Faction = Faction.Gunes, Tier = UltiTier.K1,
                Targeting = UltiTargeting.AreaAny, Size = 3, IsArea = true, Duration = "1 tur",
                Text = "İçindeki rakip birimler Kör olur: sonraki turlarında saldırıları 0 hasar verir. Alandaki Sis dağılır." });
            Add(new UltiDef { Id = KavurucuAtes, Name = "Kavurucu Ateş", Faction = Faction.Gunes, Tier = UltiTier.K2,
                Targeting = UltiTargeting.TilesEnemyQuadrant, Size = 6, IsArea = true, TargetsPlayer = true, Duration = "3 tur",
                Text = "En fazla 6 karelik düz çizgi ya da 2×3 alan Yanan olur: giren ya da orada tur başlatan birim 2 hasar alır (tur başına 1 kez); yapılar 1 hasar." });
            Add(new UltiDef { Id = AnkaYukselisi, Name = "Anka Yükselişi", Faction = Faction.Gunes, Tier = UltiTier.K3, IsNew = true,
                Targeting = UltiTargeting.OwnDeployTile, Duration = "Kalıcı",
                Text = "Anka Kuşu çağırır: SAL 4 · CAN 6 · HIZ 3 · Uçan. Her kendi tur sonunda çevresindeki 8 kareye 1 hasar. İlk ölümünde aynı karede 3 CAN ile yeniden doğar." });

            // ---------- Bulut ----------
            Add(new UltiDef { Id = SimsekCarpmasi, Name = "Şimşek Çarpması", Faction = Faction.Bulut, Tier = UltiTier.K1,
                Targeting = UltiTargeting.EnemyEntity, Duration = "Anında",
                Text = "4 hasar; karargaha 2. Hedef Islak karedeyse, bitişik Islak karelerdeki rakiplere 2 zincir hasar." });
            Add(new UltiDef { Id = IyilestiriciYagmur, Name = "İyileştirici Yağmur", Faction = Faction.Bulut, Tier = UltiTier.K1,
                Targeting = UltiTargeting.OwnQuadrant, Duration = "3 tur",
                Text = "Kendi birim ve yapıları her tur başında +1 CAN. Çeyrek Islak olur: Yanan ve Kor söner, Şimşek zincirlenir." });
            Add(new UltiDef { Id = Hortum, Name = "Hortum", Faction = Faction.Bulut, Tier = UltiTier.K2,
                Targeting = UltiTargeting.AreaEnemyQuadrant, Size = 3, IsArea = true, TargetsPlayer = true, Duration = "2 tur",
                Text = "İçindeki birimler 3 kareye kadar uzaktaki rastgele boş karelere savrulur ve 1 hasar alır; yapılar yalnızca 1 hasar alır. Hortum her kendi tur başında seçtiğin yönde 2 kare ilerler." });
            Add(new UltiDef { Id = SuperHucre, Name = "Süper Hücre", Faction = Faction.Bulut, Tier = UltiTier.K3, IsNew = true,
                Targeting = UltiTargeting.EnemyQuadrant, IsArea = true, TargetsPlayer = true, Duration = "2 tur",
                Text = "Çeyrekteki rakip birimlerin HIZ'ı 1'e düşer, menzilli saldırı yapamazlar. Her kendi tur başında çeyrekteki en yüksek CAN'lı rakip birime 3 hasar." });

            // ---------- Yıldız ----------
            Add(new UltiDef { Id = KayanYildiz, Name = "Kayan Yıldız", Faction = Faction.Yildiz, Tier = UltiTier.K1,
                Targeting = UltiTargeting.OwnUnitToTile, Duration = "Anında",
                Text = "Herhangi bir boş kareye ışınlanır (rakip karargaha 2 kareden yakın olamaz). İndiği yerin çevresindeki rakiplere 1 hasar; birim +1 EN kazanır." });
            Add(new UltiDef { Id = YildizGecidi, Name = "Yıldız Geçidi", Faction = Faction.Yildiz, Tier = UltiTier.K1, IsNew = true,
                Targeting = UltiTargeting.TwoTiles, Duration = "3 tur",
                Text = "Birbirine bağlı iki geçit açar. Geçide giren her birim (dost ya da düşman) ötekinden çıkar ve hareketine devam eder." });
            Add(new UltiDef { Id = KaraDelik, Name = "Kara Delik", Faction = Faction.Yildiz, Tier = UltiTier.K2,
                Targeting = UltiTargeting.CenterAny, Size = 5, IsArea = true, TargetsPlayer = true, Duration = "2 tur",
                Text = "5×5 alandaki birimler merkeze doğru olabildiğince çekilir; birbirine çarpanlar 1 hasar alır. Merkezin 3×3 çevresinde birimlerin HIZ'ı 0 olur (saldırabilirler)." });
            Add(new UltiDef { Id = MeteorYagmuru, Name = "Meteor Yağmuru", Faction = Faction.Yildiz, Tier = UltiTier.K3,
                Targeting = UltiTargeting.AreaAny, Size = 4, IsArea = true, TargetsPlayer = true, Duration = "Gecikmeli",
                Text = "Kullanılınca 4×4 alan kırmızı işaretlenir, sonraki kendi tur başında düşer. Birimlere 3, yapılara 2 hasar; karargaha en fazla 3." });
        }
    }
}
