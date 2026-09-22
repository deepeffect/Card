using System;
using System.Linq;

namespace DortGok.Core
{
    /// <summary>
    /// GDD "TurnManager": faz durum makinesi, sıra, eleme. Faz adımları Bölüm 4'teki tablo ile birebir aynıdır.
    /// </summary>
    public sealed class TurnManager
    {
        private readonly RulesEngine _e;

        public TurnManager(RulesEngine engine) { _e = engine; }

        private GameState S => _e.State;
        private RulesConfig R => _e.Rules;

        public int OrderPos(int seat) => (seat - S.FirstSeat + Board.Seats) % Board.Seats;

        // ---------------- Maç kurulumu ----------------

        internal void StartMatch(GameSetup setup)
        {
            S.Phase = Phase.Kurulum;
            S.FirstSeat = setup.FirstSeat >= 0 ? setup.FirstSeat : S.Rng.Next(Board.Seats);
            for (int seat = 0; seat < Board.Seats; seat++)
            {
                var p = new PlayerState
                {
                    Seat = seat,
                    Faction = setup.Factions[seat],
                    Team = R.TeamMode ? seat % 2 : seat
                };
                S.Players[seat] = p;
                var hq = new Headquarters
                {
                    Id = _e.NewEntityId(),
                    Owner = seat,
                    Tiles = Board.HqTiles(seat),
                    Hp = R.HqHp,
                    MaxHp = R.HqHp
                };
                S.Hqs[seat] = hq;
                p.HqId = hq.Id;
                foreach (var t in hq.Tiles) _e.Grid.MarkHq(t, seat);
                foreach (var u in UltiCatalog.ForFaction(p.Faction)) p.UltiCooldown[u.Id] = 0;
            }

            for (int seat = 0; seat < Board.Seats; seat++)
            {
                var p = S.Players[seat];
                p.OrderPos = OrderPos(seat);
                _e.Cards.BuildDeck(p, setup.CustomDecks?[seat], setup.ShuffleDecks);
                _e.Cards.Draw(seat, R.StartingHand);
                // Sıra avantajı telafisi (Bölüm 2)
                if (p.OrderPos == 0) p.SkipFirstDraw = true;
                if (p.OrderPos == 2) p.Ge = Math.Min(R.GeMax, R.Seat3BonusGe);
                if (p.OrderPos == 3) p.PendingBonusAp = R.Seat4BonusAp;
            }

            S.Round = 1;
            _e.Emit(new GameEvent { Type = GameEventType.MatchStarted, Player = S.FirstSeat, Message = $"Maç başladı. İlk oyuncu: {S.Players[S.FirstSeat].Name}." });
            _e.Emit(new GameEvent { Type = GameEventType.RoundStarted, Amount = 1, Message = "Raund 1" });
            BeginTurn(S.FirstSeat);
        }

        // ---------------- Şafak ----------------

