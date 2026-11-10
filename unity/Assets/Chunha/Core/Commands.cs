using System;
using System.Linq;
using static Chunha.Core.G;

namespace Chunha.Core
{
    public readonly struct Result
    {
        public readonly bool ok; public readonly string msg;
        public Result(bool ok, string msg) { this.ok = ok; this.msg = msg; }
        public static Result Fail(string m) => new Result(false, m);
        public static Result Ok(string m) => new Result(true, m);
    }

    public enum Dom { Farm, Trade, Dike, Relief, Wall, Draft, Train }

    public static class Commands
    {
        public static readonly Dom[] AllDomestic = { Dom.Farm, Dom.Trade, Dom.Dike, Dom.Relief, Dom.Wall, Dom.Draft, Dom.Train };

        public static (string label, string cost, Stat stat) Info(Dom d) => d switch
        {
            Dom.Farm => ("개간", "금 50", Stat.Pol),
            Dom.Trade => ("상업", "금 50", Stat.Pol),
            Dom.Dike => ("치수", "금 50", Stat.Pol),
            Dom.Relief => ("시혜", "병량 1000", Stat.Cha),
            Dom.Wall => ("성벽", "금 60", Stat.Pol),
            Dom.Draft => ("징병", "금 약 250", Stat.Cha),
            _ => ("훈련", "없음", Stat.Land),
        };

        static Officer Ready(GameState s, string oid, string faction, out Result err)
        {
            var o = s.Officer(oid);
            err = default;
            if (o == null || o.status != OfficerStatus.Serving || o.faction != faction) { err = Result.Fail("명령할 수 없는 장수다."); return null; }
            if (o.acted) { err = Result.Fail($"{o.name}은(는) 이번 달 이미 움직였다."); return null; }
            return o;
        }

        public static double DomesticEffect(Officer o, Dom cmd)
        {
            var st = o.stats;
            switch (cmd)
            {
                case Dom.Farm:
                case Dom.Trade: return Round(8 + st.pol / 6.0 + (o.HasTrait("내정") ? 4 : 0));
                case Dom.Dike: return Round(3 + st.pol / 20.0);
                case Dom.Relief: return Round(3 + st.cha / 20.0 + (o.HasTrait("인덕") ? 3 : 0));
                case Dom.Wall: return Round(3 + st.pol / 25.0);
                case Dom.Draft: return Round(1000 + st.cha * 20.0);
                default: return Round(4 + st.land / 12.0);
            }
        }

        public static Result Domestic(GameState s, string faction, string oid, Dom cmd)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var c = s.City(o.city);
            if (c.faction != faction) return Result.Fail("아군 도시가 아니다.");
            var eff = DomesticEffect(o, cmd);
            switch (cmd)
            {
                case Dom.Farm:
                    if (c.gold < 50) return Result.Fail("금이 부족하다.");
                    if (c.agri >= 999) return Result.Fail("더 개간할 땅이 없다.");
                    c.gold -= 50; c.agri = Clamp(c.agri + eff, 0, 999); break;
                case Dom.Trade:
                    if (c.gold < 50) return Result.Fail("금이 부족하다.");
                    if (c.commerce >= 999) return Result.Fail("상업이 이미 최고 수준이다.");
                    c.gold -= 50; c.commerce = Clamp(c.commerce + eff, 0, 999); break;
                case Dom.Dike:
                    if (c.gold < 50) return Result.Fail("금이 부족하다.");
                    if (c.flood >= 100) return Result.Fail("제방이 이미 완벽하다.");
                    c.gold -= 50; c.flood = Clamp(c.flood + eff, 0, 100); break;
                case Dom.Relief:
                    if (c.food < 1000) return Result.Fail("병량이 부족하다.");
                    if (c.loyalty >= 100) return Result.Fail("민심이 이미 최고다.");
                    c.food -= 1000; c.loyalty = Clamp(c.loyalty + eff, 0, 100);
                    s.Faction(faction).mandate = Clamp(s.Faction(faction).mandate + 0.5, 0, 100); break;
                case Dom.Wall:
                    if (c.gold < 60) return Result.Fail("금이 부족하다.");
                    if (c.wall >= 100) return Result.Fail("성벽이 이미 완벽하다.");
                    c.gold -= 60; c.wall = Clamp(c.wall + eff, 0, 100); break;
                case Dom.Draft:
                {
                    var cap = Math.Floor(c.pop * 400);
                    var n = Math.Min(eff, cap - c.troops);
                    var cost = Math.Ceiling(n / 10);
                    if (n < 200) return Result.Fail("더 모을 장정이 없다.");
                    if (c.gold < cost) return Result.Fail("금이 부족하다.");
                    c.gold -= cost;
                    c.training = Round((c.troops * c.training + n * 20) / (c.troops + n));
                    c.troops += n;
                    c.loyalty = Clamp(c.loyalty - 3, 0, 100);
                    break;
                }
                case Dom.Train:
                    if (c.training >= 100) return Result.Fail("이미 정예다.");
                    if (c.troops < 100) return Result.Fail("훈련할 병사가 없다.");
                    c.training = Clamp(c.training + eff, 0, 100); break;
            }
            o.acted = true;
            return Result.Ok($"{o.name}: {Info(cmd).label} (+{eff})");
        }

