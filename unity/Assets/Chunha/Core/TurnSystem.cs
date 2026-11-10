using System;
using System.Collections.Generic;
using System.Linq;
using static Chunha.Core.G;

namespace Chunha.Core
{
    public static class TurnSystem
    {
        public static bool IsFrontier(GameState s, City c)
            => Neighbors(s, c.id).Any(n => { var nc = s.City(n); return !Same(nc.faction, c.faction) && IsHostile(s, c.faction, nc.faction); });

        static double Threat(GameState s, City c)
        {
            double t = 0;
            foreach (var n in Neighbors(s, c.id))
            {
                var nc = s.City(n);
                if (!None(nc.faction) && nc.faction != c.faction && IsHostile(s, c.faction, nc.faction)) t = Math.Max(t, nc.troops);
            }
            return t;
        }

        /// <summary>도시 운영 방침에 따라 남은 장수에게 일을 맡긴다 (플레이어 위임·AI 공용)</summary>
        public static void AutoDomestic(GameState s, City c, string mode)
        {
            if (None(c.faction) || mode == Policy.Direct) return;
            var f = c.faction;
            List<Officer> Idle() => CityOfficers(s, c.id, f).Where(o => !o.acted).ToList();
            int guard = 0;
            while (Idle().Count > 0 && guard++ < 10)
            {
                var os = Idle();
                var fr = IsFrontier(s, c);
                var thr = Threat(s, c);
                Dom cmd;
                var rich = c.gold > 1500 && c.troops < c.pop * 400 - 1500;
                if (mode == Policy.Military || (mode == Policy.Balanced && ((fr && c.troops < thr * 1.2) || rich)))
                {
                    if (c.troops < c.pop * 400 - 500 && c.gold > 400) cmd = Dom.Draft;
                    else if (c.training < 80 && c.troops > 2000) cmd = Dom.Train;
                    else if (c.wall < 90 && c.gold > 100) cmd = Dom.Wall;
                    else cmd = Dom.Train;
                }
                else if (mode == Policy.Welfare)
                    cmd = c.loyalty < 90 && c.food > 3000 ? Dom.Relief : c.flood < 80 ? Dom.Dike : Dom.Trade;
                else if (mode == Policy.Develop)
                    cmd = c.agri <= c.commerce ? Dom.Farm : Dom.Trade;
                else
                {
                    if (c.loyalty < 55 && c.food > 3000) cmd = Dom.Relief;
                    else if (c.flood < 40) cmd = Dom.Dike;
                    else if (c.training < 60 && c.troops > 3000 && fr) cmd = Dom.Train;
                    else cmd = c.agri <= c.commerce ? Dom.Farm : Dom.Trade;
                }
                var statKey = cmd == Dom.Relief || cmd == Dom.Draft ? Stat.Cha : cmd == Dom.Train ? Stat.Land : Stat.Pol;
                var o = Best(os, statKey)[0];
                var r = Commands.Domestic(s, f, o.id, cmd);
                if (!r.ok)
                {
                    var alt = c.troops > 1000 && c.training < 100 ? Dom.Train : Dom.Farm;
                    var r2 = Commands.Domestic(s, f, o.id, alt);
                    if (!r2.ok) o.acted = true;
                }
            }
        }

