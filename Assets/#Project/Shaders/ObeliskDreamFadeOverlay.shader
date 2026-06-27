Shader "UI/ObeliskDreamFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FadeAlpha ("Fade Alpha", Range(0,1)) = 1
        _DreamAmount ("Dream Amount", Range(0,2)) = 0.5
        _NoiseScale ("Noise Scale", Range(1,80)) = 20
        _Warp ("Warp", Range(0,1)) = 0.15
        _Vignette ("Vignette", Range(0,1)) = 0.4
        _SoftHalo ("Soft Halo", Range(0,1)) = 0.15
        _VeilTimeScale ("Veil Time Scale", Range(0,3)) = 0.35

        _TintA ("Near Black Tint", Color) = (0.005,0.004,0.009,1)
        _TintB ("Dream Tint", Color) = (0.105,0.080,0.190,1)
        _TintC ("Strange Tint", Color) = (0.030,0.115,0.085,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _FadeAlpha;
            float _DreamAmount;
            float _NoiseScale;
            float _Warp;
            float _Vignette;
            float _SoftHalo;
            float _VeilTimeScale;
            fixed4 _TintA;
            fixed4 _TintB;
            fixed4 _TintC;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(37.2, 19.7);

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += noise(p) * amp;
                    p = p * 2.03 + shift;
                    amp *= 0.5;
                }

                return v;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 centered = uv - 0.5;
                float time = _Time.y * _VeilTimeScale;

                float slowWave = sin(time * 0.73 + centered.x * 4.0 - centered.y * 3.0) * 0.5 + 0.5;
                float2 warpedUv = uv;
                warpedUv.x += sin((uv.y + time * 0.09) * 8.0) * _Warp * 0.018 * _DreamAmount;
                warpedUv.y += cos((uv.x - time * 0.07) * 7.0) * _Warp * 0.014 * _DreamAmount;

                float n1 = fbm(warpedUv * _NoiseScale + float2(time * 0.35, -time * 0.22));
                float n2 = fbm(warpedUv * (_NoiseScale * 0.42) + float2(-time * 0.13, time * 0.29));
                float veil = saturate(n1 * 0.72 + n2 * 0.28);

                float dist = length(centered * float2(1.12, 0.92));
                float vignette = smoothstep(0.18, 0.78, dist);
                float halo = 1.0 - smoothstep(0.02, 0.62, dist);

                float baseAlpha = saturate(_FadeAlpha);
                float midOnly = baseAlpha * (1.0 - baseAlpha) * 4.0;
                float organic = (veil - 0.5) * _DreamAmount * 0.18 * midOnly;
                float edgeLift = vignette * _Vignette * 0.20 * midOnly;
                float pulseLift = slowWave * _DreamAmount * 0.035 * midOnly;
                float outAlpha = saturate(baseAlpha + organic + edgeLift + pulseLift);

                fixed3 colorA = _TintA.rgb;
                fixed3 colorB = lerp(_TintB.rgb, _TintC.rgb, veil * 0.55 + slowWave * 0.25);
                fixed3 rgb = lerp(colorA, colorB, saturate(_DreamAmount * (0.25 + veil * 0.65)));
                rgb += halo * _SoftHalo * _DreamAmount * fixed3(0.08, 0.06, 0.12);
                rgb *= 0.82 + veil * 0.18;

                #ifdef UNITY_UI_CLIP_RECT
                outAlpha *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return fixed4(rgb, outAlpha) * IN.color.a;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