        public void BeginTurn(int seat)
        {
            var p = S.Players[seat];
            S.Current = seat;
            S.TurnIndex++;
            p.TurnsTaken++;
            S.Phase = Phase.Safak;
            _e.Emit(new GameEvent { Type = GameEventType.TurnStarted, Player = seat, Amount = p.TurnsTaken, Message = $"— {p.Name} turu başlıyor (Raund {S.Round}) —" });

            // Tur başına bir kez kurallarını sıfırla
            p.UltiUsedThisTurn = false;
            p.ExtraDrawsThisTurn = 0;
            p.ResonanceThisTurn = 0;
            p.FreeMoveUsed = false;
            foreach (var u in S.UnitsOf(seat))
            {
                u.Moved = u.Attacked = u.Defended = false;
                u.FloopsUsed = 0;
                u.RetreatAvailable = false;
            }
            foreach (var s in S.StructuresOf(seat)) s.FloopsUsed = 0;

            // "Sonraki kendi turuna kadar" süren durumlar (Savun, Gölge, Kör, Takımyıldız Zırhı) burada biter.
            foreach (var u in S.Units.Values)
                u.Statuses.RemoveAll(st => st.Expiry == StatusExpiry.StartOfTurn && st.ExpiryPlayer == seat && st.Kind != StatusKind.Burning);

            // Gök Çöküşü (Bölüm 6): 15. raunddan itibaren karargah tur başında artan hasar alır.
            if (S.Round >= R.CollapseStartRound)
            {
                int dmg = 1 + (S.Round - R.CollapseStartRound) / R.CollapseStepRounds;
                _e.Say($"Gök Çöküşü: {p.Name} karargahı {dmg} hasar alıyor.", seat);
                _e.Damage.DealToHq(S.Hqs[seat], dmg, new DamageSource { Player = -1, Kind = DamageKind.Collapse, Label = "Gök Çöküşü" });
                if (Interrupted(seat)) return;
            }

            // 1) Gecikmeli büyüler çözülür (Meteor düşer); yapanın tur başı tetikleri (Hortum, Süper Hücre).
            _e.Ultis.ResolveDelayed(seat);
            _e.Effects.RunCasterTurnStart(seat);
            if (Interrupted(seat)) return;

            // 2) AP yenilenir; birimler +2 EN alır (en fazla 3); dinlenme yapıları.
            p.Ap = R.ApForTurn(p.TurnsTaken) + p.PendingBonusAp;
            p.PendingBonusAp = 0;
            _e.Cards.EmitAp(p);
            foreach (var u in S.UnitsOf(seat).ToList())
            {
                u.En = Math.Min(R.MaxEn, u.En + R.EnRegen);
                foreach (var s in S.Structures.Values)
                {
                    if (!_e.IsAlly(s.Owner, u.Owner) || s.Pos.Chebyshev(u.Pos) != 1) continue;
                    if (s.Def.Passive == CardPassive.DinlenmeKampi)
                    {
                        u.En = Math.Min(R.MaxEn, u.En + 1);
                        _e.Damage.HealUnit(u, 1, s.Def.Name);
                    }
                    else if (s.Def.Passive == CardPassive.BulutDuragi)
                    {
                        u.En = Math.Min(R.MaxEn, u.En + 1);
                    }
                }
            }

            // 3) Oyuncunun birimlerine tur başı zemin etkileri ve Yanık işler.
            _e.Effects.RunOwnerTurnStart(seat);
            foreach (var u in S.UnitsOf(seat).ToList())
            {
                var burn = u.Get(StatusKind.Burning);
                if (burn == null || u.IsDead) continue;
                u.Statuses.Remove(burn);
                _e.Damage.DealToUnit(u, burn.Value, new DamageSource { Player = burn.SourcePlayer, Kind = DamageKind.Passive, Label = "Yanık" });
            }
            if (Interrupted(seat)) return;

            // 4) +5 GE, Sunak kontrolü, Gözlemevi.
            _e.GainGe(seat, R.GeTurnStart, "tur başı");
            if (S.UnitsOf(seat).Any(u => !u.IsIllusion && Board.IsAltar(u.Pos)))
                _e.GainGe(seat, R.GeAltar, "Gök Sunağı");
            foreach (var s in S.StructuresOf(seat))
                if (s.Def.Passive == CardPassive.Gozlemevi) _e.GainGe(seat, 3, "Gözlemevi");

            // 5) Ulti bekleme süreleri azalır; oyuncunun süresi dolan etkileri kalkar.
            foreach (var key in p.UltiCooldown.Keys.ToList())
                if (p.UltiCooldown[key] > 0) p.UltiCooldown[key]--;
            _e.Effects.DecrementAndExpire(seat);
            foreach (var u in S.UnitsOf(seat).Where(x => x.IsIllusion).ToList())
            {
                u.IllusionRemaining--;
                if (u.IllusionRemaining <= 0)
                {
                    _e.Say("Bir İllüzyon kopyası söndü.", seat);
                    u.Hp = 0;
                    _e.Damage.RevealIllusion(u, -1);
                }
            }
            _e.Damage.ResolveDeaths();

            // 6) 1 kart çekilir (ilk oyuncu ilk turunda çekmez).
            if (!(p.SkipFirstDraw && p.TurnsTaken == 1)) _e.Cards.Draw(seat, 1);
            if (Interrupted(seat)) return;

            S.Phase = Phase.AnaFaz;
            _e.Emit(new GameEvent { Type = GameEventType.PhaseChanged, Player = seat, Message = "Ana Faz" });
        }