        static void AiFaction(GameState s, string f)
        {
            var cities = FactionCities(s, f);
            // 1) 인재
            foreach (var c in cities)
            {
                var os = CityOfficers(s, c.id, f).Where(o => !o.acted).ToList();
                if (os.Count == 0) continue;
                var found = FreeOfficers(s, c.id).Where(o => o.found && o.special != "thrice_visit").ToList();
                if (found.Count > 0) Commands.Recruit(s, f, Best(os, Stat.Cha)[0].id, found[0].id);
                else if (Chance(s, 0.25) && FreeOfficers(s, c.id).Count > 0) Commands.Search(s, f, Best(os, Stat.Int)[0].id);
            }
            // 2) 포로
            foreach (var cap in s.captives.Where(x => x.by == f).ToList())
            {
                var t = s.Officer(cap.officer);
                var fc = FactionCities(s, f);
                var c = fc.Count > 0 ? fc[0] : null;
                var os = c != null ? CityOfficers(s, c.id, f).Where(o => !o.acted).ToList() : new List<Officer>();
                if (os.Count > 0 && Chance(s, 0.6))
                {
                    t.city = c.id;
                    var r = Commands.Recruit(s, f, Best(os, Stat.Cha)[0].id, t.id);
                    if (r.ok && t.status == OfficerStatus.Serving) continue;
                }
                Commands.ReleaseCaptive(s, f, t.id, Chance(s, 0.1));
            }
            // 3) 공격
            foreach (var c in FactionCities(s, f))
            {
                var os = CityOfficers(s, c.id, f).Where(o => !o.acted).ToList();
                if (os.Count == 0 || c.troops < 6000) continue;
                var grace = AbsMonth(s) - ScenarioYear * 12 < GraceMonths;
                var targets = Neighbors(s, c.id).Select(s.City)
                    .Where(t => t.faction != f && IsHostile(s, f, t.faction) && !(grace && t.faction == s.player)).ToList();
                if (targets.Count == 0) continue;
                var myLead = Best(os, Stat.War).Take(3).ToList();
                var myPow = c.troops * 0.8 * (0.6 + c.training / 200) * (0.7 + (myLead.Count > 0 ? myLead[0].stats.war : 50) / 300.0);
                var scored = targets.Select(t =>
                {
                    var dl = !None(t.faction) ? BattleSystem.Leader(s, CityOfficers(s, t.id, t.faction).Select(o => o.id)) : null;
                    var defPow = Math.Max(500, t.troops) * (0.6 + t.training / 200) * (0.7 + (dl?.stats.war ?? 40) / 300.0) * (1 + t.wall / 200);
                    return (t, ratio: myPow / defPow);
                }).OrderByDescending(x => x.ratio).ToList();
                var pickT = scored[0];
                var others = Neighbors(s, c.id).Select(s.City)
                    .Where(n => n.id != pickT.t.id && !None(n.faction) && n.faction != f && IsHostile(s, f, n.faction)).Select(n => n.troops * 0.6).ToList();
                var otherThreat = Math.Max(0, others.Count > 0 ? others.Max() : 0);
                var troops = Math.Floor(Math.Min(c.troops * 0.8, c.troops - otherThreat));
                if (pickT.ratio * (troops / (c.troops * 0.8)) > 1.15 && troops >= 3000 && Chance(s, 0.5))
                {
                    var args = new AttackArgs { from = c.id, to = pickT.t.id, officers = myLead.Select(o => o.id).ToList(), troops = troops };
                    if (BattleSystem.CanAttack(s, f, args) == null)
                    {
                        var b = BattleSystem.Start(s, f, args);
                        BattleSystem.AutoResolve(s, b);
                    }
                }
            }
            // 4) 후방 → 전방
            foreach (var c in FactionCities(s, f))
            {
                if (IsFrontier(s, c) || c.troops < 6000) continue;
                var os = CityOfficers(s, c.id, f).Where(o => !o.acted).ToList();
                if (os.Count == 0) continue;
                var dst = Neighbors(s, c.id).Select(s.City).Where(n => n.faction == f && IsFrontier(s, n)).ToList();
                if (dst.Count > 0) Commands.Transport(s, f, os[0].id, Pick(s, dst).id, Math.Floor(c.troops * 0.5), 0, Math.Floor(c.food * 0.3));
            }
            // 5) 외교
            if (Chance(s, 0.08))
            {
                var others = s.factions.Where(x => x.alive && x.id != f && x.id != s.player).ToList();
                var t = others.Count > 0 ? Pick(s, others) : null;
                var rel = t != null ? RelationOf(s, f, t.id) : null;
                var c = FactionCities(s, f).FirstOrDefault(x => x.gold > 600);
                var o = c != null ? Best(CityOfficers(s, c.id, f).Where(x => !x.acted), Stat.Cha).FirstOrDefault() : null;
                if (t != null && rel != null && rel.alliance <= 0 && rel.favor > 45 && o != null) Commands.ProposeAlliance(s, f, o.id, t.id);
            }
            // 6) 내정
            foreach (var c in FactionCities(s, f)) AutoDomestic(s, c, Policy.Balanced);
        }

