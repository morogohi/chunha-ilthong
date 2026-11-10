// 천하 지도: 선지 바탕 + 먼 산 + 흐르는 강 + 도로 + 도시(세력 먹 번짐) + 안개.
using System.Collections;
using System.Collections.Generic;
using Chunha.Art;
using Chunha.Core;
using Chunha.FX;
using UnityEngine;

namespace Chunha.Map
{
    public class MapView : MonoBehaviour
    {
        public readonly Dictionary<string, CityNode> nodes = new Dictionary<string, CityNode>();
        readonly List<Transform> mist = new List<Transform>();
        public Transform fxRoot;

        /// <summary>프로토타입 지도 좌표(400×470) → 월드 좌표</summary>
        public static Vector3 W(float x, float y) => new Vector3((x - 200f) / 40f, -(y - 235f) / 40f, 0);
        public static Vector3 CityPos(City c) => W(c.x + 20, c.y + 20);

        public static readonly Rect Bounds = new Rect(-5.2f, -6.2f, 10.8f, 12.2f);

        public void Build(GameState s)
        {
            fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(transform, false);

            // 바탕 종이
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(paper.GetComponent<Collider>());
            paper.name = "Paper";
            paper.transform.SetParent(transform, false);
            paper.transform.position = new Vector3(Bounds.center.x, Bounds.center.y, 1);
            paper.transform.localScale = new Vector3(Bounds.width + 6, Bounds.height + 6, 1);
            var pr = paper.GetComponent<MeshRenderer>();
            pr.sharedMaterial = ProcArt.Mat("InkPaper");
            pr.sortingOrder = -100;

            // 먼 산 (수묵 능선)
            Mountain(W(30, 230), 4.2f, 0.20f);   // 진령
            Mountain(W(170, 30), 3.4f, 0.16f);   // 태항산
            Mountain(W(80, 420), 3.8f, 0.14f);   // 형남의 산
            Mountain(W(390, 250), 2.6f, 0.10f);  // 대별산
            Mountain(W(10, 120), 2.8f, 0.15f);   // 농서

            // 강
            River(new[] { W(0, 130), W(70, 112), W(140, 124), W(200, 150), W(260, 156), W(330, 134), W(410, 140) }, 0.34f);          // 황하
            River(new[] { W(-10, 322), W(80, 300), W(150, 336), W(210, 352), W(270, 398), W(330, 412), W(410, 386) }, 0.42f);        // 장강
            River(new[] { W(110, 290), W(150, 318), W(180, 360), W(212, 352) }, 0.18f);                                            // 한수

            // 도로
            foreach (var r in s.roads)
            {
                var a = CityPos(s.City(r.a)); var b = CityPos(s.City(r.b));
                var lr = Line("Road", new[] { a, b }, 0.07f, Palette.Ink2.WithA(0.55f), -40);
                var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = ProcArt.DashTexture };
                lr.material = m;
                lr.textureMode = LineTextureMode.Tile;
            }

            // 도시
            int i = 0;
            foreach (var c in s.cities)
            {
                var go = new GameObject("City_" + c.id);
                go.transform.SetParent(transform, false);
                go.transform.position = CityPos(c);
                var n = go.AddComponent<CityNode>();
                n.Build(c, i++);
                nodes[c.id] = n;
            }

            // 안개
            for (int k = 0; k < 7; k++)
            {
                var sr = Sprite("Mist", ProcArt.SoftCircle, new Vector3(Random.Range(-6f, 6f), Random.Range(-6f, 6f), 0), Color.white.WithA(0.10f), 30);
                sr.transform.localScale = new Vector3(Random.Range(5f, 8f), Random.Range(2f, 3.2f), 1);
                mist.Add(sr.transform);
            }
            Refresh(s, false);
        }

        void Mountain(Vector3 pos, float scale, float alpha)
        {
            var sr = Sprite("Mountain", ProcArt.Mountain, pos + new Vector3(0, -0.6f, 0), Palette.Ink.WithA(alpha), -60);
            sr.transform.localScale = new Vector3(scale, scale * 0.8f, 1);
        }

        void River(Vector3[] pts, float width)
        {
            var smooth = Catmull(pts, 8);
            var lr = Line("River", smooth, width, Color.white, -50);
            lr.material = ProcArt.Mat("InkRiver");
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 4;
            lr.colorGradient = Grad(Palette.River);
        }

