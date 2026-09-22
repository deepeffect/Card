using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>Oyunun tüm durumu; saf C#, motordan bağımsız (GDD Bölüm 5: GameState).</summary>
    public sealed class GameState
    {
        public RulesConfig Rules;
        public PlayerState[] Players = new PlayerState[Board.Seats];
        public readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();
        public readonly Dictionary<int, Structure> Structures = new Dictionary<int, Structure>();
        public Headquarters[] Hqs = new Headquarters[Board.Seats];
        public readonly GridModel Grid = new GridModel();
        public readonly Dictionary<int, AreaEffectInstance> Effects = new Dictionary<int, AreaEffectInstance>();
        public readonly List<PlayerEffect> PlayerEffects = new List<PlayerEffect>();
        public readonly List<DelayedSpell> Delayed = new List<DelayedSpell>();
        public DeterministicRng Rng;

        public int Current;
        public int FirstSeat;
        public int Round;
        /// <summary>Oyun boyunca oynanan tur sayısı; "tur başına 1 kez" kuralları için saat.</summary>
        public int TurnIndex;
        public Phase Phase = Phase.Kurulum;
        public int WinnerTeam = -1;
        public int WinnerSeat = -1;
        public int EliminationsSoFar;

        public int NextEntityId = 1;
        public int NextCastId = 1;
        public int NextCardInstanceId = 1;

        public MatchStats Stats = new MatchStats();

        public PlayerState CurrentPlayer => Players[Current];

        public bool IsOver => Phase == Phase.OyunSonu;

        public Headquarters HqOf(int seat) => Hqs[seat];

        public IEnumerable<Unit> UnitsOf(int seat)
        {
            foreach (var u in Units.Values)
                if (u.Owner == seat) yield return u;
        }

        public IEnumerable<Structure> StructuresOf(int seat)
        {
            foreach (var s in Structures.Values)
                if (s.Owner == seat) yield return s;
        }

        public Entity GetEntity(int id)
        {
            if (Units.TryGetValue(id, out var u)) return u;
            if (Structures.TryGetValue(id, out var s)) return s;
            for (int i = 0; i < Hqs.Length; i++)
                if (Hqs[i] != null && Hqs[i].Id == id) return Hqs[i];
            return null;
        }
    }

    /// <summary>Denge ölçümleri için maç istatistikleri (GDD Bölüm 6 hedef metrikleri).</summary>
    public sealed class MatchStats
    {
        public int TotalHqDamage;
        public int UltiHqDamage;
        public int FirstK3Round = -1;
        public int Commands;
        /// <summary>[koltuk][raund] → o raundda karargahın aldığı hasar.</summary>
        public readonly Dictionary<int, Dictionary<int, int>> HqDamageByRound = new Dictionary<int, Dictionary<int, int>>();
        public readonly int[] UltisCast = new int[Board.Seats];

        public void AddHqDamage(int seat, int round, int amount)
        {
            if (!HqDamageByRound.TryGetValue(seat, out var perRound))
                HqDamageByRound[seat] = perRound = new Dictionary<int, int>();
            perRound.TryGetValue(round, out var v);
            perRound[round] = v + amount;
        }

        /// <summary>Herhangi bir karargahın 2 ardışık raundda toplam en az "threshold" CAN kaybedip kaybetmediği.</summary>
        public bool HadBurst(int threshold)
        {
            foreach (var perRound in HqDamageByRound.Values)
                foreach (var kv in perRound)
                {
                    perRound.TryGetValue(kv.Key + 1, out var next);
                    if (kv.Value + next >= threshold) return true;
                }
            return false;
        }
    }
}
