using System.Linq;
using DortGok.Core;
using NUnit.Framework;

namespace DortGok.Tests
{
    /// <summary>GDD Bölüm 1–2: tahta, deste, sıra telafisi, AP, EN, konuşlanma.</summary>
    public class BoardAndTurnTests
    {
        [Test]
        public void Tahta_KarargahSunakVeCeyrekler_GDDdekiGibi()
        {
            CollectionAssert.AreEquivalent(new[] { new Coord(0, 0), new Coord(0, 1), new Coord(1, 0), new Coord(1, 1) }, Board.HqTiles(0));
            CollectionAssert.AreEquivalent(new[] { new Coord(10, 10), new Coord(10, 11), new Coord(11, 10), new Coord(11, 11) }, Board.HqTiles(2));
            Assert.AreEqual(0, Board.QuadrantOf(new Coord(5, 5)));
            Assert.AreEqual(1, Board.QuadrantOf(new Coord(5, 6)));
            Assert.AreEqual(2, Board.QuadrantOf(new Coord(6, 6)));
            Assert.AreEqual(3, Board.QuadrantOf(new Coord(6, 5)));
            Assert.IsTrue(Board.AltarTiles.All(t => Board.IsAltar(t)));
            Assert.AreEqual(4, Board.AltarTiles.Select(Board.QuadrantOf).Distinct().Count(), "Sunak her çeyrekten 1 kare içerir.");
        }

        [Test]
        public void Deste_30Kart_CekirdekUcerKopyaArti15Notr()
        {
            foreach (Faction f in new[] { Faction.Ay, Faction.Gunes, Faction.Bulut, Faction.Yildiz })
            {
                var deck = CardCatalog.BuildStarterDeck(f, 3);
                Assert.AreEqual(30, deck.Count);
                foreach (var core in CardCatalog.CoreCards(f)) Assert.AreEqual(3, deck.Count(id => id == core));
                Assert.AreEqual(15, deck.Count(id => CardCatalog.Get(id).Faction == Faction.Notr));
            }
        }

        [Test]
        public void Kurulum_Elde5Kart_SiraTelafisi()
        {
            var e = TestKit.NewMatch(firstSeat: 0);
            var s = e.State;
            Assert.AreEqual(0, s.Current);
            Assert.AreEqual(5, s.Players[0].Hand.Count, "1. oyuncu ilk turunda kart çekmez.");
            Assert.AreEqual(3, s.Players[0].Ap);
            Assert.AreEqual(10, s.Players[2].Ge, "3. oyuncu +10 GE ile başlar.");
            e.PassUntil(1);
            Assert.AreEqual(6, s.Players[1].Hand.Count);
            e.PassUntil(3);
            Assert.AreEqual(4, s.Players[3].Ap, "4. oyuncu tek seferlik +1 AP alır.");
        }

        [Test]
        public void AksiyonPuani_3_4_5_6_SonraSabit()
        {
            var e = TestKit.NewMatch();
            var expected = new[] { 3, 4, 5, 6, 6 };
            foreach (var ap in expected)
            {
                Assert.AreEqual(ap, e.State.Players[0].Ap);
                e.PassRound();
            }
        }

        [Test]
        public void Enerji_YeniBirim1EN_TurBasi2_EnFazla3()
        {
            var e = TestKit.NewMatch();
            var card = e.State.Players[0].Hand[0];
            e.Ok(new PlayCardCommand { Player = 0, CardInstanceId = card.InstanceId, Target = TargetSpec.AtTile(new Coord(0, 4)) });
            var u = e.State.UnitsOf(0).Single();
            Assert.AreEqual(1, u.En);
            e.PassRound();
            Assert.AreEqual(3, u.En);
            e.Ok(new MoveCommand { Player = 0, UnitId = u.Id, To = new Coord(1, 5) });
            e.PassRound();
            Assert.AreEqual(3, u.En, "EN 3'ü geçemez.");
        }

        [Test]
        public void Konuslanma_Karargah3KareVeYapiBitisigi()
        {
            var e = TestKit.NewMatch();
            Assert.IsTrue(e.Cards.IsDeployable(0, new Coord(0, 4)));
            Assert.IsTrue(e.Cards.IsDeployable(0, new Coord(4, 4)));
            Assert.IsFalse(e.Cards.IsDeployable(0, new Coord(0, 5)));
            e.Build(0, CardCatalog.OkcuKulesi, 3, 4);
            Assert.IsTrue(e.Cards.IsDeployable(0, new Coord(3, 5)), "Kendi yapısına bitişik kare konuşlanma alanıdır.");
        }

        [Test]
        public void Yapi_YalnizcaKendiCeyregine_SunagaDegil_EnFazla4()
        {
            var e = TestKit.NewMatch();
            Assert.IsTrue(e.Cards.IsBuildable(0, new Coord(3, 3)));
            Assert.IsFalse(e.Cards.IsBuildable(0, new Coord(3, 7)), "Başka çeyrek.");
            Assert.IsFalse(e.Cards.IsBuildable(0, new Coord(5, 5)), "Sunak karesi.");
            for (int i = 0; i < 4; i++) e.Build(0, CardCatalog.Paratoner, 2, 2 + i);
            var p = e.State.Players[0];
            var tower = new CardInstance { InstanceId = 999, CardId = CardCatalog.OkcuKulesi };
            p.Hand.Add(tower);
            e.GiveAp(0, 6);
            e.Fails(new PlayCardCommand { Player = 0, CardInstanceId = 999, Target = TargetSpec.AtTile(new Coord(4, 4)) });
        }

