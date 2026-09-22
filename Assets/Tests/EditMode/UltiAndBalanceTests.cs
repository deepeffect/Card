using System.Linq;
using DortGok.Core;
using NUnit.Framework;

namespace DortGok.Tests
{
    /// <summary>GDD Bölüm 3 ve 6: ultiler, kademe kuralları, hedef koruması, karargah tavanı, Son Siper.</summary>
    public class UltiAndBalanceTests
    {
        [Test]
        public void GDDTesti_Meteor_SuresiDolanKaraDeliktenOnceCozulur()
        {
            var e = TestKit.NewMatch(firstSeat: 3);
            e.State.Round = 6;
            var a = e.Spawn(0, CardCatalog.Sovalye, 4, 6);
            var b = e.Spawn(0, CardCatalog.Sovalye, 2, 4);
            e.GiveGe(3, 60);
            e.Ok(TestKit.Ulti(3, UltiCatalog.KaraDelik, TargetSpec.AtTile(new Coord(4, 4))));
            Assert.AreEqual(new Coord(4, 4), a.Pos, "İlk birim merkeze çekildi.");
            Assert.AreEqual(new Coord(3, 4), b.Pos, "İkinci birim merkezdekine çarptı.");
            Assert.AreEqual(3, a.Hp);
            Assert.AreEqual(3, b.Hp);
            Assert.AreEqual(0, e.Stats.Speed(a), "Çekim alanında HIZ 0.");

            e.PassRound();
            e.GiveGe(3, 100);
            e.Ok(TestKit.Ulti(3, UltiCatalog.MeteorYagmuru, TargetSpec.AtTile(new Coord(3, 3))));
            Assert.IsTrue(e.State.Units.ContainsKey(a.Id), "Meteor hemen düşmez.");

            e.PassRound();
            Assert.IsFalse(e.State.Units.ContainsKey(a.Id), "Meteor Kara Delik'in topladığı birime isabet etti.");
            Assert.IsFalse(e.State.Units.ContainsKey(b.Id));
            Assert.IsFalse(e.State.Effects.Values.Any(f => f.Def.Id == EffectCatalog.Cekim), "Çekim alanı Meteor'dan sonra kalktı.");
        }

        [Test]
        public void K3_Ilk5RauntKilitli_MactaEnFazla2()
        {
            var e = TestKit.NewMatch(firstSeat: 3);
            e.GiveGe(3, 100);
            StringAssert.Contains("kilitli", e.Ultis.Availability(3, UltiCatalog.MeteorYagmuru));
            e.State.Round = 6;
            Assert.IsNull(e.Ultis.Availability(3, UltiCatalog.MeteorYagmuru));
            e.State.Players[3].K3Uses = 2;
            Assert.IsNotNull(e.Ultis.Availability(3, UltiCatalog.MeteorYagmuru));
        }

        [Test]
        public void GokYankisi_K3SonrasiDigerlerininK3u1RaundKilitli()
        {
            var e = TestKit.NewMatch(firstSeat: 3);
            e.State.Round = 6;
            e.GiveGe(3, 100);
            e.Ok(TestKit.Ulti(3, UltiCatalog.MeteorYagmuru, TargetSpec.AtTile(new Coord(4, 8))));
            e.Ok(new EndTurnCommand { Player = 3 });
            Assert.AreEqual(0, e.State.Current);
            e.GiveGe(0, 100);
            StringAssert.Contains("Gök Yankısı", e.Ultis.Availability(0, UltiCatalog.Tutulma));
            e.PassRound();
            e.GiveGe(0, 100);
            Assert.IsNull(e.Ultis.Availability(0, UltiCatalog.Tutulma));
        }

