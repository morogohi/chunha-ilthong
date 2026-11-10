// 게임 진행의 중심: 상태, 저장, 되돌리기, 카메라, 턴 넘김 연출.
// 씬에 아무것도 없어도 Bootstrap이 이 오브젝트를 만들어 바로 플레이할 수 있다.
using System;
using System.Collections;
using System.Collections.Generic;
using Chunha.Art;
using Chunha.Battles;
using Chunha.Core;
using Chunha.FX;
using Chunha.Map;
using Chunha.UI;
using UnityEngine;

namespace Chunha
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GameRoot>() != null) return;
            var go = new GameObject("ChunhaGame");
            go.AddComponent<GameRoot>();
        }
    }

    public partial class GameRoot : MonoBehaviour
    {
        public static GameRoot I { get; private set; }
        const string SaveKey = "chunha-ilthong-save-v1";

        public GameState S { get; private set; }
        readonly LinkedList<GameState> history = new LinkedList<GameState>();
        string selected;

        Transform rig;
        Camera cam;
        CameraShake shake;
        MapView map;
        BattleView battleView;
        Weather weather;
        Canvas canvas, overlay;
        RectTransform safeRoot;   // 노치·홈 인디케이터를 피한 영역 (HUD·타이틀·전투 HUD)
        Toast toast;
        bool busy;   // 연출 중에는 입력을 막는다

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Input.multiTouchEnabled = true;

            rig = new GameObject("CameraRig").transform;
            var existing = Camera.main;
            if (existing != null) existing.gameObject.SetActive(false);
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig, false);
            camGo.transform.localPosition = new Vector3(0, 0, -10);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Paper;
            shake = camGo.AddComponent<CameraShake>();
            camGo.AddComponent<AudioListener>();

            weather = new GameObject("Weather").AddComponent<Weather>();
            weather.cam = cam;

            battleView = new GameObject("BattleView").AddComponent<BattleView>();
            battleView.shake = shake;
            battleView.cam = cam;
            gameObject.AddComponent<SoundKit>();
            LoadAchievements();

            canvas = UIKit.CreateCanvas("UI", 10);
            overlay = UIKit.CreateCanvas("Overlay", 50);
            safeRoot = SafeArea.Create(canvas);
            toast = Chunha.UI.Toast.Create(overlay.transform);
            BuildWipe();
            ShowTitleV2();
        }

        // ───────────────────────── 시작·저장
        public void StartNew(string faction)
        {
            int seed = (int)(DateTime.Now.Ticks % 2147483646) + 1;
            Begin(G.NewGame(faction, seed));
        }

        public void Continue()
        {
            var s = Load();
            if (s != null) Begin(s);
        }

        void Begin(GameState s)
        {
            S = s;
            history.Clear();
            ClearTitle();
            HideTitleScene();
            if (map != null) Destroy(map.gameObject);
            map = new GameObject("Map").AddComponent<MapView>();
            map.Build(S);
            var mine = G.FactionCities(S, S.player);
            var home = mine.Find(c => c.governor == S.Faction(S.player).ruler) ?? mine[0];
            selected = home.id;
            BuildHud();
            map.Select(selected);
            FocusCity(selected, true);
            weather.SetMonth(S.month);
            ApplySeason(false);
            Save();
            ShowReport();
            MaybeTutorial();
            CheckAchievements();
        }

        // 모바일은 백그라운드에서 언제든 종료될 수 있다. 전투 중이 아니면 이번 달 명령까지 저장한다.
        void OnApplicationPause(bool paused) { if (paused && !inBattle) Save(); }

        public void Save()
        {
            if (S == null) return;
            try { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(S)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("저장 실패: " + e.Message); }
        }

        public static GameState Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(SaveKey)) return null;
                var s = JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(SaveKey));
                if (s == null || s.version != 1 || s.cities == null || s.cities.Count == 0) return null;
                s.AfterLoad();
                return s;
            }
            catch { return null; }
        }

        public static void ClearSave() { PlayerPrefs.DeleteKey(SaveKey); }

        void PushHistory()
        {
            history.AddLast(S.Clone());
            while (history.Count > 3) history.RemoveFirst();
        }

        public void Undo()
        {
            if (busy || history.Count == 0) return;
            S = history.Last.Value;
            history.RemoveLast();
            map.Refresh(S, false);
            weather.SetMonth(S.month);
            Save();
            RefreshAll();
            Toast("한 턴 전으로 되돌렸다.");
        }

        public void Toast(string msg) => toast.Show(msg);

        /// <summary>명령 결과를 알리고 화면을 갱신한다</summary>
        public void Apply(Result r, string fxCity = null, Color? fxColor = null)
        {
            Toast(r.msg);
            if (r.ok && fxCity != null) Particles.Sparkle(map.fxRoot, MapView.CityPos(S.City(fxCity)) + Vector3.up * 0.3f, fxColor ?? Palette.Gold);
            map.Refresh(S, true);
            PlayQueuedEvents(quick: true);
            Save();
            RefreshAll();
            CheckAchievements();
        }

        // ───────────────────────── 카메라
        public void FocusCity(string id, bool instant = false)
        {
            var p = MapView.CityPos(S.City(id));
            // 아래쪽 도시 카드에 가리지 않도록 도시가 화면 위쪽에 오게 맞춘다
            var target = new Vector3(p.x, p.y - cam.orthographicSize * 0.32f, 0);
            target = ClampToMap(target);
            StopFocus();
            if (instant) rig.position = target;
            else focusCo = StartCoroutine(Tw.Move(rig, target, 0.45f));
        }

        Coroutine focusCo;
        /// <summary>진행 중인 카메라 이동을 멈춘다 (끌기·연속 탭과 싸우지 않게)</summary>
        void StopFocus() { if (focusCo != null) StopCoroutine(focusCo); focusCo = null; }

        Vector3 ClampToMap(Vector3 p)
        {
            var b = MapView.Bounds;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float minX = b.xMin + halfW * 0.5f, maxX = b.xMax - halfW * 0.5f;
            float minY = b.yMin - halfH * 0.6f, maxY = b.yMax - halfH * 0.3f;
            return new Vector3(Mathf.Clamp(p.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX)), Mathf.Clamp(p.y, minY, maxY), 0);
        }

        public void Select(string id)
        {
            if (S == null || id == null) return;
            selected = id;
            map.Select(id);
            FocusCity(id);
            RefreshCityCard();
        }

        // ───────────────────────── 턴 넘김
        public void EndTurn()
        {
            if (busy || S == null || S.over) return;
            StartCoroutine(EndTurnRoutine());
        }

        IEnumerator EndTurnRoutine()
        {
            busy = true;
            PushHistory();
            S.events.Clear();
            var before = S.Clone();
            TurnSystem.EndTurn(S);
            yield return PlayEvents(before);
            map.Refresh(S, true);
            weather.SetMonth(S.month);
            ApplySeason(true);
            Save();
            RefreshAll();
            busy = false;
            if (S.over) ShowEnd(); else ShowReport();
            CheckAchievements();
        }

        bool skipPlayback;
        public void SkipPlayback() => skipPlayback = true;

        /// <summary>AI 턴에 일어난 일을 지도 위에서 차례로 보여 준다 (행군 → 먹 번짐 → 재해)</summary>
        IEnumerator PlayEvents(GameState before)
        {
            skipPlayback = false;
            var evs = new List<GameEvent>(S.events);
            S.events.Clear();
            int shown = 0;
            // 지도는 바뀌기 전 상태에서 시작해, 사건마다 그 도시만 바꿔 칠한다
            map.Refresh(before, false);
            ShowPlaybackBar(true);
            foreach (var e in evs)
            {
                if (skipPlayback) break;
                if (shown > 10) break;
                var city = e.city != null ? S.City(e.city) : null;
                switch (e.kind)
                {
                    case GameEventKind.BattleStarted:
                    {
                        var from = S.City(e.from);
                        var col = Palette.Hex(S.Faction(e.faction).color);
                        SetPlaybackText($"{G.FactionName(S, e.faction)}이(가) {from.name}에서 {city.name}(으)로 진군한다");
                        yield return PanTo(MapView.CityPos(city));
                        SoundKit.Play(Sfx.March);
                        yield return map.March(MapView.CityPos(from), MapView.CityPos(city), col, 0.9f);
                        shown++;
                        break;
                    }
                    case GameEventKind.CityCaptured:
                    {
                        SetPlaybackText($"{G.FactionName(S, e.faction)}이(가) {city.name}을(를) 빼앗았다");
                        var col = Palette.Hex(S.Faction(e.faction).color);
                        map.nodes[city.id].SetFaction(e.faction, col, true, map.fxRoot);
                        shake.Shake(0.12f, 0.3f);
                        SoundKit.Play(Sfx.Drum); SoundKit.Play(Sfx.Stamp, 0.7f);
                        if (e.other == S.player) Haptics.Heavy();   // 내 성을 잃었을 때
                        yield return Tw.Wait(1.2f);
                        break;
                    }
                    case GameEventKind.AttackRepelled:
                        SetPlaybackText($"{city.name}이(가) 공격을 막아 냈다");
                        Particles.Smoke(map.fxRoot, MapView.CityPos(city), 0.6f);
                        yield return Tw.Wait(0.6f);
                        break;
                    case GameEventKind.Disaster:
                        if (city.faction != S.player && shown > 4) break;
                        SetPlaybackText($"{city.name}에 {TurnSystem.EventName(e.text)}");
                        yield return PanTo(MapView.CityPos(city));
                        Particles.Disaster(map.fxRoot, MapView.CityPos(city), e.text);
                        SoundKit.Play(e.text == "plague" ? Sfx.Swish : Sfx.Fire, 0.5f);
                        yield return Tw.Wait(0.9f);
                        shown++;
                        break;
                    case GameEventKind.Harvest:
                        Particles.Sparkle(map.fxRoot, MapView.CityPos(city), Palette.Gold);
                        break;
                    case GameEventKind.FactionFell:
                        SetPlaybackText($"{S.Faction(e.faction).name} 멸망");
                        SoundKit.Play(Sfx.Gong, 0.8f);
                        shake.Shake(0.2f, 0.5f);
                        yield return Tw.Wait(1.0f);
                        break;
                }
            }
            ShowPlaybackBar(false);
            FocusCity(selected);
        }

        /// <summary>플레이어 명령 직후 생긴 이벤트 (수송 행군 등)를 짧게 보여 준다</summary>
        void PlayQueuedEvents(bool quick)
        {
            foreach (var e in S.events)
                if (e.kind == GameEventKind.Transport)
                    StartCoroutine(map.March(MapView.CityPos(S.City(e.from)), MapView.CityPos(S.City(e.city)), Palette.Hex(S.Faction(e.faction).color), quick ? 0.8f : 1.1f));
            S.events.Clear();
        }

        IEnumerator PanTo(Vector3 p)
        {
            var target = ClampToMap(new Vector3(p.x, p.y - cam.orthographicSize * 0.2f, 0));
            if (Vector3.Distance(rig.position, target) < 0.5f) yield break;
            yield return Tw.Move(rig, target, 0.4f);
        }
    }
}
