namespace DortGok.Core
{
    /// <summary>
    /// GDD'deki tüm sayısal kurallar tek yerde. Varsayılanlar dokümandaki değerlerdir;
    /// denge simülasyonları bu nesneyi kopyalayıp değiştirerek deneme yapar.
    /// </summary>
    public sealed class RulesConfig
    {
        // Bölüm 1: oyun alanı ve eleme
        public int HqHp = 30;
        public int HqArmor = 1;
        public int DeployRadius = 3;
        public int ConquestGe = 15;
        public bool TeamMode = false;           // Opsiyonel 2v2: çapraz oyuncular takım

        /// <summary>
        /// GDD'de iki okuma var: tahta tablosu "kendi yapılarına bitişik kareler konuşlanma alanıdır" der,
        /// İleri Karakol kartı ise bunu yalnızca kendisi için söyler. Varsayılan tahta tablosunun genel kuralıdır.
        /// </summary>
        public bool AllStructuresExtendDeployment = true;

        // Bölüm 2: Aksiyon Puanı
        public int[] ApByTurn = { 3, 4, 5, 6 };
        public int ExtraDrawCost = 1;
        public int ExtraDrawsPerTurn = 1;

        // Bölüm 2: Gök Enerjisi
        public int GeMax = 100;
        public int GeTurnStart = 5;
        public int GeAltar = 10;
        public int GeKill = 8;
        public int GeOwnLoss = 6;
        public int GePerHqDamage = 2;
        public int[] TierCost = { 30, 60, 100 };

        // Bölüm 2: birimler
        public int MaxEn = 3;
        public int EnRegen = 2;
        public int SummonEn = 1;
        public int DefendArmor = 2;
        public int MaxStructures = 4;

        // Bölüm 2: deste ve el
        public int StartingHand = 5;
        public int HandLimit = 8;
        public int CopiesPerCoreCard = 3;
        public int Seat3BonusGe = 10;           // 3. oyuncu +10 GE ile başlar
        public int Seat4BonusAp = 1;            // 4. oyuncu tek seferlik +1 AP

        // Bölüm 6: dengeleme
        public int[] TierCooldown = { 2, 3, 5 };
        public int K3LockedRounds = 5;
        public int K3MaxUses = 2;
        public int UltiHqCapPerCast = 3;
        public int UltiHqCapPerRound = 5;
        public int LastStandThreshold = 10;
        public int CollapseStartRound = 15;
        public int CollapseStepRounds = 3;

        // Online tur süresi (Bölüm 4)
        public int TurnSeconds = 90;

        /// <summary>Simülasyon güvenlik sınırı; GDD kuralı değildir.</summary>
        public int SafetyMaxRounds = 80;

        public int ApForTurn(int turnNumber)
        {
            int i = turnNumber - 1;
            if (i < 0) i = 0;
            if (i >= ApByTurn.Length) i = ApByTurn.Length - 1;
            return ApByTurn[i];
        }

        public RulesConfig Clone()
        {
            var c = (RulesConfig)MemberwiseClone();
            c.ApByTurn = (int[])ApByTurn.Clone();
            c.TierCost = (int[])TierCost.Clone();
            c.TierCooldown = (int[])TierCooldown.Clone();
            return c;
        }
    }
}