        [Test]
        public void TurdaEnFazla1Ulti_VeBeklemeSuresi()
        {
            var e = TestKit.NewMatch(firstSeat: 2);
            var t1 = e.Spawn(0, CardCatalog.KumulusDevi, 3, 3);
            e.GiveGe(2, 100);
            e.Ok(TestKit.Ulti(2, UltiCatalog.SimsekCarpmasi, TargetSpec.AtEntity(t1.Id)));
            Assert.AreEqual(4, t1.Hp);
            e.Fails(TestKit.Ulti(2, UltiCatalog.IyilestiriciYagmur, null));
            e.PassRound();
            e.GiveGe(2, 100);
            StringAssert.Contains("Bekleme", e.Ultis.Availability(2, UltiCatalog.SimsekCarpmasi));
            e.PassRound();
            e.GiveGe(2, 100);
            Assert.IsNull(e.Ultis.Availability(2, UltiCatalog.SimsekCarpmasi), "K1 bekleme süresi 2 tur.");
        }

        [Test]
        public void HedefKorumasi_BirOyuncuRaundaEnFazla1K2K3()
        {
            var e = TestKit.NewMatch(firstSeat: 0);
            e.GiveGe(0, 60);
            e.Ok(TestKit.Ulti(0, UltiCatalog.ZehirliSis, TargetSpec.AtTile(new Coord(2, 6))));
            e.Ok(new EndTurnCommand { Player = 0 });
            e.Ok(new EndTurnCommand { Player = 1 });
            Assert.AreEqual(2, e.State.Current);
            e.GiveGe(2, 60);
            var err = e.Validate(TestKit.Ulti(2, UltiCatalog.Hortum, new TargetSpec { Tile = new Coord(2, 6), Direction = Coord.Up }));
            StringAssert.Contains("Hedef koruması", err);
            Assert.IsNull(e.Validate(TestKit.Ulti(2, UltiCatalog.Hortum, new TargetSpec { Tile = new Coord(2, 2), Direction = Coord.Up })), "Başka bir oyuncu hedef alınabilir.");
        }

        [Test]
        public void KarargahTavani_UltiBasina3_RaundaBasina5()
        {
            var e = TestKit.NewMatch(tweak: r => r.HqArmor = 0);
            var hq = e.Hq(1);
            var src = new DamageSource { Player = 3, Kind = DamageKind.Ulti, IsUlti = true, CastId = 101 };
            Assert.AreEqual(3, e.Damage.DealToHq(hq, 10, src));
            Assert.AreEqual(0, e.Damage.DealToHq(hq, 10, src), "Aynı büyü 3'ten fazla veremez.");
            src.CastId = 102;
            Assert.AreEqual(2, e.Damage.DealToHq(hq, 10, src), "Raund tavanı 5.");
            src.CastId = 103;
            Assert.AreEqual(0, e.Damage.DealToHq(hq, 10, src));
            Assert.AreEqual(25, hq.Hp);
        }

        [Test]
        public void SonSiper_10unAltinda_UltiyeBagisik_KartCeker()
        {
            var e = TestKit.NewMatch();
            var hq = e.Hq(1);
            hq.Hp = 11;
            int hand = e.State.Players[1].Hand.Count;
            e.Damage.DealToHq(hq, 3, new DamageSource { Player = 0, Kind = DamageKind.Attack });
            Assert.AreEqual(9, hq.Hp);
            Assert.IsTrue(hq.LastStandActive);
            Assert.AreEqual(hand + 1, e.State.Players[1].Hand.Count);
            Assert.AreEqual(0, e.Damage.DealToHq(hq, 3, new DamageSource { Player = 0, Kind = DamageKind.Ulti, IsUlti = true, CastId = 5 }));
            e.PassUntil(1);
            e.Ok(new EndTurnCommand { Player = 1 });
            Assert.IsFalse(hq.LastStandActive, "Sahibinin sonraki turunun sonunda biter.");
        }

