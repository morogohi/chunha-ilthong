// 전투 무대: 두 진형의 병사 스프라이트가 전술에 따라 움직인다.
// 돌격 → 진형 충돌·먼지·화면 흔들림, 화계 → 불화살과 불길, 방어 → 방패벽 번쩍임, 퇴각 → 이탈.
using System.Collections;
using System.Collections.Generic;
using Chunha.Art;
using Chunha.Core;
using Chunha.FX;
using UnityEngine;

namespace Chunha.Battles
{
    public class BattleView : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(60, 0, 0);
        class Formation
        {
            public Transform root;
            public readonly List<SpriteRenderer> men = new List<SpriteRenderer>();
            public SpriteRenderer banner;
            public Vector3 home;
            public bool facingLeft;
            public int alive;
            public double startTroops;
        }

        Formation atk, def;
        Transform stage;
        SpriteRenderer wall;
        public CameraShake shake;
        public Camera cam;
        SpriteRenderer flash;

        public void Setup(GameState s, Battle b)
        {
            Clear();
            stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);
            stage.position = Origin;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(stage, false);
            ground.transform.localPosition = new Vector3(0, -0.5f, 2);
            ground.transform.localScale = new Vector3(20, 16, 1);
            var gm = new Material(ProcArt.Mat("InkPaper"));
            gm.SetColor("_PaperColor", new Color(0.86f, 0.82f, 0.72f));
            gm.SetFloat("_Stain", 0.14f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
            ground.GetComponent<MeshRenderer>().sortingOrder = -100;

            // 먼 산과 지평선
            for (int i = 0; i < 3; i++)
            {
                var m = Sprite(stage, ProcArt.Mountain, new Vector3(-3 + i * 3, 1.6f, 0), Palette.Ink.WithA(0.12f + i * 0.04f), -80);
                m.transform.localScale = new Vector3(3.2f, 2.2f, 1);
            }
            var horizon = Sprite(stage, ProcArt.Brush, new Vector3(0, 1.5f, 0), Palette.Ink.WithA(0.25f), -70);
            horizon.transform.localScale = new Vector3(4, 0.25f, 1);

            var city = s.City(b.city);
            bool hasWall = !G.None(b.defender.faction);
            if (hasWall)
            {
                wall = Sprite(stage, ProcArt.Wall, new Vector3(2.35f, -1.9f, 0), Palette.Ink.WithA(0.35f + (float)city.wall / 250f), 5);
                wall.transform.localScale = new Vector3(0.95f, 1.1f, 1);
            }

            var aCol = Palette.Hex(s.Faction(b.attacker.faction).color);
            var dCol = G.None(b.defender.faction) ? Palette.Neutral : Palette.Hex(s.Faction(b.defender.faction).color);
            // 세로 화면 기준: 보이는 폭 약 ±2.8, 진형은 위에서 아래로 쌓인다 (상·하단 UI를 피해서)
            atk = MakeFormation(b.attacker, aCol, new Vector3(-1.45f, 0.5f, 0), false);
            def = MakeFormation(b.defender, dCol, new Vector3(hasWall ? 1.15f : 1.45f, 0.5f, 0), true);
            // 화면 섬광용 흰 판 (일기토·화계 성공 때 번쩍)
            flash = Sprite(stage, ProcArt.SoftCircle, new Vector3(0, 0, 0), Color.white.WithA(0), 90);
            flash.transform.localScale = new Vector3(30, 30, 1);
            StartCoroutine(Enter());
            SoundKit.Play(Sfx.Drum);
        }

