using System.Linq;
using DortGok.Core;
using NUnit.Framework;

namespace DortGok.Tests
{
    /// <summary>GDD Bölüm 3–4: saldırı, savunma, karşı saldırı, Floop'lar, pasifler, zemin etkileri ve etkileşimler.</summary>
    public class CombatAndEffectTests
    {
        [Test]
        public void GDDOrnegi_AlevSovalyesi_SavunanSovalyeye1Hasar_KarsiSaldiriylaOlur()
        {
            var e = TestKit.NewMatch();
            var alev = e.Spawn(0, CardCatalog.AlevSovalyesi, 3, 3);
            var knight = e.Spawn(1, CardCatalog.Sovalye, 3, 4);
            knight.Statuses.Add(new Status { Kind = StatusKind.Defending, Value = 2, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = 1 });
            e.Ok(new AttackCommand { Player = 0, UnitId = alev.Id, TargetId = knight.Id });
            Assert.AreEqual(3, knight.Hp, "3 − 2 = 1 hasar.");
            Assert.IsFalse(e.State.Units.ContainsKey(alev.Id), "Şövalye 2 hasarla karşılık verir, Alev Şövalyesi (CAN 2) yok olur.");
        }

        [Test]
        public void MenzilliSaldiriya_KarsilikVerilmez_EnAz1Hasar()
        {
            var e = TestKit.NewMatch();
            var archer = e.Spawn(0, CardCatalog.KorOkcusu, 3, 3);
            var knight = e.Spawn(1, CardCatalog.Sovalye, 3, 5);
            knight.Statuses.Add(new Status { Kind = StatusKind.Defending, Value = 2, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = 1 });
            e.Ok(new AttackCommand { Player = 0, UnitId = archer.Id, TargetId = knight.Id });
            Assert.AreEqual(3, knight.Hp, "2 − 2 → en az 1 hasar.");
            Assert.AreEqual(2, archer.Hp);
        }

        [Test]
        public void YapilarKarargahaHasarVeremez_YananKarargahKaresineKonmaz()
        {
            var e = TestKit.NewMatch();
            var mancinik = e.Build(0, CardCatalog.KizginMancinik, 1, 7);
            e.Ok(new FloopCommand { Player = 0, SourceId = mancinik.Id, Target = TargetSpec.AtTile(new Coord(0, 10)) });
            Assert.AreEqual(30, e.Hq(1).Hp);
            Assert.IsFalse(e.Effects.TileHas(new Coord(0, 10), EffectTag.Ates));
        }

        [Test]
        public void KarargahSavunmasi_BitisikRakibe1Hasar()
        {
            var e = TestKit.NewMatch();
            var enemy = e.Spawn(1, CardCatalog.Sovalye, 2, 1);
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(3, enemy.Hp);
        }

        [Test]
        public void OkcuKulesi_TurSonundaEnDusukCanliRakibe2Hasar()
        {
            var e = TestKit.NewMatch();
            e.Build(0, CardCatalog.OkcuKulesi, 3, 3);
            var weak = e.Spawn(1, CardCatalog.GeceBaykusu, 3, 6);
            var strong = e.Spawn(1, CardCatalog.Sovalye, 5, 5);
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.IsFalse(e.State.Units.ContainsKey(weak.Id));
            Assert.AreEqual(4, strong.Hp);
        }

        [Test]
        public void Eleme_HarabeVeFetihOdulu()
        {
            var e = TestKit.NewMatch();
            e.Hq(1).Hp = 1;
            var attacker = e.Spawn(0, CardCatalog.Sovalye, 2, 10);
            var victim = e.Spawn(1, CardCatalog.Sovalye, 4, 8);
            int ge = e.State.Players[0].Ge;
            e.Ok(new AttackCommand { Player = 0, UnitId = attacker.Id, TargetId = e.Hq(1).Id });
            Assert.IsFalse(e.State.Players[1].Alive);
            Assert.IsTrue(e.Grid.IsRuin(new Coord(0, 10)));
            Assert.IsFalse(e.State.Units.ContainsKey(victim.Id));
            Assert.AreEqual(ge + 15, e.State.Players[0].Ge, "Fetih Ödülü +15 GE.");
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(2, e.State.Current, "Elenen oyuncunun sırası atlanır.");
        }