        [Test]
        public void Paratoner_SimsegiCeker()
        {
            var e = TestKit.NewMatch(firstSeat: 2);
            var unit = e.Spawn(0, CardCatalog.Sovalye, 4, 4);
            var rod = e.Build(0, CardCatalog.Paratoner, 4, 6);
            e.GiveGe(2, 30);
            e.Ok(TestKit.Ulti(2, UltiCatalog.SimsekCarpmasi, TargetSpec.AtEntity(unit.Id)));
            Assert.AreEqual(4, unit.Hp);
            Assert.IsFalse(e.State.Structures.ContainsKey(rod.Id));
        }

        [Test]
        public void Simsek_KarargahaIki_IslakZincir()
        {
            var e = TestKit.NewMatch(firstSeat: 2, tweak: r => r.HqArmor = 0);
            e.GiveGe(2, 30);
            e.Ok(TestKit.Ulti(2, UltiCatalog.SimsekCarpmasi, TargetSpec.AtEntity(e.Hq(0).Id)));
            Assert.AreEqual(28, e.Hq(0).Hp);

            var e2 = TestKit.NewMatch(firstSeat: 2);
            var t = e2.Spawn(0, CardCatalog.KumulusDevi, 4, 4);
            var n = e2.Spawn(0, CardCatalog.Sovalye, 4, 5);
            e2.Effects.Apply(EffectCatalog.Islak, 2, new[] { t.Pos, n.Pos }, 2, 0, false, Coord.None);
            e2.GiveGe(2, 30);
            e2.Ok(TestKit.Ulti(2, UltiCatalog.SimsekCarpmasi, TargetSpec.AtEntity(t.Id)));
            Assert.AreEqual(4, t.Hp);
            Assert.AreEqual(2, n.Hp, "Bitişik Islak karedeki rakibe 2 zincir hasar.");
        }

        [Test]
        public void GokKalkani_AlanBuyusuHasariniYariyaIndirir()
        {
            var e = TestKit.NewMatch();
            e.Build(0, CardCatalog.GokKalkani, 3, 3);
            var u = e.Spawn(0, CardCatalog.Sovalye, 3, 4);
            e.Damage.DealToUnit(u, 3, new DamageSource { Player = 3, Kind = DamageKind.Ulti, IsUlti = true, IsArea = true, CastId = 9 });
            Assert.AreEqual(3, u.Hp, "3 → 1.");
            e.Damage.DealToUnit(u, 1, new DamageSource { Player = 3, Kind = DamageKind.Ulti, IsUlti = true, IsArea = true, CastId = 9 });
            Assert.AreEqual(3, u.Hp, "1 → 0.");
        }

        [Test]
        public void GeceYarisi_MNZveHIZ_Eksi1_EnAz1_VeKuleler()
        {
            var e = TestKit.NewMatch();
            var archer = e.Spawn(1, CardCatalog.KorOkcusu, 3, 8);
            var tower = e.Build(1, CardCatalog.OkcuKulesi, 4, 8);
            e.GiveGe(0, 30);
            e.Ok(TestKit.Ulti(0, UltiCatalog.GeceYarisi, TargetSpec.AtPlayer(1)));
            Assert.AreEqual(2, e.Stats.Range(archer));
            Assert.AreEqual(1, e.Stats.Speed(archer), "HIZ 1 − 1 → en az 1.");
            Assert.AreEqual(2, e.Stats.StructureRange(tower));
            e.PassRound();
            e.PassRound();
            Assert.AreEqual(3, e.Stats.Range(archer), "2 tur sonra biter.");
        }

        [Test]
        public void SafakVakti_KendiBirimlerine_Sal1_Hiz1()
        {
            var e = TestKit.NewMatch(new[] { Faction.Gunes, Faction.Ay, Faction.Bulut, Faction.Yildiz });
            var u = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.GiveGe(0, 30);
            e.Ok(TestKit.Ulti(0, UltiCatalog.SafakVakti, null));
            Assert.AreEqual(3, e.Stats.Atk(u));
            Assert.AreEqual(3, e.Stats.Speed(u));
        }

