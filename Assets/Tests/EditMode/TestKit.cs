using System.Collections.Generic;
using System.Linq;
using DortGok.Core;
using NUnit.Framework;

namespace DortGok.Tests
{
    /// <summary>Testler için kurgusal masa: sabit desteler, ilk oyuncu belli, durum doğrudan ayarlanabilir.</summary>
    internal static class TestKit
    {
        /// <summary>Koltuk 0'dan başlayan, destesi tamamen Şövalye olan (çekilişleri öngörülebilir) maç.</summary>
        public static RulesEngine NewMatch(Faction[] factions = null, int firstSeat = 0, System.Action<RulesConfig> tweak = null, bool team = false)
        {
            var rules = new RulesConfig { TeamMode = team };
            tweak?.Invoke(rules);
            var decks = new List<string>[4];
            for (int i = 0; i < 4; i++) decks[i] = Enumerable.Repeat(CardCatalog.Sovalye, 30).ToList();
            return RulesEngine.CreateMatch(new GameSetup
            {
                Seed = 7,
                FirstSeat = firstSeat,
                Rules = rules,
                CustomDecks = decks,
                Factions = factions ?? new[] { Faction.Ay, Faction.Gunes, Faction.Bulut, Faction.Yildiz }
            });
        }

        public static Unit Spawn(this RulesEngine e, int seat, string cardId, int row, int col, int en = 3)
        {
            var u = e.Cards.SpawnUnit(seat, cardId, new Coord(row, col), -1);
            u.En = en;
            return u;
        }

        public static Structure Build(this RulesEngine e, int seat, string cardId, int row, int col) =>
            e.Cards.SpawnStructure(seat, cardId, new Coord(row, col), -1);

        public static CommandResult Ok(this RulesEngine e, Command c)
        {
            var r = e.Execute(c);
            Assert.IsTrue(r.Ok, "Komut reddedildi: " + r.Error);
            return r;
        }

        public static void Fails(this RulesEngine e, Command c)
        {
            var r = e.Execute(c);
            Assert.IsFalse(r.Ok, "Komutun reddedilmesi bekleniyordu.");
        }

        /// <summary>Sırayı istenen koltuğa getirene kadar turları bitirir.</summary>
        public static void PassUntil(this RulesEngine e, int seat)
        {
            for (int i = 0; i < 8 && e.State.Current != seat; i++)
                e.Ok(new EndTurnCommand { Player = e.State.Current });
            Assert.AreEqual(seat, e.State.Current);
        }

        /// <summary>Tam bir raund geçirip aynı oyuncuya döner.</summary>
        public static void PassRound(this RulesEngine e)
        {
            int seat = e.State.Current;
            e.Ok(new EndTurnCommand { Player = seat });
            e.PassUntil(seat);
        }

        public static void GiveGe(this RulesEngine e, int seat, int ge) => e.State.Players[seat].Ge = ge;

        public static void GiveAp(this RulesEngine e, int seat, int ap) => e.State.Players[seat].Ap = ap;

        public static Headquarters Hq(this RulesEngine e, int seat) => e.State.Hqs[seat];

        public static CastUltiCommand Ulti(int seat, string id, TargetSpec t) =>
            new CastUltiCommand { Player = seat, UltiId = id, Target = t ?? new TargetSpec() };
    }
}
