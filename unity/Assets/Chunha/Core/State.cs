using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chunha.Core
{
    /// <summary>
    /// 상태 생성과 공용 헬퍼. TypeScript 프로토타입(src/core/state.ts)과 같은 난수·반올림 규칙을 쓴다.
    /// 무소속은 항상 빈 문자열("")로 표현한다 (JsonUtility가 null을 ""로 저장하기 때문).
    /// </summary>
    public static class G
    {
        public const string Neutral = "";
        public const int GraceMonths = 6;
        public const int ScenarioYear = 200;
        public const string ScenarioTitle = "관도의 각오";
        public static readonly string[] FactionOrder = { "cao", "yuan", "liu", "sun", "biao", "ma" };

        public static bool None(string f) => string.IsNullOrEmpty(f);
        public static bool Same(string a, string b) => (None(a) && None(b)) || a == b;

        // ── 난수 (mulberry32, JS와 비트 단위로 같다)
        public static double Rand(GameState s)
        {
            unchecked
            {
                s.rng = s.rng + 0x6d2b79f5;
                int t = s.rng;
                t = (t ^ (int)((uint)t >> 15)) * (t | 1);
                t ^= t + (t ^ (int)((uint)t >> 7)) * (t | 61);
                return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
            }
        }

        public static bool Chance(GameState s, double p) => Rand(s) < Math.Max(0, Math.Min(1, p));
        public static T Pick<T>(GameState s, IList<T> arr) => arr[(int)Math.Floor(Rand(s) * arr.Count)];
        public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        /// <summary>JS Math.round와 같다 (0.5는 위로)</summary>
        public static double Round(double v) => Math.Floor(v + 0.5);
        public static string Fmt(double n) => Round(n).ToString("#,0", CultureInfo.InvariantCulture);

        public static GameState NewGame(string player, int seed)
        {
            var s = new GameState { seed = seed, rng = seed, year = ScenarioYear, month = 1, player = player };
            foreach (var r in Data.Cities)
                s.cities.Add(new City
                {
                    id = r.Id, name = r.Name, x = r.X, y = r.Y, faction = r.Faction ?? "", governor = r.Governor ?? "",
                    agri = r.Agri, commerce = r.Commerce, flood = r.Flood, loyalty = r.Loyalty, wall = r.Wall, pop = r.Pop,
                    troops = r.Troops, training = 50, gold = r.Gold, food = r.Food, river = r.River, policy = Policy.Balanced,
                });
            foreach (var r in Data.Officers)
                s.officers.Add(new Officer
                {
                    id = r.Id, name = r.Name, courtesy = r.Courtesy, born = r.Born,
                    stats = new Stats { war = r.War, @int = r.Int, pol = r.Pol, cha = r.Cha, land = r.Land, naval = r.Naval },
                    loyalty = r.Loyalty, ambition = r.Ambition, affinity = r.Affinity,
                    traits = string.IsNullOrEmpty(r.Traits) ? new List<string>() : r.Traits.Split(',').ToList(),
                    faction = r.Faction ?? "", city = r.City == "puyang_front" ? "puyang" : r.City,
                    status = r.Faction != null ? OfficerStatus.Serving : OfficerStatus.Free,
                });
            s.Officer("yanliang").city = "ye";
            s.Officer("zhugeliang").special = "thrice_visit";
            s.Officer("zhugeliang").visits = 0;
            foreach (var f in Data.Factions) s.factions.Add(f.Clone());
            for (int i = 0; i < FactionOrder.Length; i++)
                for (int j = i + 1; j < FactionOrder.Length; j++)
                    s.relations.Add(new Relation { a = FactionOrder[i], b = FactionOrder[j], favor = 40 });
            foreach (var (a, b, favor) in Data.InitialRelations) RelationOf(s, a, b).favor = favor;
            foreach (var (a, b) in Data.Roads) s.roads.Add(new Road { a = a, b = b });
            foreach (var c in s.cities)
                if (c.faction == player) c.policy = c.governor == s.Faction(player).ruler ? Policy.Direct : Policy.Balanced;
            s.report = new List<string> { $"{ScenarioYear}년 1월, 시나리오 「{ScenarioTitle}」를 연다. {s.Faction(player).name}의 기록을 시작한다." };
            return s;
        }

        public static int AbsMonth(GameState s) => s.year * 12 + (s.month - 1);

        public static List<string> Neighbors(GameState s, string id)
        {
            var o = new List<string>();
            foreach (var r in s.roads)
            {
                if (r.a == id) o.Add(r.b);
                else if (r.b == id) o.Add(r.a);
            }
            return o;
        }

        public static bool IsAdjacent(GameState s, string a, string b) => Neighbors(s, a).Contains(b);
        public static List<City> FactionCities(GameState s, string f) => s.cities.Where(c => c.faction == f && !None(f)).ToList();

        public static List<Officer> CityOfficers(GameState s, string city, string faction)
            => s.officers.Where(o => o.city == city && o.status == OfficerStatus.Serving && Same(o.faction, faction)).ToList();

        public static List<Officer> CityOfficersAny(GameState s, string city)
            => s.officers.Where(o => o.city == city && o.status == OfficerStatus.Serving).ToList();

        public static List<Officer> FreeOfficers(GameState s, string city)
            => s.officers.Where(o => o.city == city && o.status == OfficerStatus.Free).ToList();

        public static Relation RelationOf(GameState s, string a, string b)
            => s.relations.FirstOrDefault(r => (r.a == a && r.b == b) || (r.a == b && r.b == a));

        public static bool IsHostile(GameState s, string a, string b)
        {
            if (None(a) || None(b)) return true;
            if (a == b) return false;
            var r = RelationOf(s, a, b);
            return r == null || (r.alliance <= 0 && r.truce <= 0);
        }

        public static void AddLog(GameState s, string text, string tone = "info")
        {
            s.log.Add(new LogEntry { t = AbsMonth(s), text = text, tone = tone });
            if (s.log.Count > 300) s.log.RemoveRange(0, s.log.Count - 300);
        }

        public static void Report(GameState s, string text) => s.report.Add(text);
        public static string CityName(GameState s, string id) => s.City(id)?.name ?? id;
        public static string FactionName(GameState s, string id) => None(id) ? "공백지" : s.Faction(id)?.name ?? id;
        public static bool IsRuler(GameState s, Officer o) => !None(o.faction) && s.Faction(o.faction)?.ruler == o.id;

        /// <summary>능력치 내림차순 (안정 정렬 — JS Array.sort와 같은 순서)</summary>
        public static List<Officer> Best(IEnumerable<Officer> os, Stat k) => os.OrderByDescending(o => o.stats.Get(k)).ToList();
    }

    public static partial class Data
    {
        public static readonly Faction[] Factions =
        {
            new Faction { id = "cao", name = "조조군", ruler = "caocao", color = "#B08A3E", mandate = 60, alive = true },
            new Faction { id = "yuan", name = "원소군", ruler = "yuanshao", color = "#6B5B8E", mandate = 65, alive = true },
            new Faction { id = "liu", name = "유비군", ruler = "liubei", color = "#B5432F", mandate = 55, alive = true },
            new Faction { id = "sun", name = "손권군", ruler = "sunquan", color = "#2F5D7C", mandate = 55, alive = true },
            new Faction { id = "biao", name = "유표군", ruler = "liubiao", color = "#5E7D4A", mandate = 50, alive = true },
            new Faction { id = "ma", name = "마등군", ruler = "mateng", color = "#4A4A48", mandate = 45, alive = true },
        };

        public static readonly (string a, string b, double favor)[] InitialRelations =
        {
            ("cao", "yuan", 10), ("liu", "cao", 15), ("liu", "yuan", 60), ("liu", "biao", 55), ("sun", "biao", 15),
        };
    }
}
