using System.Collections.Generic;

namespace DortGok.Core
{
    public enum GameEventType
    {
        MatchStarted,
        RoundStarted,
        TurnStarted,
        PhaseChanged,
        TurnEnded,
        CardDrawn,
        CardDiscarded,
        CardPlayed,
        UnitSummoned,
        StructureBuilt,
        UpgradeAttached,
        UnitMoved,
        UnitForcedMoved,
        UnitTeleported,
        AttackPerformed,
        Defended,
        Damaged,
        Healed,
        EntityDestroyed,
        UnitReborn,
        IllusionRevealed,
        PlayerEliminated,
        GameOver,
        UltiCast,
        MeteorMarked,
        EffectAdded,
        EffectRemoved,
        EffectMoved,
        FloopUsed,
        StatusChanged,
        GeChanged,
        ApChanged,
        LastStand,
        ControlStarted,
        ControlEnded,
        Log
    }

    /// <summary>
    /// Kural motorunun yayınladığı olay. Sunum kuyruğu bunları sırayla animasyona çevirir;
    /// kural motoru animasyonu beklemez (GDD Bölüm 5).
    /// </summary>
    public sealed class GameEvent
    {
        public GameEventType Type;
        public int Player = -1;
        public int EntityId = -1;
        public int TargetId = -1;
        public Coord From = Coord.None;
        public Coord To = Coord.None;
        public int Amount;
        public string Id;
        public List<Coord> Tiles;
        public string Message;

        public override string ToString() => Message ?? Type.ToString();
    }
}