        [Test]
        public void GunesPatlamasi_Kor_SaldiriSifir_SisDagilir()
        {
            var e = TestKit.NewMatch(new[] { Faction.Gunes, Faction.Ay, Faction.Bulut, Faction.Yildiz });
            var foe = e.Spawn(1, CardCatalog.Sovalye, 4, 7);
            e.Effects.Apply(EffectCatalog.FenerSisi, 1, new[] { new Coord(4, 8) }, 1, 0, false, Coord.None);
            e.GiveGe(0, 30);
            e.Ok(TestKit.Ulti(0, UltiCatalog.GunesPatlamasi, TargetSpec.AtTile(new Coord(3, 6))));
            Assert.IsTrue(foe.Has(StatusKind.Blinded));
            Assert.IsFalse(e.Effects.TileHas(new Coord(4, 8), EffectTag.Sis));
            e.Ok(new EndTurnCommand { Player = 0 });
            var mine = e.Spawn(0, CardCatalog.KumulusDevi, 4, 6);
            e.Ok(new AttackCommand { Player = 1, UnitId = foe.Id, TargetId = mine.Id });
            Assert.AreEqual(8, mine.Hp, "Kör birimin saldırısı 0 hasar.");
        }

        [Test]
        public void Tutulma_RakipBirimiYonet_KaragahaSaldiramaz_TurSonu2Hasar()
        {
            var e = TestKit.NewMatch(firstSeat: 0);
            e.State.Round = 6;
            var victim = e.Spawn(1, CardCatalog.Sovalye, 3, 8);
            var otherFoe = e.Spawn(1, CardCatalog.KumulusDevi, 3, 9);
            e.GiveGe(0, 100);
            e.Ok(TestKit.Ulti(0, UltiCatalog.Tutulma, new TargetSpec { Player = 1, EntityIds = new System.Collections.Generic.List<int> { victim.Id } }));
            Assert.AreEqual(0, victim.ControlledBy);
            e.Ok(new AttackCommand { Player = 0, UnitId = victim.Id, TargetId = otherFoe.Id });
            Assert.AreEqual(6, otherFoe.Hp, "Yönetilen birim sahibinin birimine saldırdı.");
            Assert.AreEqual(1, victim.Hp, "Kümülüs karşılık verdi.");
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(-1, victim.ControlledBy);
            Assert.IsFalse(e.State.Units.ContainsKey(victim.Id), "Tur sonunda 2 Tutulma hasarı.");
        }

        [Test]
        public void KayanYildiz_RakipKarargaha2KaredenYakinOlamaz()
        {
            var e = TestKit.NewMatch(firstSeat: 3);
            var u = e.Spawn(3, CardCatalog.Sovalye, 8, 2, en: 1);
            e.GiveGe(3, 30);
            e.Fails(TestKit.Ulti(3, UltiCatalog.KayanYildiz, new TargetSpec { EntityId = u.Id, Tile = new Coord(2, 1) }));
            var foe = e.Spawn(0, CardCatalog.Sovalye, 4, 5);
            e.Ok(TestKit.Ulti(3, UltiCatalog.KayanYildiz, new TargetSpec { EntityId = u.Id, Tile = new Coord(4, 4) }));
            Assert.AreEqual(new Coord(4, 4), u.Pos);
            Assert.AreEqual(2, u.En);
            Assert.AreEqual(3, foe.Hp);
        }

        [Test]
        public void YildizGecidi_GirenBirimOtekindenCikar()
        {
            var e = TestKit.NewMatch(firstSeat: 3);
            e.GiveGe(3, 30);
            e.Ok(TestKit.Ulti(3, UltiCatalog.YildizGecidi, new TargetSpec { Tile = new Coord(8, 3), Tile2 = new Coord(3, 8) }));
            var u = e.Spawn(3, CardCatalog.Sovalye, 8, 2);
            var reach = e.Actions.ReachableFor(u);
            Assert.IsTrue(reach.ContainsKey(new Coord(3, 9)), "Geçitten geçip bir kare daha ilerler.");
            e.Ok(new MoveCommand { Player = 3, UnitId = u.Id, To = new Coord(3, 9) });
            Assert.AreEqual(new Coord(3, 9), u.Pos);
        }