        /// <summary>Şafak sırasında oyuncu elendiyse ya da oyun bittiyse true.</summary>
        private bool Interrupted(int seat)
        {
            _e.Damage.ResolveDeaths();
            CheckGameOver();
            if (S.IsOver) return true;
            if (!S.Players[seat].Alive)
            {
                AdvanceToNext(seat);
                return true;
            }
            return false;
        }

        // ---------------- Alacakaranlık ----------------

        public void EndTurn(int seat, System.Collections.Generic.List<int> discards)
        {
            var p = S.Players[seat];
            S.Phase = Phase.Alacakaranlik;
            _e.Emit(new GameEvent { Type = GameEventType.PhaseChanged, Player = seat, Message = "Alacakaranlık" });

            // 1) Savunma yapıları otomatik saldırır. 2) Karargah bitişik rakiplere 1 hasar verir.
            _e.Actions.StructureAutoAttacks(seat);
            _e.Actions.HqRetaliation(seat);
            _e.Damage.ResolveDeaths();

            // 3) Tur sonu etkileri: Anka yanması, Tutulma sona erer, Ay'ın Gölge Örtüsü.
            _e.Actions.AnkaBurn(seat);
            _e.Ultis.EndControl(seat);
            if (p.Faction == Faction.Ay && p.Alive)
            {
                foreach (var u in S.UnitsOf(seat))
                    if (!u.Attacked && !u.Has(StatusKind.Shadow))
                        u.Statuses.Add(new Status { Kind = StatusKind.Shadow, SourcePlayer = seat, Expiry = StatusExpiry.StartOfTurn, ExpiryPlayer = seat });
            }
            _e.Damage.ResolveDeaths();

            // 4) El 8 karta indirilir.
            if (p.Alive) _e.Cards.EnforceHandLimit(seat, discards);

            // Bu tura ait durumlar (Hücum, Ok Yağmuru, Vur-Kaç) biter.
            foreach (var u in S.Units.Values)
            {
                u.Statuses.RemoveAll(st => st.Expiry == StatusExpiry.EndOfTurn && st.ExpiryPlayer == seat);
                if (u.Owner == seat) u.RetreatAvailable = false;
            }

            p.K3EchoLock = false;
            var hq = S.Hqs[seat];
            if (hq.LastStandActive && p.TurnsTaken >= hq.LastStandEndsAfterOwnerTurn)
            {
                hq.LastStandEndsAfterOwnerTurn = -1;
                _e.Say($"{p.Name} karargahının Son Siper koruması sona erdi.", seat);
            }

            _e.Emit(new GameEvent { Type = GameEventType.TurnEnded, Player = seat });
            CheckGameOver();
            if (S.IsOver) return;
            AdvanceToNext(seat);
        }

        private void AdvanceToNext(int fromSeat)
        {
            if (S.IsOver) return;
            int next = fromSeat;
            for (int i = 0; i < Board.Seats; i++)
            {
                next = Board.NextClockwise(next);
                if (S.Players[next].Alive) break;
            }
            if (!S.Players[next].Alive) return;
            if (OrderPos(next) <= OrderPos(fromSeat))
            {
                S.Round++;
                _e.Emit(new GameEvent { Type = GameEventType.RoundStarted, Amount = S.Round, Message = $"Raund {S.Round}" });
                if (S.Round > R.SafetyMaxRounds)
                {
                    EndByTimeout();
                    return;
                }
            }
            BeginTurn(next);
        }

        /// <summary>Sıradaki oyuncu kendi turunda elendiyse (ör. Gök Çöküşü) sırayı ilerletir.</summary>
        internal void AdvanceFromEliminatedCurrent()
        {
            if (S.Phase == Phase.AnaFaz || S.Phase == Phase.Alacakaranlik)
                AdvanceToNext(S.Current);
        }