        [Test]
        public void OlumOdulleri_Rakip8_Kendi6()
        {
            var e = TestKit.NewMatch();
            var a = e.Spawn(0, CardCatalog.AlevSovalyesi, 3, 3);
            var owl = e.Spawn(1, CardCatalog.GeceBaykusu, 3, 4);
            int g0 = e.State.Players[0].Ge, g1 = e.State.Players[1].Ge;
            e.Ok(new AttackCommand { Player = 0, UnitId = a.Id, TargetId = owl.Id });
            Assert.AreEqual(g0 + 8, e.State.Players[0].Ge);
            Assert.AreEqual(g1 + 6, e.State.Players[1].Ge);
        }

        [Test]
        public void GolgeOrtusu_SaldirmayanAyBirimi_2KaredenUzaktanHedefAlinamaz()
        {
            var e = TestKit.NewMatch();
            var ay = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.IsTrue(ay.Has(StatusKind.Shadow));
            var far = e.Spawn(1, CardCatalog.KorOkcusu, 3, 6);
            var near = e.Spawn(1, CardCatalog.KorOkcusu, 5, 5);
            Assert.IsFalse(e.Actions.AttackTargets(far).Contains(ay));
            Assert.IsTrue(e.Actions.AttackTargets(near).Contains(ay));
        }

        [Test]
        public void GizliBirim_HedefAlinamaz_SaldirincaBozulur_HilalHancer2Ek()
        {
            var e = TestKit.NewMatch();
            var knight = e.Spawn(0, CardCatalog.GolgeSovalyesi, 3, 3);
            e.Cards.Attach(knight, new CardInstance { InstanceId = 900, CardId = CardCatalog.HilalHancer });
            e.Ok(new FloopCommand { Player = 0, SourceId = knight.Id });
            Assert.IsTrue(knight.Has(StatusKind.Hidden));
            var enemy = e.Spawn(1, CardCatalog.KumulusDevi, 3, 4);
            Assert.IsFalse(e.CanBeTargeted(knight, 1, enemy.Pos, false));
            e.Ok(new AttackCommand { Player = 0, UnitId = knight.Id, TargetId = enemy.Id });
            Assert.AreEqual(8 - (2 + 1 + 2), enemy.Hp, "SAL 2 + Hançer 1 + Gizli 2.");
            Assert.IsFalse(knight.Has(StatusKind.Hidden));
        }

        [Test]
        public void Isaret_SonrakiSaldiri2EkVeZirhYoksayar()
        {
            var e = TestKit.NewMatch();
            var owl = e.Spawn(0, CardCatalog.GeceBaykusu, 3, 1);
            var knight = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            var target = e.Spawn(1, CardCatalog.Sovalye, 3, 4);
            target.Statuses.Add(new Status { Kind = StatusKind.Defending, Value = 2, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = 1 });
            e.Ok(new FloopCommand { Player = 0, SourceId = owl.Id, Target = TargetSpec.AtEntity(target.Id) });
            e.Ok(new AttackCommand { Player = 0, UnitId = knight.Id, TargetId = target.Id });
            Assert.AreEqual(0, target.Hp <= 0 ? 0 : target.Hp);
        }

        [Test]
        public void Illuzyon_KopyayaSaldiraninENi0_KopyaDagilir()
        {
            var e = TestKit.NewMatch();
            var original = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.GiveGe(0, 30);
            e.Ok(TestKit.Ulti(0, UltiCatalog.Illuzyon, TargetSpec.AtEntity(original.Id)));
            var copies = e.State.Units.Values.Where(u => u.IsIllusion).ToList();
            Assert.AreEqual(2, copies.Count);
            Assert.IsTrue(copies.All(c => c.Pos.Chebyshev(original.Pos) == 1 && c.Hp == 1));
            e.Ok(new EndTurnCommand { Player = 0 });
            var copy = copies[0];
            var attacker = e.Spawn(1, CardCatalog.Sovalye, copy.Pos.Row + (copy.Pos.Row > 0 ? -1 : 1), copy.Pos.Col);
            if (!e.Actions.AttackTargets(attacker).Contains(copy)) Assert.Inconclusive("Kurgu kopyaya bitişik değil.");
            e.Ok(new AttackCommand { Player = 1, UnitId = attacker.Id, TargetId = copy.Id });
            Assert.IsFalse(e.State.Units.ContainsKey(copy.Id));
            Assert.AreEqual(0, attacker.En);
        }