        [Test]
        public void Hareket_4Yon_DusmanKeser_DostGecilir_UcanAsar()
        {
            var e = TestKit.NewMatch();
            var knight = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.Spawn(1, CardCatalog.Sovalye, 3, 4);
            e.Spawn(0, CardCatalog.Sovalye, 4, 3);
            var reach = e.Actions.ReachableFor(knight);
            Assert.IsFalse(reach.ContainsKey(new Coord(3, 5)), "Düşman birim yolu keser.");
            Assert.IsTrue(reach.ContainsKey(new Coord(5, 3)), "Dost birimin içinden geçilir.");
            Assert.IsFalse(reach.ContainsKey(new Coord(4, 3)), "Dost birimin üstünde durulamaz.");

            var archer = e.Spawn(0, CardCatalog.KorOkcusu, 7, 2);
            var archerReach = e.Actions.ReachableFor(archer);
            Assert.IsFalse(archerReach.ContainsKey(new Coord(8, 3)), "Çapraz hareket yok (4 yön).");
            Assert.AreEqual(4, archerReach.Count);

            var owl = e.Spawn(0, CardCatalog.GeceBaykusu, 2, 7);
            e.Spawn(1, CardCatalog.Sovalye, 2, 8);
            Assert.IsTrue(e.Actions.ReachableFor(owl).ContainsKey(new Coord(2, 9)), "Uçan birim düşmanın üzerinden geçer.");
        }

        [Test]
        public void EylemlerTurBasinaBirKez_Ve1EN()
        {
            var e = TestKit.NewMatch();
            var u = e.Spawn(0, CardCatalog.Sovalye, 3, 3, en: 3);
            e.Ok(new MoveCommand { Player = 0, UnitId = u.Id, To = new Coord(3, 4) });
            e.Fails(new MoveCommand { Player = 0, UnitId = u.Id, To = new Coord(3, 3) });
            e.Ok(new DefendCommand { Player = 0, UnitId = u.Id });
            Assert.AreEqual(1, u.En);
            Assert.AreEqual(2, e.Stats.Armor(u));
        }

        [Test]
        public void SunakKontrolu_TurBasinda10GE()
        {
            var e = TestKit.NewMatch();
            e.Spawn(0, CardCatalog.Sovalye, 5, 5);
            int before = e.State.Players[0].Ge;
            e.PassRound();
            Assert.AreEqual(before + 5 + 10, e.State.Players[0].Ge);
        }

        [Test]
        public void ElSiniri8_TurSonundaAtilir()
        {
            var e = TestKit.NewMatch();
            var p = e.State.Players[0];
            for (int i = 0; i < 5; i++) p.Hand.Add(new CardInstance { InstanceId = 500 + i, CardId = CardCatalog.Sovalye });
            Assert.AreEqual(10, p.Hand.Count);
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(8, p.Hand.Count);
        }

        [Test]
        public void DesteBitince_KarargahArtanHasar()
        {
            var e = TestKit.NewMatch();
            e.State.Players[1].Deck.Clear();
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(29, e.Hq(1).Hp, "İlk boş çekiş 1 hasar (Zırh uygulanmaz).");
            Assert.AreEqual(1, e.State.Players[1].Fatigue);
            e.Ok(new DrawExtraCommand { Player = 1 });
            Assert.AreEqual(27, e.Hq(1).Hp, "İkinci boş çekiş 2 hasar.");
        }

        [Test]
        public void GokCokusu_15RaunddanItibaren()
        {
            var e = TestKit.NewMatch();
            e.State.Round = 15;
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(29, e.Hq(1).Hp);
            e.State.Round = 18;
            e.PassUntil(2);
            Assert.AreEqual(28, e.Hq(2).Hp, "18. raundda hasar 2'ye çıkar.");
        }

        [Test]
        public void IkiyeIki_CaprazOyuncularTakim_DostaSaldirilamaz()
        {
            var e = TestKit.NewMatch(team: true);
            Assert.IsTrue(e.IsAlly(0, 2));
            Assert.IsTrue(e.IsEnemy(0, 1));
            var a = e.Spawn(0, CardCatalog.Sovalye, 5, 5);
            var ally = e.Spawn(2, CardCatalog.Sovalye, 5, 6);
            e.Fails(new AttackCommand { Player = 0, UnitId = a.Id, TargetId = ally.Id });
        }

        [Test]
        public void Determinizm_AyniTohumAyniMac()
        {
            var r1 = MatchRunner.RunBotMatch(42);
            var r2 = MatchRunner.RunBotMatch(42);
            Assert.IsNull(r1.Error, r1.Error);
            Assert.AreEqual(r1.WinnerSeat, r2.WinnerSeat);
            Assert.AreEqual(r1.Rounds, r2.Rounds);
            Assert.AreEqual(r1.Commands, r2.Commands);
            Assert.AreEqual(r1.TotalHqDamage, r2.TotalHqDamage);
        }
    }
}
