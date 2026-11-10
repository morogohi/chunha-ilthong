// 그림 파일 없이 코드로 그리는 스프라이트·텍스처. 모두 흰색으로 그리고 사용하는 쪽에서 색을 입힌다.
// 정식 원화가 들어오면 같은 이름의 스프라이트를 Resources에서 우선 불러오도록 바꾸면 된다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chunha.Art
{
    public static class Palette
    {
        public static readonly Color Paper = Hex("#EFE8D8");
        public static readonly Color Paper2 = Hex("#F6F1E6");
        public static readonly Color Ink = Hex("#1E1C1A");
        public static readonly Color Ink2 = Hex("#45413B");
        public static readonly Color Ink3 = Hex("#6B655C");
        public static readonly Color Line = Hex("#C9C0AE");
        public static readonly Color Red = Hex("#B5432F");
        public static readonly Color Seal = Hex("#A63A2B");
        public static readonly Color Good = Hex("#3F6B3A");
        public static readonly Color Neutral = Hex("#B9B1A1");
        public static readonly Color River = new Color(0.42f, 0.53f, 0.60f, 0.6f);
        public static readonly Color Gold = Hex("#C9A24A");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        public static Color WithA(this Color c, float a) { c.a = a; return c; }
        public static Color Darken(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    public static class ProcArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, int w, int h, Func<float, float, float> alpha, Vector2? pivot = null, float ppu = 64, Vector4 border = default)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            cache[key] = s;
            return s;
        }

        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }
        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        /// <summary>흰 사각형. uGUI Image는 스프라이트가 없으면 Filled 설정을 무시하므로 채우기 막대·와이프에 반드시 쓴다.</summary>
        public static Sprite White => Make("white", 4, 4, (u, v) => 1);

        /// <summary>부드러운 원 (입자, 먼지, 안개)</summary>
        public static Sprite SoftCircle => Make("soft", 64, 64, (u, v) => { float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; return 1 - Smooth(0.2f, 1f, r); });

        /// <summary>단단한 원 (도시 마커)</summary>
        public static Sprite Disc => Make("disc", 128, 128, (u, v) => { float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2; return 1 - Smooth(0.94f, 1f, r); }, null, 128);

        /// <summary>붓으로 그린 고리 (선택 표시)</summary>
        public static Sprite Ring => Make("ring", 128, 128, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) * 2; float ang = Mathf.Atan2(dy, dx);
            float w = 0.07f + 0.04f * Noise(ang * 3 + 10, 1); float gap = Smooth(-2.6f, -2.2f, ang) * (1 - Smooth(-2.0f, -1.6f, ang));
            return (1 - Smooth(w * 0.6f, w, Mathf.Abs(r - 0.85f))) * (1 - gap * 0.9f);
        }, null, 128);

        /// <summary>둥근 사각형 9-slice (UI 패널·버튼)</summary>
        public static Sprite RoundRect => Make("rrect", 64, 64, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f) * 64, y = Mathf.Abs(v - 0.5f) * 64; float r = 18, e = 32 - r;
            float dx = Mathf.Max(x - e, 0), dy = Mathf.Max(y - e, 0);
            return 1 - Smooth(r - 1.2f, r, Mathf.Sqrt(dx * dx + dy * dy));
        }, null, 64, new Vector4(24, 24, 24, 24));

        /// <summary>붓 획 (제목 띠, 일기토 컷인)</summary>
        public static Sprite Brush => Make("brush", 256, 64, (u, v) =>
        {
            float edge = 0.32f + 0.1f * Noise(u * 14, 3) + 0.06f * Noise(u * 40, 7);
            float body = 1 - Smooth(edge - 0.04f, edge, Mathf.Abs(v - 0.5f));
            float dry = Noise(u * 5, v * 34) > 0.78f - 0.3f * u ? 0.35f : 1f;   // 붓결 방향(가로)으로 갈라지는 갈필, 끝으로 갈수록 많아진다
            float taper = Smooth(0f, 0.08f, u) * (1 - Smooth(0.85f, 1f, u + 0.08f * Noise(v * 5, 2)));
            return body * dry * taper;
        });

        /// <summary>먹 얼룩 (먹 튀김 입자)</summary>
        public static Sprite InkSplat => Make("splat", 64, 64, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) * 2; float ang = Mathf.Atan2(dy, dx);
            float rad = 0.55f + 0.3f * Noise(ang * 2 + 4, 0.5f);
            return 1 - Smooth(rad - 0.1f, rad, r);
        });

        /// <summary>병사 실루엣 (투구를 쓰고 창을 세워 든 보병) — 아래 중앙이 기준점</summary>
        public static Sprite Soldier => Make("soldier", 32, 48, (u, v) =>
        {
            float a = 0;
            float hx = u - 0.42f, hy = v - 0.78f;
            if (hx * hx / 0.010f + hy * hy / 0.006f < 1) a = 1;                                          // 머리
            if (v > 0.80f && v < 0.90f && Mathf.Abs(u - 0.42f) < 0.11f * (1 - (v - 0.80f) / 0.12f)) a = 1;   // 투구
            float bodyW = 0.13f + (0.70f - v) * 0.22f;
            if (v > 0.32f && v < 0.71f && Mathf.Abs(u - 0.42f) < bodyW) a = 1;                            // 갑옷·포
            if (v > 0.52f && v < 0.60f && u > 0.42f && u < 0.78f) a = 1;                                  // 창 쥔 팔
            if (v < 0.34f && (Mathf.Abs(u - 0.33f) < 0.065f || Mathf.Abs(u - 0.52f) < 0.065f)) a = 1;     // 다리
            if (v < 0.05f && (Mathf.Abs(u - 0.30f) < 0.10f || Mathf.Abs(u - 0.55f) < 0.10f)) a = 1;       // 신
            if (Mathf.Abs(u - 0.78f) < 0.028f && v > 0.04f && v < 0.92f) a = 1;                            // 창대
            if (v >= 0.90f && Mathf.Abs(u - 0.78f) < 0.075f * (1 - (v - 0.90f) / 0.10f)) a = 1;            // 창날
            return a;
        }, new Vector2(0.45f, 0f), 48);

        /// <summary>군기 (깃대 + 흔들리는 깃발)</summary>
        public static Sprite Banner => Make("banner", 48, 96, (u, v) =>
        {
            float a = 0;
            if (u < 0.1f && u > 0.03f) a = 1;                                                            // 깃대
            float wave = 0.04f * Mathf.Sin(v * 12);
            if (v > 0.45f && v < 0.95f && u > 0.1f && u < 0.92f + wave - (0.95f - v) * 0.1f) a = 1;       // 깃발
            if (v > 0.95f && u < 0.16f) a = 1;                                                           // 장식
            return a;
        }, new Vector2(0.05f, 0f), 64);

        /// <summary>불꽃</summary>
        public static Sprite Flame => Make("flame", 32, 48, (u, v) =>
        {
            float w = 0.45f * Mathf.Sqrt(Mathf.Clamp01(1 - v)) * (0.6f + v); float d = Mathf.Abs(u - 0.5f);
            return (1 - Smooth(w * 0.6f, w, d)) * (1 - Smooth(0.85f, 1f, v));
        }, new Vector2(0.5f, 0f));

        /// <summary>화살</summary>
        public static Sprite Arrow => Make("arrow", 64, 12, (u, v) =>
        {
            float a = Mathf.Abs(v - 0.5f) < 0.08f && u < 0.85f ? 1 : 0;
            if (u > 0.82f && Mathf.Abs(v - 0.5f) < (1 - u) * 2.6f) a = 1;
            if (u < 0.14f && Mathf.Abs(v - 0.5f) < 0.38f && Mathf.Abs(v - 0.5f) > 0.12f && ((int)(u * 30)) % 2 == 0) a = 0.8f;
            return a;
        });

        public static Sprite Petal => Make("petal", 24, 24, (u, v) => { float x = (u - 0.5f) * 2, y = (v - 0.5f) * 3.2f; return 1 - Smooth(0.7f, 1f, x * x + y * y); });
        public static Sprite Leaf => Make("leaf", 24, 32, (u, v) => { float x = (u - 0.5f) * 2, y = (v - 0.5f) * 2; float w = 0.6f * (1 - y * y); return (1 - Smooth(w - 0.1f, w, Mathf.Abs(x))) + (Mathf.Abs(x) < 0.05f ? 0.3f : 0); });
        public static Sprite RainDrop => Make("rain", 4, 32, (u, v) => 1 - Smooth(0.6f, 1f, Mathf.Abs(u - 0.5f) * 2) * 0.5f - (1 - v) * 0.6f);

        /// <summary>먼 산 실루엣 (능선이 겹친 수묵 산수)</summary>
        public static Sprite Mountain => Make("mountain", 256, 128, (u, v) =>
        {
            float ridge1 = 0.55f + 0.30f * Noise(u * 4, 1) + 0.08f * Noise(u * 16, 2);
            float ridge2 = 0.35f + 0.22f * Noise(u * 5 + 7, 3) + 0.06f * Noise(u * 20, 4);
            float shape = Mathf.Sin(u * Mathf.PI);
            float a1 = v < ridge1 * shape ? 0.45f * (1 - v / Mathf.Max(0.01f, ridge1 * shape)) + 0.25f : 0;
            float a2 = v < ridge2 * shape ? 0.55f : 0;
            float edge = Mathf.Abs(v - ridge1 * shape) < 0.012f ? 0.6f : 0;
            return Mathf.Max(Mathf.Max(a1, a2), edge) * Smooth(0f, 0.12f, v + 0.02f);
        }, new Vector2(0.5f, 0f), 64);

        /// <summary>성벽 (전투 장면): 가로로 긴 성벽 + 성가퀴 + 성문 아치 + 문루</summary>
        public static Sprite Wall => Make("wall", 128, 96, (u, v) =>
        {
            float a = 0;
            if (v < 0.62f) a = 1;                                                                  // 성벽 몸체
            if (v >= 0.62f && v < 0.72f && ((int)(u * 12)) % 2 == 0) a = 1;                        // 성가퀴
            float gx = (u - 0.5f) / 0.13f, gy = v / 0.40f;
            if (gy < 1 && gx * gx + Mathf.Max(0, gy - 0.6f) * Mathf.Max(0, gy - 0.6f) / 0.16f < 1) a = 0.18f; // 성문 아치
            if (v >= 0.62f && v < 0.84f && Mathf.Abs(u - 0.5f) < 0.16f) a = 1;                     // 문루 몸체
            if (v >= 0.84f && v < 0.98f && Mathf.Abs(u - 0.5f) < 0.24f - (v - 0.84f) * 1.2f) a = 1;  // 문루 지붕
            if (a == 1 && v < 0.62f && ((int)(v * 14) % 2 == 0 ? Mathf.Repeat(u * 10, 1) : Mathf.Repeat(u * 10 + 0.5f, 1)) < 0.06f) a = 0.8f; // 돌 줄눈
            return a;
        }, new Vector2(0.5f, 0f), 48);

        /// <summary>점선 (도로) — LineRenderer Tile 모드용 텍스처</summary>
        public static Texture2D DashTexture
        {
            get
            {
                var s = Make("dash", 32, 8, (u, v) => u < 0.55f ? 1 - Smooth(0.6f, 1f, Mathf.Abs(v - 0.5f) * 2) : 0);
                s.texture.wrapMode = TextureWrapMode.Repeat;
                return s.texture;
            }
        }

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        /// <summary>Resources/Shaders의 셰이더로 머티리얼을 만든다. 셰이더가 없으면 Sprites/Default로 대신한다.</summary>
        public static Material Mat(string shader, string key = null)
        {
            key ??= shader;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            var sh = Resources.Load<Shader>("Shaders/" + shader) ?? Shader.Find("Chunha/" + shader) ?? Shader.Find("Sprites/Default");
            m = new Material(sh) { name = key };
            mats[key] = m;
            return m;
        }

        public static Material SpriteMat
        {
            get
            {
                if (mats.TryGetValue("sprite", out var m) && m != null) return m;
                m = new Material(Shader.Find("Sprites/Default")) { name = "sprite" };
                mats["sprite"] = m;
                return m;
            }
        }

        static Font font;
        /// <summary>한글 글꼴: Resources/Fonts/KoreanFont.ttf가 있으면 그것을, 없으면 기본 글꼴(OS 한글 글꼴로 자동 대체)을 쓴다.</summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("Fonts/KoreanFont");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }
    }
}
