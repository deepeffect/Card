namespace DortGok.Core
{
    /// <summary>Kartın kalıcı (pasif) yeteneği; kural motoru bu kimliklere bakarak kanca çalıştırır.</summary>
    public enum CardPassive
    {
        None,
        HilalHancer,      // Gizliyken yapılan saldırı +2
        GunesKilici,      // Vurduğu birim Yanık olur
        FirtinaMizragi,   // Vurduğu birimi 1 kare iter
        GalaksiAsasi,     // Floop tur başına 2 kez
        KumulusKalkani,   // Bitişik dostlara gelen menzilli hasar -1
        NovaPatlamasi,    // Yok olunca çevresine 2 hasar
        SisFeneri,        // 3×3 alanda koşulsuz Gölge Örtüsü
        DinlenmeKampi,    // Bitişik dostlara +1 EN, +1 CAN
        BulutDuragi,      // Bitişik dostlara +1 EN
        KaragahSurlari,   // Karargaha +1 Zırh
        IleriKarakol,     // Bitişik kareler konuşlanma alanı
        Paratoner,        // Tek hedefli rakip ultileri çeker
        GokKalkani,       // Alan büyüsü hasarını yarıya indirir
        OkcuKulesi,       // Tur sonunda otomatik atış
        Gozlemevi         // Tur başında +3 GE
    }

    public enum FloopKind
    {
        Gizlen,             // Gölge Şövalyesi
        CekIt,              // Ay Fısıldayanı
        Isaretle,           // Gece Baykuşu
        SisOrtusu,          // Sis Feneri
        Hucum,              // Alev Şövalyesi
        OkYagmuru,          // Kor Okçusu
        Feda,               // Kıvılcım Koşucusu
        MancinikAtisi,      // Kızgın Mancınık
        Sifa,               // Yağmur Şamanı
        RuzgarlaSavur,      // Bulut Durağı
        VurKac,             // Rüzgâr Süvarisi
        IsikOku,            // Yıldız Çırağı
        TakimyildizZirhi,   // Takımyıldız Muhafızı
        Gozlem              // Gözlemevi
    }

    public sealed class FloopDef
    {
        public FloopKind Kind;
        public string Name;
        public int Cost;
        public string Text;
    }

    /// <summary>Bir kartın tasarım verisi. Sayılar GDD Bölüm 2 ve 3'teki tablolardan gelir.</summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public Faction Faction;
        public CardType Type;
        public int Cost;

        // Birim ve yapı
        public int Atk;
        public int Hp;
        public int Speed;
        public int Range;
        public bool Flying;
        public StructureRole Role;

        // Geliştirme
        public UpgradeSlot Slot;
        public int AtkBonus;
        public int ArmorBonus;
        public int HpBonus;
        public int RangeBonus;

        public FloopDef Floop;
        public CardPassive Passive;
        public bool IsToken;

        /// <summary>Kartın üzerindeki kural metni.</summary>
        public string Text;

        /// <summary>Geliştirme kartlarının modele yaptığı görsel değişim.</summary>
        public string Visual;

        public bool HasFloop => Floop != null;

        public string StatLine
        {
            get
            {
                switch (Type)
                {
                    case CardType.Birim:
                        return $"SAL {Atk} · CAN {Hp} · HIZ {Speed} · MNZ {Range}" + (Flying ? " · Uçan" : "");
                    case CardType.Yapi:
                        return Range > 0 ? $"CAN {Hp} · MNZ {Range}" : $"CAN {Hp}";
                    default:
                        return "—";
                }
            }
        }
    }
}