        static void Economy(GameState s)
        {
            foreach (var c in s.cities)
            {
                var tax = Round(c.commerce / 10 * (0.5 + c.loyalty / 200));
                if (!None(c.faction)) c.gold += tax;
                if (s.month == 7)
                {
                    var harvest = Round(c.agri * 40 * (0.5 + c.loyalty / 200));
                    c.food += harvest;
                    if (c.faction == s.player)
                    {
                        Report(s, $"{c.name}에서 병량 {Fmt(harvest)}을(를) 거두었다.");
                        s.Emit(new GameEvent { kind = GameEventKind.Harvest, city = c.id, faction = c.faction });
                    }
                }
                var eat = Round(c.troops / 60);
                c.food -= eat;
                if (c.food < 0)
                {
                    var lost = Round(c.troops * 0.1);
                    c.troops -= lost;
                    c.food = 0;
                    c.loyalty = Clamp(c.loyalty - 3, 0, 100);
                    if (c.faction == s.player) Report(s, $"{c.name}의 병량이 떨어져 병사 {Fmt(lost)}이(가) 흩어졌다.");
                }
                if (c.loyalty > 70) c.loyalty -= 1;
                if (c.loyalty < 40) c.loyalty += 1;
                c.pop = Clamp(c.pop + (c.loyalty > 60 ? 0.2 : -0.1), 20, 200);
            }
        }

        public static string EventName(string kind) => kind == "flood" ? "홍수" : kind == "locust" ? "메뚜기" : "역병";
        static string Warn(string k) => k == "locust" ? "메뚜기 떼가 일어날 조짐이 있다" : k == "flood" ? "강물이 불어나고 있다" : "역병의 소문이 돈다";
        static string Hit(string k) => k == "locust" ? "메뚜기 떼가 들판을 휩쓸었다" : k == "flood" ? "큰물이 졌다" : "역병이 돌았다";

        static void Events(GameState s)
        {
            var now = AbsMonth(s);
            foreach (var e in s.pending.Where(p => p.month <= now).ToList())
            {
                var c = s.City(e.city);
                if (e.kind == "locust") { c.food = Round(c.food * 0.6); c.agri = Round(c.agri * 0.95); }
                if (e.kind == "flood")
                {
                    var m = 1 - (1 - c.flood / 100) * 0.25;
                    c.agri = Round(c.agri * m); c.commerce = Round(c.commerce * m);
                    c.wall = Clamp(c.wall - Round((100 - c.flood) / 10), 0, 100);
                }
                if (e.kind == "plague") { c.troops = Round(c.troops * 0.9); c.pop = c.pop * 0.95; }
                if (c.faction == s.player) Report(s, $"{s.month}월, {c.name}에 {Hit(e.kind)}.");
                s.Emit(new GameEvent { kind = GameEventKind.Disaster, city = c.id, text = e.kind });
            }
            s.pending.RemoveAll(p => p.month <= now);
            foreach (var c in s.cities)
            {
                if (!Chance(s, 0.025)) continue;
                var kinds = c.river ? new[] { "flood", "flood", "locust", "plague" } : new[] { "locust", "plague" };
                var kind = Pick(s, kinds);
                s.pending.Add(new PendingEvent { kind = kind, city = c.id, month = now + 1 });
                if (c.faction == s.player) Report(s, $"{c.name}: {Warn(kind)}. 다음 달을 대비하라.");
                s.Emit(new GameEvent { kind = GameEventKind.DisasterWarning, city = c.id, text = kind });
            }
        }

        static void LoyaltyAndDefection(GameState s)
        {
            foreach (var o in s.officers)
            {
                if (o.status != OfficerStatus.Serving || None(o.faction) || IsRuler(s, o)) continue;
                var ruler = s.Officer(s.Faction(o.faction).ruler);
                var aff = Math.Min(Math.Abs(ruler.affinity - o.affinity), 150 - Math.Abs(ruler.affinity - o.affinity));
                if (aff < 15 && o.loyalty < 100 && Chance(s, 0.2)) o.loyalty++;
                if (o.loyalty < 40 && o.ambition > 50 && Chance(s, 0.15))
                {
                    var f = o.faction;
                    o.status = OfficerStatus.Free; o.faction = ""; o.found = true;
                    var c = s.City(o.city);
                    if (c.governor == o.id) c.governor = "";
                    if (f == s.player) Report(s, $"{o.name}이(가) 충성을 저버리고 떠났다.");
                    AddLog(s, $"{o.name} 출분 ({FactionName(s, f)})", f == s.player ? "bad" : "info");
                    s.Emit(new GameEvent { kind = GameEventKind.OfficerLeft, city = c.id, faction = f, other = o.id });
                }
            }
            foreach (var c in s.cities)
            {
                if (None(c.faction)) continue;
                var os = CityOfficers(s, c.id, c.faction);
                if (None(c.governor) || !os.Any(o => o.id == c.governor)) c.governor = os.Count > 0 ? os[0].id : "";
            }
        }

