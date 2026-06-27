Shader "Obelisk/UI/Fast Cloud Drift Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Intensity ("Intensity", Range(0, 1.5)) = 0.85
        _CloudSpeed ("Cloud Speed", Range(-20, 20)) = 8.0
        _SkySpeed ("Sky Speed", Range(-10, 10)) = 1.75

        _SkyDetection ("Sky Detection", Range(0, 2)) = 1.0
        _CloudDetection ("Cloud Detection", Range(0, 2)) = 1.0
        _TreeProtection ("Tree Protection", Range(0, 2)) = 1.15
        _HorizonProtection ("Horizon Protection", Range(0, 1)) = 0.30

        _TrailStrength ("Timelapse Trail", Range(0, 1)) = 0.75
        _Turbulence ("Turbulence", Range(0, 1)) = 0.45
        _SkyTintStrength ("Sky Tint Strength", Range(0, 1)) = 0.35
        _CloudGlow ("Cloud Glow", Range(0, 2)) = 0.75
        _ShutterPulse ("Shutter Pulse", Range(0, 1)) = 0.28
        _Noise ("Fine Noise", Range(0, 1)) = 0.10

        _TintA ("Tint A", Color) = (0.38, 0.68, 1.0, 1)
        _TintB ("Tint B", Color) = (0.75, 1.0, 0.38, 1)
        _TintC ("Tint C", Color) = (1.0, 0.55, 0.92, 1)

        _Seed ("Seed", Float) = 12.34
        _ObeliskTime ("Obelisk Time", Float) = 0

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
            "Queue"="Transparent+20"
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
            Name "ObeliskFastCloudOverlaySafe"

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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Intensity;
            float _CloudSpeed;
            float _SkySpeed;

            float _SkyDetection;
            float _CloudDetection;
            float _TreeProtection;
            float _HorizonProtection;

            float _TrailStrength;
            float _Turbulence;
            float _SkyTintStrength;
            float _CloudGlow;
            float _ShutterPulse;
            float _Noise;

            fixed4 _TintA;
            fixed4 _TintB;
            fixed4 _TintC;

            float _Seed;
            float _ObeliskTime;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 uv)
            {
                float v = 0.0;
                float a = 0.5;

                v += noise2(uv) * a;
                uv *= 2.03;
                a *= 0.5;

                v += noise2(uv) * a;
                uv *= 2.11;
                a *= 0.5;

                v += noise2(uv) * a;

                return v;
            }

            float lum(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float satApprox(float3 c)
            {
                float mx = max(c.r, max(c.g, c.b));
                float mn = min(c.r, min(c.g, c.b));
                return mx - mn;
            }

            float skyMask(float3 c, float2 uv)
            {
                float l = lum(c);
                float s = satApprox(c);

                float blue = c.b - max(c.r, c.g) * 0.72;
                float blueSky = smoothstep(0.01, 0.22, blue * _SkyDetection) * smoothstep(0.16, 0.50, l);

                float pale = smoothstep(0.45, 0.82, l) * (1.0 - smoothstep(0.14, 0.42, s));
                float top = smoothstep(0.06, 0.85, uv.y);

                float mask = max(blueSky, pale * top * 0.55 * _SkyDetection);

                float greenTree = smoothstep(0.02, 0.20, c.g - max(c.r, c.b) * 0.82) * smoothstep(0.04, 0.45, l);
                float darkTree = 1.0 - smoothstep(0.07, 0.28, l);

                mask *= 1.0 - saturate(greenTree * _TreeProtection);
                mask *= 1.0 - saturate(darkTree * _TreeProtection * 0.90);

                float horizon = smoothstep(0.08 + _HorizonProtection * 0.22, 0.38 + _HorizonProtection * 0.24, uv.y);
                mask *= horizon;

                return saturate(mask);
            }

            float cloudMask(float3 c, float sky, float2 uv)
            {
                float l = lum(c);
                float s = satApprox(c);

                float white = smoothstep(0.45, 0.90, l * _CloudDetection) * (1.0 - smoothstep(0.16, 0.58, s));
                float paleBlue = smoothstep(0.36, 0.80, l) * smoothstep(0.00, 0.24, c.b - c.r * 0.82);

                float mask = max(white, paleBlue * 0.50) * saturate(sky + 0.45);
                mask = pow(saturate(mask), 0.75);

                return saturate(mask);
            }

            float2 movedUv(float2 uv, float t, float layer)
            {
                float band = floor(uv.y * (12.0 + layer * 8.0));
                float bandRnd = hash21(float2(band, _Seed + layer * 17.0));
                float bandSpeed = lerp(0.65, 1.60, bandRnd);

                float turbulence = fbm(float2(uv.x * (2.0 + layer), uv.y * (4.0 + layer)) + float2(_Seed, t * 0.08));
                float shear = (uv.y - 0.5) * (0.18 + _Turbulence * 0.42);
                float speed = _CloudSpeed * 0.085 * bandSpeed;

                float2 outUv = uv;
                outUv.x += t * speed + shear + turbulence * _Turbulence * 0.12;
                outUv.y += sin(t * 0.18 + uv.x * 6.0 + layer * 2.0) * 0.008;

                outUv.x = frac(outUv.x);

                return outUv;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _ObeliskTime;

                fixed4 baseSample = (tex2D(_MainTex, uv) + _TextureSampleAdd) * i.color;
                float3 baseColor = baseSample.rgb;

                float baseSky = skyMask(baseColor, uv);

                float2 uv1 = movedUv(uv, t, 0.0);
                float2 uv2 = movedUv(uv, t - _TrailStrength * 0.55, 1.0);
                float2 uv3 = movedUv(uv, t - _TrailStrength * 1.10, 2.0);
                float2 uv4 = movedUv(uv, t + _TrailStrength * 0.35, 3.0);

                fixed4 s1 = tex2D(_MainTex, uv1) + _TextureSampleAdd;
                fixed4 s2 = tex2D(_MainTex, uv2) + _TextureSampleAdd;
                fixed4 s3 = tex2D(_MainTex, uv3) + _TextureSampleAdd;
                fixed4 s4 = tex2D(_MainTex, uv4) + _TextureSampleAdd;

                float sky1 = skyMask(s1.rgb, uv);
                float sky2 = skyMask(s2.rgb, uv);
                float sky3 = skyMask(s3.rgb, uv);
                float sky4 = skyMask(s4.rgb, uv);

                float c1 = cloudMask(s1.rgb, sky1, uv);
                float c2 = cloudMask(s2.rgb, sky2, uv);
                float c3 = cloudMask(s3.rgb, sky3, uv);
                float c4 = cloudMask(s4.rgb, sky4, uv);

                float trail = saturate(_TrailStrength);
                float totalMask = c1 + c2 * 0.70 * trail + c3 * 0.50 * trail + c4 * 0.30 * trail;
                totalMask = saturate(totalMask) * saturate(baseSky + 0.65);

                float3 movedCloud =
                    s1.rgb * 0.42 +
                    s2.rgb * 0.28 * trail +
                    s3.rgb * 0.20 * trail +
                    s4.rgb * 0.12 * trail;

                float totalWeight = 0.42 + 0.28 * trail + 0.20 * trail + 0.12 * trail;
                movedCloud /= max(0.001, totalWeight);

                float skyFlow = fbm(uv * float2(2.4, 1.6) + float2(t * _SkySpeed * 0.08 + _Seed, t * _SkySpeed * 0.02));
                float3 tint = lerp(_TintA.rgb, _TintB.rgb, smoothstep(0.18, 0.75, skyFlow));
                tint = lerp(tint, _TintC.rgb, smoothstep(0.82, 0.98, skyFlow));

                float3 cloudColor = lerp(movedCloud, tint, _SkyTintStrength * (0.25 + skyFlow * 0.75));
                cloudColor += totalMask * _CloudGlow * float3(0.10, 0.12, 0.10);

                float shutter = sin(uv.x * 12.0 + t * _CloudSpeed * 2.0 + _Seed);
                shutter = smoothstep(0.68, 1.0, shutter) * _ShutterPulse;
                cloudColor += shutter * baseSky * float3(0.06, 0.08, 0.10);

                float grain = hash21(floor(uv * float2(1600.0, 1200.0)) + floor(t * 30.0) + _Seed) - 0.5;
                cloudColor += grain * _Noise * 0.08 * saturate(baseSky + totalMask);

                float3 skyWash = lerp(baseColor, tint, baseSky * _SkyTintStrength * 0.22);
                float3 finalColor = lerp(skyWash, cloudColor, totalMask);

                float alpha = saturate(max(totalMask * _Intensity, baseSky * _Intensity * _SkyTintStrength * 0.10));
                alpha *= baseSample.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(saturate(finalColor), alpha);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
