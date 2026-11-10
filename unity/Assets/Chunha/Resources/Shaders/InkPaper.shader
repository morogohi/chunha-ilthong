// 선지(한지) 바탕: 종이 결, 먹 번짐 얼룩, 천천히 흐르는 안개. 텍스처 없이 절차적으로 그린다.
Shader "Chunha/InkPaper"
{
    Properties
    {
        _PaperColor ("Paper", Color) = (0.937, 0.910, 0.847, 1)
        _InkColor ("Ink", Color) = (0.118, 0.110, 0.102, 1)
        _Grain ("Grain", Range(0, 0.2)) = 0.06
        _Stain ("Ink Stain", Range(0, 0.3)) = 0.08
        _Mist ("Mist", Range(0, 0.3)) = 0.10
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Scale ("Noise Scale", Float) = 6
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _PaperColor, _InkColor;
            float _Grain, _Stain, _Mist, _Vignette, _Scale;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p) { float v = 0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * vnoise(p); p *= 2.03; a *= 0.5; } return v; }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * _Scale;
                float grain = (hash(i.uv * 900) - 0.5) * _Grain;              // 종이 알갱이
                float fiber = (vnoise(float2(p.x * 18, p.y * 1.5)) - 0.5) * _Grain; // 닥섬유 결
                float stain = smoothstep(0.55, 0.85, fbm(p * 0.7 + 3.1)) * _Stain;  // 먹 얼룩
                float t = _Time.y * 0.02;
                float mist = smoothstep(0.45, 0.9, fbm(p * 0.45 + float2(t, t * 0.6))) * _Mist; // 흐르는 안개
                float2 d = i.uv - 0.5;
                float vig = saturate(dot(d, d) * 2.2) * _Vignette;
                fixed3 col = _PaperColor.rgb + grain + fiber;
                col = lerp(col, _InkColor.rgb, stain + vig * 0.6);
                col = lerp(col, fixed3(1, 1, 1), mist);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
