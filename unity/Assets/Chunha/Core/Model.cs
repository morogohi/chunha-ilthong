// 게임 상태 모델. Unity JsonUtility로 저장할 수 있도록 public 필드 + List만 쓴다 (Dictionary 없음).
using System;
using System.Collections.Generic;

namespace Chunha.Core
{
    [Serializable]
    public class Stats
    {
        public int war, @int, pol, cha, land, naval;
        public int Get(Stat k) => k switch
        {
            Stat.War => war, Stat.Int => @int, Stat.Pol => pol, Stat.Cha => cha, Stat.Land => land, _ => naval,
        };
        public Stats Clone() => (Stats)MemberwiseClone();
    }

    public enum Stat { War, Int, Pol, Cha, Land, Naval }

    public static class OfficerStatus
    {
        public const string Serving = "serving", Free = "free", Captive = "captive", Dead = "dead";
    }

    [Serializable]
    public class Officer
    {
        public string id, name, courtesy;
        public int born;
        public Stats stats = new Stats();
        public double loyalty;
        public int ambition, affinity;
        public List<string> traits = new List<string>();
        public string faction; // null 또는 "" = 무소속
        public string city;
        public string status;
        public bool acted, found;
        public string special = ""; // "thrice_visit"
        public int visits;

        public bool HasTrait(string t) => traits.Contains(t);
        public Officer Clone()
        {
            var o = (Officer)MemberwiseClone();
            o.stats = stats.Clone();
            o.traits = new List<string>(traits);
            return o;
        }
    }

    public static class Policy
    {
        public const string Direct = "direct", Develop = "develop", Military = "military", Welfare = "welfare", Balanced = "balanced";
        public static readonly string[] All = { Direct, Develop, Military, Welfare, Balanced };
        public static string Label(string p) => p switch
        {
            Direct => "직할", Develop => "개발", Military => "군비", Welfare => "진흥", _ => "균형",
        };
    }

    [Serializable]
    public class City
    {
        public string id, name;
        public float x, y;
        public string faction, governor;
        public double agri, commerce, flood, loyalty, wall, pop, troops, training, gold, food;
        public bool river;
        public string policy;
        public City Clone() => (City)MemberwiseClone();
    }

    [Serializable]
    public class Faction
    {
        public string id, name, ruler, color;
        public double mandate;
        public bool alive;
        public Faction Clone() => (Faction)MemberwiseClone();
    }

    [Serializable]
    public class Relation
    {
        public string a, b;
        public int alliance, truce;
        public double favor;
        public Relation Clone() => (Relation)MemberwiseClone();
    }

    [Serializable]
    public class PendingEvent
    {
        public string kind; // locust | flood | plague
        public string city;
        public int month;
    }

    [Serializable]
    public class LogEntry
    {
        public int t;
        public string text, tone;
    }

    [Serializable]
    public class Captive
    {
        public string officer, by;
    }

    [Serializable]
    public class Road
    {
        public string a, b;
    }

    [Serializable]
    public class GameState
    {
        public int version = 1;
        public int seed, rng, year, month;
        public string player;
        public List<City> cities = new List<City>();
        public List<Officer> officers = new List<Officer>();
        public List<Faction> factions = new List<Faction>();
        public List<Relation> relations = new List<Relation>();
        public List<Road> roads = new List<Road>();
        public List<PendingEvent> pending = new List<PendingEvent>();
        public List<LogEntry> log = new List<LogEntry>();
        public List<string> report = new List<string>();
        public List<Captive> captives = new List<Captive>();
        public List<string> chronicle = new List<string>();
        public string winner;
        public bool over;

        /// <summary>표현 계층(Unity)에 알리는 이벤트. 저장되지 않는다.</summary>
        [NonSerialized] public List<GameEvent> events = new List<GameEvent>();

        [NonSerialized] Dictionary<string, City> _c;
        [NonSerialized] Dictionary<string, Officer> _o;
        [NonSerialized] Dictionary<string, Faction> _f;

        public City City(string id)
        {
            if (_c == null) { _c = new Dictionary<string, City>(); foreach (var c in cities) _c[c.id] = c; }
            return id != null && _c.TryGetValue(id, out var v) ? v : null;
        }

        public Officer Officer(string id)
        {
            if (_o == null) { _o = new Dictionary<string, Officer>(); foreach (var o in officers) _o[o.id] = o; }
            return id != null && _o.TryGetValue(id, out var v) ? v : null;
        }

        public Faction Faction(string id)
        {
            if (_f == null) { _f = new Dictionary<string, Faction>(); foreach (var f in factions) _f[f.id] = f; }
            return id != null && _f.TryGetValue(id, out var v) ? v : null;
        }

        public void Emit(GameEvent e)
        {
            if (events == null) events = new List<GameEvent>();
            events.Add(e);
        }

        public GameState Clone()
        {
            var s = (GameState)MemberwiseClone();
            s._c = null; s._o = null; s._f = null;
            s.events = new List<GameEvent>();
            s.cities = cities.ConvertAll(x => x.Clone());
            s.officers = officers.ConvertAll(x => x.Clone());
            s.factions = factions.ConvertAll(x => x.Clone());
            s.relations = relations.ConvertAll(x => x.Clone());
            s.roads = roads.ConvertAll(x => new Road { a = x.a, b = x.b });
            s.pending = pending.ConvertAll(x => new PendingEvent { kind = x.kind, city = x.city, month = x.month });
            s.log = log.ConvertAll(x => new LogEntry { t = x.t, text = x.text, tone = x.tone });
            s.report = new List<string>(report);
            s.captives = captives.ConvertAll(x => new Captive { officer = x.officer, by = x.by });
            s.chronicle = new List<string>(chronicle);
            return s;
        }

        /// <summary>역직렬화 후 캐시를 비운다.</summary>
        public void AfterLoad() { _c = null; _o = null; _f = null; events = new List<GameEvent>(); }
    }

    public static class Tactic
    {
        public const string Charge = "charge", Fire = "fire", Defend = "defend", Retreat = "retreat";
        public static string Label(string t) => t switch
        {
            Charge => "돌격", Fire => "화계", Defend => "방어", _ => "퇴각",
        };
    }

    [Serializable]
    public class Side
    {
        public string faction;
        public List<string> officers = new List<string>();
        public double troops, morale, food, start;
        public Side Clone() { var x = (Side)MemberwiseClone(); x.officers = new List<string>(officers); return x; }
    }

    [Serializable]
    public class Battle
    {
        public Side attacker, defender;
        public string city, from;
        public int round, maxRounds;
        public List<string> log = new List<string>();
        public bool done;
        public string result; // attacker | defender | null
        /// <summary>마지막 라운드의 연출 정보 (Unity가 애니메이션에 쓴다)</summary>
        public RoundFx lastFx;
        public Battle Clone()
        {
            var b = (Battle)MemberwiseClone();
            b.attacker = attacker.Clone(); b.defender = defender.Clone();
            b.log = new List<string>(log);
            return b;
        }
    }

    [Serializable]
    public class RoundFx
    {
        public string atkTactic, defTactic;
        public double dmgToAtk, dmgToDef;
        public bool duel, duelAtkWon, atkFireOk, defFireOk;
        public string duelWinner, duelLoser;
    }

    public enum GameEventKind { Transport, BattleStarted, CityCaptured, AttackRepelled, FactionFell, Disaster, DisasterWarning, Harvest, OfficerJoined, OfficerLeft, Alliance, Unified, Defeated }

    public class GameEvent
    {
        public GameEventKind kind;
        public string city, from, faction, other, text;
    }
}
