Shader "Obelisk/UI/Malign Black Entity No Face"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _Seed ("Seed", Float) = 1
        _ObeliskTime ("Time", Float) = 0
        _Glitch ("Glitch", Range(0,2)) = 1
        _Mutation ("Mutation", Range(0,2)) = 1
        _SpriteTrace ("Sprite Trace", Range(0,1)) = 0.82
        _AbstractHoles ("Abstract Holes", Range(0,1)) = 0.52
        _HatAntennaMadness ("Hat Antenna Madness", Range(0,1)) = 0.78
        _PixelAbsurdity ("Pixel Absurdity", Range(0,3)) = 1
        _PaletteWrongness ("Palette Wrongness", Range(0,1)) = 0.5
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
            float _Seed;
            float _ObeliskTime;
            float _Glitch;
            float _Mutation;
            float _SpriteTrace;
            float _AbstractHoles;
            float _HatAntennaMadness;
            float _PixelAbsurdity;
            float _PaletteWrongness;
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
                return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 9.173) * 43758.5453123);
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
                float frame = floor(_ObeliskTime * (7.0 + _Glitch * 8.0));

                float row = floor(uv.y * (24.0 + _PixelAbsurdity * 28.0));
                float rowRnd = hash(float2(row, frame));

                float2 shiftedUv = uv;
                shiftedUv.x += (rowRnd - 0.5) * 0.078 * _Glitch * step(0.50, rowRnd);

                float2 blockUv = floor(uv * (18.0 + _PixelAbsurdity * 42.0));
                float blockRnd = hash(blockUv + frame);

                float2 tearUv = shiftedUv;
                tearUv.y += (blockRnd - 0.5) * 0.040 * _Mutation * step(0.82, blockRnd);
                tearUv.x += sin(uv.y * 90.0 + _Seed) * 0.005 * _Glitch;

                fixed4 tex = tex2D(_MainTex, tearUv);
                float mask = tex.a;
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));

                float edge = 1.0 - smoothstep(0.0, 0.035, min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)));

                // trous non-faciaux : grille cassée + fissures verticales, pas placés comme des yeux.
                float abstractHoleRnd = hash(floor(float2(uv.x * 11.0, uv.y * 17.0)) + floor(frame * 0.23));
                float fissure = step(0.982 - _AbstractHoles * 0.12, abstractHoleRnd);
                fissure *= step(0.18, uv.y) * step(uv.y, 0.92);

                float missing = step(1.0 - saturate(_Glitch * 0.20 + _PixelAbsurdity * 0.07), blockRnd);
                float antenna = step(0.988 - _HatAntennaMadness * 0.20, hash(float2(floor(uv.x * 31.0), floor(uv.y * 13.0) + frame)));

                float3 black = float3(0.0, 0.0, 0.0);
                float3 darkPurple = float3(0.075, 0.025, 0.14);
                float3 deadGreen = float3(0.02, 0.12, 0.055);
                float3 wrongPink = float3(0.34, 0.04, 0.22);
                float3 ash = float3(0.22, 0.22, 0.22);

                float3 col = lerp(black, ash, lum * _SpriteTrace * 0.58);
                col = lerp(col, darkPurple, edge * (0.42 + _Pulse * 0.35));
                col = lerp(col, deadGreen, missing * 0.26 * _PaletteWrongness);
                col = lerp(col, wrongPink, antenna * 0.52 * _PaletteWrongness);

                float inversion = step(0.985, hash(blockUv + frame * 1.7));
                col = lerp(col, 1.0 - col, inversion * 0.18 * _Glitch);

                float cut = lerp(1.0, step(0.08, blockRnd), saturate(_Mutation * 0.09));
                cut *= 1.0 - fissure * 0.72 * saturate(_AbstractHoles);

                float alpha = mask * _Alpha * cut;
                alpha *= 0.95 + missing * 0.16 + _Pulse * 0.10;
                alpha = saturate(alpha);

                return fixed4(col, alpha) * i.color;
            }
            ENDCG
        }
    }
}
