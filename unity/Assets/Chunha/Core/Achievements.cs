// 업적: 플레이 동기를 주는 장기 목표. 상태와 전투 결과만 보고 판정한다 (Unity 비의존, .NET에서 테스트).
using System;
using System.Collections.Generic;
using System.Linq;
using static Chunha.Core.G;

namespace Chunha.Core
{
    public readonly struct AchievementDef
    {
        public readonly string id, title, desc;
        public AchievementDef(string id, string title, string desc) { this.id = id; this.title = title; this.desc = desc; }
    }

    [Serializable]
    public class AchievementSave
    {
        public List<string> unlocked = new List<string>();
        public bool Has(string id) => unlocked.Contains(id);
    }

    public static class Achievements
    {
        public static readonly AchievementDef[] All =
        {
            new AchievementDef("first_capture", "첫 승전", "처음으로 적의 성을 빼앗았다"),
            new AchievementDef("five_cities", "오성의 군주", "성 5개를 거느렸다"),
            new AchievementDef("half_realm", "천하의 절반", "12성 중 6성을 차지했다"),
            new AchievementDef("unified", "천하일통", "천하를 하나로 묶었다"),
            new AchievementDef("thrice_visit", "삼고초려", "와룡을 세 번 찾아가 맞아들였다"),
            new AchievementDef("duel_win", "일기토", "장수끼리의 일기토에서 이겼다"),
            new AchievementDef("fire_attack", "적벽의 불길", "화계로 적진을 불태웠다"),
            new AchievementDef("talent_15", "인재의 숲", "장수 15명을 거느렸다"),
            new AchievementDef("hearts", "민심을 얻다", "한 성의 민심을 100으로 만들었다"),
            new AchievementDef("underdog", "약자의 반격", "유비 또는 마등으로 성 3개를 거느렸다"),
            new AchievementDef("decade", "십 년의 기록", "10년 동안 세력을 지켰다"),
            new AchievementDef("mercy", "덕으로 다스리다", "포로를 처형하지 않고 성 4개를 거느렸다"),
        };

        public static AchievementDef Get(string id) => All.First(a => a.id == id);

        /// <summary>현재 상태(와 방금 끝난 전투 라운드)를 보고 새로 달성한 업적을 돌려준다. save에 기록한다.</summary>
        public static List<AchievementDef> Evaluate(GameState s, AchievementSave save, RoundFx fx = null, bool playerAttacking = true)
        {
            var got = new List<AchievementDef>();
            void Check(string id, bool cond) { if (cond && !save.Has(id)) { save.unlocked.Add(id); got.Add(Get(id)); } }
            if (s == null) return got;
            var p = s.player;
            var cities = FactionCities(s, p);
            var officers = s.officers.Count(o => o.status == OfficerStatus.Serving && o.faction == p);
            Check("first_capture", s.chronicle.Any(c => c.Contains("함락") && c.Contains(FactionName(s, p))));
            Check("five_cities", cities.Count >= 5);
            Check("half_realm", cities.Count >= 6);
            Check("unified", s.over && s.winner == p);
            Check("thrice_visit", s.Officer("zhugeliang")?.faction == p && s.Officer("zhugeliang").status == OfficerStatus.Serving);
            if (fx != null)
            {
                Check("duel_win", fx.duel && fx.duelAtkWon == playerAttacking);
                Check("fire_attack", playerAttacking ? fx.atkFireOk : fx.defFireOk);
            }
            Check("talent_15", officers >= 15);
            Check("hearts", cities.Any(c => c.loyalty >= 100));
            Check("underdog", (p == "liu" || p == "ma") && cities.Count >= 3);
            Check("decade", !s.over && s.year >= ScenarioYear + 10);
            Check("mercy", cities.Count >= 4 && !s.chronicle.Any(c => c.Contains(FactionName(s, p) + "이(가)") && c.Contains("처형")));
            return got;
        }
    }
}
