using System;
using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>
    /// GDD'deki 20 fraksiyon çekirdek kartı, 9 nötr kart ve Anka jetonu.
    /// Her istatistik Bölüm 2 (nötr havuz) ve Bölüm 3 (fraksiyon tabloları) ile birebir aynıdır.
    /// </summary>
    public static class CardCatalog
    {
        // Nötr
        public const string Sovalye = "sovalye";
        public const string DahaIyiKilic = "daha_iyi_kilic";
        public const string CelikZirh = "celik_zirh";
        public const string OkcuKulesi = "okcu_kulesi";
        public const string DinlenmeKampi = "dinlenme_kampi";
        public const string KaragahSurlari = "karargah_surlari";
        public const string IleriKarakol = "ileri_karakol";
        public const string Paratoner = "paratoner";
        public const string GokKalkani = "gok_kalkani";
        // Ay
        public const string GolgeSovalyesi = "golge_sovalyesi";
        public const string AyFisildayani = "ay_fisildayani";
        public const string GeceBaykusu = "gece_baykusu";
        public const string SisFeneri = "sis_feneri";
        public const string HilalHancer = "hilal_hancer";
        // Güneş
        public const string AlevSovalyesi = "alev_sovalyesi";
        public const string KorOkcusu = "kor_okcusu";
        public const string KivilcimKosucusu = "kivilcim_kosucusu";
        public const string KizginMancinik = "kizgin_mancinik";
        public const string GunesKilici = "gunes_kilici";
        // Bulut
        public const string RuzgarSuvarisi = "ruzgar_suvarisi";
        public const string YagmurSamani = "yagmur_samani";
        public const string KumulusDevi = "kumulus_devi";
        public const string BulutDuragi = "bulut_duragi";
        public const string FirtinaMizragi = "firtina_mizragi";
        // Yıldız
        public const string YildizCiragi = "yildiz_ciragi";
        public const string TakimyildizMuhafizi = "takimyildiz_muhafizi";
        public const string NovaGolemi = "nova_golemi";
        public const string Gozlemevi = "gozlemevi";
        public const string GalaksiAsasi = "galaksi_asasi";
        // Jeton
        public const string AnkaKusu = "anka_kusu";

        private static readonly Dictionary<string, CardDef> _all = new Dictionary<string, CardDef>();
        private static readonly List<CardDef> _ordered = new List<CardDef>();

        public static IReadOnlyList<CardDef> All => _ordered;

        public static CardDef Get(string id)
        {
            if (id != null && _all.TryGetValue(id, out var def)) return def;
            throw new KeyNotFoundException("Bilinmeyen kart: " + id);
        }

        public static bool TryGet(string id, out CardDef def) => _all.TryGetValue(id ?? "", out def);

        /// <summary>Fraksiyonun 5 çekirdek kartı, GDD tablolarındaki sırayla.</summary>
        public static string[] CoreCards(Faction f)
        {
            switch (f)
            {
                case Faction.Ay: return new[] { GolgeSovalyesi, AyFisildayani, GeceBaykusu, SisFeneri, HilalHancer };
                case Faction.Gunes: return new[] { AlevSovalyesi, KorOkcusu, KivilcimKosucusu, KizginMancinik, GunesKilici };
                case Faction.Bulut: return new[] { RuzgarSuvarisi, YagmurSamani, KumulusDevi, BulutDuragi, FirtinaMizragi };
                case Faction.Yildiz: return new[] { YildizCiragi, TakimyildizMuhafizi, NovaGolemi, Gozlemevi, GalaksiAsasi };
                default: return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Destedeki 15 nötr kart. GDD 9 nötr kart ve 15 nötr yuva tanımlıyor ama dağılımı vermiyor;
        /// bu dağılım (3+2+2+2+2+1+1+1+1) bir tasarım boşluğunu doldurur.
        /// </summary>
        public static readonly (string id, int copies)[] NeutralDeckSlots =
        {
            (Sovalye, 3), (DahaIyiKilic, 2), (CelikZirh, 2), (OkcuKulesi, 2), (DinlenmeKampi, 2),
            (KaragahSurlari, 1), (IleriKarakol, 1), (Paratoner, 1), (GokKalkani, 1)
        };

        /// <summary>Başlangıç destesi: 5 çekirdek kartın 3'er kopyası + 15 nötr kart = 30.</summary>
        public static List<string> BuildStarterDeck(Faction f, int copiesPerCore)
        {
            var deck = new List<string>(30);
            foreach (var id in CoreCards(f))
                for (int i = 0; i < copiesPerCore; i++) deck.Add(id);
            foreach (var (id, copies) in NeutralDeckSlots)
                for (int i = 0; i < copies; i++) deck.Add(id);
            return deck;
        }

        private static void Add(CardDef d)
        {
            _all.Add(d.Id, d);
            _ordered.Add(d);
        }

        private static FloopDef F(FloopKind kind, string name, int cost, string text) =>
            new FloopDef { Kind = kind, Name = name, Cost = cost, Text = text };

        static CardCatalog()
        {
            // ---------- Nötr kart havuzu (Bölüm 2) ----------
            Add(new CardDef { Id = Sovalye, Name = "Şövalye", Faction = Faction.Notr, Type = CardType.Birim, Cost = 2,
                Atk = 2, Hp = 4, Speed = 2, Range = 1, Text = "" });
            Add(new CardDef { Id = DahaIyiKilic, Name = "Daha İyi Kılıç", Faction = Faction.Notr, Type = CardType.Gelistirme, Cost = 1,
                Slot = UpgradeSlot.Silah, AtkBonus = 1, Text = "+1 SAL.",
                Visual = "Modelin kılıcı daha büyük, parlayan bir kılıçla değişir." });
            Add(new CardDef { Id = CelikZirh, Name = "Çelik Zırh", Faction = Faction.Notr, Type = CardType.Gelistirme, Cost = 2,
                Slot = UpgradeSlot.Zirh, ArmorBonus = 1, HpBonus = 1, Text = "+1 Zırh, +1 CAN.",
                Visual = "Modele zırh plakaları eklenir." });
            Add(new CardDef { Id = OkcuKulesi, Name = "Okçu Kulesi", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 3,
                Hp = 6, Range = 3, Role = StructureRole.Savunma, Passive = CardPassive.OkcuKulesi,
                Text = "Tur sonunda menzildeki en düşük CAN'lı rakibe 2 hasar." });
            Add(new CardDef { Id = DinlenmeKampi, Name = "Dinlenme Kampı", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 2,
                Hp = 4, Role = StructureRole.Dinlenme, Passive = CardPassive.DinlenmeKampi,
                Text = "Bitişik dost birimler tur başında +1 EN ve +1 CAN alır." });
            Add(new CardDef { Id = KaragahSurlari, Name = "Karargah Surları", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 3,
                Hp = 5, Role = StructureRole.Guclendirme, Passive = CardPassive.KaragahSurlari,
                Text = "Ayakta kaldıkça karargah +1 Zırh kazanır." });
            Add(new CardDef { Id = IleriKarakol, Name = "İleri Karakol", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 2,
                Hp = 3, Role = StructureRole.Karakol, Passive = CardPassive.IleriKarakol,
                Text = "Bitişik kareler konuşlanma alanı sayılır." });
            Add(new CardDef { Id = Paratoner, Name = "Paratoner", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 2,
                Hp = 4, Role = StructureRole.Savunma, Passive = CardPassive.Paratoner,
                Text = "3 kare içindeki tek hedefli rakip ultiler bu yapıya yönlenir." });
            Add(new CardDef { Id = GokKalkani, Name = "Gök Kalkanı", Faction = Faction.Notr, Type = CardType.Yapi, Cost = 4,
                Hp = 5, Role = StructureRole.Savunma, Passive = CardPassive.GokKalkani,
                Text = "Çevresindeki 3×3 alana düşen alan büyülerinin hasarını yarıya indirir (aşağı yuvarlanır)." });

            // ---------- Ay Takımı ----------
            Add(new CardDef { Id = GolgeSovalyesi, Name = "Gölge Şövalyesi", Faction = Faction.Ay, Type = CardType.Birim, Cost = 2,
                Atk = 2, Hp = 3, Speed = 2, Range = 1,
                Floop = F(FloopKind.Gizlen, "Gizlen", 1, "Gizli olur; saldırıncaya ya da hasar alıncaya kadar hedef alınamaz.") });
            Add(new CardDef { Id = AyFisildayani, Name = "Ay Fısıldayanı", Faction = Faction.Ay, Type = CardType.Birim, Cost = 3,
                Atk = 1, Hp = 4, Speed = 2, Range = 3,
                Floop = F(FloopKind.CekIt, "Çek / İt", 2, "3 kare içindeki bir rakip birimi 2 kareye kadar çek ya da it.") });
            Add(new CardDef { Id = GeceBaykusu, Name = "Gece Baykuşu", Faction = Faction.Ay, Type = CardType.Birim, Cost = 1,
                Atk = 1, Hp = 1, Speed = 3, Range = 1, Flying = true,
                Floop = F(FloopKind.Isaretle, "İşaretle", 1, "4 kare içindeki bir rakibi İşaretler; ona yapılan sonraki saldırı +2 hasar verir ve Zırh'ı yok sayar.") });
            Add(new CardDef { Id = SisFeneri, Name = "Sis Feneri", Faction = Faction.Ay, Type = CardType.Yapi, Cost = 2,
                Hp = 5, Role = StructureRole.Diger, Passive = CardPassive.SisFeneri,
                Text = "Çevresindeki 3×3 alanda dost birimler Gölge Örtüsü'nden koşulsuz yararlanır.",
                Floop = F(FloopKind.SisOrtusu, "Sis Örtüsü", 1, "Bu alanı 1 tur hasarsız Sis'le kaplar.") });
            Add(new CardDef { Id = HilalHancer, Name = "Hilal Hançer", Faction = Faction.Ay, Type = CardType.Gelistirme, Cost = 1,
                Slot = UpgradeSlot.Silah, AtkBonus = 1, Passive = CardPassive.HilalHancer,
                Text = "+1 SAL; Gizliyken yapılan saldırı +2 hasar.",
                Visual = "Modelin kılıcı ay ışığıyla parlayan bir hilal hançere dönüşür." });

            // ---------- Güneş Takımı ----------
            Add(new CardDef { Id = AlevSovalyesi, Name = "Alev Şövalyesi", Faction = Faction.Gunes, Type = CardType.Birim, Cost = 2,
                Atk = 3, Hp = 2, Speed = 2, Range = 1,
                Floop = F(FloopKind.Hucum, "Hücum", 1, "Bu tur +1 HIZ; saldırısı hedefin arkasındaki kareye de 1 hasar verir.") });
            Add(new CardDef { Id = KorOkcusu, Name = "Kor Okçusu", Faction = Faction.Gunes, Type = CardType.Birim, Cost = 2,
                Atk = 2, Hp = 2, Speed = 1, Range = 3,
                Floop = F(FloopKind.OkYagmuru, "Ok Yağmuru", 1, "Hedefe bitişik ikinci bir rakibe de 1 hasar.") });
            Add(new CardDef { Id = KivilcimKosucusu, Name = "Kıvılcım Koşucusu", Faction = Faction.Gunes, Type = CardType.Birim, Cost = 1,
                Atk = 1, Hp = 1, Speed = 3, Range = 1,
                Floop = F(FloopKind.Feda, "Feda", 1, "Kendini feda eder; çevresindeki 8 kareye 2 hasar verir ve Kor bırakır.") });
            Add(new CardDef { Id = KizginMancinik, Name = "Kızgın Mancınık", Faction = Faction.Gunes, Type = CardType.Yapi, Cost = 3,
                Hp = 5, Range = 4, Role = StructureRole.Savunma,
                Floop = F(FloopKind.MancinikAtisi, "Mancınık Atışı", 2, "Menzildeki bir kareye 2 hasar; kare 1 tur Yanan olur.") });
            Add(new CardDef { Id = GunesKilici, Name = "Güneş Kılıcı", Faction = Faction.Gunes, Type = CardType.Gelistirme, Cost = 2,
                Slot = UpgradeSlot.Silah, AtkBonus = 2, Passive = CardPassive.GunesKilici,
                Text = "+2 SAL; vurduğu birim Yanık olur (kendi sonraki tur başında 1 hasar).",
                Visual = "Kılıç alev alan bir güneş kılıcına dönüşür." });

            // ---------- Bulut Takımı ----------
            Add(new CardDef { Id = RuzgarSuvarisi, Name = "Rüzgâr Süvarisi", Faction = Faction.Bulut, Type = CardType.Birim, Cost = 2,
                Atk = 2, Hp = 2, Speed = 3, Range = 1, Flying = true,
                Floop = F(FloopKind.VurKac, "Vur-Kaç", 1, "Bu tur saldırdıktan sonra EN harcamadan 2 kare geri çekilir.") });
            Add(new CardDef { Id = YagmurSamani, Name = "Yağmur Şamanı", Faction = Faction.Bulut, Type = CardType.Birim, Cost = 3,
                Atk = 1, Hp = 4, Speed = 2, Range = 3,
                Floop = F(FloopKind.Sifa, "Şifa", 1, "3 kare içindeki bir dost birim 2 CAN iyileşir; karesi 2 tur Islak olur.") });
            Add(new CardDef { Id = KumulusDevi, Name = "Kümülüs Devi", Faction = Faction.Bulut, Type = CardType.Birim, Cost = 4,
                Atk = 3, Hp = 8, Speed = 1, Range = 1, Passive = CardPassive.KumulusKalkani,
                Text = "Bitişik dost birimlere gelen menzilli hasar −1." });
            Add(new CardDef { Id = BulutDuragi, Name = "Bulut Durağı", Faction = Faction.Bulut, Type = CardType.Yapi, Cost = 2,
                Hp = 4, Role = StructureRole.Dinlenme, Passive = CardPassive.BulutDuragi,
                Text = "Bitişik dost birimler tur başında +1 EN alır.",
                Floop = F(FloopKind.RuzgarlaSavur, "Rüzgârla Savur", 1, "Bitişik bir dost birimi 4 kare içindeki boş kareye savurur.") });
            Add(new CardDef { Id = FirtinaMizragi, Name = "Fırtına Mızrağı", Faction = Faction.Bulut, Type = CardType.Gelistirme, Cost = 2,
                Slot = UpgradeSlot.Silah, AtkBonus = 1, RangeBonus = 1, Passive = CardPassive.FirtinaMizragi,
                Text = "+1 SAL, +1 MNZ; vurduğu birimi 1 kare geri iter, engele çarparsa 1 hasar.",
                Visual = "Mızrak ucunda şimşek çıtırdar." });

            // ---------- Yıldız Takımı ----------
            Add(new CardDef { Id = YildizCiragi, Name = "Yıldız Çırağı", Faction = Faction.Yildiz, Type = CardType.Birim, Cost = 2,
                Atk = 1, Hp = 3, Speed = 2, Range = 3,
                Floop = F(FloopKind.IsikOku, "Işık Oku", 1, "Menzildeki bir hedefe 2 hasar.") });
            Add(new CardDef { Id = TakimyildizMuhafizi, Name = "Takımyıldız Muhafızı", Faction = Faction.Yildiz, Type = CardType.Birim, Cost = 3,
                Atk = 2, Hp = 5, Speed = 2, Range = 1,
                Floop = F(FloopKind.TakimyildizZirhi, "Takımyıldız Zırhı", 1, "Bitişik dost birimler sonraki kendi turuna kadar +1 Zırh.") });
            Add(new CardDef { Id = NovaGolemi, Name = "Nova Golemi", Faction = Faction.Yildiz, Type = CardType.Birim, Cost = 4,
                Atk = 4, Hp = 6, Speed = 1, Range = 1, Passive = CardPassive.NovaPatlamasi,
                Text = "Yok olduğunda çevresindeki 8 kareye 2 hasar verir (dost ve düşman)." });
            Add(new CardDef { Id = Gozlemevi, Name = "Gözlemevi", Faction = Faction.Yildiz, Type = CardType.Yapi, Cost = 3,
                Hp = 5, Role = StructureRole.Diger, Passive = CardPassive.Gozlemevi,
                Text = "Tur başında +3 GE.",
                Floop = F(FloopKind.Gozlem, "Gözlem", 1, "Destenin üstündeki 3 karta bak, birini ele al, diğerlerini alta koy.") });
            Add(new CardDef { Id = GalaksiAsasi, Name = "Galaksi Asası", Faction = Faction.Yildiz, Type = CardType.Gelistirme, Cost = 2,
                Slot = UpgradeSlot.Silah, AtkBonus = 1, RangeBonus = 1, Passive = CardPassive.GalaksiAsasi,
                Text = "+1 SAL, +1 MNZ; taşıyan birimin Floop'u tur başına 2 kez kullanılabilir.",
                Visual = "Asanın ucunda minik bir galaksi döner." });

            // ---------- Jeton: Anka Yükselişi (Güneş K3) ----------
            Add(new CardDef { Id = AnkaKusu, Name = "Anka Kuşu", Faction = Faction.Gunes, Type = CardType.Birim, Cost = 0,
                Atk = 4, Hp = 6, Speed = 3, Range = 1, Flying = true, IsToken = true,
                Text = "Her kendi tur sonunda çevresindeki 8 kareye 1 hasar. İlk ölümünde aynı karede 3 CAN ile yeniden doğar." });
        }
    }
}
