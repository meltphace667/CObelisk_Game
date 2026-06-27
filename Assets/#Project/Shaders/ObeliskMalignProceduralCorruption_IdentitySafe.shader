Shader "Obelisk/UI/Malign Procedural Corruption Identity Safe"
{
    Properties
    {
        [PerRendererData] _MainTex ("White Texture", 2D) = "white" {}
        _EntityCenter ("Entity Center", Vector) = (0.5,0.5,0,0)
        _EntitySize ("Entity Size", Vector) = (0.2,0.3,0,0)
        _Seed ("Seed", Float) = 1
        _ObeliskTime ("Time", Float) = 0
        _Strength ("Strength", Range(0,2)) = 1
        _WorldInfluence ("World Influence", Range(0,1)) = 0.1
        _PaletteWrongness ("Palette Wrongness", Range(0,1)) = 0.5
        _Inversion ("Inversion", Range(0,1)) = 0.1
        _PixelAbsurdity ("Pixel Absurdity", Range(0,3)) = 1
        _AbsurdityLevel ("Absurdity Level", Range(0,3)) = 1
        _MaxAlpha ("Max Alpha", Range(0,1)) = 0.38
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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

            float4 _EntityCenter, _EntitySize;
            float _Seed, _ObeliskTime, _Strength, _WorldInfluence, _PaletteWrongness, _Inversion, _PixelAbsurdity, _AbsurdityLevel, _MaxAlpha;

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            float hash(float2 p) { return frac(sin(dot(p, float2(269.5, 183.3)) + _Seed * 17.31) * 43758.5453); }

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
                float frame = floor(_ObeliskTime * (5.0 + _AbsurdityLevel * 4.0));

                float2 center = _EntityCenter.xy;
                float2 size = max(_EntitySize.xy, float2(0.02, 0.02));

                float2 boxDist = abs(uv - center) / (size * (1.05 + _Strength * 1.15));
                float localBox = 1.0 - smoothstep(0.65, 1.58, max(boxDist.x, boxDist.y));

                float radial = distance(uv, center);
                float psychic = 1.0 - smoothstep(0.12, 0.52 + _Strength * 0.18, radial);
                float influence = saturate(max(localBox, psychic * 0.58) * _Strength + _WorldInfluence);

                float row = floor(uv.y * (26.0 + _PixelAbsurdity * 34.0));
                float rowRnd = hash(float2(row, frame));
                float2 block = floor(uv * (11.0 + _PixelAbsurdity * 28.0));
                float blockRnd = hash(block + frame);

                float grid = step(0.972 - _PixelAbsurdity * 0.025, blockRnd);
                float tear = step(0.965 - _AbsurdityLevel * 0.025, rowRnd);
                float stray = step(0.988 - _WorldInfluence * 0.08, hash(block * 0.37 + frame * 2.0));

                float3 black = float3(0.0, 0.0, 0.0);
                float3 violet = float3(0.10, 0.035, 0.16);
                float3 green = float3(0.015, 0.11, 0.055);
                float3 magenta = float3(0.35, 0.02, 0.20);

                float3 col = black;
                col = lerp(col, violet, _PaletteWrongness * 0.55);
                col = lerp(col, green, grid * _PaletteWrongness * 0.45);
                col = lerp(col, magenta, tear * _PaletteWrongness * 0.55);

                float alpha = influence * (0.08 + _Strength * 0.20);
                alpha += grid * influence * 0.18;
                alpha += tear * influence * 0.16;
                alpha += stray * _WorldInfluence * 0.22;
                alpha *= 1.0 + _Inversion * 0.45;
                alpha = min(saturate(alpha), _MaxAlpha);

                return fixed4(col, alpha) * i.color;
            }
            ENDCG
        }
    }
}
