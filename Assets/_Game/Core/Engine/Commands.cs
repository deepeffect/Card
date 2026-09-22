using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>Kart, Floop ve ulti hedeflerini taşıyan genel hedef tanımı.</summary>
    public sealed class TargetSpec
    {
        public Coord Tile = Coord.None;
        public Coord Tile2 = Coord.None;
        public int EntityId = -1;
        public List<int> EntityIds;
        public List<Coord> Tiles;
        public int Player = -1;
        public Coord Direction = Coord.None;
        public int Amount;
        /// <summary>Ay Fısıldayanı: true = çek, false = it.</summary>
        public bool Pull = true;
        /// <summary>Gözlemevi: seçilen kartın sırası.</summary>
        public int Choice = -1;

        public static TargetSpec None => new TargetSpec();
        public static TargetSpec AtTile(Coord c) => new TargetSpec { Tile = c };
        public static TargetSpec AtEntity(int id) => new TargetSpec { EntityId = id };
        public static TargetSpec AtPlayer(int seat) => new TargetSpec { Player = seat };
    }

    public abstract class Command
    {
        public int Player;
    }

    public sealed class PlayCardCommand : Command
    {
        public int CardInstanceId;
        public TargetSpec Target = new TargetSpec();
    }

    public sealed class MoveCommand : Command
    {
        public int UnitId;
        public Coord To;
    }

    /// <summary>Vur-Kaç: saldırıdan sonra EN harcamadan en fazla 2 kare geri çekilme.</summary>
    public sealed class RetreatCommand : Command
    {
        public int UnitId;
        public Coord To;
    }

    public sealed class AttackCommand : Command
    {
        public int UnitId;
        public int TargetId;
        /// <summary>Ok Yağmuru'nun ikinci hedefi (boşsa otomatik seçilir).</summary>
        public int SecondaryTargetId = -1;
    }

    public sealed class DefendCommand : Command
    {
        public int UnitId;
    }

    public sealed class FloopCommand : Command
    {
        public int SourceId;
        public TargetSpec Target = new TargetSpec();
    }

    public sealed class CastUltiCommand : Command
    {
        public string UltiId;
        public TargetSpec Target = new TargetSpec();
    }

    /// <summary>Hortum'un bir sonraki tur başında ilerleyeceği yönü değiştirir.</summary>
    public sealed class SteerTornadoCommand : Command
    {
        public int EffectId;
        public Coord Direction;
    }

    public sealed class DrawExtraCommand : Command { }

    public sealed class EndTurnCommand : Command
    {
        /// <summary>El sınırı aşılırsa atılacak kartlar; boşsa en son çekilenler atılır.</summary>
        public List<int> Discards;
    }

    public struct CommandResult
    {
        public bool Ok;
        public string Error;
        public static CommandResult Success => new CommandResult { Ok = true };
        public static CommandResult Fail(string error) => new CommandResult { Ok = false, Error = error };
        public override string ToString() => Ok ? "OK" : Error;
    }
}
