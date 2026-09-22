using System;
using System.Collections.Generic;

namespace DortGok.Core
{
    public sealed class GameSetup
    {
        public int Seed = 1;
        public RulesConfig Rules = new RulesConfig();
        /// <summary>Koltuk başına fraksiyon; varsayılan GDD oturma düzeni.</summary>
        public Faction[] Factions = { Faction.Ay, Faction.Gunes, Faction.Bulut, Faction.Yildiz };
        /// <summary>-1: ilk oyuncu tohumdan rastgele seçilir (GDD).</summary>
        public int FirstSeat = -1;
        public bool ShuffleDecks = true;
        /// <summary>Testler için isteğe bağlı sabit desteler (üstten alta kart kimlikleri).</summary>
        public List<string>[] CustomDecks;
        /// <summary>Olay günlüğünü bellekte tut (simülasyonlarda kapatılır).</summary>
        public bool KeepLog = true;
    }

    /// <summary>
    /// Kural Motoru (GDD Bölüm 5): komutu doğrular, uygular, GameState'i günceller ve olay yayınlar.
    /// Animasyonu beklemez; sunum katmanı olay akışını kendi hızında oynatır.
    /// </summary>
    public sealed class RulesEngine
    {
        public GameState State { get; }
        public RulesConfig Rules => State.Rules;
        public GridModel Grid => State.Grid;

        public StatResolver Stats { get; }
        public EffectSystem Effects { get; }
        public ForcedMovementService Forced { get; }
        public DamageSystem Damage { get; }
        public CardManager Cards { get; }
        public TurnManager Turns { get; }
        public UnitActionSystem Actions { get; }
        public FloopSystem Floops { get; }
        public UltiSystem Ultis { get; }

        public event Action<GameEvent> EventRaised;
        public readonly List<GameEvent> Log = new List<GameEvent>();
        public bool KeepLog = true;

        private RulesEngine(GameState state)
        {
            State = state;
            state.Grid.Engine = this;
            Stats = new StatResolver(this);
            Effects = new EffectSystem(this);
            Forced = new ForcedMovementService(this);
            Damage = new DamageSystem(this);
            Cards = new CardManager(this);
            Turns = new TurnManager(this);
            Actions = new UnitActionSystem(this);
            Floops = new FloopSystem(this);
            Ultis = new UltiSystem(this);
        }

        public static RulesEngine CreateMatch(GameSetup setup, Action<GameEvent> listener = null)
        {
            var state = new GameState
            {
                Rules = setup.Rules ?? new RulesConfig(),
                Rng = new DeterministicRng((ulong)setup.Seed * 2654435761UL + 12345UL)
            };
            var engine = new RulesEngine(state) { KeepLog = setup.KeepLog };
            if (listener != null) engine.EventRaised += listener;
            engine.Turns.StartMatch(setup);
            return engine;
        }

        // ---------------- Komutlar ----------------

        public CommandResult Execute(Command cmd)
        {
            if (cmd == null) return CommandResult.Fail("Komut boş.");
            string error = Validate(cmd);
            if (error != null) return CommandResult.Fail(error);
            Apply(cmd);
            AfterAction();
            State.Stats.Commands++;
            return CommandResult.Success;
        }

        /// <summary>Komutu uygulamadan doğrular; geçerliyse null, değilse Türkçe gerekçe döner.</summary>
        public string Validate(Command cmd)
        {
            if (State.IsOver) return "Oyun bitti.";
            if (cmd.Player != State.Current) return "Sıra sende değil.";
            if (State.Phase != Phase.AnaFaz) return "Şu an eylem yapılamaz (" + Names.Of(State.Phase) + ").";
            switch (cmd)
            {
                case PlayCardCommand c: return Cards.ValidatePlay(c);
                case MoveCommand c: return Actions.ValidateMove(c);
                case RetreatCommand c: return Actions.ValidateRetreat(c);
                case AttackCommand c: return Actions.ValidateAttack(c);
                case DefendCommand c: return Actions.ValidateDefend(c);
                case FloopCommand c: return Floops.Validate(c);
                case CastUltiCommand c: return Ultis.Validate(c);
                case SteerTornadoCommand c: return Ultis.ValidateSteer(c);
                case DrawExtraCommand c: return Cards.ValidateExtraDraw(c);
                case EndTurnCommand _: return null;
                default: return "Bilinmeyen komut.";
            }
        }

        private void Apply(Command cmd)
        {
            switch (cmd)
            {
                case PlayCardCommand c: Cards.ApplyPlay(c); break;
                case MoveCommand c: Actions.ApplyMove(c); break;
                case RetreatCommand c: Actions.ApplyRetreat(c); break;
                case AttackCommand c: Actions.ApplyAttack(c); break;
                case DefendCommand c: Actions.ApplyDefend(c); break;
                case FloopCommand c: Floops.Apply(c); break;
                case CastUltiCommand c: Ultis.Apply(c); break;
                case SteerTornadoCommand c: Ultis.ApplySteer(c); break;
                case DrawExtraCommand c: Cards.ApplyExtraDraw(c); break;
                case EndTurnCommand c: Turns.EndTurn(c.Player, c.Discards); break;
            }
        }

