using System;
using System.Collections.Generic;
using System.Linq;

namespace DortGok.Core
{
    public sealed class MatchResult
    {
        public int Seed;
        public int WinnerSeat = -1;
        public Faction WinnerFaction;
        public int WinnerOrderPos = -1;
        public int Rounds;
        public int TotalHqDamage;
        public int UltiHqDamage;
        public int FirstK3Round = -1;
        public bool Burst20In2Rounds;
        public bool TimedOut;
        public int Commands;
        public string Error;
    }

    /// <summary>Bot-bot maçları (GDD Bölüm 6: her sürümde binlerce maç simüle edilir).</summary>
    public static class MatchRunner
    {
        public static MatchResult RunBotMatch(int seed, RulesConfig rules = null, Faction[] factions = null)
        {
            var result = new MatchResult { Seed = seed };
            try
            {
                var setup = new GameSetup { Seed = seed, Rules = rules?.Clone() ?? new RulesConfig(), KeepLog = false };
                if (factions != null) setup.Factions = factions;
                var engine = RulesEngine.CreateMatch(setup);
                var bots = Enumerable.Range(0, Board.Seats).Select(s => new SimpleBot(engine, s)).ToArray();
                var s0 = engine.State;
                int guard = 0;
                while (!s0.IsOver && guard++ < 4000)
                {
                    int before = s0.TurnIndex;
                    bots[s0.Current].PlayTurn();
                    if (!s0.IsOver && s0.TurnIndex == before)
                    {
                        // Bot turu bitiremediyse zorla bitir (hata sayılır).
                        var r = engine.Execute(new EndTurnCommand { Player = s0.Current });
                        if (!r.Ok) { result.Error = "Tur bitirilemedi: " + r.Error; break; }
                        result.Error = result.Error ?? "Bot turu kendisi bitirmedi.";
                    }
                }
                if (!s0.IsOver && result.Error == null) result.Error = "Maç sonlanmadı.";

                result.Rounds = s0.Round;
                result.WinnerSeat = s0.WinnerSeat;
                if (s0.WinnerSeat >= 0)
                {
                    result.WinnerFaction = s0.Players[s0.WinnerSeat].Faction;
                    result.WinnerOrderPos = s0.Players[s0.WinnerSeat].OrderPos;
                }
                result.TotalHqDamage = s0.Stats.TotalHqDamage;
                result.UltiHqDamage = s0.Stats.UltiHqDamage;
                result.FirstK3Round = s0.Stats.FirstK3Round;
                result.Burst20In2Rounds = s0.Stats.HadBurst(20);
                result.TimedOut = s0.Round > s0.Rules.SafetyMaxRounds;
                result.Commands = s0.Stats.Commands;
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
            }
            return result;
        }

        public sealed class Report
        {
            public int Matches;
            public int Errors;
            public int TimedOut;
            public double[] FactionWinRate = new double[4];
            public double[] SeatOrderWinRate = new double[4];
            public double AvgRounds;
            public double UltiShare;
            public double AvgFirstK3Round;
            public double BurstRate;
            public List<MatchResult> Failed = new List<MatchResult>();
        }

        public static Report RunMany(int count, int firstSeed, RulesConfig rules = null, Action<int> progress = null)
        {
            var results = new List<MatchResult>(count);
            for (int i = 0; i < count; i++)
            {
                results.Add(RunBotMatch(firstSeed + i, rules));
                progress?.Invoke(i + 1);
            }
            return Summarize(results);
        }

        public static Report Summarize(List<MatchResult> results)
        {
            var rep = new Report { Matches = results.Count };
            var ok = results.Where(r => r.Error == null).ToList();
            rep.Errors = results.Count - ok.Count;
            rep.Failed = results.Where(r => r.Error != null).Take(10).ToList();
            rep.TimedOut = ok.Count(r => r.TimedOut);
            var decided = ok.Where(r => r.WinnerSeat >= 0).ToList();
            if (decided.Count > 0)
            {
                for (int f = 0; f < 4; f++) rep.FactionWinRate[f] = decided.Count(r => (int)r.WinnerFaction == f) / (double)decided.Count;
                for (int p = 0; p < 4; p++) rep.SeatOrderWinRate[p] = decided.Count(r => r.WinnerOrderPos == p) / (double)decided.Count;
            }
            if (ok.Count > 0)
            {
                rep.AvgRounds = ok.Average(r => r.Rounds);
                int total = ok.Sum(r => r.TotalHqDamage);
                rep.UltiShare = total > 0 ? ok.Sum(r => r.UltiHqDamage) / (double)total : 0;
                var k3 = ok.Where(r => r.FirstK3Round > 0).ToList();
                rep.AvgFirstK3Round = k3.Count > 0 ? k3.Average(r => r.FirstK3Round) : 0;
                rep.BurstRate = ok.Count(r => r.Burst20In2Rounds) / (double)ok.Count;
            }
            return rep;
        }
    }
}
