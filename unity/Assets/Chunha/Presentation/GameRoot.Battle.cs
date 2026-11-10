// 출진 준비 시트와 전투 화면 (전술 선택 → 무대 연출 → 결과·포로 처리).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chunha.Art;
using Chunha.Battles;
using Chunha.Core;
using Chunha.FX;
using Chunha.Map;
using Chunha.UI;
using UnityEngine;
using UnityEngine.UI;
using static Chunha.Core.G;

namespace Chunha
{
    public partial class GameRoot
    {
        AttackArgs plan;
        Battle battle;
        bool inBattle;
        RectTransform battleHud;
        Image atkTroops, defTroops, atkMorale, defMorale;
        Text atkText, defText, battleLog, roundText;
        RectTransform tacticRow, resultRoot;
        Vector3 rigBeforeBattle;
        bool resultSoundPending;
        readonly HashSet<string> battleCaptives = new HashSet<string>();
        int captivesBefore;
        float sizeBeforeBattle;

        void OpenAttack(string from, string to)
        {
            if (busy || inBattle) return;
            var os = CityOfficers(S, from, S.player).Where(o => !o.acted).OrderByDescending(o => o.stats.war).Take(2).Select(o => o.id).ToList();
            if (to == null) to = Neighbors(S, from).FirstOrDefault(n => S.City(n).faction != S.player && IsHostile(S, S.player, S.City(n).faction));
            plan = new AttackArgs { from = from, to = to, officers = os, troops = System.Math.Max(1000, System.Math.Floor(S.City(from).troops * 0.7 / 500) * 500) };
            sheet?.Close();
            sheet = new Sheet(canvas.transform, this, $"{S.City(from).name}에서 출진", 0.82f);
            sheet.onClosed = RefreshAll;
            FillAttack();
        }

        void FillAttack()
        {
            if (!sheetOpen) return;
            var root = sheet.content;
            UIKit.Clear(root);
            var from = S.City(plan.from);
            Info(root, "목표");
            var targets = Neighbors(S, from.id).Where(n => S.City(n).faction != S.player && IsHostile(S, S.player, S.City(n).faction)).ToList();
            if (targets.Count == 0) { Info(root, "공격할 수 있는 인접 도시가 없다."); return; }
            var trow = UIKit.Rect("Targets", root); UIKit.Size(trow, -1, 110); UIKit.HList(trow, 14);
            foreach (var t in targets)
            {
                var tt = t; bool on = plan.to == t;
                UIKit.Button(trow, $"{CityName(S, t)} · {Fmt(S.City(t).troops)}", () => { plan.to = tt; FillAttack(); }, on ? Palette.Ink : Palette.Paper2, on ? Palette.Paper : Palette.Ink, 34);
            }
            var to = S.City(plan.to);
            Info(root, "장수 (최대 3명)");
            foreach (var o in CityOfficers(S, from.id, S.player).Where(x => !x.acted))
            {
                var oo = o; bool on = plan.officers.Contains(o.id);
                Choice(root, $"{o.name}  <size=28>무 {o.stats.war} · 지 {o.stats.@int} · {(to.river ? $"수 {o.stats.naval}" : $"육 {o.stats.land}")}</size>", on ? "● 선택" : "", () =>
                {
                    if (plan.officers.Contains(oo.id)) plan.officers.Remove(oo.id);
                    else if (plan.officers.Count < 3) plan.officers.Add(oo.id);
                    else Toast("장수는 최대 3명이다.");
                    FillAttack();
                });
            }
            Info(root, $"병력 <b>{Fmt(plan.troops)}</b> / {Fmt(from.troops)} · 병량 {Fmt(plan.troops * 0.3)} 소모");
            var prow = UIKit.Rect("Presets", root); UIKit.Size(prow, -1, 110); UIKit.HList(prow, 14);
            foreach (var pct in new[] { 30, 50, 70, 90 })
            {
                var pp = pct;
                UIKit.Button(prow, $"{pct}%", () => { plan.troops = System.Math.Max(1000, System.Math.Floor(from.troops * pp / 100.0 / 500) * 500); FillAttack(); }, Palette.Paper2, Palette.Ink, 36);
            }
            Info(root, $"{to.name}: 병력 {Fmt(to.troops)}, 성벽 {Round(to.wall)}{(to.river ? ", 수상전" : "")}");
            var err = BattleSystem.CanAttack(S, S.player, plan);
            if (err != null) Info(root, $"<color=#B5432F>{err}</color>");
            else
            {
                var clone = S.Clone();
                var b = BattleSystem.Start(clone, S.player, plan);
                Info(root, $"모의전 예상 승률 <b>{Round(BattleSystem.SimulateWinRate(clone, b, 16) * 100)}%</b>");
            }
            var go = UIKit.Button(root, "출진", () => { if (BattleSystem.CanAttack(S, S.player, plan) == null) StartCoroutine(BeginBattle()); }, Palette.Red, Color.white, 46);
            UIKit.Size((RectTransform)go.transform, -1, 140);
            go.interactable = err == null;
        }

