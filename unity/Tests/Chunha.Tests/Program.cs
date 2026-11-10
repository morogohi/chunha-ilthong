// Unity 없이 돌리는 코어 테스트. 외부 패키지 없이 쓰려고 작은 테스트 러너를 직접 둔다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Chunha.Core;
using static Chunha.Core.G;

static class Program
{
    static int pass, fail;
    static readonly List<string> failures = new List<string>();
    static string group = "";

    static void Group(string g) { group = g; Console.WriteLine($"\n▶ {g}"); }

    static void Test(string name, Action body)
    {
        try { body(); pass++; Console.WriteLine($"  ✓ {name}"); }
        catch (Exception e) { fail++; failures.Add($"{group} > {name}: {e.Message}"); Console.WriteLine($"  ✗ {name}\n      {e.Message}"); }
    }

    static void True(bool c, string msg = "조건이 거짓") { if (!c) throw new Exception(msg); }
    static void Eq<T>(T a, T b, string msg = "") { if (!EqualityComparer<T>.Default.Equals(a, b)) throw new Exception($"기대값 {b}, 실제값 {a} {msg}"); }

    static GameState Fresh(string p = "cao", int seed = 42) => NewGame(p, seed);

    static int Main()
    {
        DataIntegrity();
        Domestic();
        People();
        DiplomacyScheme();
        Battles();
        Turns();
        Persistence();
        AchievementTests();
        Parity();
        Simulation();

        Console.WriteLine($"\n결과: {pass} 통과, {fail} 실패");
        foreach (var f in failures) Console.WriteLine("  - " + f);
        File.WriteAllText("test-summary.txt", $"pass={pass} fail={fail}\n" + string.Join("\n", failures));
        return fail == 0 ? 0 : 1;
    }

    static void DataIntegrity()
    {
        Group("데이터 무결성");
        var s = Fresh();
        Test("모든 장수의 소재 도시가 존재한다", () => { foreach (var o in s.officers) True(s.City(o.city) != null, o.id); });
        Test("도로가 실제 도시를 잇고 지도가 하나로 연결된다", () =>
        {
            foreach (var r in s.roads) True(s.City(r.a) != null && s.City(r.b) != null);
            var seen = new HashSet<string> { "ye" }; var q = new Queue<string>(new[] { "ye" });
            while (q.Count > 0) foreach (var n in Neighbors(s, q.Dequeue())) if (seen.Add(n)) q.Enqueue(n);
            Eq(seen.Count, s.cities.Count);
        });
        Test("군주는 자기 세력 도시에 있다", () =>
        {
            foreach (var f in s.factions) { var r = s.Officer(f.ruler); Eq(r.faction, f.id); Eq(s.City(r.city).faction, f.id); }
        });
        Test("능력치 1~100, 95 이상은 능력치마다 3명 이하 (산정 기준)", () =>
        {
            foreach (Stat k in Enum.GetValues(typeof(Stat)))
            {
                var top = s.officers.Where(o => o.stats.Get(k) >= 95).ToList();
                True(top.Count <= 3, $"{k}: {string.Join(",", top.Select(o => o.name))}");
                foreach (var o in s.officers) True(o.stats.Get(k) >= 1 && o.stats.Get(k) <= 100);
            }
        });
        Test("12성 6세력 47장수", () => { Eq(s.cities.Count, 12); Eq(s.factions.Count, 6); Eq(s.officers.Count, 47); });
    }

    static void Domestic()
    {
        Group("내정");
        Test("개간은 금 50을 쓰고 농업을 올리며, 한 달에 한 번만 행동한다", () =>
        {
            var s = Fresh(); var c = s.City("xuchang"); var g = c.gold; var a = c.agri;
            True(Commands.Domestic(s, "cao", "xunyu", Dom.Farm).ok);
            Eq(c.gold, g - 50); True(c.agri > a);
            True(!Commands.Domestic(s, "cao", "xunyu", Dom.Trade).ok);
        });
        Test("다른 세력 장수에게는 명령할 수 없다", () => True(!Commands.Domestic(Fresh(), "cao", "yuanshao", Dom.Farm).ok));
        Test("징병은 인구 상한과 금을 지킨다", () =>
        {
            var s = Fresh(); var c = s.City("runan"); c.gold = 10;
            True(!Commands.Domestic(s, "liu", "liubei", Dom.Draft).ok);
            c.gold = 99999; c.troops = c.pop * 400 - 100;
            True(!Commands.Domestic(s, "liu", "guanyu", Dom.Draft).ok);
        });
    }

