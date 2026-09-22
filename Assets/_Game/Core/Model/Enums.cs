using System;

namespace DortGok.Core
{
    /// <summary>Dört fraksiyon (GDD Bölüm 3). Nötr kartlar herkesin destesine girer.</summary>
    public enum Faction { Ay = 0, Gunes = 1, Bulut = 2, Yildiz = 3, Notr = 4 }

    public enum CardType { Birim, Yapi, Gelistirme }

    /// <summary>Yapı alt türleri (GDD Bölüm 2).</summary>
    public enum StructureRole { Savunma, Dinlenme, Guclendirme, Karakol, Diger }

    /// <summary>Geliştirme yuvaları: 1 Silah + 1 Zırh.</summary>
    public enum UpgradeSlot { Yok, Silah, Zirh }

    public enum UltiTier { K1 = 0, K2 = 1, K3 = 2 }

    /// <summary>Tur fazları (GDD Bölüm 4).</summary>
    public enum Phase { Kurulum, Safak, AnaFaz, Alacakaranlik, OyunSonu }

    public enum EntityKind { None, Unit, Structure, Hq }

    /// <summary>Etki katmanları (GDD Bölüm 4): Zemin, Hava, Alan.</summary>
    public enum EffectLayer { Zemin = 0, Hava = 1, Alan = 2 }

    [Flags]
    public enum EffectTag
    {
        None = 0,
        Ates = 1 << 0,
        Su = 1 << 1,
        Sis = 1 << 2,
        Zehir = 1 << 3,
        Buhar = 1 << 4,
        Cekim = 1 << 5,
        Gecit = 1 << 6,
        Hortum = 1 << 7,
        Firtina = 1 << 8,
        Yagmur = 1 << 9
    }

    /// <summary>Etki ve istatistik değiştiricilerinde kullanılan istatistikler.</summary>
    public enum Stat { Sal, Hiz, Mnz, Zirh, YapiMnz }

    public enum ModOp { Add, Set }

    /// <summary>Etkinin kimleri etkilediği; büyüyü yapana göre.</summary>
    public enum Affects { Enemies, Allies, All }

    /// <summary>GDD tetik zamanları: Tur başı, Giriş, Anında (OnApply), Gecikmeli ve yapanın tur başı.</summary>
    public enum Trigger { OnApply, OwnerTurnStart, CasterTurnStart, OnEnter }

    public enum ActionKind { Damage, Heal, StrikeHighestHp, TornadoAdvance }

    public enum ShapeKind { Single, Square, Rect, Line, Quadrant, Pair }

    public enum TargetZone { Any, EnemyQuadrant, OwnQuadrant, OwnDeploy }

    public enum StatusKind
    {
        Hidden,       // Gizli
        Shadow,       // Gölge Örtüsü
        Blinded,      // Kör
        Burning,      // Yanık
        Marked,       // İşaretli
        Defending,    // Savun (+2 Zırh)
        Charge,       // Hücum (Alev Şövalyesi)
        ArrowRain,    // Ok Yağmuru (Kor Okçusu)
        HitAndRun,    // Vur-Kaç (Rüzgâr Süvarisi)
        GuardArmor    // Takımyıldız Muhafızı (+1 Zırh)
    }

    public enum StatusExpiry { StartOfTurn, EndOfTurn, Manual }

    public enum DamageKind
    {
        Attack, Counter, Floop, Ulti, TileEffect, Structure, HqRetaliation,
        Passive, Collision, Fatigue, Collapse, Sacrifice
    }

    public static class Names
    {
        public static string Of(Faction f)
        {
            switch (f)
            {
                case Faction.Ay: return "Ay";
                case Faction.Gunes: return "Güneş";
                case Faction.Bulut: return "Bulut";
                case Faction.Yildiz: return "Yıldız";
                default: return "Nötr";
            }
        }

        public static string Of(CardType t)
        {
            switch (t)
            {
                case CardType.Birim: return "Birim";
                case CardType.Yapi: return "Yapı";
                default: return "Geliştirme";
            }
        }

        public static string Of(UpgradeSlot s) => s == UpgradeSlot.Silah ? "Silah" : s == UpgradeSlot.Zirh ? "Zırh" : "";

        public static string Of(StructureRole r)
        {
            switch (r)
            {
                case StructureRole.Savunma: return "Savunma";
                case StructureRole.Dinlenme: return "Dinlenme";
                case StructureRole.Guclendirme: return "Güçlendirme";
                case StructureRole.Karakol: return "Karakol";
                default: return "";
            }
        }

        public static string Of(StatusKind s)
        {
            switch (s)
            {
                case StatusKind.Hidden: return "Gizli";
                case StatusKind.Shadow: return "Gölge";
                case StatusKind.Blinded: return "Kör";
                case StatusKind.Burning: return "Yanık";
                case StatusKind.Marked: return "İşaretli";
                case StatusKind.Defending: return "Savunmada";
                case StatusKind.Charge: return "Hücum";
                case StatusKind.ArrowRain: return "Ok Yağmuru";
                case StatusKind.HitAndRun: return "Vur-Kaç";
                case StatusKind.GuardArmor: return "Takımyıldız Zırhı";
                default: return s.ToString();
            }
        }

        public static string Of(Phase p)
        {
            switch (p)
            {
                case Phase.Safak: return "Şafak";
                case Phase.AnaFaz: return "Ana Faz";
                case Phase.Alacakaranlik: return "Alacakaranlık";
                case Phase.OyunSonu: return "Oyun Sonu";
                default: return "Kurulum";
            }
        }

        public static string Of(UltiTier t) => "K" + ((int)t + 1);
    }
}
