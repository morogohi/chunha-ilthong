// 강: LineRenderer(Texture Mode = Tile)에 쓴다. uv.x 방향으로 물결 먹선이 흐른다.
Shader "Chunha/InkRiver"
{
    Properties
    {
        _Color ("Color", Color) = (0.42, 0.53, 0.60, 0.55)
        _Speed ("Flow Speed", Float) = 0.6
        _Freq ("Wave Frequency", Float) = 3
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
            fixed4 _Color; float _Speed, _Freq;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float across = abs(i.uv.y - 0.5) * 2;               // 0 = 강 가운데, 1 = 가장자리
                float body = 1 - smoothstep(0.55, 1.0, across);      // 부드러운 강변
                float flow = sin((i.uv.x - _Time.y * _Speed) * _Freq * 6.2831 + i.uv.y * 4);
                float ripple = smoothstep(0.7, 1.0, flow) * (1 - across) * 0.6;
                fixed4 c = _Color * i.color;
                c.rgb = lerp(c.rgb, fixed3(0.95, 0.95, 0.92), ripple);
                c.a *= body;
                return c;
            }
            ENDCG
        }
    }
}
