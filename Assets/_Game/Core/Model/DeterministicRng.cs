using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>
    /// Maçın tohum (seed) değerinden üretilen rastgelelik (GDD Bölüm 4, kural 5):
    /// ağdaki herkes ve tekrar oynatmalar aynı sonucu görür. SplitMix64 tabanlı.
    /// </summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(ulong seed) { _state = seed; }

        public ulong State => _state;

        public ulong NextULong()
        {
            ulong z = unchecked(_state += 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        /// <summary>[0, maxExclusive) aralığında sayı.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