        [Test]
        public void SuperHucre_Hiz1_MenzilYok_TurBasindaEnYuksekCanliya3()
        {
            var e = TestKit.NewMatch(firstSeat: 2);
            e.State.Round = 6;
            var big = e.Spawn(0, CardCatalog.KumulusDevi, 3, 3);
            var archer = e.Spawn(0, CardCatalog.YildizCiragi, 2, 4);
            e.GiveGe(2, 100);
            e.Ok(TestKit.Ulti(2, UltiCatalog.SuperHucre, TargetSpec.AtPlayer(0)));
            Assert.AreEqual(1, e.Stats.Speed(big));
            Assert.AreEqual(1, e.Stats.Range(archer));
            e.PassRound();
            Assert.AreEqual(5, big.Hp);
        }

        [Test]
        public void IyilestiriciYagmur_CeyrekIslak_KendiBirimleriIyilesir()
        {
            var e = TestKit.NewMatch(firstSeat: 2);
            var u = e.Spawn(2, CardCatalog.Sovalye, 8, 8);
            u.Hp = 1;
            e.Effects.Apply(EffectCatalog.Yanan, 0, new[] { new Coord(7, 7) }, 3, 0, false, Coord.None);
            e.GiveGe(2, 30);
            e.Ok(TestKit.Ulti(2, UltiCatalog.IyilestiriciYagmur, null));
            Assert.IsTrue(e.Effects.TileHas(new Coord(9, 9), EffectTag.Su));
            Assert.IsFalse(e.Effects.TileHas(new Coord(7, 7), EffectTag.Ates), "Yağmur ateşi söndürür.");
            e.PassRound();
            Assert.AreEqual(2, u.Hp);
        }

        [Test]
        public void KavurucuAtes_RakipCeyregindeCizgi_Yanan3Tur()
        {
            var e = TestKit.NewMatch(new[] { Faction.Gunes, Faction.Ay, Faction.Bulut, Faction.Yildiz });
            e.GiveGe(0, 60);
            var line = Enumerable.Range(6, 6).Select(c => new Coord(4, c)).ToList();
            e.Ok(TestKit.Ulti(0, UltiCatalog.KavurucuAtes, new TargetSpec { Tiles = line }));
            Assert.IsTrue(line.All(t => e.Effects.TileHasEffect(t, EffectCatalog.Yanan)));
            var own = Enumerable.Range(0, 3).Select(c => new Coord(4, c)).ToList();
            Assert.IsNotNull(e.Ultis.ValidateTarget(UltiCatalog.Get(UltiCatalog.KavurucuAtes), 0, new TargetSpec { Tiles = own }), "Kendi çeyreği ateşe verilemez.");
        }

        [Test]
        public void AnkaYukselisi_KonuslanmaAlaninaCagrilir()
        {
            var e = TestKit.NewMatch(new[] { Faction.Gunes, Faction.Ay, Faction.Bulut, Faction.Yildiz });
            e.State.Round = 6;
            e.GiveGe(0, 100);
            e.Ok(TestKit.Ulti(0, UltiCatalog.AnkaYukselisi, TargetSpec.AtTile(new Coord(3, 3))));
            var anka = e.State.Units.Values.Single(u => u.CardId == CardCatalog.AnkaKusu);
            Assert.IsTrue(anka.Flying);
            var foe = e.Spawn(1, CardCatalog.Sovalye, 3, 4);
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(3, foe.Hp, "Anka tur sonunda çevresine 1 hasar verir.");
        }

        [Test]
        public void Bot_50MacHatasizBiter()
        {
            for (int seed = 100; seed < 150; seed++)
            {
                var r = MatchRunner.RunBotMatch(seed);
                Assert.IsNull(r.Error, $"Tohum {seed}: {r.Error}");
                Assert.IsTrue(r.WinnerSeat >= 0);
            }
        }
    }
}