        static Gradient Grad(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(c.a, 0.06f), new GradientAlphaKey(c.a, 0.94f), new GradientAlphaKey(0, 1) });
            return g;
        }

        static Vector3[] Catmull(Vector3[] p, int seg)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < p.Length - 1; i++)
            {
                var p0 = p[Mathf.Max(i - 1, 0)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Mathf.Min(i + 2, p.Length - 1)];
                for (int k = 0; k < seg; k++)
                {
                    float t = k / (float)seg, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            o.Add(p[p.Length - 1]);
            return o.ToArray();
        }

        LineRenderer Line(string name, Vector3[] pts, float width, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.widthMultiplier = width;
            lr.startColor = c; lr.endColor = c;
            lr.sortingOrder = order;
            lr.numCornerVertices = 2;
            return lr;
        }

        SpriteRenderer Sprite(string name, Sprite sp, Vector3 pos, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            for (int i = 0; i < mist.Count; i++)
            {
                var t = mist[i];
                var p = t.position;
                p.x += Time.deltaTime * (0.08f + 0.03f * i);
                if (p.x > 9) p.x = -9;
                t.position = p;
            }
        }

        /// <summary>상태를 지도에 반영한다. animate=true면 바뀐 세력을 먹 번짐으로 칠한다.</summary>
        public void Refresh(GameState s, bool animate)
        {
            foreach (var c in s.cities)
            {
                var col = G.None(c.faction) ? Palette.Neutral : Palette.Hex(s.Faction(c.faction).color);
                var n = nodes[c.id];
                n.SetFaction(c.faction, col, animate, fxRoot);
                n.SetInfo(c, c.faction == s.player, s.pending.Exists(p => p.city == c.id) && c.faction == s.player);
            }
        }

        public void Select(string id)
        {
            foreach (var kv in nodes) kv.Value.SetSelected(kv.Key == id);
        }

        public CityNode NodeAt(Vector3 world)
        {
            var hit = Physics2D.OverlapPoint(world);
            return hit ? hit.GetComponent<CityNode>() : null;
        }

        /// <summary>도로를 따라 군대가 행군한다 (깃발 + 병사 + 먼지)</summary>
        public IEnumerator March(Vector3 from, Vector3 to, Color color, float dur = 1.1f, int soldiers = 6)
        {
            var g = new GameObject("March").transform;
            g.SetParent(fxRoot, false);
            g.position = from;
            var dir = (to - from).normalized;
            bool left = dir.x < 0;
            var banner = new GameObject("Banner").AddComponent<SpriteRenderer>();
            banner.transform.SetParent(g, false);
            banner.sprite = ProcArt.Banner; banner.color = color; banner.sortingOrder = 55; banner.flipX = left;
            banner.transform.localScale = Vector3.one * 0.55f;
            var men = new List<Transform>();
            for (int i = 0; i < soldiers; i++)
            {
                var sr = new GameObject("Man").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(g, false);
                sr.sprite = ProcArt.Soldier; sr.color = Palette.Ink.WithA(0.9f); sr.sortingOrder = 54; sr.flipX = left;
                sr.transform.localScale = Vector3.one * 0.32f;
                sr.transform.localPosition = new Vector3(-dir.x * (0.18f + (i / 2) * 0.16f), -0.08f + (i % 2) * 0.12f - dir.y * (i / 2) * 0.16f, 0);
                men.Add(sr.transform);
            }
            float t = 0, dustT = 0;
            while (t < dur)
            {
                t += Time.deltaTime; dustT += Time.deltaTime;
                float k = Ease.InOutCubic(Mathf.Clamp01(t / dur));
                g.position = Vector3.Lerp(from, to, k);
                for (int i = 0; i < men.Count; i++)
                {
                    var p = men[i].localPosition;
                    p.y += Mathf.Sin(Time.time * 18 + i) * 0.004f;
                    men[i].localPosition = p;
                }
                banner.transform.localScale = new Vector3(0.55f * (1 + 0.06f * Mathf.Sin(Time.time * 9)), 0.55f, 1);
                if (dustT > 0.12f) { dustT = 0; Particles.Dust(fxRoot, g.position - dir * 0.3f, 0.35f); }
                yield return null;
            }
            Destroy(g.gameObject, 0.05f);
        }
    }

    public class CityNode : MonoBehaviour
    {
        public string id;
        MeshRenderer blob;
        Material blobMat;
        SpriteRenderer outline, fill, ring, flag;
        TextMesh nameText, troopsText, warn;
        string faction = "\u0000";
        bool selected;
        float seed;

        public void Build(City c, int index)
        {
            id = c.id;
            seed = index * 1.37f;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Territory";
            q.transform.SetParent(transform, false);
            q.transform.localScale = Vector3.one * 2.8f;
            blob = q.GetComponent<MeshRenderer>();
            blobMat = new Material(ProcArt.Mat("InkBlob"));
            blobMat.SetFloat("_Seed", seed);
            blob.sharedMaterial = blobMat;
            blob.sortingOrder = -20;

            outline = Sprite("Outline", ProcArt.Disc, Palette.Ink, 0.72f, 10);
            fill = Sprite("Fill", ProcArt.Disc, Palette.Neutral, 0.62f, 11);
            ring = Sprite("Ring", ProcArt.Ring, Palette.Ink, 1.15f, 12);
            ring.enabled = false;
            flag = Sprite("Flag", ProcArt.Banner, Palette.Red, 0.42f, 13);
            flag.transform.localPosition = new Vector3(0.18f, 0.2f, 0);
            flag.enabled = false;

            nameText = Text("Name", c.name, 0.042f, Palette.Paper2, new Vector3(0, 0, 0), 20);   // 두 글자가 원 안에 들어가는 크기
            troopsText = Text("Troops", "", 0.034f, Palette.Ink2, new Vector3(0, -0.55f, 0), 20);
            warn = Text("Warn", "!", 0.08f, Palette.Red, new Vector3(0.36f, 0.3f, 0), 21);
            warn.gameObject.SetActive(false);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;
        }

        SpriteRenderer Sprite(string n, Sprite sp, Color c, float scale, int order)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }

        TextMesh Text(string n, string s, float size, Color c, Vector3 pos, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMesh>();
            t.font = ProcArt.Font;
            t.fontSize = 64;
            t.characterSize = size;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = c;
            t.text = s;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = ProcArt.Font.material;
            mr.sortingOrder = order;
            return t;
        }

        public void SetFaction(string f, Color col, bool animate, Transform fx)
        {
            if (f == faction) return;
            bool first = faction == "\u0000";
            faction = f;
            fill.color = col;
            var blobCol = col.WithA(G.None(f) ? 0.10f : 0.32f);
            if (first || !animate) { blobMat.SetColor("_Color", blobCol); blobMat.SetFloat("_Progress", 1); return; }
            StartCoroutine(Spread(blobCol, fx, col));
        }

        IEnumerator Spread(Color to, Transform fx, Color solid)
        {
            Particles.InkSplash(fx, transform.position, solid.Darken(0.7f));
            var from = blobMat.GetColor("_Color");
            yield return Tw.Run(0.25f, k => blobMat.SetFloat("_Progress", 1 - k * 0.9f));
            blobMat.SetColor("_Color", to);
            yield return Tw.Run(1.1f, k => blobMat.SetFloat("_Progress", 0.1f + k * 0.9f), Ease.OutBack);
            yield return Tw.Scale(fill.transform, Vector3.one * 0.8f, 0.12f);
            yield return Tw.Scale(fill.transform, Vector3.one * 0.62f, 0.25f, Ease.OutBack);
            _ = from;
        }

        public void SetInfo(City c, bool mine, bool warning)
        {
            troopsText.text = $"{Mathf.RoundToInt((float)c.troops / 1000f)}k";
            flag.enabled = mine;
            outline.transform.localScale = Vector3.one * (mine ? 0.78f : 0.72f);
            warn.gameObject.SetActive(warning);
        }

        public void SetSelected(bool on)
        {
            selected = on;
            ring.enabled = on;
        }

        void Update()
        {
            if (selected)
            {
                ring.transform.Rotate(0, 0, -25 * Time.deltaTime);
                ring.transform.localScale = Vector3.one * (1.15f + 0.06f * Mathf.Sin(Time.time * 3));
            }
            if (flag.enabled) flag.transform.localScale = new Vector3(0.42f * (1 + 0.08f * Mathf.Sin(Time.time * 6 + seed)), 0.42f, 1);
            if (warn.gameObject.activeSelf) warn.color = Palette.Red.WithA(0.5f + 0.5f * Mathf.Sin(Time.time * 6));
            // 세력 영역이 숨 쉬듯 미세하게 움직인다
            blob.transform.localScale = Vector3.one * (2.8f + 0.06f * Mathf.Sin(Time.time * 0.8f + seed));
        }
    }
}