        // ---------------- Eleme ----------------

        /// <summary>Karargahı yıkılan oyuncu elenir; birim ve yapıları kalkar, karargah kareleri Harabe olur.</summary>
        public void Eliminate(int seat, int killer)
        {
            var p = S.Players[seat];
            if (!p.Alive) return;
            var hq = S.Hqs[seat];
            hq.Hp = 0;
            hq.IsDead = true;
            p.Alive = false;
            p.EliminatedRound = S.Round;
            p.EliminationOrder = S.EliminationsSoFar++;

            foreach (var t in hq.Tiles)
            {
                _e.Grid.UnmarkHq(t);
                _e.Grid.SetRuin(t);
            }
            foreach (var u in S.Units.Values.Where(x => x.Owner == seat).ToList())
            {
                u.IsDead = true;
                S.Units.Remove(u.Id);
                _e.Grid.ClearOccupant(u.Pos, u.Id);
                _e.Emit(new GameEvent { Type = GameEventType.EntityDestroyed, Player = seat, EntityId = u.Id, To = u.Pos, Id = u.CardId });
            }
            foreach (var s in S.Structures.Values.Where(x => x.Owner == seat).ToList())
            {
                s.IsDead = true;
                S.Structures.Remove(s.Id);
                _e.Grid.ClearOccupant(s.Pos, s.Id);
                _e.Emit(new GameEvent { Type = GameEventType.EntityDestroyed, Player = seat, EntityId = s.Id, To = s.Pos, Id = s.CardId });
            }
            // Elenen oyuncunun büyüleri de masadan kalkar (GDD'de belirtilmemiş; süreleri onun turlarıyla sayıldığı için).
            _e.Effects.RemoveAllOf(seat);
            // Başkalarının yönettiği birimleri sahiplerine döner.
            foreach (var u in S.Units.Values.Where(x => x.ControlledBy == seat)) u.ControlledBy = -1;

            _e.Emit(new GameEvent { Type = GameEventType.PlayerEliminated, Player = seat, TargetId = killer, Message = $"{p.Name} elendi! Karargah Harabe'ye dönüştü." });

            if (killer >= 0 && _e.IsEnemy(killer, seat) && S.Players[killer].Alive)
                _e.GainGe(killer, R.ConquestGe, "Fetih Ödülü");

            CheckGameOver();
        }

        public void CheckGameOver()
        {
            if (S.IsOver) return;
            var aliveTeams = S.Players.Where(p => p.Alive).Select(p => p.Team).Distinct().ToList();
            if (aliveTeams.Count > 1) return;
            S.Phase = Phase.OyunSonu;
            if (aliveTeams.Count == 1)
            {
                S.WinnerTeam = aliveTeams[0];
                var winners = S.Players.Where(p => p.Alive).ToList();
                S.WinnerSeat = winners.Count == 1 ? winners[0].Seat : winners[0].Seat;
                string names = string.Join(" + ", winners.Select(w => w.Name));
                _e.Emit(new GameEvent { Type = GameEventType.GameOver, Player = S.WinnerSeat, Message = $"Oyun bitti! Kazanan: {names}." });
            }
            else
            {
                _e.Emit(new GameEvent { Type = GameEventType.GameOver, Message = "Oyun bitti: ayakta karargah kalmadı." });
            }
        }

        /// <summary>Simülasyon güvenlik sınırı: en yüksek karargah CAN'ına sahip oyuncu kazanır.</summary>
        private void EndByTimeout()
        {
            var best = S.Players.Where(p => p.Alive).OrderByDescending(p => S.Hqs[p.Seat].Hp).First();
            S.Phase = Phase.OyunSonu;
            S.WinnerTeam = best.Team;
            S.WinnerSeat = best.Seat;
            _e.Emit(new GameEvent { Type = GameEventType.GameOver, Player = best.Seat, Message = "Güvenlik sınırı: maç zaman aşımıyla bitti." });
        }
    }
}
