// 코드로 만드는 파티클: 먹 튀김, 먼지, 불길, 연기, 수확, 재해, 계절 날씨.
using System.Collections.Generic;
using Chunha.Art;
using UnityEngine;

namespace Chunha.FX
{
    public static class Particles
    {
        static readonly Dictionary<Sprite, Material> mats = new Dictionary<Sprite, Material>();

        static Material MatFor(Sprite sprite)
        {
            if (mats.TryGetValue(sprite, out var m) && m != null) return m;
            m = new Material(Shader.Find("Sprites/Default")) { mainTexture = sprite.texture, name = "ps_" + sprite.name };
            mats[sprite] = m;
            return m;
        }

        /// <summary>파티클 시스템을 만들고 기본값을 채운다. 설정이 끝나면 Play()를 부른다.</summary>
        public static ParticleSystem Create(string name, Transform parent, Vector3 pos, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = MatFor(sprite);
            r.sortingOrder = sortingOrder;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static void SetVelocity(ParticleSystem ps, float minX, float maxX, float minY, float maxY)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(minX, maxX);
            v.y = new ParticleSystem.MinMaxCurve(minY, maxY);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        static void FadeOut(ParticleSystem ps, Color c)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            // startColor와 곱해지므로 여기서는 흰색·알파 곡선만 준다 (색이 제곱되지 않게)
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>한 번 터지고 사라지는 파티클 (먹 튀김, 먼지, 불꽃, 금빛 등)</summary>
        public static void Burst(Transform parent, Vector3 pos, Sprite sprite, Color color, int count, float speed, float life, float size, float gravity = 0, float radius = 0.2f, int order = 40)
        {
            var ps = Create("burst", parent, pos, sprite, order);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = color;
            main.gravityModifier = gravity;
            var shape = ps.shape; shape.radius = radius;
            FadeOut(ps, color);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.3f));
            ps.Play();
            ps.Emit(count);
            Object.Destroy(ps.gameObject, life + 0.5f);
        }

        public static void InkSplash(Transform p, Vector3 pos, Color c) => Burst(p, pos, ProcArt.InkSplat, c, 26, 2.4f, 0.9f, 0.45f, 0, 0.3f, 45);
        public static void Dust(Transform p, Vector3 pos, float scale = 1) => Burst(p, pos, ProcArt.SoftCircle, new Color(0.55f, 0.48f, 0.38f, 0.45f), (int)(18 * scale), 1.2f * scale, 1.1f, 0.6f * scale, -0.05f, 0.4f * scale, 35);
        public static void Smoke(Transform p, Vector3 pos, float scale = 1) => Burst(p, pos, ProcArt.SoftCircle, new Color(0.25f, 0.23f, 0.22f, 0.5f), (int)(20 * scale), 0.8f, 1.8f, 0.9f * scale, -0.08f, 0.5f * scale, 46);
        public static void Sparkle(Transform p, Vector3 pos, Color c) => Burst(p, pos, ProcArt.SoftCircle, c, 16, 1.2f, 1.0f, 0.18f, -0.15f, 0.3f, 48);

        /// <summary>화계 불길: 위로 솟는 불꽃 + 연기</summary>
        public static void Fire(Transform parent, Vector3 pos, float width, float duration)
        {
            var ps = Create("fire", parent, pos, ProcArt.Flame, 47);
            var main = ps.main;
            main.loop = true;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            var em = ps.emission; em.rateOverTime = 70 * width;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(width, 0.4f, 0.1f);
            SetVelocity(ps, -0.15f, 0.15f, 0.8f, 1.8f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(0.9f, 0.3f, 0.1f), 0.5f), new GradientColorKey(new Color(0.2f, 0.15f, 0.12f), 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.95f, 0.15f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
            ps.Play();
            var stopper = ps.gameObject.AddComponent<AutoStop>();
            stopper.after = duration;
            Smoke(parent, pos + Vector3.up * 0.8f, width * 0.6f);
        }

        /// <summary>메뚜기 떼 / 역병 안개 / 홍수 물결</summary>
        public static void Disaster(Transform parent, Vector3 pos, string kind)
        {
            switch (kind)
            {
                case "locust": Burst(parent, pos, ProcArt.SoftCircle, new Color(0.25f, 0.22f, 0.12f, 0.9f), 60, 2.5f, 1.6f, 0.09f, 0, 0.6f, 48); break;
                case "plague": Burst(parent, pos, ProcArt.SoftCircle, new Color(0.45f, 0.5f, 0.38f, 0.35f), 24, 0.4f, 2.4f, 1.4f, -0.02f, 0.5f, 46); break;
                default: Burst(parent, pos, ProcArt.Ring, new Color(0.35f, 0.5f, 0.62f, 0.7f), 8, 0.2f, 1.6f, 1.6f, 0, 0.1f, 46); break;
            }
        }
    }

    /// <summary>일정 시간 뒤 방출을 멈추고 오브젝트를 지운다.</summary>
    public class AutoStop : MonoBehaviour
    {
        public float after = 1;
        float t;
        void Update()
        {
            t += Time.deltaTime;
            if (t < after) return;
            var ps = GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2f);
            enabled = false;
        }
    }

    /// <summary>계절 날씨: 봄 꽃잎, 여름 비, 가을 낙엽, 겨울 눈. 카메라를 따라다닌다.</summary>
    public class Weather : MonoBehaviour
    {
        ParticleSystem ps;
        int season = -1;
        public Camera cam;

        public static int SeasonOf(int month) => month >= 3 && month <= 5 ? 0 : month >= 6 && month <= 8 ? 1 : month >= 9 && month <= 11 ? 2 : 3;

        public void SetMonth(int month)
        {
            int s = SeasonOf(month);
            if (s == season && ps != null) return;
            season = s;
            if (ps != null) Destroy(ps.gameObject);
            Sprite sp; Color c; float rate, size, fallMin, fallMax, sway;
            switch (s)
            {
                case 0: sp = ProcArt.Petal; c = new Color(0.93f, 0.72f, 0.74f, 0.85f); rate = 6; size = 0.16f; fallMin = -0.5f; fallMax = -0.25f; sway = 0.5f; break;
                case 1: sp = ProcArt.RainDrop; c = new Color(0.45f, 0.52f, 0.6f, 0.45f); rate = 70; size = 0.35f; fallMin = -9f; fallMax = -7f; sway = 0.3f; break;
                case 2: sp = ProcArt.Leaf; c = new Color(0.72f, 0.42f, 0.18f, 0.85f); rate = 5; size = 0.2f; fallMin = -0.7f; fallMax = -0.35f; sway = 0.8f; break;
                default: sp = ProcArt.SoftCircle; c = new Color(1f, 1f, 1f, 0.9f); rate = 22; size = 0.12f; fallMin = -0.8f; fallMax = -0.4f; sway = 0.35f; break;
            }
            ps = Particles.Create("weather", transform, transform.position + Vector3.up * 7, sp, 60);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = s == 1 ? 1.6f : 14f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(14, 0.2f, 0.1f);
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(-sway, sway); v.y = new ParticleSystem.MinMaxCurve(fallMin, fallMax); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            if (s != 1)
            {
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                var n = ps.noise; n.enabled = true; n.strength = 0.4f; n.frequency = 0.3f;
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (s == 1) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2; }
            ps.Play();
            ps.Simulate(8f, true, true);   // 시작부터 화면에 차 있게
            ps.Play();
        }

        void LateUpdate()
        {
            if (cam != null) transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
        }
    }
}
