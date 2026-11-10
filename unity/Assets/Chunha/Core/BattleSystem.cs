using System;
using System.Collections.Generic;
using System.Linq;
using static Chunha.Core.G;

namespace Chunha.Core
{
    public class AttackArgs
    {
        public string from, to;
        public List<string> officers = new List<string>();
        public double troops;
    }

    public static class BattleSystem
    {
        public static Officer Leader(GameState s, Side side) => Leader(s, side.officers);

        public static Officer Leader(GameState s, IEnumerable<string> ids)
        {
            var os = ids.Select(s.Officer).Where(o => o != null).ToList();
            if (os.Count == 0) return null;
            return os.Aggregate((a, b) => a.stats.war + a.stats.land > b.stats.war + b.stats.land ? a : b);
        }

        static Officer BestInt(GameState s, Side side)
        {
            var os = side.officers.Select(s.Officer).Where(o => o != null).ToList();
            if (os.Count == 0) return null;
            return os.Aggregate((a, b) => a.stats.@int > b.stats.@int ? a : b);
        }

        public static string CanAttack(GameState s, string faction, AttackArgs a)
        {
            var from = s.City(a.from);
            var to = s.City(a.to);
            if (from == null || to == null) return "도시가 없다.";
            if (from.faction != faction) return "아군 도시에서만 출진할 수 있다.";
            if (!IsAdjacent(s, a.from, a.to)) return "인접한 도시만 공격할 수 있다.";
            if (!IsHostile(s, faction, to.faction)) return "동맹·휴전 중인 세력은 공격할 수 없다.";
            if (to.faction == faction) return "아군 도시다.";
            if (a.officers.Count == 0 || a.officers.Count > 3) return "장수는 1~3명이어야 한다.";
            foreach (var id in a.officers)
            {
                var o = s.Officer(id);
                if (o == null || o.faction != faction || o.city != a.from || o.status != OfficerStatus.Serving) return "출진할 수 없는 장수가 있다.";
                if (o.acted) return $"{o.name}은(는) 이번 달 이미 움직였다.";
            }
            if (a.troops < 1000) return "병력은 최소 1,000이 필요하다.";
            if (a.troops > from.troops) return "병력이 부족하다.";
            if (from.food < a.troops * 0.3) return "병량이 부족하다 (병력의 30% 필요).";
            return null;
        }

        public static Battle Start(GameState s, string faction, AttackArgs a)
        {
            var err = CanAttack(s, faction, a);
            if (err != null) throw new InvalidOperationException(err);
            var from = s.City(a.from);
            var to = s.City(a.to);
            var food = Round(a.troops * 0.3);
            from.troops -= a.troops;
            from.food -= food;
            foreach (var id in a.officers) s.Officer(id).acted = true;
            var defOfficers = !None(to.faction)
                ? CityOfficers(s, to.id, to.faction).OrderByDescending(o => o.stats.war + o.stats.land).Take(3).Select(o => o.id).ToList()
                : new List<string>();
            var b = new Battle
            {
                attacker = new Side { faction = faction, officers = new List<string>(a.officers), troops = a.troops, start = a.troops, morale = 60 + Round(from.training / 4), food = food },
                defender = new Side { faction = to.faction ?? Neutral, officers = defOfficers, troops = to.troops, start = to.troops, morale = 60 + Round(to.training / 4), food = to.food },
                city = to.id, from = from.id, round = 0, maxRounds = 15, done = false, result = null,
            };
            b.log.Add($"{FactionName(s, faction)}이(가) {CityName(s, from.id)}에서 {to.name}(으)로 진군했다. {Fmt(a.troops)} 대 {Fmt(to.troops)}.");
            to.troops = 0; // 수비 병력은 전투 중 b.defender가 들고 있다
            s.Emit(new GameEvent { kind = GameEventKind.BattleStarted, from = from.id, city = to.id, faction = faction, other = to.faction });
            return b;
        }

