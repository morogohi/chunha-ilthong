// 타이틀 무대: 달 아래 겹겹이 이어진 수묵 산, 산 사이를 흐르는 안개, 능선을 따라 행군하는 군대.
// 카메라가 이 무대를 비추는 동안 UI가 제목을 붓으로 써 내려간다.
using System.Collections.Generic;
using Chunha.Art;
using UnityEngine;

namespace Chunha.Map
{
    public class TitleScene : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0, 60, 0);
        readonly List<(Transform t, float speed, float baseX)> layers = new List<(Transform, float, float)>();
        readonly List<Transform> mist = new List<Transform>();
        Transform army;
        float armyX = -7;

        public void Build()
        {
            transform.position = Origin;
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(paper.GetComponent<Collider>());
            paper.transform.SetParent(transform, false);
            paper.transform.localPosition = new Vector3(0, 0, 2);
            paper.transform.localScale = new Vector3(14, 20, 1);
            var m = new Material(ProcArt.Mat("InkPaper"));
            m.SetColor("_PaperColor", new Color(0.95f, 0.91f, 0.84f));
            m.SetFloat("_Mist", 0.16f);
            m.SetFloat("_Vignette", 0.55f);
            var mr = paper.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.sortingOrder = -200;

            // 달과 달무리
            Sprite(ProcArt.SoftCircle, new Vector3(1.6f, 4.6f, 0), new Color(1f, 0.93f, 0.8f, 0.55f), 4.2f, -190);
            Sprite(ProcArt.Disc, new Vector3(1.6f, 4.6f, 0), new Color(0.98f, 0.95f, 0.88f, 1f), 1.9f, -189);
            Sprite(ProcArt.Disc, new Vector3(1.6f, 4.6f, 0), Palette.Seal.WithA(0.18f), 1.95f, -188);

            // 겹겹의 산 (멀수록 옅고 느리다)
            float[] y = { 0.8f, -0.6f, -2.0f, -3.4f };
            float[] a = { 0.10f, 0.18f, 0.30f, 0.62f };
            float[] sc = { 6.5f, 5.6f, 4.8f, 4.2f };
            for (int i = 0; i < y.Length; i++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    var sr = Sprite(ProcArt.Mountain, new Vector3(k * sc[i] * 1.6f + i * 0.9f, y[i], 0), Palette.Ink.WithA(a[i]), 1, -180 + i * 10);
                    sr.transform.localScale = new Vector3(sc[i], sc[i] * (0.55f + i * 0.08f), 1);
                    layers.Add((sr.transform, 0.05f + i * 0.06f, sr.transform.localPosition.x));
                }
                var ms = Sprite(ProcArt.SoftCircle, new Vector3(Random.Range(-3f, 3f), y[i] + 0.3f, 0), Color.white.WithA(0.22f), 1, -175 + i * 10);
                ms.transform.localScale = new Vector3(9, 1.4f, 1);
                mist.Add(ms.transform);
            }

            // 능선 위를 지나는 군대 실루엣
            army = new GameObject("Army").transform;
            army.SetParent(transform, false);
            army.localPosition = new Vector3(armyX, -2.05f, 0);
            var banner = Sprite(ProcArt.Banner, Vector3.zero, Palette.Red.WithA(0.9f), 0.7f, -144, army);
            banner.transform.localPosition = new Vector3(0.5f, 0, 0);
            for (int i = 0; i < 9; i++)
            {
                var s = Sprite(ProcArt.Soldier, new Vector3(-i * 0.24f, 0, 0), Palette.Ink.WithA(0.8f), 0.38f, -145, army);
                s.transform.localPosition = new Vector3(-i * 0.24f, (i % 2) * 0.04f, 0);
            }
        }

        SpriteRenderer Sprite(Sprite sp, Vector3 local, Color c, float scale, int order, Transform parent = null)
        {
            var sr = new GameObject(sp.name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent ?? transform, false);
            sr.transform.localPosition = local;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = sp; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            float t = Time.time;
            foreach (var (tr, speed, bx) in layers)
            {
                if (!tr) continue;
                var p = tr.localPosition;
                p.x = bx + Mathf.Sin(t * speed * 0.25f) * speed * 3;   // 숨 쉬듯 좌우로 흐른다 (시차)
                tr.localPosition = p;
            }
            for (int i = 0; i < mist.Count; i++)
            {
                var p = mist[i].localPosition;
                p.x += Time.deltaTime * (0.15f + i * 0.05f);
                if (p.x > 9) p.x = -9;
                mist[i].localPosition = p;
            }
            if (army)
            {
                armyX += Time.deltaTime * 0.35f;
                if (armyX > 7) armyX = -7;
                army.localPosition = new Vector3(armyX, -2.05f + Mathf.Sin(armyX * 0.6f) * 0.15f, 0);
                for (int i = 0; i < army.childCount; i++)
                {
                    var c = army.GetChild(i);
                    var s = c.localScale;
                    c.localScale = new Vector3(s.x, s.x * (1 + 0.04f * Mathf.Sin(t * 9 + i)), 1);
                }
            }
        }
    }
}
