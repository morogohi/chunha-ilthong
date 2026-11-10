// 화면 구성: 타이틀, 상단·하단 바, 도시 카드, 명령 시트, 출진 시트, 월간 보고(두루마리), 기록, 엔딩.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chunha.Art;
using Chunha.Core;
using Chunha.FX;
using Chunha.UI;
using UnityEngine;
using UnityEngine.UI;
using static Chunha.Core.G;

namespace Chunha
{
    public partial class GameRoot
    {
        RectTransform titleRoot, hudRoot, cityCard, cityContent, playbackBar;
        Text dateText, factionText, resText, playbackText;
        Button undoBtn, endBtn;
        Sheet sheet;
        Image wipe;
        bool sheetOpen => sheet != null && sheet.Open;
        string cmdTab = "domestic";

        static readonly Dictionary<string, string> Difficulty = new Dictionary<string, string>
        { ["cao"] = "보통", ["yuan"] = "쉬움", ["liu"] = "어려움", ["sun"] = "보통", ["biao"] = "보통", ["ma"] = "어려움" };

        // ───────────────────────── 타이틀
        void ShowTitle()
        {
            titleRoot = UIKit.Rect("Title", safeRoot).Stretch();
            var bg = titleRoot.gameObject.AddComponent<Image>(); bg.color = Palette.Paper.WithA(0.92f);
            var content = UIKit.Scroll(titleRoot, 24, 64);

            var seal = UIKit.Rect("SealRow", content); UIKit.HList(seal, 20).childForceExpandWidth = false; UIKit.Size(seal, -1, 90);
            Seal(seal, 90);
            var sub = UIKit.Label(seal, "기록관 허문이 이 시대를 적는다", 34, Palette.Ink3); UIKit.Size(sub.rectTransform, 700, 90);

            var title = UIKit.Label(content, "천하일통", 150, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold); UIKit.Size(title.rectTransform, -1, 210);
            var lead = UIKit.Label(content, $"{ScenarioYear}년, 시나리오 「{ScenarioTitle}」. 12개의 성을 두고 여섯 군웅이 맞선다. 한 달에 한 번, 군주로서 명령을 내려 천하를 하나로 묶어라.", 38, Palette.Ink2);
            UIKit.Size(lead.rectTransform, -1, 170);

            var saved = Load();
            if (saved != null && !saved.over)
                UIKit.Button(content, $"이어하기 — {saved.year}년 {saved.month}월 {FactionName(saved, saved.player)}", Continue, Palette.Red, Color.white, 40);

            var h = UIKit.Label(content, "군주를 고른다", 44, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold); UIKit.Size(h.rectTransform, -1, 90);
            var preview = NewGame("cao", 1);
            foreach (var id in FactionOrder)
            {
                var f = preview.Faction(id);
                var cs = FactionCities(preview, id);
                var ruler = preview.Officer(f.ruler);
                var fid = id;
                var b = UIKit.Button(content, "", () => StartNew(fid), Palette.Paper2);
                UIKit.Size((RectTransform)b.transform, -1, 170);
                var row = UIKit.Rect("Row", b.transform).Stretch(28, 16, 28, 16);
                var hl = UIKit.HList(row, 28); hl.childForceExpandWidth = false;
                var sw = UIKit.Panel(row, Palette.Hex(f.color), "Swatch"); UIKit.Size(sw.rectTransform, 24, -1);
                var txt = UIKit.Label(row, $"<b>{ruler.name}</b>\n<size=30>{f.name} · {cs.Count}성 · 병력 {Fmt(cs.Sum(c => c.troops))}</size>", 46);
                UIKit.Size(txt.rectTransform, 680, -1);
                var d = UIKit.Label(row, Difficulty[id], 32, Palette.Ink2, TextAnchor.MiddleRight); UIKit.Size(d.rectTransform, 160, -1);
            }
            var foot = UIKit.Label(content, $"Unity 버전 v0.1 · 12성 축소판 · 개전 후 {GraceMonths}개월은 AI가 플레이어를 공격하지 않는다", 28, Palette.Ink3);
            UIKit.Size(foot.rectTransform, -1, 100);
        }

