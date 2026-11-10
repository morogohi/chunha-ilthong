// v0.2 추가 화면: 애니메이션 타이틀, 장수 카드(레이더 차트), 업적 팝업·목록, 첫 플레이 안내, 설정, 계절 색조.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chunha.Art;
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
        const string AchKey = "chunha-achievements-v1";
        const string TutorialKey = "chunha-tutorial-done";
        AchievementSave achievements;
        TitleScene titleScene;
        readonly Queue<AchievementDef> achQueue = new Queue<AchievementDef>();
        bool achShowing;

        // ───────────────────────── 업적
        void LoadAchievements()
        {
            try { achievements = JsonUtility.FromJson<AchievementSave>(PlayerPrefs.GetString(AchKey, "")) ?? new AchievementSave(); }
            catch { achievements = new AchievementSave(); }
            if (achievements.unlocked == null) achievements.unlocked = new List<string>();
        }

        void CheckAchievements(RoundFx fx = null, bool playerAttacking = true)
        {
            if (S == null || achievements == null) return;
            var got = Achievements.Evaluate(S, achievements, fx, playerAttacking);
            if (got.Count == 0) return;
            PlayerPrefs.SetString(AchKey, JsonUtility.ToJson(achievements));
            PlayerPrefs.Save();
            foreach (var a in got) achQueue.Enqueue(a);
            if (!achShowing) StartCoroutine(ShowAchievements());
        }

        /// <summary>업적 달성: 위에서 내려오는 두루마리 띠 + 낙관 찍힘 + 팡파르</summary>
        IEnumerator ShowAchievements()
        {
            achShowing = true;
            while (achQueue.Count > 0)
            {
                var a = achQueue.Dequeue();
                var band = UIKit.Panel(overlay.transform, Palette.Ink.WithA(0.94f), "Achievement");
                var rt = band.rectTransform;
                rt.anchorMin = new Vector2(0.05f, 1); rt.anchorMax = new Vector2(0.95f, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 190); rt.anchoredPosition = new Vector2(0, 220);
                var row = UIKit.Rect("Row", rt).Stretch(30, 20, 30, 20);
                var hl = UIKit.HList(row, 28); hl.childForceExpandWidth = false;
                var seal = UIKit.Panel(row, Palette.Seal, "Seal", false); UIKit.Size(seal.rectTransform, 140, 140);
                var st = UIKit.Label(seal.transform, "業", 80, Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold); st.rectTransform.Stretch();
                var txt = UIKit.Label(row, $"<size=30>업적 달성</size>\n<b>{a.title}</b>\n<size=28>{a.desc}</size>", 46, Palette.Paper);
                UIKit.Size(txt.rectTransform, 760, -1);
                SoundKit.Play(Sfx.Fanfare, 0.8f);
                yield return Tw.Anchored(rt, new Vector2(0, -24 - SafeArea.TopInset()), 0.45f, Ease.OutBack);
                seal.rectTransform.localScale = Vector3.one * 2.4f;
                SoundKit.Play(Sfx.Stamp);
                yield return Tw.Scale(seal.rectTransform, Vector3.one, 0.25f, Ease.OutBack);
                yield return new WaitForSecondsRealtime(2.2f);
                yield return Tw.Anchored(rt, new Vector2(0, 220), 0.35f, Ease.InQuad);
                Destroy(band.gameObject);
            }
            achShowing = false;
        }

        // ───────────────────────── 타이틀 (애니메이션)
        void ShowTitleV2()
        {
            if (titleScene == null)
            {
                titleScene = new GameObject("TitleScene").AddComponent<TitleScene>();
                titleScene.Build();
            }
            titleScene.gameObject.SetActive(true);
            rig.position = TitleScene.Origin;
            cam.orthographicSize = 7.5f;
            weather.SetMonth(3);   // 타이틀은 봄 꽃잎

            titleRoot = UIKit.Rect("Title", safeRoot).Stretch();
            var tap = titleRoot.gameObject.AddComponent<Image>(); tap.color = new Color(0, 0, 0, 0.001f);
            var tapBtn = titleRoot.gameObject.AddComponent<Button>(); tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(OpenFactionSelect);

            var top = UIKit.Rect("Top", titleRoot).Dock(true, 760, 240);
            var stroke = UIKit.Panel(top, Palette.Seal.WithA(0.88f), "Stroke", false);
            stroke.sprite = ProcArt.Brush; stroke.type = Image.Type.Filled; stroke.fillMethod = Image.FillMethod.Horizontal; stroke.fillAmount = 0;
            var srt = stroke.rectTransform; srt.anchorMin = new Vector2(0.08f, 0.38f); srt.anchorMax = new Vector2(0.92f, 0.78f); srt.offsetMin = srt.offsetMax = Vector2.zero;
            var title = UIKit.Label(top, "천하일통", 200, Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0, 0.30f); title.rectTransform.anchorMax = new Vector2(1, 0.86f); title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var titleCg = title.gameObject.AddComponent<CanvasGroup>(); titleCg.alpha = 0;
            var sub = UIKit.Label(top, "天下一統 · 열두 성, 여섯 군웅, 한 사람의 기록", 40, Palette.Ink2, TextAnchor.MiddleCenter);
            sub.rectTransform.anchorMin = new Vector2(0, 0.12f); sub.rectTransform.anchorMax = new Vector2(1, 0.28f); sub.rectTransform.offsetMin = sub.rectTransform.offsetMax = Vector2.zero;
            var subCg = sub.gameObject.AddComponent<CanvasGroup>(); subCg.alpha = 0;

            var sealImg = UIKit.Panel(top, Palette.Seal, "Seal", false);
            var sr = sealImg.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0.86f, 0.18f); sr.sizeDelta = new Vector2(120, 120);
            var sealT = UIKit.Label(sealImg.transform, "許", 74, Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold); sealT.rectTransform.Stretch();
            sealImg.gameObject.SetActive(false);

            var bottom = UIKit.Rect("Bottom", titleRoot).Dock(false, 520, 120);
            UIKit.VList(bottom, 24, 60);
            var saved = Load();
            if (saved != null && !saved.over)
            {
                var cont = UIKit.Button(bottom, $"이어하기 · {saved.year}년 {saved.month}월 {FactionName(saved, saved.player)}", Continue, Palette.Ink, Palette.Paper2, 42);
                UIKit.Size((RectTransform)cont.transform, -1, 140);
            }
            var hint = UIKit.Label(bottom, "화면을 눌러 군주를 고른다", 42, Palette.Ink, TextAnchor.MiddleCenter);
            UIKit.Size(hint.rectTransform, -1, 90);
            var foot = UIKit.Label(bottom, "v0.2 · 시나리오 「관도의 각오」(200년)", 28, Palette.Ink3, TextAnchor.MiddleCenter);
            UIKit.Size(foot.rectTransform, -1, 60);
            StartCoroutine(TitleIntro(stroke, titleCg, subCg, sealImg, hint));
        }

        IEnumerator TitleIntro(Image stroke, CanvasGroup title, CanvasGroup sub, Image seal, Text hint)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            SoundKit.Play(Sfx.Swish);
            StartCoroutine(Tw.Fill(stroke, 1, 0.9f, Ease.OutCubic));          // 붓이 지나가며 먹 띠를 남긴다
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Tw.Fade(title, 1, 0.6f);
            yield return Tw.Fade(sub, 1, 0.5f);
            if (seal) { seal.gameObject.SetActive(true); seal.rectTransform.localScale = Vector3.one * 2.6f; SoundKit.Play(Sfx.Stamp); yield return Tw.Scale(seal.rectTransform, Vector3.one, 0.25f, Ease.OutBack); }
            while (hint) { hint.color = Palette.Ink.WithA(0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.6f))); yield return null; }
        }

        void OpenFactionSelect()
        {
            if (sheetOpen) return;
            SoundKit.Play(Sfx.Swish, 0.7f);
            sheet = new Sheet(canvas.transform, this, "군주를 고른다", 0.78f);
            var root = sheet.content;
            var preview = NewGame("cao", 1);
            foreach (var id in FactionOrder)
            {
                var f = preview.Faction(id);
                var cs = FactionCities(preview, id);
                var ruler = preview.Officer(f.ruler);
                var fid = id;
                var b = UIKit.Button(root, "", () => ChooseFaction(fid), Palette.Paper2);
                UIKit.Size((RectTransform)b.transform, -1, 190);
                var row = UIKit.Rect("Row", b.transform).Stretch(28, 18, 28, 18);
                var hl = UIKit.HList(row, 28); hl.childForceExpandWidth = false;
                var sw = UIKit.Panel(row, Palette.Hex(f.color), "Seal", false); UIKit.Size(sw.rectTransform, 150, -1);
                var sl = UIKit.Label(sw.transform, ruler.name.Substring(0, 1), 80, Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold); sl.rectTransform.Stretch();
                var t = ruler.stats;
                var txt = UIKit.Label(row, $"<b>{ruler.name}</b>  <size=30>{f.name}</size>\n<size=28>{cs.Count}성 · 병력 {Fmt(cs.Sum(c => c.troops))} · 무 {t.war} 지 {t.@int} 매 {t.cha}</size>", 48);
                UIKit.Size(txt.rectTransform, 620, -1);
                var d = UIKit.Label(row, Difficulty[id], 32, id == "liu" || id == "ma" ? Palette.Red : Palette.Ink2, TextAnchor.MiddleRight, FontStyle.Bold); UIKit.Size(d.rectTransform, 140, -1);
            }
        }

        // 저장 칸은 하나다: 진행 중인 판이 있으면 지우기 전에 묻는다
        void ChooseFaction(string fid)
        {
            var saved = ActiveSave();
            sheet.Close();
            if (saved == null) { StartNew(fid); return; }
            StartCoroutine(ConfirmOverwrite(saved, fid));
        }

        IEnumerator ConfirmOverwrite(GameState saved, string fid)
        {
            while (sheetOpen) yield return null;
            var preview = NewGame(fid, 1);
            sheet = new Sheet(canvas.transform, this, "새로 시작할까요?", 0.42f);
            var root = sheet.content;
            Info(root, $"저장 칸은 하나다. <b>{preview.Officer(preview.Faction(fid).ruler).name}</b>(으)로 시작하면 진행 중인 기록 <b>{saved.year}년 {saved.month}월 {FactionName(saved, saved.player)}</b>이(가) 지워진다.");
            var row = UIKit.Rect("Buttons", root); UIKit.Size(row, -1, 130); UIKit.HList(row, 20);
            UIKit.Button(row, "취소", () => sheet.Close(), Palette.Paper2, Palette.Ink, 40);
            UIKit.Button(row, "지우고 새로 시작", () => { sheet.Close(); ClearSave(); StartNew(fid); }, Palette.Red, Color.white, 40);
        }

        void HideTitleScene() { if (titleScene) titleScene.gameObject.SetActive(false); }

        // ───────────────────────── 장수 카드
        void OpenOfficer(string id)
        {
            if (busy || inBattle) return;
            var o = S.Officer(id);
            if (o == null) return;
            sheet?.Close();
            sheet = new Sheet(canvas.transform, this, "", 0.8f);
            sheet.onClosed = RefreshAll;
            var root = sheet.content;
            var col = None(o.faction) ? Palette.Neutral : Palette.Hex(S.Faction(o.faction).color);

            var head = UIKit.Rect("Head", root); UIKit.Size(head, -1, 230);
            var hl = UIKit.HList(head, 32); hl.childForceExpandWidth = false;
            var seal = UIKit.Panel(head, col, "Seal", false); UIKit.Size(seal.rectTransform, 220, 220);
            var sealT = UIKit.Label(seal.transform, o.name.Substring(0, 1), 140, Palette.Paper2, TextAnchor.MiddleCenter, FontStyle.Bold); sealT.rectTransform.Stretch();
            var role = IsRuler(S, o) ? "군주" : S.City(o.city)?.governor == o.id ? "태수" : "장수";
            var name = UIKit.Label(head, $"<b><size=96>{o.name}</size></b>\n자 {o.courtesy} · {S.year - o.born}세 · {role}\n<size=30>{FactionName(S, o.faction)} · {CityName(S, o.city)}</size>", 38);
            UIKit.Size(name.rectTransform, 700, -1);

            // 레이더 차트: 바탕 육각 + 값 육각 + 축 이름
            var chart = UIKit.Rect("Chart", root); UIKit.Size(chart, -1, 620);
            var bgGo = UIKit.Rect("Grid", chart); bgGo.anchorMin = bgGo.anchorMax = new Vector2(0.5f, 0.5f); bgGo.sizeDelta = new Vector2(480, 480);
            var grid = bgGo.gameObject.AddComponent<RadarGraphic>(); grid.color = Palette.Line.WithA(0.6f);
            var mid = UIKit.Rect("Mid", chart); mid.anchorMin = mid.anchorMax = new Vector2(0.5f, 0.5f); mid.sizeDelta = new Vector2(240, 240);
            var midG = mid.gameObject.AddComponent<RadarGraphic>(); midG.color = Palette.Paper2.WithA(0.9f);
            var valGo = UIKit.Rect("Values", chart); valGo.anchorMin = valGo.anchorMax = new Vector2(0.5f, 0.5f); valGo.sizeDelta = new Vector2(480, 480);
            var val = valGo.gameObject.AddComponent<RadarGraphic>(); val.color = col.WithA(0.75f);
            var st = o.stats;
            val.SetValues(new[] { st.war / 100f, st.@int / 100f, st.pol / 100f, st.cha / 100f, st.land / 100f, st.naval / 100f });
            val.SetReveal(0);
            StartCoroutine(Tw.Run(0.7f, k => { if (val) val.SetReveal(k); }, Ease.OutBack));
            string[] labels = { $"무력 {st.war}", $"지력 {st.@int}", $"정치 {st.pol}", $"매력 {st.cha}", $"육전 {st.land}", $"수전 {st.naval}" };
            for (int i = 0; i < 6; i++)
            {
                float ang = Mathf.PI / 2 - i * Mathf.PI * 2 / 6;
                var l = UIKit.Label(chart, labels[i], 34, Palette.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                var lr = l.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f); lr.sizeDelta = new Vector2(220, 60);
                lr.anchoredPosition = new Vector2(Mathf.Cos(ang) * 300, Mathf.Sin(ang) * 285);
            }

            if (o.traits.Count > 0) Info(root, "특성  " + string.Join("  ", o.traits.Select(t => $"<b>「{t}」</b>")));
            if (!IsRuler(S, o) && o.status == OfficerStatus.Serving)
            {
                Info(root, $"충성 {Round(o.loyalty)}");
                var bar = UIKit.Bar(root, o.loyalty < 50 ? Palette.Red : Palette.Ink, 18);
                bar.fillAmount = (float)(o.loyalty / 100);
            }
            Info(root, o.acted ? "이번 달 행동을 마쳤다." : "이번 달 명령을 기다린다.");
            SoundKit.Play(Sfx.Swish, 0.6f);
        }

        // ───────────────────────── 첫 플레이 안내 (기록관 허문)
        static readonly string[] TutorialSteps =
        {
            "나는 기록관 허문이오. 이 지도는 천하 열두 성이오. 도시를 눌러 형세를 보시오.",
            "아래 카드에서 내정·인사·외교·계략·출진을 고르시오. 장수 한 사람은 한 달에 한 번 움직이오.",
            "운영 방침을 정해 두면, 명령하지 않은 장수들이 알아서 일하오.",
            "준비가 끝나면 '턴 종료'를 누르시오. 한 달 동안 천하에서 일어난 일을 내가 적어 올리겠소.",
        };

        void MaybeTutorial()
        {
            if (PlayerPrefs.GetInt(TutorialKey, 0) == 1) return;
            StartCoroutine(Tutorial());
        }

        IEnumerator Tutorial()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            while (sheetOpen) yield return null;      // 월간 보고를 닫은 뒤 시작
            for (int i = 0; i < TutorialSteps.Length; i++)
            {
                bool next = false, skip = false;
                var card = UIKit.Panel(overlay.transform, Palette.Paper2, "Coach");
                var rt = card.rectTransform;
                rt.anchorMin = new Vector2(0.04f, 1); rt.anchorMax = new Vector2(0.96f, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 330); rt.anchoredPosition = new Vector2(0, -210 - SafeArea.TopInset());
                var row = UIKit.Rect("Row", rt).Stretch(30, 120, 30, 24);
                var hl = UIKit.HList(row, 24); hl.childForceExpandWidth = false;
                Seal(row, 110);
                var t = UIKit.Label(row, TutorialSteps[i], 36, Palette.Ink); UIKit.Size(t.rectTransform, 780, -1);
                var btns = UIKit.Rect("Buttons", rt).Dock(false, 110, 10);
                UIKit.HList(btns, 20, 0).padding = new RectOffset(30, 30, 0, 0);
                UIKit.Button(btns, "건너뛰기", () => skip = true, Palette.Paper, Palette.Ink2, 34);
                UIKit.Button(btns, i == TutorialSteps.Length - 1 ? "시작하자" : $"다음 ({i + 1}/{TutorialSteps.Length})", () => next = true, Palette.Ink, Palette.Paper, 36);
                var cg = card.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0;
                yield return Tw.Fade(cg, 1, 0.25f);
                while (!next && !skip) yield return null;
                Destroy(card.gameObject);
                if (skip) break;
            }
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
        }

        // ───────────────────────── 계절 색조 (종이 색이 달마다 바뀐다)
        static Color SeasonPaper(int month)
        {
            switch (Weather.SeasonOf(month))
            {
                case 0: return new Color(0.95f, 0.91f, 0.87f);   // 봄: 엷은 복사꽃
                case 1: return new Color(0.90f, 0.92f, 0.85f);   // 여름: 풀빛
                case 2: return new Color(0.95f, 0.89f, 0.78f);   // 가을: 볏짚
                default: return new Color(0.90f, 0.91f, 0.92f);  // 겨울: 눈빛
            }
        }

        void ApplySeason(bool animate)
        {
            if (S == null) return;
            var m = ProcArt.Mat("InkPaper");
            var to = SeasonPaper(S.month);
            if (!animate) { m.SetColor("_PaperColor", to); return; }
            var from = m.GetColor("_PaperColor");
            StartCoroutine(Tw.Run(1.2f, k => m.SetColor("_PaperColor", Color.Lerp(from, to, k))));
        }

        // ───────────────────────── 설정·업적 목록·공유 (기록 시트 아래)
        void AppendSettings(Transform root)
        {
            Info(root, $"<b>업적</b>  {achievements.unlocked.Count}/{Achievements.All.Length}");
            foreach (var a in Achievements.All)
            {
                bool has = achievements.Has(a.id);
                Info(root, has ? $"<color=#A63A2B>■</color> <b>{a.title}</b>  <size=28>{a.desc}</size>" : $"<color=#B9B1A1>□ {a.title}  <size=28>{a.desc}</size></color>");
            }
            Info(root, "<b>설정</b>");
            var row = UIKit.Rect("Settings", root); UIKit.Size(row, -1, 120); UIKit.HList(row, 16);
            UIKit.Button(row, SoundKit.SoundOn ? "소리 켜짐" : "소리 꺼짐", () => { SoundKit.SoundOn = !SoundKit.SoundOn; ShowLog(); }, Palette.Paper2);
            UIKit.Button(row, Haptics.On ? "진동 켜짐" : "진동 꺼짐", () => { Haptics.On = !Haptics.On; ShowLog(); }, Palette.Paper2);
            UIKit.Button(row, "안내 다시 보기", () => { PlayerPrefs.SetInt(TutorialKey, 0); sheet.Close(); MaybeTutorial(); }, Palette.Paper2);
            var share = UIKit.Button(root, "나만의 삼국사 복사하기", () =>
            {
                var text = $"「천하일통」 {FactionName(S, S.player)}의 기록 ({S.year}년 {S.month}월)\n" + string.Join("\n", S.chronicle.Skip(System.Math.Max(0, S.chronicle.Count - 12)))
                           + $"\n업적 {achievements.unlocked.Count}/{Achievements.All.Length}";
                GUIUtility.systemCopyBuffer = text;
                Toast("삼국사를 복사했다. 메신저에 붙여 넣어 자랑하자.");
            }, Palette.Seal, Color.white);
            UIKit.Size((RectTransform)share.transform, -1, 120);
        }
    }
}