        [Test]
        public void KorIzi_GunesBirimiOldurunceKareyeKor()
        {
            var e = TestKit.NewMatch(new[] { Faction.Gunes, Faction.Ay, Faction.Bulut, Faction.Yildiz });
            var alev = e.Spawn(0, CardCatalog.AlevSovalyesi, 3, 3);
            var owl = e.Spawn(1, CardCatalog.GeceBaykusu, 3, 4);
            e.Ok(new AttackCommand { Player = 0, UnitId = alev.Id, TargetId = owl.Id });
            Assert.IsTrue(e.Effects.TileHasEffect(new Coord(3, 4), EffectCatalog.Kor));
        }

        [Test]
        public void GunesKilici_Yanik_SonrakiTurBasinda1Hasar()
        {
            var e = TestKit.NewMatch();
            var knight = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.Cards.Attach(knight, new CardInstance { InstanceId = 901, CardId = CardCatalog.GunesKilici });
            var target = e.Spawn(1, CardCatalog.KumulusDevi, 3, 4);
            e.Ok(new AttackCommand { Player = 0, UnitId = knight.Id, TargetId = target.Id });
            Assert.AreEqual(8 - 4, target.Hp);
            Assert.IsTrue(target.Has(StatusKind.Burning));
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(3, target.Hp);
            Assert.IsFalse(target.Has(StatusKind.Burning));
        }

        [Test]
        public void FirtinaMizragi_1KareIter_MenzilliOldugundanKarsiSaldiriYok()
        {
            var e = TestKit.NewMatch();
            var rider = e.Spawn(0, CardCatalog.RuzgarSuvarisi, 3, 3);
            e.Cards.Attach(rider, new CardInstance { InstanceId = 902, CardId = CardCatalog.FirtinaMizragi });
            var target = e.Spawn(1, CardCatalog.KumulusDevi, 3, 5);
            e.Ok(new AttackCommand { Player = 0, UnitId = rider.Id, TargetId = target.Id });
            Assert.AreEqual(new Coord(3, 6), target.Pos);
            Assert.AreEqual(2, rider.Hp);
        }

        [Test]
        public void KumulusDevi_BitisikDostaMenzilliHasarEksi1()
        {
            var e = TestKit.NewMatch();
            e.Spawn(1, CardCatalog.KumulusDevi, 3, 5);
            var buddy = e.Spawn(1, CardCatalog.Sovalye, 3, 4);
            var archer = e.Spawn(0, CardCatalog.KorOkcusu, 3, 2);
            e.Ok(new AttackCommand { Player = 0, UnitId = archer.Id, TargetId = buddy.Id });
            Assert.AreEqual(3, buddy.Hp, "2 − 1 = 1 hasar.");
        }

        [Test]
        public void NovaGolemi_OlunceCevresine2Hasar_DostVeDusman()
        {
            var e = TestKit.NewMatch();
            var nova = e.Spawn(3, CardCatalog.NovaGolemi, 4, 4);
            var friend = e.Spawn(3, CardCatalog.Sovalye, 4, 5);
            var foe = e.Spawn(0, CardCatalog.Sovalye, 3, 4);
            e.Damage.DealToUnit(nova, 20, new DamageSource { Player = 0, Kind = DamageKind.Attack });
            e.Damage.ResolveDeaths();
            Assert.AreEqual(2, friend.Hp);
            Assert.AreEqual(2, foe.Hp);
        }