        static double Skill(GameState s, Battle b, Officer o)
        {
            if (o == null) return 30;
            var river = s.City(b.city).river;
            return river ? (o.stats.naval + o.stats.land) / 2.0 * 0.5 + o.stats.naval * 0.5 : o.stats.land;
        }

        static double Power(GameState s, Battle b, Side side, bool isDef)
        {
            var l = Leader(s, side);
            double war = l?.stats.war ?? 40;
            var city = s.City(isDef ? b.city : b.from);
            var training = city != null ? city.training : 50;
            return side.troops * (0.5 + war / 200 + Skill(s, b, l) / 200) * (0.6 + side.morale / 250) * (0.8 + training / 250);
        }

        public static string AiTactic(GameState s, Battle b, bool isDef)
        {
            var me = isDef ? b.defender : b.attacker;
            var en = isDef ? b.attacker : b.defender;
            if (!isDef && (me.morale < 25 || me.troops < en.troops * 0.35)) return Tactic.Retreat;
            double mi = BestInt(s, me)?.stats.@int ?? 30;
            double ei = BestInt(s, en)?.stats.@int ?? 30;
            if (mi > ei + 8 && Chance(s, 0.5)) return Tactic.Fire;
            if (isDef && me.troops < en.troops * 0.7) return Tactic.Defend;
            return Tactic.Charge;
        }

        public static double FireChance(GameState s, Side me, Side en)
        {
            var o = BestInt(s, me);
            var e = BestInt(s, en);
            var bonus = o != null && o.HasTrait("화공") ? 0.15 : 0;
            return Clamp(0.3 + ((o?.stats.@int ?? 30) - (e?.stats.@int ?? 30)) / 150.0 + bonus, 0.05, 0.85);
        }

        static double Mul(string t) => t == Tactic.Charge ? 1.3 : t == Tactic.Defend ? 0.5 : 1;
        static double Take(string t) => t == Tactic.Charge ? 1.1 : t == Tactic.Defend ? 0.6 : 1;