        IEnumerator BeginBattle()
        {
            sheet?.Close();
            busy = true;
            PushHistory();
            S.events.Clear();
            var from = S.City(plan.from); var to = S.City(plan.to);
            SoundKit.Play(Sfx.March);
            yield return map.March(MapView.CityPos(from), MapView.CityPos(to), Palette.Hex(S.Faction(S.player).color), 0.8f);
            battle = BattleSystem.Start(S, S.player, plan);
            S.events.Clear();
            battleCaptives.Clear();
            resultSoundPending = true;
            captivesBefore = S.captives.Count;
            yield return WipeIn();
            inBattle = true;
            rigBeforeBattle = rig.position; sizeBeforeBattle = cam.orthographicSize;
            // 상단 HUD 300px·하단 HUD 640px을 피해, 보이는 띠(약 y −2.1 ~ +4.3)의 가운데에 진형이 오게 한다
            rig.position = BattleView.Origin + new Vector3(0, -1.4f, 0);
            cam.orthographicSize = 6.2f;
            if (hudRoot) hudRoot.gameObject.SetActive(false);
            battleView.Setup(S, battle);
            BuildBattleHud();
            yield return WipeOut();
            yield return Tw.Wait(0.5f);   // 진형 입장 연출(0.8초)이 끝난 뒤 입력을 받는다
            busy = false;
        }

        void BuildBattleHud()
        {
            if (battleHud) Destroy(battleHud.gameObject);
            battleHud = UIKit.Rect("BattleHUD", safeRoot).Stretch();
            var top = UIKit.Panel(battleHud, Palette.Paper.WithA(0.94f), "Top", false).rectTransform.Dock(true, 300);
            roundText = UIKit.Label(top, "", 44, Palette.Ink, TextAnchor.UpperCenter, FontStyle.Bold);
            roundText.rectTransform.Stretch(0, 220, 0, 20);
            var sides = UIKit.Rect("Sides", top).Stretch(32, 20, 32, 90); UIKit.HList(sides, 24);
            (atkText, atkTroops, atkMorale) = SidePanel(sides, battle.attacker);
            (defText, defTroops, defMorale) = SidePanel(sides, battle.defender);

            var bottom = UIKit.Panel(battleHud, Palette.Paper.WithA(0.96f), "Bottom", false).rectTransform.Dock(false, 640);
            battleLog = UIKit.Label(bottom, "", 30, Palette.Ink2, TextAnchor.UpperLeft);
            battleLog.rectTransform.Stretch(40, 380, 40, 24);
            tacticRow = UIKit.Rect("Tactics", bottom).Stretch(32, 30, 32, 280);
            var g = UIKit.Grid(tacticRow, 2, 110, 18); g.cellSize = new Vector2(490, 110);
            foreach (var t in new[] { Tactic.Charge, Tactic.Fire, Tactic.Defend, Tactic.Retreat })
            {
                var tt = t;
                UIKit.Button(tacticRow, Tactic.Label(t), () => { if (!busy) StartCoroutine(TacticRound(tt)); }, t == Tactic.Charge ? Palette.Red : Palette.Paper2, t == Tactic.Charge ? Color.white : Palette.Ink, 44);
            }
            UIKit.Button(tacticRow, "모의전으로 끝내기", () => { if (!busy) StartCoroutine(AutoBattle()); }, Palette.Ink, Palette.Paper, 38);
            var hint = UIKit.Label(tacticRow, "<size=26>돌격: 큰 피해를 주고 받음 · 화계: 지력 · 방어: 사기 회복</size>", 26, Palette.Ink3, TextAnchor.MiddleCenter);
            _ = hint;
            UpdateBattleHud();
        }