        /// <summary>Her eylemden sonra: ölümleri çöz, elemeleri ve oyun sonunu kontrol et.</summary>
        internal void AfterAction()
        {
            Damage.ResolveDeaths();
            Turns.CheckGameOver();
            if (State.IsOver) return;
            if (!State.Players[State.Current].Alive && State.Phase != Phase.Kurulum)
                Turns.AdvanceFromEliminatedCurrent();
        }

        // ---------------- Ortak yardımcılar ----------------

        public bool IsEnemy(int a, int b) =>
            a >= 0 && b >= 0 && a != b && State.Players[a].Team != State.Players[b].Team;

        public bool IsAlly(int a, int b) => a >= 0 && b >= 0 && !IsEnemy(a, b);

        /// <summary>Etki, büyüyü yapana (caster) göre bu sahibin varlığını etkiler mi.</summary>
        public bool AffectsOwner(Affects affects, int caster, int owner)
        {
            switch (affects)
            {
                case Core.Affects.Enemies: return caster < 0 || IsEnemy(caster, owner);
                case Core.Affects.Allies: return caster >= 0 && IsAlly(caster, owner);
                default: return true;
            }
        }

        /// <summary>
        /// Hedef alınabilirlik: Gizli birimler hiç hedef alınamaz; Gölge Örtüsü altındaki birimler
        /// 2 kareden uzaktan ve tek hedefli ultilerle hedef alınamaz.
        /// </summary>
        public bool CanBeTargeted(Unit target, int byPlayer, Coord from, bool byUlti)
        {
            if (target == null || target.IsDead) return false;
            if (target.Has(StatusKind.Hidden)) return false;
            if (!IsEnemy(byPlayer, target.Owner) && !IsEnemy(byPlayer, target.ActingPlayer)) return true;
            if (IsShadowed(target))
            {
                if (byUlti) return false;
                if (from == Coord.None || from.Chebyshev(target.Pos) > 2) return false;
            }
            return true;
        }

        public bool IsShadowed(Unit u)
        {
            if (u.Has(StatusKind.Shadow)) return true;
            foreach (var s in State.Structures.Values)
                if (s.Def.Passive == CardPassive.SisFeneri && IsAlly(s.Owner, u.Owner) && s.Pos.Chebyshev(u.Pos) <= 1)
                    return true;
            return false;
        }

        public void GainGe(int seat, int amount, string reason)
        {
            if (seat < 0 || amount == 0) return;
            var p = State.Players[seat];
            if (!p.Alive) return;
            int before = p.Ge;
            p.Ge = Math.Max(0, Math.Min(Rules.GeMax, p.Ge + amount));
            if (p.Ge != before)
                Emit(new GameEvent { Type = GameEventType.GeChanged, Player = seat, Amount = p.Ge - before, Message = $"{p.Name}: {(p.Ge - before >= 0 ? "+" : "")}{p.Ge - before} GE ({reason})" });
        }

        internal int NewEntityId() => State.NextEntityId++;

        internal void Emit(GameEvent e)
        {
            if (KeepLog) Log.Add(e);
            EventRaised?.Invoke(e);
        }

        internal void Say(string message, int player = -1) =>
            Emit(new GameEvent { Type = GameEventType.Log, Player = player, Message = message });

        public string EntityName(Entity e)
        {
            switch (e)
            {
                case Unit u: return u.Def.Name;
                case Structure s: return s.Def.Name;
                case Headquarters h: return State.Players[h.Owner].Name + " karargahı";
                default: return "?";
            }
        }

        /// <summary>Bir karede duran varlık (birim, yapı ya da karargah).</summary>
        public Entity EntityAt(Coord c)
        {
            if (!c.IsOnBoard) return null;
            int hqOwner = Grid.HqOwnerAt(c);
            if (hqOwner >= 0) return State.Hqs[hqOwner].IsDead ? null : State.Hqs[hqOwner];
            int occ = Grid.OccupantAt(c);
            return occ == 0 ? null : State.GetEntity(occ);
        }

        /// <summary>Varlığın herhangi bir kareye 8 yönlü mesafesi (karargahta en yakın kare).</summary>
        public int DistanceTo(Entity e, Coord from)
        {
            switch (e)
            {
                case Unit u: return u.Pos.Chebyshev(from);
                case Structure s: return s.Pos.Chebyshev(from);
                case Headquarters h: return h.DistanceTo(from);
                default: return int.MaxValue;
            }
        }
    }
}
