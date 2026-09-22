using System.Collections.Generic;

namespace DortGok.Core
{
    public sealed class CardInstance
    {
        public int InstanceId;
        public string CardId;
        public CardDef Def => CardCatalog.Get(CardId);
        public override string ToString() => $"{Def.Name}#{InstanceId}";
    }

    public sealed class Status
    {
        public StatusKind Kind;
        public int Value;
        public int SourcePlayer;
        public StatusExpiry Expiry;
        /// <summary>StartOfTurn/EndOfTurn süreleri bu oyuncunun turuna göre biter.</summary>
        public int ExpiryPlayer;
    }

    public abstract class Entity
    {
        public int Id;
        public int Owner;
        public int Hp;
        public abstract EntityKind Kind { get; }
        public bool IsDead;
    }

    public sealed class Unit : Entity
    {
        public override EntityKind Kind => EntityKind.Unit;

        public string CardId;
        public int CardInstanceId = -1;
        public Coord Pos;

        public int BaseAtk;
        public int BaseMaxHp;
        public int BaseSpeed;
        public int BaseRange;
        public bool Flying;
        public int En;

        public string WeaponCardId;
        public int WeaponInstanceId = -1;
        public string ArmorCardId;
        public int ArmorInstanceId = -1;

        // Tur başına bir kez yapılabilen eylemler
        public bool Moved;
        public bool Attacked;
        public bool Defended;
        public int FloopsUsed;
        public bool RetreatAvailable;

        // İllüzyon kopyası
        public bool IsIllusion;
        public int IllusionRemaining;
        public int IllusionOfId = -1;

        // Anka
        public bool IsToken;
        public bool Reborn;

        // Tutulma
        public int ControlledBy = -1;
        public bool ControlMoveLeft;
        public bool ControlAttackLeft;
        public int ControlCastId;

        public int LastDamagedByPlayer = -1;
        public int SummonedTurnIndex;
        public readonly List<Status> Statuses = new List<Status>();

        public CardDef Def => CardCatalog.Get(CardId);

        /// <summary>Birimi o an yöneten oyuncu (Tutulma altında yöneten, değilse sahibi).</summary>
        public int ActingPlayer => ControlledBy >= 0 ? ControlledBy : Owner;

        public bool Has(StatusKind k)
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == k) return true;
            return false;
        }

        public Status Get(StatusKind k)
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == k) return Statuses[i];
            return null;
        }

        public int SumValue(StatusKind k)
        {
            int v = 0;
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == k) v += Statuses[i].Value;
            return v;
        }

        public string DisplayName => IsIllusion ? Def.Name : Def.Name;
    }

    public sealed class Structure : Entity
    {
        public override EntityKind Kind => EntityKind.Structure;
        public string CardId;
        public int CardInstanceId = -1;
        public Coord Pos;
        public int MaxHp;
        public int FloopsUsed;
        public int LastDamagedByPlayer = -1;
        public CardDef Def => CardCatalog.Get(CardId);
    }

    public sealed class Headquarters : Entity
    {
        public override EntityKind Kind => EntityKind.Hq;
        public Coord[] Tiles;
        public int MaxHp;

        // Son Siper (Bölüm 6)
        public bool LastStandTriggered;
        public int LastStandEndsAfterOwnerTurn = -1;
        public bool LastStandActive => LastStandEndsAfterOwnerTurn >= 0;

        // Ulti hasar tavanı (Bölüm 6)
        public int UltiDamageRound = -1;
        public int UltiDamageThisRound;
        public readonly Dictionary<int, int> UltiDamageByCast = new Dictionary<int, int>();

        public int LastDamagedByPlayer = -1;

        public int DistanceTo(Coord c)
        {
            int best = int.MaxValue;
            foreach (var t in Tiles)
            {
                int d = t.Chebyshev(c);
                if (d < best) best = d;
            }
            return best;
        }

        public bool Covers(Coord c)
        {
            foreach (var t in Tiles)
                if (t == c) return true;
            return false;
        }
    }
}