        (Text, Image, Image) SidePanel(Transform parent, Side side)
        {
            var p = UIKit.Panel(parent, Palette.Paper2, "Side");
            UIKit.VList(p.rectTransform, 8, 20);
            var col = None(side.faction) ? Palette.Neutral : Palette.Hex(S.Faction(side.faction).color);
            var t = UIKit.Label(p.transform, "", 30); UIKit.Size(t.rectTransform, -1, 80);
            var tr = UIKit.Bar(p.transform, col, 16);
            var mo = UIKit.Bar(p.transform, Palette.Ink3, 10);
            return (t, tr, mo);
        }

        void UpdateBattleHud()
        {
            if (battle == null || battleHud == null) return;
            roundText.text = $"{CityName(S, battle.city)} 전투 · {battle.round}/{battle.maxRounds}합";
            string Names(Side sd) => sd.officers.Count == 0 ? "장수 없음" : string.Join(", ", sd.officers.Select(id => S.Officer(id).name));
            atkText.text = $"<b>공격</b> {FactionName(S, battle.attacker.faction)}\n<size=26>{Names(battle.attacker)} · {Fmt(battle.attacker.troops)} · 사기 {Round(battle.attacker.morale)}</size>";
            defText.text = $"<b>수비</b> {FactionName(S, battle.defender.faction)}\n<size=26>{Names(battle.defender)} · {Fmt(battle.defender.troops)} · 사기 {Round(battle.defender.morale)}</size>";
            atkTroops.fillAmount = (float)(battle.attacker.troops / System.Math.Max(1, battle.attacker.start));
            defTroops.fillAmount = (float)(battle.defender.troops / System.Math.Max(1, battle.defender.start));
            atkMorale.fillAmount = (float)(battle.attacker.morale / 100);
            defMorale.fillAmount = (float)(battle.defender.morale / 100);
            battleLog.text = string.Join("\n", battle.log.Skip(System.Math.Max(0, battle.log.Count - 4)));
        }

        IEnumerator TacticRound(string tactic)
        {
            busy = true;
            BattleSystem.PlayRound(S, battle, tactic);
            var fx = battle.lastFx;
            if (fx != null && fx.duel) StartCoroutine(DuelCutIn(fx));
            yield return battleView.PlayRound(battle, fx);
            UpdateBattleHud();
            CheckAchievements(fx, true);
            busy = false;
            if (battle.done) ShowBattleResult();
        }

        IEnumerator AutoBattle()
        {
            busy = true;
            int guard = 0;
            while (!battle.done && guard++ < 50)
            {
                BattleSystem.PlayRound(S, battle);
                if (battle.lastFx != null && battle.lastFx.duel) StartCoroutine(DuelCutIn(battle.lastFx));
                yield return battleView.PlayRound(battle, battle.lastFx);
                UpdateBattleHud();
                CheckAchievements(battle.lastFx, true);
            }
            busy = false;
            ShowBattleResult();
        }