        void ClearTitle() { if (titleRoot) Destroy(titleRoot.gameObject); }

        void Seal(Transform parent, float size)
        {
            var s = UIKit.Panel(parent, Palette.Seal, "Seal", false);
            UIKit.Size(s.rectTransform, size, size);
            var t = UIKit.Label(s.transform, "許", (int)(size * 0.62f), Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
        }

        // ───────────────────────── 상단·하단 바 + 도시 카드
        void BuildHud()
        {
            if (hudRoot) Destroy(hudRoot.gameObject);
            hudRoot = UIKit.Rect("HUD", safeRoot).Stretch();

            var top = UIKit.Panel(hudRoot, Palette.Paper.WithA(0.96f), "Top", false).rectTransform.Dock(true, 190);
            dateText = UIKit.Label(top, "", 58, Palette.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            dateText.rectTransform.Stretch(40, 70, 400, 24);
            factionText = UIKit.Label(top, "", 34, Palette.Ink2, TextAnchor.UpperRight);
            factionText.rectTransform.Stretch(400, 70, 40, 34);
            resText = UIKit.Label(top, "", 32, Palette.Ink2, TextAnchor.LowerLeft);
            resText.rectTransform.Stretch(40, 18, 40, 110);
            var topLine = UIKit.Panel(top, Palette.Line, "Line", false).rectTransform.Dock(false, 3);
            _ = topLine;

            var bottom = UIKit.Panel(hudRoot, Palette.Paper.WithA(0.98f), "Bottom", false).rectTransform.Dock(false, 190);
            var row = UIKit.Rect("Row", bottom).Stretch(32, 30, 32, 30);
            UIKit.HList(row, 20);
            var logB = UIKit.Button(row, "기록", ShowLog); UIKit.Size((RectTransform)logB.transform, -1, -1, 1);
            undoBtn = UIKit.Button(row, "되돌리기", Undo); UIKit.Size((RectTransform)undoBtn.transform, -1, -1, 1.2f);
            endBtn = UIKit.Button(row, "턴 종료", EndTurn, Palette.Ink, Palette.Paper2, 46); UIKit.Size((RectTransform)endBtn.transform, -1, -1, 2);

            var card = UIKit.Panel(hudRoot, Palette.Paper2.WithA(0.97f), "CityCard");
            cityCard = card.rectTransform;
            cityCard.anchorMin = new Vector2(0, 0); cityCard.anchorMax = new Vector2(1, 0); cityCard.pivot = new Vector2(0.5f, 0);
            cityCard.sizeDelta = new Vector2(-24, 600);   // 지도가 더 보이도록 낮게 두고 내용은 스크롤 cityCard.anchoredPosition = new Vector2(0, 200);
            cityContent = UIKit.Scroll(cityCard, 18, 36);

            playbackBar = UIKit.Panel(hudRoot, Palette.Ink.WithA(0.88f), "Playback").rectTransform;
            playbackBar.anchorMin = new Vector2(0.04f, 1); playbackBar.anchorMax = new Vector2(0.96f, 1); playbackBar.pivot = new Vector2(0.5f, 1);
            playbackBar.sizeDelta = new Vector2(0, 150); playbackBar.anchoredPosition = new Vector2(0, -210);
            playbackText = UIKit.Label(playbackBar, "", 36, Palette.Paper, TextAnchor.MiddleLeft);
            playbackText.rectTransform.Stretch(36, 0, 260, 0);
            var skip = UIKit.Button(playbackBar, "건너뛰기", SkipPlayback, Palette.Paper.WithA(0.15f), Palette.Paper, 34);
            var srt = (RectTransform)skip.transform; srt.anchorMin = new Vector2(1, 0.5f); srt.anchorMax = new Vector2(1, 0.5f); srt.pivot = new Vector2(1, 0.5f);
            srt.sizeDelta = new Vector2(220, 100); srt.anchoredPosition = new Vector2(-24, 0);
            playbackBar.gameObject.SetActive(false);

            RefreshAll();
        }

        void ShowPlaybackBar(bool on) { if (playbackBar) playbackBar.gameObject.SetActive(on); if (cityCard) cityCard.gameObject.SetActive(!on); }
        void SetPlaybackText(string t) { if (playbackText) playbackText.text = t; }

        public void RefreshAll()
        {
            if (S == null || hudRoot == null) return;
            var f = S.Faction(S.player);
            var cs = FactionCities(S, S.player);
            dateText.text = $"{S.year}년 {S.month}월";
            factionText.text = $"<color={f.color}>■</color> {f.name} · 천명 {Round(f.mandate)}";
            resText.text = $"{cs.Count}성   병력 {Fmt(cs.Sum(c => c.troops))}   금 {Fmt(cs.Sum(c => c.gold))}   병량 {Fmt(cs.Sum(c => c.food))}";
            undoBtn.SetText($"되돌리기 {history.Count}");
            undoBtn.interactable = history.Count > 0;
            RefreshCityCard();
        }

        void RefreshCityCard()
        {
            if (cityContent == null || S == null) return;
            UIKit.Clear(cityContent);
            var c = S.City(selected);
            if (c == null) return;
            bool mine = c.faction == S.player;
            var col = None(c.faction) ? Palette.Neutral : Palette.Hex(S.Faction(c.faction).color);
            var os = None(c.faction) ? new List<Officer>() : CityOfficers(S, c.id, c.faction);
            var gov = None(c.governor) ? null : S.Officer(c.governor);

            var head = UIKit.Label(cityContent, $"<b><size=64>{c.name}</size></b>   <color=#{ColorUtility.ToHtmlStringRGB(col)}>■</color> {FactionName(S, c.faction)}   <size=30>{(gov != null ? "태수 " + gov.name : "태수 없음")}{(c.river ? " · 수상 도시" : "")}</size>", 36);
            UIKit.Size(head.rectTransform, -1, 90);
            var pend = S.pending.FirstOrDefault(p => p.city == c.id);
            if (pend != null && mine) { var w = UIKit.Label(cityContent, $"다음 달 재해 조짐: {TurnSystem.EventName(pend.kind)}", 32, Palette.Red); UIKit.Size(w.rectTransform, -1, 50); }

            var nums = UIKit.Rect("Nums", cityContent); UIKit.Size(nums, -1, 120); UIKit.HList(nums, 14);
            NumBox(nums, "병력", Fmt(c.troops)); NumBox(nums, "훈련", Fmt(c.training));
            NumBox(nums, "금", mine ? Fmt(c.gold) : "?"); NumBox(nums, "병량", mine ? Fmt(c.food) : "?");

            var grid = UIKit.Rect("Stats", cityContent); var g = UIKit.Grid(grid, 2, 64, 18); g.cellSize = new Vector2(470, 64); UIKit.Size(grid, -1, 3 * 82);
            StatBar(grid, "농업", c.agri, 999); StatBar(grid, "상업", c.commerce, 999); StatBar(grid, "치수", c.flood, 100);
            StatBar(grid, "민심", c.loyalty, 100); StatBar(grid, "성벽", c.wall, 100); StatBar(grid, "인구", c.pop, 200);

            if (mine)
            {
                var pl = UIKit.Label(cityContent, "운영 방침 — 직할이 아니면 남은 장수가 턴 종료 때 알아서 일한다", 28, Palette.Ink3); UIKit.Size(pl.rectTransform, -1, 46);
                var pills = UIKit.Rect("Policy", cityContent); UIKit.Size(pills, -1, 100); UIKit.HList(pills, 12);
                foreach (var p in Policy.All)
                {
                    var pp = p; bool on = c.policy == p;
                    UIKit.Button(pills, Policy.Label(p), () => { if (!busy && !inBattle) Apply(Commands.SetPolicy(S, S.player, selected, pp)); }, on ? Palette.Ink : Palette.Paper, on ? Palette.Paper : Palette.Ink, 34);
                }
                bool idle = os.Any(o => !o.acted);
                var acts = UIKit.Rect("Actions", cityContent); UIKit.Grid(acts, 3, 130, 16); UIKit.Size(acts, -1, 2 * 146);
                ActBtn(acts, "내정", idle, () => OpenCommands("domestic"));
                ActBtn(acts, "인사", idle, () => OpenCommands("people"));
                ActBtn(acts, "외교", idle, () => OpenCommands("diplomacy"));
                ActBtn(acts, "계략", idle, () => OpenCommands("scheme"));
                ActBtn(acts, "출진", idle, () => OpenAttack(selected, null), true);
                ActBtn(acts, "수송", idle, QuickTransport);
            }
            else if (IsHostile(S, S.player, c.faction) && Neighbors(S, c.id).Any(n => S.City(n).faction == S.player))
            {
                var b = UIKit.Button(cityContent, $"{c.name} 공격 준비", () =>
                {
                    var from = Neighbors(S, c.id).Where(n => S.City(n).faction == S.player).OrderByDescending(n => S.City(n).troops).First();
                    OpenAttack(from, c.id);
                }, Palette.Red, Color.white, 44);
                UIKit.Size((RectTransform)b.transform, -1, 130);
            }

            foreach (var o in os)
            {
                var t = o.stats;
                var role = IsRuler(S, o) ? " · 군주" : c.governor == o.id ? " · 태수" : "";
                var oid = o.id;
                var ob = UIKit.Button(cityContent, "", () => OpenOfficer(oid), Palette.Paper);
                UIKit.Size((RectTransform)ob.transform, -1, 120);
                var row = UIKit.Label(ob.transform, $"<b>{o.name}</b>{role}   <size=28>{(o.acted ? "행동 완료" : "대기")}  ›</size>\n<size=28>무 {t.war} · 지 {t.@int} · 정 {t.pol} · 매 {t.cha} · 육 {t.land} · 수 {t.naval}{(IsRuler(S, o) ? "" : $" · 충성 {Round(o.loyalty)}")}</size>",
                    38, o.acted ? Palette.Ink3 : Palette.Ink);
                row.rectTransform.Stretch(28, 6, 28, 6);
            }
            if (mine)
            {
                var free = FreeOfficers(S, c.id).Where(o => o.found).Select(o => o.name).ToList();
                if (free.Count > 0) { var fr = UIKit.Label(cityContent, "재야: " + string.Join(", ", free), 32, Palette.Ink2); UIKit.Size(fr.rectTransform, -1, 60); }
            }
        }

        void NumBox(Transform p, string label, string value)
        {
            var b = UIKit.Panel(p, Palette.Paper, "Num");
            var t = UIKit.Label(b.transform, $"<size=26>{label}</size>\n<b>{value}</b>", 40, Palette.Ink, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch();
        }

        void StatBar(Transform p, string label, double v, double max)
        {
            var cell = UIKit.Rect("Stat", p);
            UIKit.VList(cell, 6);
            var t = UIKit.Label(cell, $"{label}  <b>{Fmt(v)}</b>", 30, Palette.Ink2); UIKit.Size(t.rectTransform, -1, 38);
            var bar = UIKit.Bar(cell, Palette.Ink, 14);
            bar.fillAmount = Mathf.Clamp01((float)(v / max));
        }

        void ActBtn(Transform p, string label, bool on, System.Action act, bool primary = false)
        {
            var b = UIKit.Button(p, label, act, primary ? Palette.Red : Palette.Paper, primary ? Color.white : Palette.Ink, 44);
            b.interactable = on;
        }

        void QuickTransport()
        {
            if (busy || inBattle) return;
            var c = S.City(selected);
            var dests = Neighbors(S, c.id).Where(n => S.City(n).faction == S.player).Select(S.City).ToList();
            var o = CityOfficers(S, c.id, S.player).FirstOrDefault(x => !x.acted);
            if (dests.Count == 0 || o == null) { Toast("수송할 인접 아군 도시가 없다."); return; }
            // 가장 병력이 적은(위협받는) 인접 아군 도시로 병력 절반, 금·병량 1/3을 보낸다
            var dst = dests.OrderBy(d => d.troops).First();
            Apply(Commands.Transport(S, S.player, o.id, dst.id, System.Math.Floor(c.troops / 2), System.Math.Floor(c.gold / 3), System.Math.Floor(c.food / 3)));
        }

        // ───────────────────────── 명령 시트
        void OpenCommands(string tab)
        {
            if (busy || inBattle) return;
            cmdTab = tab;
            sheet?.Close();
            sheet = new Sheet(canvas.transform, this, $"{S.City(selected).name} · 명령", 0.74f);
            sheet.onClosed = RefreshAll;
            FillCommands();
        }

        void FillCommands()
        {
            if (!sheetOpen) return;
            var root = sheet.content;
            UIKit.Clear(root);
            var c = S.City(selected);
            var idle = CityOfficers(S, c.id, S.player).Where(o => !o.acted).ToList();
            var tabs = UIKit.Rect("Tabs", root); UIKit.Size(tabs, -1, 110); UIKit.HList(tabs, 14);
            foreach (var (k, l) in new[] { ("domestic", "내정"), ("people", "인사"), ("diplomacy", "외교"), ("scheme", "계략") })
            {
                var kk = k; bool on = cmdTab == k;
                UIKit.Button(tabs, l, () => { cmdTab = kk; FillCommands(); }, on ? Palette.Ink : Palette.Paper2, on ? Palette.Paper : Palette.Ink, 38);
            }
            if (idle.Count == 0) { Info(root, "이번 달 움직일 장수가 없다."); return; }

            if (cmdTab == "domestic")
            {
                foreach (var cmd in Commands.AllDomestic)
                {
                    var info = Commands.Info(cmd);
                    var best = Best(idle, info.stat)[0];
                    var eff = Commands.DomesticEffect(best, cmd);
                    var cc = cmd;
                    Choice(root, $"{info.label}  <size=28>{info.cost} · {best.name}</size>", $"+{Fmt(eff)}", () =>
                    {
                        var r = Commands.Domestic(S, S.player, best.id, cc);
                        Apply(r, selected, cc == Dom.Draft || cc == Dom.Train ? Palette.Red : Palette.Gold);
                        FillCommands();
                    });
                }
                Info(root, "해당 능력치가 가장 높은 대기 장수가 맡는다.");
            }
            else if (cmdTab == "people")
            {
                var ruler = idle.FirstOrDefault(o => IsRuler(S, o));
                var searcher = ruler ?? idle.OrderByDescending(o => o.stats.@int + o.stats.cha).First();
                Choice(root, $"인재 탐색  <size=28>{searcher.name}{(ruler != null ? " (군주 직접)" : "")}</size>", "탐색", () => { Apply(Commands.Search(S, S.player, searcher.id), selected); FillCommands(); });
                var recruiter = idle.OrderByDescending(o => o.stats.cha).First();
                foreach (var t in FreeOfficers(S, c.id).Where(o => o.found && o.special != "thrice_visit"))
                {
                    var tt = t;
                    Choice(root, $"등용: {t.name}  <size=28>지 {t.stats.@int} · 무 {t.stats.war} · {recruiter.name}</size>", $"{Round(Commands.RecruitChance(S, recruiter, t, S.player) * 100)}%",
                        () => { Apply(Commands.Recruit(S, S.player, recruiter.id, tt.id), selected); FillCommands(); });
                }
                foreach (var d in Neighbors(S, c.id).Where(n => S.City(n).faction == S.player))
                    foreach (var o in idle)
                    {
                        var oo = o; var dd = d;
                        Choice(root, $"이동: {o.name} → {CityName(S, d)}", "이동", () => { Apply(Commands.MoveOfficer(S, S.player, oo.id, dd)); FillCommands(); });
                    }
            }
            else if (cmdTab == "diplomacy")
            {
                var envoy = idle.OrderByDescending(o => o.stats.cha).First();
                foreach (var f in S.factions.Where(x => x.alive && x.id != S.player))
                {
                    var rel = RelationOf(S, S.player, f.id); var ff = f;
                    Info(root, $"<color={f.color}>■</color> <b>{f.name}</b>   우호 {Round(rel.favor)}{(rel.alliance > 0 ? $" · 동맹 {rel.alliance}개월" : "")}");
                    if (rel.alliance <= 0)
                        Choice(root, $"동맹 제안  <size=28>금 300 · {envoy.name}</size>", $"{Round(Commands.AllianceChance(S, envoy, S.player, f.id) * 100)}%",
                            () => { Apply(Commands.ProposeAlliance(S, S.player, envoy.id, ff.id), selected); FillCommands(); });
                    Choice(root, "예물  <size=28>금 300 · 우호 상승</size>", $"+{Round(5 + envoy.stats.cha / 10.0)}",
                        () => { Apply(Commands.SendGift(S, S.player, envoy.id, ff.id), selected); FillCommands(); });
                }
            }
            else
            {
                var plotter = idle.OrderByDescending(o => o.stats.@int).First();
                var adj = Neighbors(S, c.id).Select(S.City).Where(n => !None(n.faction) && IsHostile(S, S.player, n.faction)).ToList();
                if (adj.Count == 0) Info(root, "인접한 적 도시가 없다.");
                foreach (var n in adj)
                {
                    var nn = n;
                    Info(root, $"<b>{n.name}</b>  <size=28>{FactionName(S, n.faction)}</size>");
                    Choice(root, $"유언비어  <size=28>민심 하락 · {plotter.name}</size>", $"민심 {Round(n.loyalty)}", () => { Apply(Commands.Rumor(S, S.player, plotter.id, nn.id)); FillCommands(); });
                    foreach (var t in CityOfficers(S, n.id, n.faction).Where(o => !IsRuler(S, o)))
                    {
                        var tt = t;
                        Choice(root, $"이간: {t.name}  <size=28>충성 하락</size>", $"{Round(Commands.SchemeChance(plotter, t) * 100)}%", () => { Apply(Commands.Sow(S, S.player, plotter.id, tt.id)); FillCommands(); });
                    }
                }
            }
        }

        void Choice(Transform root, string label, string right, System.Action act)
        {
            var b = UIKit.Button(root, "", act, Palette.Paper2);
            UIKit.Size((RectTransform)b.transform, -1, 130);
            var l = UIKit.Label(b.transform, label, 38); l.rectTransform.Stretch(32, 0, 220, 0);
            var r = UIKit.Label(b.transform, right, 38, Palette.Ink, TextAnchor.MiddleRight, FontStyle.Bold); r.rectTransform.Stretch(700, 0, 32, 0);
        }

        void Info(Transform root, string text)
        {
            var t = UIKit.Label(root, text, 32, Palette.Ink2);
            UIKit.Size(t.rectTransform, -1, 70);
        }

        // ───────────────────────── 월간 보고 (두루마리)
        void ShowReport()
        {
            sheet?.Close();
            sheet = new Sheet(overlay.transform, this, $"{S.year}년 {S.month}월 보고", 0.7f, false);
            var root = sheet.content;
            var head = UIKit.Rect("Head", root); UIKit.Size(head, -1, 110); UIKit.HList(head, 24).childForceExpandWidth = false;
            Seal(head, 100);
            var who = UIKit.Label(head, "<b>기록관 허문</b>", 40); UIKit.Size(who.rectTransform, 600, 100);
            var paper = UIKit.Panel(root, Palette.Paper2, "Scroll");
            UIKit.VList(paper.rectTransform, 16, 36);
            UIKit.FitHeight(paper.rectTransform);
            foreach (var line in S.report)
            {
                var t = UIKit.Label(paper.transform, line, 36, Palette.Ink);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIKit.Size(t.rectTransform, -1, 46 + 44 * Mathf.Max(0, line.Length / 26));
            }
            var go = UIKit.Button(root, "명령을 내린다", () => sheet.Close(), Palette.Red, Color.white, 44);
            UIKit.Size((RectTransform)go.transform, -1, 130);
            StartCoroutine(Unroll(paper.rectTransform, head.GetChild(0)));
        }

        IEnumerator Unroll(RectTransform paper, Transform seal)
        {
            paper.localScale = new Vector3(1, 0, 1);
            SoundKit.Play(Sfx.Swish, 0.8f);
            seal.localScale = Vector3.one * 2.2f;
            seal.localRotation = Quaternion.Euler(0, 0, -18);
            yield return Tw.Wait(0.25f);
            SoundKit.Play(Sfx.Stamp);
            StartCoroutine(Tw.Run(0.3f, k => { if (seal) { seal.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k); seal.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-18, -4, k)); } }, Ease.OutBack));
            yield return Tw.Scale(paper, Vector3.one, 0.55f, Ease.OutCubic);
        }