        Formation MakeFormation(Side side, Color col, Vector3 home, bool facingLeft)
        {
            var f = new Formation { home = home, facingLeft = facingLeft, startTroops = System.Math.Max(1, side.start) };
            f.root = new GameObject(facingLeft ? "Defender" : "Attacker").transform;
            f.root.SetParent(stage, false);
            f.root.localPosition = home + new Vector3(facingLeft ? 5 : -5, 0, 0);
            int n = Mathf.Clamp(Mathf.RoundToInt((float)side.troops / 600f), 4, 48);
            int cols = 5;
            var body = Color.Lerp(Palette.Ink, col, 0.35f);
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, r = i / cols;
                float x = (facingLeft ? 1 : -1) * c * 0.27f + (r % 2) * 0.12f;
                float y = -r * 0.22f + c * 0.02f;
                var sr = Sprite(f.root, ProcArt.Soldier, new Vector3(x, y, 0), body, 20 + r * 2);
                sr.flipX = facingLeft;
                sr.transform.localScale = Vector3.one * 0.5f;
                f.men.Add(sr);
            }
            f.alive = n;
            f.banner = Sprite(f.root, ProcArt.Banner, new Vector3((facingLeft ? 1 : -1) * 0.2f, 0.45f, 0), col, 40);
            f.banner.flipX = facingLeft;
            f.banner.transform.localScale = Vector3.one * 1.1f;
            return f;
        }

        IEnumerator Enter()
        {
            StartCoroutine(Tw.Move(atk.root, Origin + atk.home, 0.8f));
            yield return Tw.Move(def.root, Origin + def.home, 0.8f);
        }

        SpriteRenderer Sprite(Transform parent, Sprite sp, Vector3 local, Color c, int order)
        {
            var sr = new GameObject(sp.name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = local;
            sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            foreach (var f in new[] { atk, def })
            {
                if (f == null) continue;
                for (int i = 0; i < f.men.Count; i++)
                {
                    var m = f.men[i];
                    if (m == null || m.color.a < 0.9f) continue;
                    var p = m.transform.localScale;
                    m.transform.localScale = new Vector3(p.x, 0.5f * (1 + 0.025f * Mathf.Sin(Time.time * 5 + i)), 1);
                }
                if (f.banner) f.banner.transform.localScale = new Vector3(1.1f * (1 + 0.06f * Mathf.Sin(Time.time * 7)), 1.1f, 1);
            }
        }

        /// <summary>한 합의 연출. 공격군 → 수비군 순서로 전술을 보여 주고 사상자를 반영한다.</summary>
        public IEnumerator PlayRound(Battle b, RoundFx fx)
        {
            if (fx == null || atk == null) yield break;
            if (fx.duel) yield return DuelFlash(fx.duelAtkWon);
            yield return Act(atk, def, fx.atkTactic, fx.atkFireOk);
            yield return Act(def, atk, fx.defTactic, fx.defFireOk);
            Casualties(def, b.defender.troops);
            Casualties(atk, b.attacker.troops);
            FloatText(def.root.position + Vector3.up * 1.2f, fx.dmgToDef);
            FloatText(atk.root.position + Vector3.up * 1.2f, fx.dmgToAtk);
            yield return Tw.Wait(0.35f);
            if (b.done) yield return Finale(b.result == "attacker");
        }

        IEnumerator Act(Formation me, Formation enemy, string tactic, bool fireOk)
        {
            var dir = me.facingLeft ? -1 : 1;
            switch (tactic)
            {
                case Tactic.Charge:
                {
                    var home = Origin + me.home;
                    var hit = Vector3.Lerp(home, Origin + enemy.home, 0.62f);
                    SoundKit.Play(Sfx.March, 0.8f);
                    yield return Tw.Move(me.root, hit, 0.35f, Ease.InQuad);
                    if (shake) shake.Shake(0.18f, 0.35f);
                    SoundKit.Play(Sfx.Drum); SoundKit.Play(Sfx.Clash, 0.6f);
                    StartCoroutine(ZoomPunch(0.35f));
                    Haptics.Heavy();
                    Particles.Dust(stage, hit + new Vector3(dir * 0.8f, -0.6f, 0), 1.6f);
                    Particles.InkSplash(stage, hit + new Vector3(dir * 0.9f, 0, 0), Palette.Ink.WithA(0.8f));
                    yield return Tw.Wait(0.12f);
                    yield return Tw.Move(me.root, home, 0.45f);
                    break;
                }
                case Tactic.Fire:
                {
                    SoundKit.Play(Sfx.Swish);
                    yield return Volley(me, enemy, fireOk);
                    if (fireOk) { Particles.Fire(stage, enemy.root.position + new Vector3(0, -0.6f, 0), 2.6f, 1.4f); if (shake) shake.Shake(0.08f, 0.5f); SoundKit.Play(Sfx.Fire); StartCoroutine(Flash(new Color(1f, 0.6f, 0.25f), 0.35f)); }
                    else Particles.Smoke(stage, enemy.root.position + new Vector3(-dir * 1.2f, 0, 0), 0.8f);
                    yield return Tw.Wait(0.4f);
                    break;
                }
                case Tactic.Defend:
                {
                    SoundKit.Play(Sfx.Drum, 0.5f);
                    var shield = Sprite(stage, ProcArt.Brush, me.root.position - stage.position + new Vector3(dir * 1.0f, -0.2f, 0), Color.white.WithA(0), 60);
                    shield.transform.localScale = new Vector3(0.6f, 3.6f, 1);
                    shield.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    yield return Tw.Fade(shield, 0.85f, 0.12f);
                    yield return Tw.Fade(shield, 0f, 0.4f);
                    Destroy(shield.gameObject);
                    break;
                }
                case Tactic.Retreat:
                    yield return Tw.Move(me.root, me.root.position + new Vector3(-dir * 7, 0, 0), 0.8f, Ease.InQuad);
                    break;
            }
        }

        IEnumerator Volley(Formation me, Formation enemy, bool ok)
        {
            var arrows = new List<(Transform t, Vector3 a, Vector3 b)>();
            for (int i = 0; i < 9; i++)
            {
                var a = me.root.position + new Vector3(Random.Range(-0.6f, 0.6f), 0.6f + Random.Range(0, 0.6f), 0);
                var bpos = enemy.root.position + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.8f, 0.2f), 0);
                var sr = Sprite(stage, ProcArt.Arrow, a - stage.position, ok ? new Color(1f, 0.55f, 0.2f) : Palette.Ink, 55);
                sr.transform.localScale = Vector3.one * 0.5f;
                arrows.Add((sr.transform, a, bpos));
            }
            yield return Tw.Run(0.7f, k =>
            {
                foreach (var (t, a, bp) in arrows)
                {
                    if (!t) continue;
                    var p = Vector3.Lerp(a, bp, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
                    var d = (Vector3.Lerp(a, bp, Mathf.Min(1, k + 0.02f)) + Vector3.up * Mathf.Sin(Mathf.Min(1, k + 0.02f) * Mathf.PI) * 2.2f) - p;
                    t.position = p;
                    t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
            }, k => k);
            foreach (var (t, _, _) in arrows) if (t) Destroy(t.gameObject);
        }

        void Casualties(Formation f, double troops)
        {
            int target = Mathf.Clamp(Mathf.CeilToInt((float)(troops / f.startTroops) * f.men.Count), troops > 0 ? 1 : 0, f.men.Count);
            int toKill = f.alive - target;
            for (int i = f.men.Count - 1; i >= 0 && toKill > 0; i--)
            {
                var m = f.men[i];
                if (m == null || m.color.a < 0.9f) continue;
                StartCoroutine(Fall(m, f.facingLeft));
                toKill--; f.alive--;
            }
        }

        IEnumerator Fall(SpriteRenderer m, bool left)
        {
            var c = m.color; c.a = 0.89f; m.color = c;
            var start = m.transform.localRotation;
            yield return Tw.Run(0.35f, k => { if (m) m.transform.localRotation = Quaternion.Slerp(start, Quaternion.Euler(0, 0, left ? -80 : 80), k); });
            yield return Tw.Fade(m, 0.15f, 0.6f);
        }

        void FloatText(Vector3 pos, double dmg)
        {
            if (dmg <= 0) return;
            var go = new GameObject("Dmg");
            go.transform.SetParent(stage, false);
            go.transform.position = pos;
            var t = go.AddComponent<TextMesh>();
            t.font = ProcArt.Font; t.fontSize = 64; t.characterSize = 0.06f; t.anchor = TextAnchor.MiddleCenter;
            t.color = Palette.Red; t.text = "-" + G.Fmt(dmg);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = ProcArt.Font.material; mr.sortingOrder = 80;
            StartCoroutine(FloatUp(go.transform, t));
        }

        IEnumerator FloatUp(Transform tr, TextMesh t)
        {
            var from = tr.position;
            yield return Tw.Run(1.1f, k =>
            {
                if (!tr) return;
                tr.position = from + Vector3.up * k * 0.9f;
                var c = t.color; c.a = 1 - Mathf.Max(0, k - 0.6f) / 0.4f; t.color = c;
            });
            if (tr) Destroy(tr.gameObject);
        }

        IEnumerator DuelFlash(bool atkWon)
        {
            // 두 장수가 맞부딪히는 섬광: 화면 가운데에 붓 획 두 줄이 엇갈린다 (이름 컷인은 UI가 띄운다)
            SoundKit.Play(Sfx.Gong, 0.9f);
            var a = Sprite(stage, ProcArt.Brush, new Vector3(-6, 0.6f, 0), Palette.Ink, 70);
            var b = Sprite(stage, ProcArt.Brush, new Vector3(6, -0.2f, 0), Palette.Red, 70);
            a.transform.localScale = b.transform.localScale = new Vector3(3, 1.4f, 1);
            b.flipX = true;
            StartCoroutine(Tw.Move(a.transform, Origin + new Vector3(-0.5f, 0.6f, 0), 0.25f));
            yield return Tw.Move(b.transform, Origin + new Vector3(0.5f, -0.2f, 0), 0.25f);
            if (shake) shake.Shake(0.3f, 0.3f);
            SoundKit.Play(Sfx.Clash); Haptics.Heavy();
            StartCoroutine(Flash(Color.white, 0.5f));
            StartCoroutine(SlowMo(0.35f, 0.55f));
            Particles.InkSplash(stage, Origin + new Vector3(0, 0.2f, 0), Palette.Ink);
            var loser = atkWon ? b : a;
            yield return Tw.Wait(0.5f);
            StartCoroutine(Tw.Fade(a, 0, 0.3f));
            yield return Tw.Move(loser.transform, loser.transform.position + Vector3.down * 2, 0.3f, Ease.InQuad);
            yield return Tw.Fade(b, 0, 0.2f);
            Destroy(a.gameObject); Destroy(b.gameObject);
        }

        IEnumerator Finale(bool attackerWon)
        {
            var loser = attackerWon ? def : atk;
            var winner = attackerWon ? atk : def;
            int dir = loser.facingLeft ? 1 : -1;
            StartCoroutine(Tw.Move(loser.root, loser.root.position + new Vector3(dir * 8, 0, 0), 1.0f, Ease.InQuad));
            SoundKit.Play(Sfx.Gong, 0.7f);
            yield return Tw.Scale(winner.banner.transform, Vector3.one * 1.6f, 0.4f, Ease.OutBack);
            Particles.Sparkle(stage, winner.banner.transform.position + Vector3.up, Palette.Gold);
            if (attackerWon && wall) StartCoroutine(Tw.Fade(wall, 0.15f, 0.8f));
            yield return Tw.Wait(0.6f);
        }

        /// <summary>순간적으로 확대했다가 돌아오는 카메라 펀치</summary>
        IEnumerator ZoomPunch(float dur)
        {
            if (cam == null) yield break;
            float baseSize = cam.orthographicSize;
            yield return Tw.Run(dur * 0.3f, k => { if (cam) cam.orthographicSize = baseSize * (1 - 0.06f * k); });
            yield return Tw.Run(dur * 0.7f, k => { if (cam) cam.orthographicSize = baseSize * (0.94f + 0.06f * k); });
            if (cam) cam.orthographicSize = baseSize;
        }

        IEnumerator Flash(Color c, float dur)
        {
            if (flash == null) yield break;
            flash.color = c.WithA(0.75f);
            yield return Tw.Fade(flash, 0, dur);
        }

        /// <summary>일기토 순간의 슬로 모션 (실제 시간 기준으로 되돌린다)</summary>
        IEnumerator SlowMo(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
        }

        public void Clear()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;   // 슬로 모션 중에 끝나도 원래 속도로
            if (stage) Destroy(stage.gameObject);
            atk = def = null;
            wall = null;
        }
    }
}