        /// <summary>일기토 컷인: 붓 획 위로 두 장수의 이름이 양쪽에서 미끄러져 들어온다</summary>
        IEnumerator DuelCutIn(RoundFx fx)
        {
            var band = UIKit.Rect("Duel", overlay.transform);
            band.anchorMin = new Vector2(0, 0.5f); band.anchorMax = new Vector2(1, 0.5f); band.sizeDelta = new Vector2(0, 360); band.anchoredPosition = new Vector2(0, 260);
            var stroke = band.gameObject.AddComponent<Image>(); stroke.sprite = ProcArt.Brush; stroke.color = Palette.Ink.WithA(0.92f); stroke.raycastTarget = false;
            var w = S.Officer(fx.duelWinner); var l = S.Officer(fx.duelLoser);
            var a = UIKit.Label(band, $"{w.name}", 96, Palette.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            var b = UIKit.Label(band, $"{l.name}", 72, Palette.Paper.WithA(0.6f), TextAnchor.MiddleCenter);
            var title = UIKit.Label(band, "일 기 토", 44, Palette.Red, TextAnchor.UpperCenter, FontStyle.Bold);
            title.rectTransform.Stretch(0, 0, 0, 10);
            a.rectTransform.sizeDelta = b.rectTransform.sizeDelta = new Vector2(600, 160);
            a.rectTransform.anchoredPosition = new Vector2(-1200, 30); b.rectTransform.anchoredPosition = new Vector2(1200, -60);
            StartCoroutine(Tw.Anchored(a.rectTransform, new Vector2(-180, 30), 0.35f, Ease.OutBack));
            yield return Tw.Anchored(b.rectTransform, new Vector2(220, -60), 0.35f, Ease.OutBack);
            yield return Tw.Wait(1.1f);
            var cg = band.gameObject.AddComponent<CanvasGroup>();
            yield return Tw.Fade(cg, 0, 0.3f);
            Destroy(band.gameObject);
        }

        void ShowBattleResult()
        {
            foreach (var c in S.captives.Skip(captivesBefore)) if (c.by == S.player) battleCaptives.Add(c.officer);
            captivesBefore = S.captives.Count;
            if (resultRoot) Destroy(resultRoot.gameObject);
            tacticRow.gameObject.SetActive(false);
            var bottom = (RectTransform)tacticRow.parent;
            resultRoot = UIKit.Rect("Result", bottom).Stretch(32, 30, 32, 280);
            var list = UIKit.Scroll(resultRoot, 14, 0);   // 포로가 많아도 스크롤된다
            bool win = battle.result == "attacker";
            if (resultSoundPending) { resultSoundPending = false; SoundKit.Play(win ? Sfx.Fanfare : Sfx.Gong, 0.8f); if (win) Haptics.Heavy(); }
            var back = UIKit.Button(list, "돌아가기", () => { if (!busy) StartCoroutine(EndBattle()); }, Palette.Ink, Palette.Paper, 42);
            UIKit.Size((RectTransform)back.transform, -1, 120);
            var t = UIKit.Label(list, win ? "<b>승리</b> — 성을 얻었다" : "<b>패배</b> — 성을 얻지 못했다", 44, win ? Palette.Good : Palette.Red);
            UIKit.Size(t.rectTransform, -1, 70);
            foreach (var cap in S.captives.Where(c => c.by == S.player && battleCaptives.Contains(c.officer)).ToList())
            {
                var o = S.Officer(cap.officer); var id = o.id;
                var row = UIKit.Rect("Captive", list); UIKit.Size(row, -1, 100); UIKit.HList(row, 12);
                var name = UIKit.Label(row, $"포로 <b>{o.name}</b>", 34); UIKit.Size(name.rectTransform, 300, -1);
                UIKit.Button(row, "등용", () => CaptiveRecruit(id), Palette.Paper2, Palette.Ink, 32);
                UIKit.Button(row, "해방", () => { Toast(Commands.ReleaseCaptive(S, S.player, id, false).msg); ShowBattleResult(); }, Palette.Paper2, Palette.Ink, 32);
                UIKit.Button(row, "처형", () => { Toast(Commands.ReleaseCaptive(S, S.player, id, true).msg); ShowBattleResult(); }, Palette.Paper2, Palette.Red, 32);
            }
        }

        void CaptiveRecruit(string id)
        {
            var city = S.City(battle.city);
            var o = CityOfficers(S, city.id, S.player).OrderByDescending(x => x.stats.cha).FirstOrDefault();
            if (o == null) { Toast("설득할 장수가 없다."); return; }
            o.acted = false;                 // 전투 직후 설득은 이번 달 행동으로 치지 않는다
            S.Officer(id).city = city.id;
            var r = Commands.Recruit(S, S.player, o.id, id);
            o.acted = true;
            Toast(r.msg);
            ShowBattleResult();
        }

        IEnumerator EndBattle()
        {
            busy = true;
            yield return WipeIn();
            battleView.Clear();
            if (battleHud) Destroy(battleHud.gameObject);
            inBattle = false;
            rig.position = rigBeforeBattle; cam.orthographicSize = sizeBeforeBattle;
            if (hudRoot) hudRoot.gameObject.SetActive(true);
            selected = battle.city;
            map.Select(selected);
            yield return WipeOut();
            map.Refresh(S, true);   // 함락했다면 여기서 먹이 번진다
            S.events.Clear();
            battle = null;
            Save();
            RefreshAll();
            busy = false;
            CheckAchievements();
            if (S.over) ShowEnd();
        }
    }
}