        [Test]
        public void Anka_IlkOlumundeAyniKarede3CanlaDogar()
        {
            var e = TestKit.NewMatch();
            var anka = e.Cards.SpawnUnit(1, CardCatalog.AnkaKusu, new Coord(4, 8), -1);
            e.Damage.DealToUnit(anka, 20, new DamageSource { Player = 0, Kind = DamageKind.Attack });
            e.Damage.ResolveDeaths();
            Assert.IsTrue(e.State.Units.ContainsKey(anka.Id));
            Assert.AreEqual(3, anka.Hp);
            Assert.AreEqual(new Coord(4, 8), anka.Pos);
            e.Damage.DealToUnit(anka, 20, new DamageSource { Player = 0, Kind = DamageKind.Attack });
            e.Damage.ResolveDeaths();
            Assert.IsFalse(e.State.Units.ContainsKey(anka.Id));
        }

        [Test]
        public void KivilcimFeda_Cevreye2HasarVeKor()
        {
            var e = TestKit.NewMatch();
            var spark = e.Spawn(0, CardCatalog.KivilcimKosucusu, 4, 4);
            var foe = e.Spawn(1, CardCatalog.Sovalye, 4, 5);
            e.Ok(new FloopCommand { Player = 0, SourceId = spark.Id });
            Assert.IsFalse(e.State.Units.ContainsKey(spark.Id));
            Assert.AreEqual(2, foe.Hp);
            Assert.IsTrue(e.Effects.TileHasEffect(new Coord(3, 3), EffectCatalog.Kor));
        }

        [Test]
        public void YildizPasifi_KozmikRezonans_FloopBasina3_TurdaEnFazla9()
        {
            var e = TestKit.NewMatch(new[] { Faction.Yildiz, Faction.Ay, Faction.Bulut, Faction.Gunes });
            var p = e.State.Players[0];
            int ge = p.Ge;
            for (int i = 0; i < 4; i++)
            {
                var apprentice = e.Spawn(0, CardCatalog.YildizCiragi, 2 + i, 4);
                e.Spawn(1, CardCatalog.KumulusDevi, 2 + i, 6);
                e.GiveAp(0, 5);
                var target = e.Actions.AttackTargets(apprentice).First();
                e.Ok(new FloopCommand { Player = 0, SourceId = apprentice.Id, Target = TargetSpec.AtEntity(target.Id) });
            }
            Assert.AreEqual(ge + 9, p.Ge);
        }

        [Test]
        public void BulutPasifi_ArkaRuzgar_IlkHareketENHarcamaz()
        {
            var e = TestKit.NewMatch(new[] { Faction.Bulut, Faction.Ay, Faction.Gunes, Faction.Yildiz });
            var a = e.Spawn(0, CardCatalog.Sovalye, 3, 3, en: 0);
            var b = e.Spawn(0, CardCatalog.Sovalye, 4, 4, en: 0);
            e.Ok(new MoveCommand { Player = 0, UnitId = a.Id, To = new Coord(3, 4) });
            Assert.AreEqual(0, a.En);
            e.Fails(new MoveCommand { Player = 0, UnitId = b.Id, To = new Coord(4, 5) });
        }

        [Test]
        public void DinlenmeKampi_BitisigeEkEN_VeCan()
        {
            var e = TestKit.NewMatch();
            e.Build(0, CardCatalog.DinlenmeKampi, 3, 3);
            var u = e.Spawn(0, CardCatalog.Sovalye, 3, 4, en: 0);
            u.Hp = 2;
            e.PassRound();
            Assert.AreEqual(3, u.En, "+2 tur başı +1 kamp.");
            Assert.AreEqual(3, u.Hp);
        }

        [Test]
        public void Islak_ArtiYanan_IkisiKalkar_BuharOlur()
        {
            var e = TestKit.NewMatch();
            var c = new Coord(4, 4);
            e.Effects.Apply(EffectCatalog.Yanan, 1, new[] { c }, 3, 0, false, Coord.None);
            Assert.IsTrue(e.Effects.TileHas(c, EffectTag.Ates));
            e.Effects.Apply(EffectCatalog.Islak, 2, new[] { c }, 2, 0, false, Coord.None);
            Assert.IsFalse(e.Effects.TileHas(c, EffectTag.Ates));
            Assert.IsFalse(e.Effects.TileHas(c, EffectTag.Su));
            Assert.IsTrue(e.Effects.TileHasEffect(c, EffectCatalog.Buhar));
        }