        public static Result Search(GameState s, string faction, string oid)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            o.acted = true;
            var hidden = FreeOfficers(s, o.city).Where(x => !x.found).ToList();
            var zg = FreeOfficers(s, o.city).FirstOrDefault(x => x.special == "thrice_visit");
            if (zg != null && IsRuler(s, o))
            {
                zg.found = true;
                zg.visits++;
                if (zg.visits >= 3)
                {
                    Join(s, zg, faction, o.city, 90);
                    Report(s, $"{o.name}이(가) 세 번째로 초려를 찾았다. {zg.name}이(가) 마침내 출사했다.");
                    s.chronicle.Add($"{s.year}년 {s.month}월, {o.name}의 삼고초려. {zg.name} 출사.");
                    s.Emit(new GameEvent { kind = GameEventKind.OfficerJoined, city = o.city, faction = faction, other = zg.id, text = "삼고초려" });
                    return Result.Ok($"{zg.name}이(가) 출사를 결심했다!");
                }
                return Result.Ok($"{zg.name}의 초려를 찾았다 ({zg.visits}/3). 아직 만나주지 않는다.");
            }
            var candidates = hidden.Where(x => x.special != "thrice_visit").ToList();
            if (candidates.Count > 0 && Chance(s, (o.stats.@int + o.stats.cha) / 220.0))
            {
                var f = candidates[0];
                f.found = true;
                return Result.Ok($"{CityName(s, o.city)}에서 재야의 {f.name}을(를) 찾아냈다.");
            }
            if (zg != null && !zg.found)
            {
                zg.found = true;
                return Result.Ok($"와룡이라 불리는 {zg.name}의 소문을 들었다. 군주가 직접 찾아가야 한다.");
            }
            var c = s.City(o.city);
            if (Chance(s, 0.3))
            {
                var g = 50 + Math.Floor((double)o.stats.@int);
                c.gold += g;
                return Result.Ok($"인재는 없었으나 금 {g}을(를) 얻었다.");
            }
            return Result.Ok("아무도 찾지 못했다.");
        }

        public static double RecruitChance(GameState s, Officer o, Officer target, string faction)
        {
            var ruler = s.Officer(s.Faction(faction).ruler);
            var aff = Math.Abs(ruler.affinity - target.affinity);
            var affDist = Math.Min(aff, 150 - aff);
            var p = 0.15 + o.stats.cha / 220.0 + s.Faction(faction).mandate / 400 - affDist / 120.0;
            if (target.status == OfficerStatus.Captive) p -= target.loyalty / 250;
            return Clamp(p, 0.05, 0.95);
        }

        static void Join(GameState s, Officer t, string faction, string city, double loyalty)
        {
            t.status = OfficerStatus.Serving;
            t.faction = faction;
            t.city = city;
            t.loyalty = loyalty;
            t.found = true;
            t.acted = true;
            s.captives.RemoveAll(c => c.officer == t.id);
            s.Faction(faction).mandate = Clamp(s.Faction(faction).mandate + 2, 0, 100);
        }

        public static Result Recruit(GameState s, string faction, string oid, string targetId)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var t = s.Officer(targetId);
            if (t == null) return Result.Fail("대상이 없다.");
            if (t.special == "thrice_visit" && t.status == OfficerStatus.Free) return Result.Fail($"{t.name}은(는) 군주가 직접 세 번 찾아가야 한다 (탐색).");
            var isFree = t.status == OfficerStatus.Free && t.found && t.city == o.city;
            var isCaptive = t.status == OfficerStatus.Captive && s.captives.Any(c => c.officer == t.id && c.by == faction);
            if (!isFree && !isCaptive) return Result.Fail("이 도시에서 등용할 수 있는 인물이 아니다.");
            o.acted = true;
            var p = RecruitChance(s, o, t, faction);
            if (Chance(s, p))
            {
                Join(s, t, faction, o.city, 70 + Round(o.stats.cha / 10.0));
                Report(s, $"{t.name}이(가) {FactionName(s, faction)}에 몸을 맡겼다.");
                s.Emit(new GameEvent { kind = GameEventKind.OfficerJoined, city = o.city, faction = faction, other = t.id });
                return Result.Ok($"{t.name} 등용 성공!");
            }
            return Result.Ok($"{t.name}이(가) 고개를 저었다. (성공 확률 {Round(p * 100)}%)");
        }

        public static Result ReleaseCaptive(GameState s, string faction, string targetId, bool execute)
        {
            var t = s.Officer(targetId);
            var cap = s.captives.FirstOrDefault(c => c.officer == targetId && c.by == faction);
            if (t == null || cap == null) return Result.Fail("포로가 아니다.");
            s.captives.Remove(cap);
            if (execute)
            {
                t.status = OfficerStatus.Dead;
                s.Faction(faction).mandate = Clamp(s.Faction(faction).mandate - 5, 0, 100);
                foreach (var o in s.officers)
                    if (o.faction == faction && o.status == OfficerStatus.Serving) o.loyalty = Clamp(o.loyalty - 2, 0, 100);
                Report(s, $"{t.name}이(가) 처형되었다. 사관은 이 일을 적어 둔다.");
                s.chronicle.Add($"{s.year}년 {s.month}월, {FactionName(s, faction)}이(가) {t.name}을(를) 처형했다.");
                return Result.Ok($"{t.name} 처형");
            }
            t.status = OfficerStatus.Free;
            t.faction = "";
            t.found = true;
            s.Faction(faction).mandate = Clamp(s.Faction(faction).mandate + 1, 0, 100);
            return Result.Ok($"{t.name}을(를) 풀어 주었다.");
        }

        public static Result MoveOfficer(GameState s, string faction, string oid, string to)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            if (!IsAdjacent(s, o.city, to)) return Result.Fail("인접한 도시가 아니다.");
            if (s.City(to).faction != faction) return Result.Fail("아군 도시로만 이동할 수 있다.");
            var from = s.City(o.city);
            if (from.governor == o.id) from.governor = "";
            o.city = to;
            o.acted = true;
            if (None(s.City(to).governor)) s.City(to).governor = o.id;
            return Result.Ok($"{o.name}이(가) {CityName(s, to)}(으)로 이동했다.");
        }

        public static Result Transport(GameState s, string faction, string oid, string to, double troops, double gold, double food)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var from = s.City(o.city);
            var dst = s.City(to);
            if (!IsAdjacent(s, from.id, to) || dst.faction != faction) return Result.Fail("인접한 아군 도시로만 수송할 수 있다.");
            troops = Math.Max(0, Math.Min(troops, from.troops));
            gold = Math.Max(0, Math.Min(gold, from.gold));
            food = Math.Max(0, Math.Min(food, from.food));
            if (troops + gold + food <= 0) return Result.Fail("보낼 것이 없다.");
            if (troops > 0) dst.training = Round((dst.troops * dst.training + troops * from.training) / (dst.troops + troops));
            from.troops -= troops; dst.troops += troops;
            from.gold -= gold; dst.gold += gold;
            from.food -= food; dst.food += food;
            o.acted = true;
            if (troops > 0) s.Emit(new GameEvent { kind = GameEventKind.Transport, from = from.id, city = to, faction = faction });
            return Result.Ok($"{CityName(s, to)}에 수송 완료");
        }

        public static Result SetPolicy(GameState s, string faction, string city, string policy)
        {
            var c = s.City(city);
            if (c.faction != faction) return Result.Fail("아군 도시가 아니다.");
            c.policy = policy;
            return Result.Ok($"{c.name} 운영 방침 변경");
        }

        public static double AllianceChance(GameState s, Officer o, string faction, string target)
        {
            var rel = RelationOf(s, faction, target);
            if (rel == null) return 0;
            return Clamp((rel.favor + o.stats.cha / 2.0 - 40) / 100 + (o.HasTrait("외교") ? 0.15 : 0), 0.02, 0.9);
        }

        public static Result ProposeAlliance(GameState s, string faction, string oid, string target)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var rel = RelationOf(s, faction, target);
            if (rel == null || s.Faction(target) == null || !s.Faction(target).alive) return Result.Fail("외교 상대가 없다.");
            if (rel.alliance > 0) return Result.Fail("이미 동맹 중이다.");
            var c = s.City(o.city);
            if (c.gold < 300) return Result.Fail("예물(금 300)이 부족하다.");
            c.gold -= 300;
            o.acted = true;
            var p = AllianceChance(s, o, faction, target);
            if (Chance(s, p))
            {
                rel.alliance = 12; rel.favor = Clamp(rel.favor + 10, 0, 100);
                Report(s, $"{FactionName(s, faction)}과(와) {FactionName(s, target)}이(가) 1년 동맹을 맺었다.");
                s.Emit(new GameEvent { kind = GameEventKind.Alliance, faction = faction, other = target });
                return Result.Ok($"{FactionName(s, target)}과(와) 동맹 성립!");
            }
            rel.favor = Clamp(rel.favor - 5, 0, 100);
            return Result.Ok($"{FactionName(s, target)}이(가) 동맹을 거절했다. (확률 {Round(p * 100)}%)");
        }

        public static Result SendGift(GameState s, string faction, string oid, string target)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var rel = RelationOf(s, faction, target);
            if (rel == null) return Result.Fail("외교 상대가 없다.");
            var c = s.City(o.city);
            if (c.gold < 300) return Result.Fail("금 300이 필요하다.");
            c.gold -= 300;
            var d = Round(5 + o.stats.cha / 10.0);
            rel.favor = Clamp(rel.favor + d, 0, 100);
            o.acted = true;
            return Result.Ok($"{FactionName(s, target)}의 우호 +{d}");
        }

        public static double SchemeChance(Officer o, Officer target) => Clamp(0.35 + (o.stats.@int - target.stats.@int) / 100.0, 0.05, 0.9);

        public static Result Sow(GameState s, string faction, string oid, string targetId)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var t = s.Officer(targetId);
            if (t == null || t.status != OfficerStatus.Serving || None(t.faction) || !IsHostile(s, faction, t.faction)) return Result.Fail("대상이 적 장수가 아니다.");
            if (!IsAdjacent(s, o.city, t.city)) return Result.Fail("인접한 도시의 장수만 노릴 수 있다.");
            if (IsRuler(s, t)) return Result.Fail("군주는 이간할 수 없다.");
            o.acted = true;
            var p = SchemeChance(o, t);
            if (Chance(s, p))
            {
                var d = 8 + Round((o.stats.@int - t.stats.@int) / 5.0 + 5);
                t.loyalty = Clamp(t.loyalty - Math.Max(5, d), 0, 100);
                return Result.Ok($"{t.name}의 충성이 흔들린다. (충성 {t.loyalty})");
            }
            return Result.Ok($"{t.name}이(가) 이간을 간파했다. (확률 {Round(p * 100)}%)");
        }

        public static Result Rumor(GameState s, string faction, string oid, string cityId)
        {
            var o = Ready(s, oid, faction, out var err);
            if (o == null) return err;
            var c = s.City(cityId);
            if (c == null || None(c.faction) || !IsHostile(s, faction, c.faction) || !IsAdjacent(s, o.city, cityId)) return Result.Fail("인접한 적 도시가 아니다.");
            o.acted = true;
            var gov = None(c.governor) ? null : s.Officer(c.governor);
            var p = Clamp(0.4 + (o.stats.@int - (gov?.stats.@int ?? 50)) / 120.0, 0.05, 0.9);
            if (Chance(s, p))
            {
                c.loyalty = Clamp(c.loyalty - 10, 0, 100);
                return Result.Ok($"{c.name}에 소문이 퍼졌다. 민심 {c.loyalty}");
            }
            return Result.Ok($"소문이 퍼지지 않았다. (확률 {Round(p * 100)}%)");
        }
    }
}