        // ───────────────────────── 기록·엔딩
        void ShowLog()
        {
            if (busy) return;
            sheet?.Close();
            sheet = new Sheet(canvas.transform, this, "기록", 0.8f);
            var root = sheet.content;
            Info(root, "<b>나만의 삼국사</b>");
            if (S.chronicle.Count == 0) Info(root, "아직 적힌 일이 없다.");
            foreach (var c in Enumerable.Reverse(S.chronicle)) Info(root, c);
            Info(root, "<b>최근 소식</b>");
            foreach (var l in S.log.Skip(System.Math.Max(0, S.log.Count - 40)).Reverse())
                Info(root, $"<color={(l.tone == "good" ? "#3F6B3A" : l.tone == "bad" ? "#B5432F" : "#45413B")}>{l.t / 12}년 {l.t % 12 + 1}월 · {l.text}</color>");
            AppendSettings(root);
            var b = UIKit.Button(root, "처음부터 다시", () => { ClearSave(); sheet.Close(); BackToTitle(); }, Palette.Paper2);
            UIKit.Size((RectTransform)b.transform, -1, 120);
        }

        void ShowEnd()
        {
            sheet?.Close();
            bool win = S.winner == S.player;
            SoundKit.Play(Sfx.Gong);
            if (win) { SoundKit.Play(Sfx.Fanfare); Haptics.Heavy(); }
            sheet = new Sheet(overlay.transform, this, win ? "천하일통" : "기록의 끝", 0.8f, false);
            var root = sheet.content;
            var head = UIKit.Rect("Head", root); UIKit.Size(head, -1, 110); UIKit.HList(head, 24).childForceExpandWidth = false;
            Seal(head, 100);
            Info(root, win ? $"{FactionName(S, S.player)}이(가) {S.year}년 {S.month}월, 천하를 하나로 묶었다." : $"{FactionName(S, S.player)}의 기록은 {S.year}년 {S.month}월에서 끊긴다.");
            foreach (var c in S.chronicle) Info(root, c);
            var b = UIKit.Button(root, "새로 시작", () => { ClearSave(); sheet.Close(); BackToTitle(); }, Palette.Red, Color.white);
            UIKit.Size((RectTransform)b.transform, -1, 130);
            if (win) for (int i = 0; i < 6; i++) Particles.Sparkle(map.fxRoot, rig.position + new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 5f), 0), Palette.Gold);
        }

        void BackToTitle()
        {
            S = null;
            if (map) Destroy(map.gameObject);
            if (hudRoot) Destroy(hudRoot.gameObject);
            ShowTitleV2();
        }

        // ───────────────────────── 화면 전환 (먹 와이프)
        void BuildWipe()
        {
            wipe = UIKit.Panel(overlay.transform, Palette.Ink, "Wipe", false);
            wipe.rectTransform.Stretch();
            wipe.type = Image.Type.Filled;
            wipe.fillMethod = Image.FillMethod.Horizontal;
            wipe.sprite = ProcArt.White;   // null이면 fill이 무시되어 화면 전체를 덮는다
            wipe.fillAmount = 0;
            wipe.raycastTarget = false;
        }

        IEnumerator WipeIn() { SoundKit.Play(Sfx.Swish, 0.8f); wipe.fillOrigin = 0; wipe.raycastTarget = true; yield return Tw.Fill(wipe, 1, 0.35f, Ease.InOutCubic); }
        IEnumerator WipeOut() { wipe.fillOrigin = 1; yield return Tw.Fill(wipe, 0, 0.35f, Ease.InOutCubic); wipe.raycastTarget = false; }
    }
}