        /// <summary>한 라운드 진행. null인 쪽은 AI가 전술을 고른다.</summary>
        public static void PlayRound(GameState s, Battle b, string atkTactic = null, string defTactic = null)
        {
            if (b.done) return;
            b.round++;
            var at = atkTactic ?? AiTactic(s, b, false);
            var dt = defTactic ?? AiTactic(s, b, true);
            var A = b.attacker; var D = b.defender;
            var fx = new RoundFx { atkTactic = at, defTactic = dt };
            b.lastFx = fx;
            var line = new List<string> { $"{b.round}합 — 공격 {Tactic.Label(at)} / 수비 {Tactic.Label(dt)}." };

            if (at == Tactic.Retreat) { Finish(s, b, "defender", "공격군이 퇴각했다."); return; }
            if (dt == Tactic.Retreat) { Finish(s, b, "attacker", "수비군이 성을 버리고 달아났다."); return; }

            if (at == Tactic.Charge || dt == Tactic.Charge)
            {
                var la = Leader(s, A); var ld = Leader(s, D);
                if (la != null && ld != null && Math.Abs(la.stats.war - ld.stats.war) >= 8 && Chance(s, 0.25))
                {
                    var pA = Clamp(0.5 + (la.stats.war - ld.stats.war) / 40.0, 0.1, 0.9);
                    var aWin = Chance(s, pA);
                    var w = aWin ? la : ld; var l = aWin ? ld : la;
                    line.Add($"일기토! {w.name}이(가) {l.name}을(를) 꺾었다.");
                    (aWin ? A : D).morale = Clamp((aWin ? A : D).morale + 15, 0, 100);
                    (aWin ? D : A).morale = Clamp((aWin ? D : A).morale - 25, 0, 100);
                    fx.duel = true; fx.duelAtkWon = aWin; fx.duelWinner = w.id; fx.duelLoser = l.id;
                }
            }

            var dmgToD = Power(s, b, A, false) * 0.08 * (0.85 + Rand(s) * 0.3);
            var dmgToA = Power(s, b, D, true) * 0.08 * (0.85 + Rand(s) * 0.3);
            var wall = !None(b.defender.faction) ? s.City(b.city).wall : s.City(b.city).wall * 0.5;

            dmgToD *= Mul(at) * Take(dt) * (1 - wall / 300);
            dmgToA *= Mul(dt) * Take(at) * (!None(b.defender.faction) ? 1.15 : 1); // 성내 지리 보정

            if (at == Tactic.Fire)
            {
                if (Chance(s, FireChance(s, A, D))) { dmgToD = D.troops * 0.15 + dmgToD * 0.5; D.morale -= 15; fx.atkFireOk = true; line.Add("화계가 성공해 수비군이 불길에 휩싸였다."); }
                else { dmgToD = 0; A.morale -= 5; line.Add("화계가 실패했다."); }
            }
            if (dt == Tactic.Fire)
            {
                if (Chance(s, FireChance(s, D, A))) { dmgToA = A.troops * 0.15 + dmgToA * 0.5; A.morale -= 15; fx.defFireOk = true; line.Add("수비군의 화계가 공격군 진영을 태웠다."); }
                else { dmgToA = 0; D.morale -= 5; line.Add("수비군의 화계가 실패했다."); }
            }
            if (at == Tactic.Defend) A.morale += 5;
            if (dt == Tactic.Defend) D.morale += 5;

            dmgToD = G.Round(Math.Min(D.troops, dmgToD));
            dmgToA = G.Round(Math.Min(A.troops, dmgToA));
            D.troops -= dmgToD;
            A.troops -= dmgToA;
            A.morale = Clamp(A.morale - G.Round(dmgToA / Math.Max(1, A.troops + dmgToA) * 60) + 2, 0, 100);
            D.morale = Clamp(D.morale - G.Round(dmgToD / Math.Max(1, D.troops + dmgToD) * 60) + 2, 0, 100);
            fx.dmgToAtk = dmgToA; fx.dmgToDef = dmgToD;

            A.food -= G.Round(A.troops * 0.02);
            if (A.food <= 0) { A.food = 0; A.morale = Clamp(A.morale - 20, 0, 100); line.Add("공격군의 병량이 바닥났다."); }

            line.Add($"피해 — 공격 {Fmt(dmgToA)} / 수비 {Fmt(dmgToD)}. 남은 병력 {Fmt(A.troops)} 대 {Fmt(D.troops)}.");
            b.log.Add(string.Join(" ", line));

            if (D.troops < 300 || D.morale <= 0) { Finish(s, b, "attacker", "수비군이 무너졌다."); return; }
            if (A.troops < 300 || A.morale <= 0) { Finish(s, b, "defender", "공격군이 무너졌다."); return; }
            if (b.round >= b.maxRounds) Finish(s, b, "defender", "공격군이 성을 함락하지 못하고 물러났다.");
        }

        public static void AutoResolve(GameState s, Battle b)
        {
            int guard = 0;
            while (!b.done && guard++ < 50) PlayRound(s, b);
        }

        /// <summary>모의전: 복제본으로 여러 번 돌려 승률을 추정한다 (원본 상태는 그대로)</summary>
        public static double SimulateWinRate(GameState s, Battle b, int runs = 20)
        {
            int wins = 0;
            for (int i = 0; i < runs; i++)
            {
                var s2 = s.Clone();
                unchecked { s2.rng = s.rng + i * 7919; }
                var b2 = b.Clone();
                b2.log = new List<string>();
                AutoResolve(s2, b2);
                if (b2.result == "attacker") wins++;
            }
            return wins / (double)runs;
        }