    static void People()
    {
        Group("인사");
        Test("삼고초려: 군주가 세 번 찾아가야 제갈량이 출사한다", () =>
        {
            var s = Fresh("liu"); s.City("xiangyang").faction = "liu"; s.Officer("liubei").city = "xiangyang";
            True(!Commands.Recruit(s, "liu", "liubei", "zhugeliang").ok);
            for (int i = 0; i < 3; i++) { s.Officer("liubei").acted = false; Commands.Search(s, "liu", "liubei"); }
            Eq(s.Officer("zhugeliang").faction, "liu");
            True(s.events.Any(e => e.kind == GameEventKind.OfficerJoined), "합류 이벤트가 발생해야 한다");
        });
        Test("탐색으로 재야 인재를 찾고 등용 확률은 5~95%", () =>
        {
            var s = Fresh("cao", 7); bool found = false;
            for (int m = 0; m < 30 && !found; m++) { Commands.Search(s, "cao", "zhangliao"); found = s.Officer("simayi").found; s.Officer("zhangliao").acted = false; }
            True(found);
            var p = Commands.RecruitChance(s, s.Officer("zhangliao"), s.Officer("simayi"), "cao");
            True(p >= 0.05 && p <= 0.95);
        });
        Test("이동과 수송은 인접한 아군 도시로만 된다", () =>
        {
            var s = Fresh();
            True(!Commands.MoveOfficer(s, "cao", "xuchu", "ye").ok);
            True(Commands.MoveOfficer(s, "cao", "xuchu", "luoyang").ok);
            var t0 = s.City("puyang").troops;
            True(Commands.Transport(s, "cao", "xunyou", "xuchang", 5000, 0, 0).ok);
            Eq(s.City("puyang").troops, t0 - 5000);
        });
    }

    static void DiplomacyScheme()
    {
        Group("외교·계략");
        Test("동맹 중인 세력은 공격할 수 없다", () =>
        {
            var s = Fresh("liu"); RelationOf(s, "liu", "cao").alliance = 5;
            var err = BattleSystem.CanAttack(s, "liu", new AttackArgs { from = "runan", to = "xuchang", officers = { "guanyu" }, troops = 3000 });
            True(err != null && err.Contains("동맹"));
        });
        Test("이간은 인접 도시 적 장수만 노린다", () =>
        {
            var s = Fresh();
            True(!Commands.Sow(s, "cao", "guojia", "zhouyu").ok);
            True(Commands.Sow(s, "cao", "guojia", "guanyu").ok);
        });
    }

