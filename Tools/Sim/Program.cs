using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DortGok.Core;

namespace DortGok.Sim
{
    /// <summary>
    /// Komutlar:
    ///   sim [maç sayısı] [ilk tohum]   Bot-bot maçları, GDD Bölüm 6 hedef metrikleriyle karşılaştırma
    ///   replay [tohum]                 Tek maçın olay günlüğü
    ///   cards [çıktı.html]             Kâğıt prototip için yazdırılabilir kart sayfası (Faz 0)
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string cmd = args.Length > 0 ? args[0] : "sim";
            switch (cmd)
            {
                case "sim":
                    return RunSim(args.Length > 1 ? int.Parse(args[1]) : 1000, args.Length > 2 ? int.Parse(args[2]) : 1);
                case "replay":
                    return Replay(args.Length > 1 ? int.Parse(args[1]) : 1);
                case "cards":
                    return PrintCards(args.Length > 1 ? args[1] : "kartlar.html");
                case "diag":
                    return Diagnose(args.Length > 1 ? int.Parse(args[1]) : 300);
                default:
                    Console.WriteLine("Kullanım: sim [n] [tohum] | replay [tohum] | cards [dosya.html]");
                    return 1;
            }
        }

        private static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static int RunSim(int count, int seed)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rep = MatchRunner.RunMany(count, seed, null, i =>
            {
                if (i % 100 == 0) Console.Error.Write($"\r{i}/{count} maç");
            });
            Console.Error.WriteLine();
            Console.WriteLine($"Dört Gök bot-bot simülasyonu: {rep.Matches} maç, {sw.Elapsed.TotalSeconds:0.0} sn");
            Console.WriteLine($"Hatalı maç: {rep.Errors}   Zaman aşımı: {rep.TimedOut}");
            Console.WriteLine();
            Console.WriteLine("| Metrik | Ölçülen | GDD hedefi |");
            Console.WriteLine("| --- | --- | --- |");
            string[] fn = { "Ay", "Güneş", "Bulut", "Yıldız" };
            for (int f = 0; f < 4; f++)
                Console.WriteLine($"| {fn[f]} kazanma oranı | {Pct(rep.FactionWinRate[f])} | %25 ± 3 |");
            for (int p = 0; p < 4; p++)
                Console.WriteLine($"| {p + 1}. sıradaki oyuncunun kazanma oranı | {Pct(rep.SeatOrderWinRate[p])} | %25 ± 3 |");
            Console.WriteLine($"| Ortalama maç süresi | {rep.AvgRounds:0.0} raund | 10–14 raund |");
            Console.WriteLine($"| Karargah hasarında ultilerin payı | {Pct(rep.UltiShare)} | En fazla %30 |");
            Console.WriteLine($"| K3'lerin ilk kullanım raundu | {rep.AvgFirstK3Round:0.0} | 7–9 |");
            Console.WriteLine($"| 2 raund içinde 20+ karargah CAN'ı kaybedilen maçlar | {Pct(rep.BurstRate)} | En fazla %5 |");
            foreach (var f in rep.Failed)
            {
                Console.WriteLine();
                Console.WriteLine($"Tohum {f.Seed}: {f.Error}");
            }
            return rep.Errors == 0 ? 0 : 2;
        }

        /// <summary>Fraksiyon başına karargah hasarının kaynağı, eleme raundu, birim ve ulti sayıları.</summary>
        private static int Diagnose(int count)
        {
            string[] fn = { "Ay", "Güneş", "Bulut", "Yıldız" };
            var hqDmg = new System.Collections.Generic.Dictionary<string, double>[4];
            for (int i = 0; i < 4; i++) hqDmg[i] = new System.Collections.Generic.Dictionary<string, double>();
            var elimRound = new double[4]; var elimCount = new int[4];
            var summoned = new double[4]; var lost = new double[4]; var ultis = new double[4]; var wins = new int[4];
            var ultiNames = new System.Collections.Generic.Dictionary<string, int>();
            var killsBy = new double[4];
            for (int m = 0; m < count; m++)
            {
                RulesEngine engine = null;
                engine = RulesEngine.CreateMatch(new GameSetup { Seed = 5000 + m, KeepLog = false }, ev =>
                {
                    var st = engine?.State;
                    if (st == null) return;
                    switch (ev.Type)
                    {
                        case GameEventType.Damaged:
                            for (int s = 0; s < 4; s++)
                                if (st.Hqs[s] != null && st.Hqs[s].Id == ev.EntityId)
                                {
                                    var k = ev.Id ?? "?";
                                    hqDmg[s].TryGetValue(k, out var v);
                                    hqDmg[s][k] = v + ev.Amount;
                                }
                            break;
                        case GameEventType.UnitSummoned:
                            summoned[(int)st.Players[ev.Player].Faction]++;
                            break;
                        case GameEventType.EntityDestroyed:
                            if (st.Units.ContainsKey(ev.EntityId)) break;
                            break;
                        case GameEventType.UltiCast:
                            ultis[(int)st.Players[ev.Player].Faction]++;
                            ultiNames.TryGetValue(ev.Id, out var n);
                            ultiNames[ev.Id] = n + 1;
                            break;
                        case GameEventType.GeChanged:
                            if (ev.Message != null && ev.Message.Contains("rakip birim yok edildi")) killsBy[(int)st.Players[ev.Player].Faction]++;
                            if (ev.Message != null && ev.Message.Contains("birim kaybı")) lost[(int)st.Players[ev.Player].Faction]++;
                            break;
                        case GameEventType.PlayerEliminated:
                            elimRound[(int)st.Players[ev.Player].Faction] += st.Round;
                            elimCount[(int)st.Players[ev.Player].Faction]++;
                            break;
                    }
                });
                var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot(engine, s)).ToArray();
                int guard = 0;
                while (!engine.State.IsOver && guard++ < 4000) bots[engine.State.Current].PlayTurn();
                if (engine.State.WinnerSeat >= 0) wins[(int)engine.State.Players[engine.State.WinnerSeat].Faction]++;
            }
            Console.WriteLine($"{count} maç teşhisi (maç başına ortalamalar)");
            for (int f = 0; f < 4; f++)
            {
                string dmg = string.Join(", ", hqDmg[f].OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / count:0.0}"));
                Console.WriteLine($"{fn[f],-7} galibiyet {wins[f],4} | elenme raundu {(elimCount[f] > 0 ? elimRound[f] / elimCount[f] : 0),5:0.0} | birim {summoned[f] / count,5:0.0} | öldürdü {killsBy[f] / count,5:0.0} | kaybetti {lost[f] / count,5:0.0} | ulti {ultis[f] / count,4:0.0} | karargah hasarı: {dmg}");
            }
            Console.WriteLine("Ulti kullanımı: " + string.Join(", ", ultiNames.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / (double)count:0.00}")));
            return 0;
        }

        private static int Replay(int seed)
        {
            var engine = RulesEngine.CreateMatch(new GameSetup { Seed = seed }, e =>
            {
                if (e.Message != null && e.Type != GameEventType.ApChanged) Console.WriteLine(e.Message);
            });
            var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot(engine, s)).ToArray();
            int guard = 0;
            while (!engine.State.IsOver && guard++ < 4000) bots[engine.State.Current].PlayTurn();
            return 0;
        }

        private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        private static int PrintCards(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Dört Gök kartları</title><style>");
            sb.AppendLine("body{font-family:system-ui,sans-serif;margin:8mm;color:#111;background:#fff}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(3,63mm);gap:4mm}");
            sb.AppendLine(".card{width:63mm;height:88mm;border:1.5px solid #222;border-radius:3mm;padding:3mm;box-sizing:border-box;display:flex;flex-direction:column;gap:1.5mm;break-inside:avoid;font-size:8.5pt}");
            sb.AppendLine(".top{display:flex;justify-content:space-between;align-items:center;font-weight:700;font-size:10pt}.cost{border:1.5px solid #222;border-radius:50%;width:7mm;height:7mm;display:flex;align-items:center;justify-content:center}");
            sb.AppendLine(".meta{font-size:7.5pt;text-transform:uppercase;letter-spacing:.04em;opacity:.75}.stats{font-weight:600}.floop{border-top:1px dashed #555;padding-top:1.5mm}.visual{font-style:italic;opacity:.8}");
            sb.AppendLine(".Ay{border-color:#6b4fa0}.Gunes{border-color:#d9822b}.Bulut{border-color:#4a90c2}.Yildiz{border-color:#1f3b73}.Notr{border-color:#666}");
            sb.AppendLine("h1{font-size:14pt}h2{font-size:12pt;margin-top:8mm}.ulti{border-style:double;border-width:4px}@media print{h1,h2{break-after:avoid}}");
            sb.AppendLine("</style></head><body><h1>Dört Gök — kâğıt prototip kartları</h1>");
            foreach (Faction f in new[] { Faction.Ay, Faction.Gunes, Faction.Bulut, Faction.Yildiz, Faction.Notr })
            {
                sb.AppendLine($"<h2>{Esc(Names.Of(f))}{(f == Faction.Notr ? " kart havuzu" : " Takımı — çekirdek kartlar (3'er kopya bas)")}</h2><div class=\"grid\">");
                foreach (var d in CardCatalog.All.Where(c => c.Faction == f && !c.IsToken))
                {
                    sb.AppendLine($"<div class=\"card {f}\"><div class=\"top\"><span>{Esc(d.Name)}</span><span class=\"cost\">{d.Cost}</span></div>");
                    string sub = Names.Of(d.Type) + (d.Type == CardType.Yapi && d.Role != StructureRole.Diger ? " · " + Names.Of(d.Role) : "") + (d.Type == CardType.Gelistirme ? " · " + Names.Of(d.Slot) : "");
                    sb.AppendLine($"<div class=\"meta\">{Esc(sub)}</div>");
                    if (d.Type != CardType.Gelistirme) sb.AppendLine($"<div class=\"stats\">{Esc(d.StatLine)}</div>");
                    if (!string.IsNullOrEmpty(d.Text)) sb.AppendLine($"<div>{Esc(d.Text)}</div>");
                    if (d.Floop != null) sb.AppendLine($"<div class=\"floop\"><b>Floop ({d.Floop.Cost} AP): {Esc(d.Floop.Name)}</b> — {Esc(d.Floop.Text)}</div>");
                    if (!string.IsNullOrEmpty(d.Visual)) sb.AppendLine($"<div class=\"visual\">{Esc(d.Visual)}</div>");
                    sb.AppendLine("</div>");
                }
                if (f != Faction.Notr)
                    foreach (var u in UltiCatalog.ForFaction(f))
                        sb.AppendLine($"<div class=\"card ulti {f}\"><div class=\"top\"><span>{Esc(u.Name)}</span><span class=\"cost\">{Names.Of(u.Tier)}</span></div><div class=\"meta\">Ulti · {new RulesConfig().TierCost[(int)u.Tier]} GE · Süre: {Esc(u.Duration)}</div><div>{Esc(u.Text)}</div></div>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("<h2>12×12 tahta</h2><p>A4'e yatay bas; her kare 15 mm. Köşelerdeki 2×2 kareler karargah, merkezdeki 2×2 Gök Sunağı.</p>");
            sb.AppendLine("<table style=\"border-collapse:collapse\">");
            for (int r = 0; r < Board.Size; r++)
            {
                sb.Append("<tr>");
                for (int c = 0; c < Board.Size; c++)
                {
                    var co = new Coord(r, c);
                    int q = Board.QuadrantOf(co);
                    string[] tint = { "#efe9f7", "#fbeedd", "#e6f1f9", "#e4e9f3" };
                    string bg = Board.IsAltar(co) ? "#f5d76e" : tint[q];
                    string label = "";
                    for (int s = 0; s < 4; s++) if (Board.HqTiles(s).Contains(co)) { bg = "#999"; label = "KRG"; }
                    sb.Append($"<td style=\"width:15mm;height:15mm;border:1px solid #444;background:{bg};text-align:center;font-size:6pt\">{label}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table></body></html>");
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("Yazıldı: " + Path.GetFullPath(path));
            return 0;
        }
    }
}
