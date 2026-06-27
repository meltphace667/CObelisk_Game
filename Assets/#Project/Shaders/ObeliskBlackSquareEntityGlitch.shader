Shader "Obelisk/UI/Black Square Entity Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _GlitchIntensity ("Glitch Intensity", Range(0,2)) = 0.8
        _Missingno ("Missingno Blocks", Range(0,2)) = 0.8
        _SliceIntensity ("Slice Intensity", Range(0,2)) = 0.5
        _Blockiness ("Blockiness", Range(4,120)) = 40
        _Seed ("Seed", Float) = 1
        _ObeliskTime ("Obelisk Time", Float) = 0
        _Pulse ("Pulse", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Alpha;
            float _GlitchIntensity;
            float _Missingno;
            float _SliceIntensity;
            float _Blockiness;
            float _Seed;
            float _ObeliskTime;
            float _Pulse;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 13.17) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float slice = floor(uv.y * (18.0 + _Missingno * 25.0));
                float sliceRnd = hash(float2(slice, floor(_ObeliskTime * 8.0)));
                float sliceShift = (sliceRnd - 0.5) * 0.045 * _SliceIntensity * (0.35 + _Pulse);
                uv.x += sliceShift * step(0.62, sliceRnd);

                fixed4 sprite = tex2D(_MainTex, uv);
                float alphaMask = sprite.a;

                float2 blockUv = floor(i.uv * _Blockiness) / _Blockiness;
                float n = hash(blockUv + floor(_ObeliskTime * (6.0 + _GlitchIntensity * 8.0)));

                float missing = step(1.0 - saturate(_Missingno * 0.34), n);
                float tears = step(0.94, hash(float2(floor(i.uv.y * 70.0), floor(_ObeliskTime * 18.0))));
                float border = 1.0 - smoothstep(0.0, 0.035, min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y)));

                float3 black = float3(0.0, 0.0, 0.0);
                float3 purpleEdge = float3(0.075, 0.035, 0.12);
                float3 sickGreen = float3(0.025, 0.09, 0.055);

                float3 col = black;
                col = lerp(col, purpleEdge, border * (0.28 + _Pulse * 0.35));
                col = lerp(col, sickGreen, missing * 0.16 * _GlitchIntensity);
                col += tears * 0.035 * _GlitchIntensity;

                float pixelCut = lerp(1.0, step(0.08, n), saturate(_GlitchIntensity * 0.10));
                float finalAlpha = _Alpha * alphaMask * pixelCut;
                finalAlpha *= 0.92 + missing * 0.10 + _Pulse * 0.15;

                return fixed4(col, saturate(finalAlpha)) * i.color;
            }
            ENDCG
        }
    }
}