    static void Battles()
    {
        Group("전투");
        Test("압도적인 공격군은 성을 빼앗고, 마지막 성을 잃은 세력은 멸망한다", () =>
        {
            var s = Fresh("cao", 3); s.City("xuchang").troops = 40000; s.City("xuchang").food = 50000; s.City("runan").troops = 3000;
            var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "runan", officers = { "caocao", "xuchu", "guojia" }, troops = 35000 });
            BattleSystem.AutoResolve(s, b);
            Eq(b.result, "attacker"); Eq(s.City("runan").faction, "cao"); True(!s.Faction("liu").alive);
            True(s.events.Any(e => e.kind == GameEventKind.CityCaptured) && s.events.Any(e => e.kind == GameEventKind.FactionFell));
        });
        Test("병력은 음수가 되지 않고 최대 15합 안에 끝난다 (시드 30개)", () =>
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var s = Fresh("cao", seed); s.City("xuchang").troops = 20000;
                var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "wan", officers = { "xuchu" }, troops = 15000 });
                BattleSystem.AutoResolve(s, b);
                True(b.done && b.round <= 15 && b.attacker.troops >= 0 && b.defender.troops >= 0, $"seed {seed}");
                True(b.lastFx != null, "연출 정보가 있어야 한다");
            }
        });
        Test("모의전 승률 추정은 원본 상태를 바꾸지 않는다", () =>
        {
            var s = Fresh("cao", 3); s.City("xuchang").troops = 40000; s.City("xuchang").food = 50000; s.City("runan").troops = 3000;
            var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "runan", officers = { "xuchu" }, troops = 30000 });
            var before = Signature(s);
            var p = BattleSystem.SimulateWinRate(s, b, 10);
            True(p > 0.5); Eq(Signature(s), before);
        });
        Test("군주는 다른 성이 남아 있으면 피신한다 (TS 판 회귀 테스트)", () =>
        {
            var s = NewGame("yuan", 11);
            s.City("luoyang").faction = "yuan";
            foreach (var o in s.officers) if (o.city == "luoyang") o.city = "xuchang";
            s.Officer("yuanshao").city = "luoyang";
            s.City("ye").faction = "cao";
            foreach (var o in s.officers) if (o.city == "ye" && o.faction == "yuan") o.city = "nanpi";
            s.City("luoyang").troops = 1000; s.City("xuchang").troops = 40000; s.City("xuchang").food = 99999;
            var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "luoyang", officers = { "xuchu" }, troops = 30000 });
            BattleSystem.AutoResolve(s, b);
            Eq(b.result, "attacker"); Eq(s.Officer("yuanshao").status, OfficerStatus.Serving); Eq(s.Officer("yuanshao").city, "nanpi");
        });
    }

    static void Turns()
    {
        Group("턴 진행");
        Test("12월 다음은 이듬해 1월", () => { var s = Fresh(); for (int i = 0; i < 12; i++) TurnSystem.EndTurn(s); Eq(s.year, 201); Eq(s.month, 1); });
        Test("7월에 병량을 거둔다", () =>
        {
            var s = Fresh(); for (int i = 0; i < 6; i++) TurnSystem.EndTurn(s);
            var f = s.City("xuchang").food; TurnSystem.EndTurn(s); True(s.City("xuchang").food > f);
        });
        Test("같은 시드면 결과가 같다", () =>
        {
            string Run() { var s = Fresh("sun", 99); for (int i = 0; i < 24; i++) { TurnSystem.AiPlaysPlayer(s); TurnSystem.EndTurn(s); } return Signature(s); }
            Eq(Run(), Run());
        });
        Test("모든 성을 얻으면 통일 엔딩", () => { var s = Fresh(); foreach (var c in s.cities) c.faction = "cao"; TurnSystem.EndTurn(s); True(s.over); Eq(s.winner, "cao"); });
        Test("모든 성을 잃으면 패배", () => { var s = Fresh("liu"); s.City("runan").faction = "cao"; TurnSystem.EndTurn(s); True(s.over); Eq(s.winner, ""); });
    }

    static void Persistence()
    {
        Group("저장");
        Test("Clone은 깊은 복사다", () =>
        {
            var s = Fresh(); var c = s.Clone();
            c.City("xuchang").gold = -1; c.Officer("caocao").stats.war = 1; c.report.Add("x");
            True(s.City("xuchang").gold != -1 && s.Officer("caocao").stats.war == 72 && !s.report.Contains("x"));
            Eq(Signature(s.Clone()), Signature(s));
        });
        Test("System.Text.Json 왕복 (JsonUtility와 같은 public 필드 규칙)", () =>
        {
            var s = Fresh(); TurnSystem.EndTurn(s);
            var opt = new JsonSerializerOptions { IncludeFields = true };
            var raw = JsonSerializer.Serialize(s, opt);
            var back = JsonSerializer.Deserialize<GameState>(raw, opt); back.AfterLoad();
            Eq(Signature(back), Signature(s));
        });
    }

    static void AchievementTests()
    {
        Group("업적");
        Test("업적 12개, id 중복 없음", () => { Eq(Achievements.All.Length, 12); Eq(Achievements.All.Select(a => a.id).Distinct().Count(), 12); });
        Test("시작 직후에는 아무 업적도 없다", () => { var sv = new AchievementSave(); Eq(Achievements.Evaluate(Fresh(), sv).Count, 0); });
        Test("성을 빼앗으면 '첫 승전', 같은 업적은 두 번 주지 않는다", () =>
        {
            var s = Fresh("cao", 3); s.City("xuchang").troops = 40000; s.City("xuchang").food = 50000; s.City("runan").troops = 3000;
            var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "runan", officers = { "xuchu" }, troops = 30000 });
            BattleSystem.AutoResolve(s, b);
            var sv = new AchievementSave();
            True(Achievements.Evaluate(s, sv).Any(a => a.id == "first_capture"));
            Eq(Achievements.Evaluate(s, sv).Count, 0);
        });
        Test("삼고초려 달성", () =>
        {
            var s = Fresh("liu"); s.City("xiangyang").faction = "liu"; s.Officer("liubei").city = "xiangyang";
            for (int i = 0; i < 3; i++) { s.Officer("liubei").acted = false; Commands.Search(s, "liu", "liubei"); }
            True(Achievements.Evaluate(s, new AchievementSave()).Any(a => a.id == "thrice_visit"));
        });
        Test("일기토·화계는 플레이어 쪽이 이겼을 때만", () =>
        {
            var sv = new AchievementSave();
            var fx = new RoundFx { duel = true, duelAtkWon = false, atkFireOk = false, defFireOk = true };
            var got = Achievements.Evaluate(Fresh(), sv, fx, playerAttacking: true);
            True(!got.Any(a => a.id == "duel_win" || a.id == "fire_attack"));
            got = Achievements.Evaluate(Fresh(), sv, fx, playerAttacking: false);
            True(got.Any(a => a.id == "duel_win") && got.Any(a => a.id == "fire_attack"));
        });
        Test("통일하면 천하일통·오성·절반이 함께 열린다", () =>
        {
            var s = Fresh(); foreach (var c in s.cities) c.faction = "cao"; TurnSystem.EndTurn(s);
            var ids = Achievements.Evaluate(s, new AchievementSave()).Select(a => a.id).ToList();
            True(ids.Contains("unified") && ids.Contains("five_cities") && ids.Contains("half_realm") && ids.Contains("mercy"));
        });
        Test("처형 기록이 있으면 '덕으로 다스리다'는 열리지 않는다", () =>
        {
            var s = Fresh(); s.Officer("yanliang").status = OfficerStatus.Captive; s.captives.Add(new Captive { officer = "yanliang", by = "cao" });
            Commands.ReleaseCaptive(s, "cao", "yanliang", true);
            s.City("wan").faction = "cao";
            True(!Achievements.Evaluate(s, new AchievementSave()).Any(a => a.id == "mercy"));
        });
    }

    // ── TS 판과의 비트 단위 일치
    static string F3(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
    public static string Signature(GameState s)
    {
        var cs = string.Join(";", s.cities.Select(c => string.Join(":", c.id, c.faction ?? "", F3(c.troops), F3(c.gold), F3(c.food), F3(c.loyalty), F3(c.agri), F3(c.commerce), F3(c.wall), F3(c.training), F3(c.flood))));
        var os = string.Join(";", s.officers.Select(o => $"{o.id}:{o.status}:{o.faction ?? ""}:{o.city}:{F3(o.loyalty)}"));
        var fs = string.Join(";", s.factions.Select(f => $"{f.id}:{(f.alive ? "true" : "false")}:{F3(f.mandate)}"));
        return $"{s.year}-{s.month}|rng={s.rng}|{cs}|{os}|{fs}|cap={s.captives.Count}|pend={s.pending.Count}";
    }

    static void Parity()
    {
        Group("TypeScript 프로토타입과 결과 일치 (같은 시드, 10년)");
        var golden = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.json")));
        foreach (var kv in golden.Where(k => !k.Key.StartsWith("battle")))
        {
            Test($"{kv.Key}: {kv.Value.Count - 1}개월 매달 일치", () =>
            {
                var parts = kv.Key.Split('-');
                var s = NewGame(parts[0], int.Parse(parts[1]));
                Diff(Signature(s), kv.Value[0], 0);
                for (int m = 1; m < kv.Value.Count; m++)
                {
                    TurnSystem.AiPlaysPlayer(s); TurnSystem.EndTurn(s);
                    Diff(Signature(s), kv.Value[m], m);
                }
            });
        }
        Test("전투 단독 시나리오 라운드별 일치", () =>
        {
            var exp = golden["battle-4242"];
            var s = NewGame("cao", 4242);
            s.City("xuchang").troops = 30000; s.City("xuchang").food = 50000;
            var b = BattleSystem.Start(s, "cao", new AttackArgs { from = "xuchang", to = "wan", officers = { "xuchu", "guojia", "caocao" }, troops = 24000 });
            var seq = new[] { "charge", "fire", "defend", "charge", "fire", "charge", "charge", "charge", "charge", "charge", "charge", "charge", "charge", "charge", "charge" };
            var got = new List<string>();
            foreach (var t in seq)
            {
                if (b.done) break;
                BattleSystem.PlayRound(s, b, t);
                got.Add($"{b.round}|{F3(b.attacker.troops)}|{F3(b.defender.troops)}|{F3(b.attacker.morale)}|{F3(b.defender.morale)}|{F3(b.attacker.food)}|{(b.done ? "true" : "false")}|{b.result ?? "null"}");
            }
            got.Add(Signature(s));
            Eq(got.Count, exp.Count, "라운드 수");
            for (int i = 0; i < got.Count; i++) Diff(got[i], exp[i], i);
        });
    }

    static void Diff(string got, string exp, int m)
    {
        if (got == exp) return;
        var g = got.Split('|', ';'); var e = exp.Split('|', ';');
        for (int i = 0; i < Math.Min(g.Length, e.Length); i++)
            if (g[i] != e[i]) throw new Exception($"{m}번째 달에서 처음 어긋남: C# '{g[i]}' vs TS '{e[i]}'");
        throw new Exception($"{m}번째 달 길이 다름");
    }

    static void Simulation()
    {
        Group("AI 자동 시뮬레이션 (6세력 × 시드 8개 × 최대 30년, 매 턴 불변식)");
        foreach (var p in FactionOrder)
            Test($"{p} × 8판", () =>
            {
                for (int seed = 1; seed <= 8; seed++)
                {
                    var s = NewGame(p, seed * 1013);
                    for (int m = 0; m < 360 && !s.over; m++)
                    {
                        TurnSystem.AiPlaysPlayer(s); TurnSystem.EndTurn(s);
                        var errs = Invariants(s);
                        if (errs.Count > 0) throw new Exception($"seed {seed} month {m}: {string.Join("; ", errs)}");
                    }
                }
            });
    }

    static List<string> Invariants(GameState s)
    {
        var e = new List<string>();
        foreach (var c in s.cities)
        {
            if (c.troops < 0 || c.gold < 0 || c.food < 0) e.Add($"{c.id} 음수");
            if (c.loyalty < 0 || c.loyalty > 100 || c.wall < 0 || c.wall > 100) e.Add($"{c.id} 범위");
            if (double.IsNaN(c.troops + c.gold + c.food + c.agri + c.commerce)) e.Add($"{c.id} NaN");
        }
        foreach (var o in s.officers)
        {
            if (o.status == OfficerStatus.Serving && (None(o.faction) || s.City(o.city).faction != o.faction)) e.Add($"{o.id} 소속 불일치");
            if (o.status == OfficerStatus.Captive && !s.captives.Any(c => c.officer == o.id)) e.Add($"{o.id} 포로 기록 없음");
        }
        foreach (var f in s.factions)
        {
            if (f.alive && FactionCities(s, f.id).Count == 0) e.Add($"{f.id} 성 없이 생존");
            if (f.alive && s.Officer(f.ruler).status != OfficerStatus.Serving) e.Add($"{f.id} 군주 부재");
        }
        return e;
    }
}
