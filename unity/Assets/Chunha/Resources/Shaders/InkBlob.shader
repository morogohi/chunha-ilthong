// 세력 영역의 먹 번짐. _Progress 0→1로 번져 나가고, 가장자리는 노이즈로 일렁인다.
Shader "Chunha/InkBlob"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.26, 0.18, 0.5)
        _Progress ("Progress", Range(0, 1)) = 1
        _Edge ("Edge Softness", Range(0.01, 0.5)) = 0.12
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Progress, _Edge, _Seed;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float hash(float2 p) { p = frac(p * float2(127.1, 311.7)); p += dot(p, p + 19.19); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p); float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float r = length(d) * 2;
                float ang = atan2(d.y, d.x);
                float2 dir = float2(cos(ang), sin(ang));                           // 각도를 원 위의 점으로 바꿔 ±180° 이음매를 없앤다
                float wob = vnoise(dir * 1.6 + float2(_Seed, _Time.y * 0.3 + _Seed)) * 0.18
                          + vnoise(i.uv * 9 + _Seed) * 0.10;                 // 번진 가장자리
                float radius = _Progress * (0.82 + wob);
                float a = 1 - smoothstep(radius - _Edge, radius, r);
                float body = 0.55 + 0.45 * smoothstep(radius, radius * 0.3, r); // 가장자리일수록 먹이 진하다
                float rim = smoothstep(radius - _Edge * 1.6, radius - _Edge * 0.4, r) * a;
                fixed4 c = _Color;
                c.a *= a * body + rim * 0.35;
                return c;
            }
            ENDCG
        }
    }
}
