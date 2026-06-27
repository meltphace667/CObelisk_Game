Shader "Obelisk/UI/Room Unified Look"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OldDigital ("Old Digital Base", Range(0, 2)) = 1
        _Contrast ("Contrast", Range(0, 2)) = 1.12
        _Saturation ("Saturation", Range(0, 2)) = 1.08
        _DarkCrush ("Dark Crush", Range(0, 1)) = 0.25

        _DreamColor ("Dream Color", Color) = (1.0, 0.86, 0.42, 1)
        _DreamStrength ("Dream Wash", Range(0, 1)) = 0.10
        _SkyAbnormal ("Sky Abnormality", Range(0, 1)) = 0.08
        _GreenPoison ("Green Wrongness", Range(0, 1)) = 0.06

        _Grain ("Fine Sensor Grain", Range(0, 1)) = 0.18
        _Vignette ("Vignette", Range(0, 1)) = 0.30
        _Chromatic ("Subtle Chromatic Drift", Range(0, 1)) = 0.02
        _MicroCut ("Micro Cuts", Range(0, 1)) = 0.00
        _Anomaly ("Small Absurd Anomalies", Range(0, 1)) = 0.00
        _Posterize ("Soft Poster Memory", Range(0, 1)) = 0.00

        _Seed ("Seed", Float) = 12.345
        _ObeliskTime ("Obelisk Time", Float) = 0
        _Breath ("Breathing Pulse", Range(0, 1)) = 0.20

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "ObeliskUnifiedLook"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _OldDigital;
            float _Contrast;
            float _Saturation;
            float _DarkCrush;
            fixed4 _DreamColor;
            float _DreamStrength;
            float _SkyAbnormal;
            float _GreenPoison;
            float _Grain;
            float _Vignette;
            float _Chromatic;
            float _MicroCut;
            float _Anomaly;
            float _Posterize;
            float _Seed;
            float _ObeliskTime;
            float _Breath;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float softNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float luma(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _ObeliskTime;
                float seed = _Seed;

                // Micro-coupures horizontales rares, pas un effet VHS.
                float row = floor(uv.y * 720.0);
                float cutRnd = hash21(float2(row, floor(t * 7.0 + seed)));
                float cutMask = step(1.0 - _MicroCut * 0.055, cutRnd) * _MicroCut;
                float cutShift = (hash21(float2(row + 18.13, seed)) - 0.5) * 0.032 * cutMask;
                uv.x += cutShift;

                // Drift chromatique subtil.
                float2 centered = uv - 0.5;
                float2 chromaOffset = centered * (_Chromatic * 0.018);

                fixed4 texR = tex2D(_MainTex, uv + chromaOffset) + _TextureSampleAdd;
                fixed4 texG = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 texB = tex2D(_MainTex, uv - chromaOffset) + _TextureSampleAdd;

                float alpha = texG.a * IN.color.a;
                float3 rgb = float3(texR.r, texG.g, texB.b) * IN.color.rgb;

                // Base vieux numérique : noirs denses, contraste, saturation.
                float dark = _DarkCrush * 0.105;
                rgb = saturate((rgb - dark) / max(0.001, 1.0 - dark));
                rgb = pow(max(rgb, 0.0001), lerp(1.0, 0.86, saturate(_OldDigital * 0.7)));
                rgb = (rgb - 0.5) * _Contrast + 0.5;

                float lum = luma(rgb);
                rgb = lerp(float3(lum, lum, lum), rgb, _Saturation);

                // Ciel anormal : météo impossible, pas bouillie glitch.
                float maxRG = max(rgb.r, rgb.g);
                float skyMask = smoothstep(0.025, 0.22, rgb.b - maxRG * 0.82) * smoothstep(0.24, 0.78, lum);

                float2 skyCell = floor(uv * float2(23.0, 15.0));
                float skyRnd = hash21(skyCell + floor(t * 0.20 + seed));
                float3 skyA = float3(0.20, 0.55, 1.00);
                float3 skyB = float3(0.62, 0.86, 0.40);
                float3 skyC = float3(0.78, 0.60, 0.92);
                float3 skyColor = lerp(skyA, skyB, smoothstep(0.15, 0.70, skyRnd));
                skyColor = lerp(skyColor, skyC, smoothstep(0.82, 0.98, skyRnd));
                rgb = lerp(rgb, skyColor, skyMask * _SkyAbnormal * (0.12 + 0.24 * skyRnd));

                // Végétation légèrement fausse.
                float greenMask = smoothstep(0.02, 0.20, rgb.g - max(rgb.r * 0.92, rgb.b * 0.78)) * smoothstep(0.15, 0.80, lum);
                float greenRnd = softNoise(uv * 8.0 + seed);
                float3 poisonA = float3(0.42, 0.72, 0.20);
                float3 poisonB = float3(0.75, 0.62, 0.18);
                float3 poison = lerp(poisonA, poisonB, greenRnd);
                rgb = lerp(rgb, poison, greenMask * _GreenPoison * 0.22);

                // Voile de couleur subliminal.
                float field = softNoise(uv * float2(3.2, 2.4) + float2(seed, t * 0.04));
                float breath = 0.5 + 0.5 * sin(t * 0.65 + seed);
                float dreamAmount = _DreamStrength * (0.055 + 0.09 * field + 0.03 * breath * _Breath);
                rgb = lerp(rgb, _DreamColor.rgb, dreamAmount);

                // Petites anomalies absurdes : cellules rares, pas de gros effet attendu.
                float2 anomalyGrid = floor(uv * float2(31.0, 19.0));
                float anomalyRnd = hash21(anomalyGrid + floor(t * 0.33 + seed * 2.1));
                float anomalyCell = step(1.0 - _Anomaly * 0.030, anomalyRnd) * _Anomaly;
                float2 cellUV = frac(uv * float2(31.0, 19.0));
                float softRect = smoothstep(0.05, 0.14, cellUV.x) * smoothstep(0.05, 0.14, cellUV.y) * smoothstep(0.95, 0.78, cellUV.x) * smoothstep(0.95, 0.78, cellUV.y);
                float3 anomalyTint = lerp(float3(0.92, 0.86, 0.55), float3(0.55, 0.78, 1.0), hash21(anomalyGrid + seed + 7.0));
                rgb = lerp(rgb, anomalyTint, anomalyCell * softRect * 0.35);

                float invertCell = step(1.0 - _Anomaly * 0.010, hash21(anomalyGrid + seed + 91.0)) * _Anomaly;
                rgb = lerp(rgb, 1.0 - rgb, invertCell * softRect * 0.18);

                // Pixels morts / étoiles numériques très rares.
                float2 px = floor(uv * float2(1600.0, 1200.0));
                float dead = step(0.99980 - _Anomaly * 0.00011, hash21(px + seed * 17.0));
                float3 deadColor = lerp(float3(0,0,0), float3(0.9, 1.0, 0.75), hash21(px + 3.0));
                rgb = lerp(rgb, deadColor, dead * (0.45 + _Anomaly * 0.40));

                // Grain capteur fin, plus fort dans les ombres.
                float noise = hash21(px + floor(t * 23.0) + seed) - 0.5;
                float shadowNoise = lerp(0.55, 1.45, saturate(1.0 - lum));
                rgb += noise * _Grain * shadowNoise * 0.075;

                // Posterisation douce de mémoire web.
                float levels = lerp(255.0, 28.0, saturate(_Posterize));
                float3 poster = floor(saturate(rgb) * levels) / max(1.0, levels);
                rgb = lerp(rgb, poster, _Posterize * 0.55);

                // Vignette identitaire.
                float d = length((IN.uv - 0.5) * float2(1.12, 1.0));
                float vig = 1.0 - smoothstep(0.32, 0.82, d) * _Vignette * 0.58;
                rgb *= vig;

                rgb = saturate(rgb);

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
