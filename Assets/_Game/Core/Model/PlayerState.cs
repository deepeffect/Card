using System.Collections.Generic;

namespace DortGok.Core
{
    public sealed class PlayerState
    {
        public int Seat;
        public Faction Faction;
        public int Team;
        public bool Alive = true;
        /// <summary>Tur sırasındaki yeri: 0 = ilk oyuncu.</summary>
        public int OrderPos;

        public int Ap;
        public int Ge;
        public int TurnsTaken;
        public int Fatigue;

        public readonly List<CardInstance> Deck = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> Discard = new List<CardInstance>();

        public readonly Dictionary<string, int> UltiCooldown = new Dictionary<string, int>();
        public int K3Uses;
        public bool K3EchoLock;          // Gök Yankısı
        public bool UltiUsedThisTurn;
        public int ExtraDrawsThisTurn;
        public int LastTargetedRound = -1; // Hedef koruması
        public int ResonanceThisTurn;      // Kozmik Rezonans
        public bool FreeMoveUsed;          // Arka Rüzgâr

        // Sıra avantajı telafisi
        public bool SkipFirstDraw;
        public int PendingBonusAp;

        public int HqId;
        public int EliminatedRound = -1;
        public int EliminationOrder = -1;

        public string Name => Names.Of(Faction) + " (" + Board.SeatNames[Seat] + ")";
    }
}