        static void RelationsTick(GameState s)
        {
            foreach (var r in s.relations)
            {
                if (r.alliance > 0)
                {
                    r.alliance--;
                    if (r.alliance == 0 && (r.a == s.player || r.b == s.player)) Report(s, $"{FactionName(s, r.a == s.player ? r.b : r.a)}과(와)의 동맹이 끝났다.");
                }
                if (r.truce > 0) r.truce--;
            }
        }

        static string HistorianLine(GameState s)
        {
            var mine = FactionCities(s, s.player);
            var troops = mine.Sum(c => c.troops);
            var gold = mine.Sum(c => c.gold);
            var line = $"{s.year}년 {s.month}월. {FactionName(s, s.player)}은(는) {mine.Count}성, 병력 {Fmt(troops)}, 금 {Fmt(gold)}을 헤아린다.";
            var strongest = s.factions.Where(f => f.alive && f.id != s.player)
                .Select(f => (f, n: FactionCities(s, f.id).Count)).OrderByDescending(x => x.n).FirstOrDefault();
            if (strongest.f != null) line += $" 가장 큰 적은 {strongest.f.name}({strongest.n}성)이다.";
            return line;
        }

        static void CheckEnd(GameState s)
        {
            var owners = new HashSet<string>(s.cities.Select(c => c.faction ?? ""));
            var alive = s.factions.Where(f => f.alive).ToList();
            if (FactionCities(s, s.player).Count == 0)
            {
                s.over = true; s.winner = "";
                Report(s, "모든 성을 잃었다. 기록은 여기서 끝난다.");
                s.Emit(new GameEvent { kind = GameEventKind.Defeated, faction = s.player });
                return;
            }
            if (owners.Count == 1 && owners.Contains(s.player))
            {
                s.over = true; s.winner = s.player;
                Report(s, $"{FactionName(s, s.player)}이(가) 천하를 하나로 묶었다.");
                s.chronicle.Add($"{s.year}년 {s.month}월, {FactionName(s, s.player)} 천하통일.");
                s.Emit(new GameEvent { kind = GameEventKind.Unified, faction = s.player });
                return;
            }
            if (alive.Count == 1 && alive[0].id != s.player) { s.over = true; s.winner = alive[0].id; }
        }

        /// <summary>달을 넘긴다. 플레이어 명령이 끝난 뒤 호출한다.</summary>
        public static void EndTurn(GameState s)
        {
            if (s.over) return;
            s.report = new List<string>();
            foreach (var c in FactionCities(s, s.player)) AutoDomestic(s, c, c.policy);
            foreach (var cap in s.captives.Where(x => x.by == s.player).ToList()) Commands.ReleaseCaptive(s, s.player, cap.officer, false);
            var ai = s.factions.Where(f => f.alive && f.id != s.player).Select(f => f.id).ToList();
            for (int i = ai.Count - 1; i > 0; i--)
            {
                var j = (int)Math.Floor(Rand(s) * (i + 1));
                (ai[i], ai[j]) = (ai[j], ai[i]);
            }
            foreach (var f in ai) if (s.Faction(f).alive) AiFaction(s, f);
            Economy(s);
            Events(s);
            LoyaltyAndDefection(s);
            RelationsTick(s);
            foreach (var o in s.officers) o.acted = false;
            s.month++;
            if (s.month > 12) { s.month = 1; s.year++; }
            CheckEnd(s);
            s.report.Insert(0, HistorianLine(s));
        }

        /// <summary>플레이어 세력도 AI가 대신 둔다 (시뮬레이션·자동 진행용)</summary>
        public static void AiPlaysPlayer(GameState s) => AiFaction(s, s.player);
    }
}