        static void Finish(GameState s, Battle b, string result, string why)
        {
            b.done = true;
            b.result = result;
            b.log.Add(why);
            var city = s.City(b.city);
            var from = s.City(b.from);
            var A = b.attacker; var D = b.defender;
            var aName = FactionName(s, A.faction); var dName = FactionName(s, D.faction);

            if (result == "attacker")
            {
                var prevFaction = city.faction;
                var defenders = !None(prevFaction) ? CityOfficers(s, city.id, prevFaction) : new List<Officer>();
                var escape = !None(prevFaction) ? Neighbors(s, city.id).Where(n => s.City(n).faction == prevFaction).ToList() : new List<string>();
                var anyOwn = !None(prevFaction) ? FactionCities(s, prevFaction).Where(c => c.id != city.id).Select(c => c.id).ToList() : new List<string>();
                foreach (var o in defenders)
                {
                    var ruler = s.Faction(prevFaction).ruler == o.id;
                    if (ruler && anyOwn.Count > 0)
                        o.city = escape.Count > 0 ? Pick(s, escape) : Pick(s, anyOwn);
                    else if (escape.Count > 0 && Chance(s, 0.5))
                        o.city = Pick(s, escape);
                    else
                    {
                        o.status = OfficerStatus.Captive;
                        s.captives.Add(new Captive { officer = o.id, by = A.faction });
                        b.log.Add($"{o.name}이(가) 사로잡혔다.");
                    }
                }
                city.faction = A.faction;
                city.troops = A.troops;
                city.training = from.training;
                city.gold = G.Round(city.gold * 0.5);
                city.food = G.Round(D.food * 0.5) + A.food;
                city.loyalty = Clamp(city.loyalty - 10, 0, 100);
                city.policy = A.faction == s.player ? Policy.Balanced : city.policy;
                foreach (var id in A.officers) s.Officer(id).city = city.id;
                var l = Leader(s, A);
                city.governor = l?.id ?? (A.officers.Count > 0 ? A.officers[0] : "");
                s.Faction(A.faction).mandate = Clamp(s.Faction(A.faction).mandate + 5, 0, 100);
                if (!None(prevFaction)) s.Faction(prevFaction).mandate = Clamp(s.Faction(prevFaction).mandate - 3, 0, 100);
                Report(s, $"{aName}이(가) {dName}의 {city.name}을(를) 빼앗았다.");
                s.chronicle.Add($"{s.year}년 {s.month}월, {aName} {city.name} 함락.");
                AddLog(s, $"{city.name} 함락 — {aName}", A.faction == s.player ? "good" : prevFaction == s.player ? "bad" : "info");
                s.Emit(new GameEvent { kind = GameEventKind.CityCaptured, city = city.id, from = from.id, faction = A.faction, other = prevFaction });
                if (!None(prevFaction)) CheckFactionAlive(s, prevFaction, A.faction);
            }
            else
            {
                from.troops += A.troops;
                from.food += A.food;
                city.troops = D.troops;
                city.food = D.food;
                Report(s, $"{aName}의 {city.name} 공격은 실패로 끝났다.");
                AddLog(s, $"{city.name} 방어 성공 — {dName}", D.faction == s.player ? "good" : A.faction == s.player ? "bad" : "info");
                s.Emit(new GameEvent { kind = GameEventKind.AttackRepelled, city = city.id, from = from.id, faction = A.faction, other = D.faction });
            }
        }

        public static void CheckFactionAlive(GameState s, string f, string by)
        {
            if (FactionCities(s, f).Count > 0) return;
            var fac = s.Faction(f);
            fac.alive = false;
            foreach (var o in s.officers)
            {
                if (o.faction == f && o.status == OfficerStatus.Serving)
                {
                    if (o.id == fac.ruler)
                    {
                        o.status = OfficerStatus.Captive;
                        s.captives.Add(new Captive { officer = o.id, by = by });
                    }
                    else
                    {
                        o.status = OfficerStatus.Free;
                        o.faction = "";
                        o.found = true;
                    }
                }
            }
            Report(s, $"{fac.name}이(가) 멸망했다.");
            s.chronicle.Add($"{s.year}년 {s.month}월, {fac.name} 멸망.");
            s.Emit(new GameEvent { kind = GameEventKind.FactionFell, faction = f, other = by });
        }
    }
}