        [Test]
        public void AyniEtkiAyniKareye_SureYenilenir_UstUsteBinmez()
        {
            var e = TestKit.NewMatch();
            var c = new Coord(4, 4);
            e.Effects.Apply(EffectCatalog.Yanan, 1, new[] { c }, 3, 0, false, Coord.None);
            e.Effects.Apply(EffectCatalog.Yanan, 1, new[] { c }, 1, 0, false, Coord.None);
            var onTile = e.Effects.At(c).Where(f => f.Def.Id == EffectCatalog.Yanan).ToList();
            Assert.AreEqual(1, onTile.Count);
            Assert.AreEqual(1, onTile[0].RemainingTurns);
        }

        [Test]
        public void Yanan_GirisVeTurBasi_TurBasinaBirKez()
        {
            var e = TestKit.NewMatch();
            e.Effects.Apply(EffectCatalog.Yanan, 1, new[] { new Coord(3, 4), new Coord(3, 5) }, 3, 0, false, Coord.None);
            var u = e.Spawn(0, CardCatalog.Sovalye, 3, 3);
            e.Ok(new MoveCommand { Player = 0, UnitId = u.Id, To = new Coord(2, 4) });
            Assert.AreEqual(4, u.Hp, "Yol bulucu yanan kareleri dolanır.");
            var v = e.Spawn(0, CardCatalog.KorOkcusu, 4, 4);
            e.Ok(new MoveCommand { Player = 0, UnitId = v.Id, To = new Coord(3, 4) });
            Assert.AreEqual(0, v.Hp <= 0 ? 0 : v.Hp, "Kor Okçusu (CAN 2) yanan kareye girince 2 hasar alır.");
        }

        [Test]
        public void ZehirliSis_RakibinTurBasinda1Hasar_MNZ1()
        {
            var e = TestKit.NewMatch();
            var archer = e.Spawn(1, CardCatalog.KorOkcusu, 2, 7);
            e.Effects.Apply(EffectCatalog.ZehirliSis, 0, Board.Rect(new Coord(1, 6), 4, 4), 3, 1, true, Coord.None);
            Assert.AreEqual(1, e.Stats.Range(archer));
            e.Ok(new EndTurnCommand { Player = 0 });
            Assert.AreEqual(1, archer.Hp);
        }

        [Test]
        public void HortumCekimAlaninda_Savurmaz()
        {
            var e = TestKit.NewMatch(new[] { Faction.Bulut, Faction.Ay, Faction.Gunes, Faction.Yildiz });
            var pinned = e.Spawn(1, CardCatalog.Sovalye, 2, 8);
            e.Effects.Apply(EffectCatalog.Cekim, 0, new[] { pinned.Pos }, 2, 1, true, Coord.None);
            var free = e.Spawn(1, CardCatalog.Sovalye, 3, 7);
            e.GiveGe(0, 60);
            e.Ok(TestKit.Ulti(0, UltiCatalog.Hortum, new TargetSpec { Tile = new Coord(1, 7), Direction = Coord.Down }));
            Assert.AreEqual(new Coord(2, 8), pinned.Pos);
            Assert.AreEqual(4, pinned.Hp);
            Assert.AreNotEqual(new Coord(3, 7), free.Pos, "Serbest birim savrulur.");
            Assert.AreEqual(3, free.Hp);
        }

        [Test]
        public void KavurucuAtes_SekilDogrulama()
        {
            Assert.IsTrue(UltiSystem.IsValidFireShape(Enumerable.Range(0, 6).Select(c => new Coord(3, 6 + c)).ToList(), 6));
            Assert.IsTrue(UltiSystem.IsValidFireShape(Board.Rect(new Coord(2, 7), 2, 3), 6));
            Assert.IsTrue(UltiSystem.IsValidFireShape(Board.Rect(new Coord(2, 7), 3, 2), 6));
            Assert.IsFalse(UltiSystem.IsValidFireShape(Enumerable.Range(0, 7).Select(c => new Coord(3, c)).ToList(), 6));
            Assert.IsFalse(UltiSystem.IsValidFireShape(new[] { new Coord(3, 6), new Coord(3, 8) }.ToList(), 6));
            Assert.IsFalse(UltiSystem.IsValidFireShape(new[] { new Coord(3, 6), new Coord(4, 7) }.ToList(), 6));
        }
    }
}
