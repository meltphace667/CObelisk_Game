Shader "Obelisk/UI/Raw Pixel Vomit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room Texture", 2D) = "white" {}
        _EntityCenter ("Entity Center", Vector) = (0.5,0.5,0,0)
        _EntitySize ("Entity Size", Vector) = (0.12,0.22,0,0)
        _Seed ("Seed", Float) = 1
        _ObeliskTime ("Time", Float) = 0
        _Strength ("Strength", Range(0,2)) = 0.75
        _Radius ("Radius", Range(0.5,4)) = 1.45
        _MaxAlpha ("Max Alpha", Range(0,1)) = 0.72
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

            sampler2D _MainTex;
            float4 _EntityCenter;
            float4 _EntitySize;
            float _Seed;
            float _ObeliskTime;
            float _Strength;
            float _Radius;
            float _MaxAlpha;

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 31.73) * 43758.5453);
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
                float2 center = _EntityCenter.xy;
                float2 entitySize = max(_EntitySize.xy, float2(0.01, 0.01));
                float2 fieldSize = entitySize * _Radius;

                float2 d = abs(uv - center) / (fieldSize * 0.5);
                float field = 1.0 - smoothstep(0.70, 1.12, max(d.x, d.y));

                float frame = floor(_ObeliskTime * (4.0 + _Strength * 6.0));
                float2 blocks = floor((uv - center) / max(entitySize.y, 0.001) * (28.0 + _Strength * 26.0));
                float rnd = hash(blocks + frame);

                float activeBlock = step(0.74 - _Strength * 0.12, rnd) * field;

                // direction vomiture : pixels arrachés autour de l'entité, pas joli, pas smooth.
                float2 vomitDir = normalize(float2(hash(float2(_Seed, 2.0)) - 0.5, hash(float2(_Seed, 9.0)) - 0.5) + float2(0.45, -0.12));
                float2 jump = vomitDir * (0.020 + 0.055 * rnd) * _Strength * activeBlock;

                // Quelques fragments viennent de plusieurs directions pour casser le côté effet propre.
                float2 rough = float2(hash(blocks * 1.7 + 4.0) - 0.5, hash(blocks * 2.3 + 8.0) - 0.5);
                float2 sourceUv = uv - jump + rough * 0.030 * _Strength * activeBlock;

                fixed4 c = tex2D(_MainTex, sourceUv);

                float blackBits = step(0.92, hash(blocks + frame * 3.1));
                c.rgb = lerp(c.rgb, float3(0.0, 0.0, 0.0), blackBits * 0.30 * _Strength);

                float alpha = activeBlock * _MaxAlpha;
                alpha *= step(0.05, field);

                return fixed4(c.rgb, saturate(alpha)) * i.color;
            }
            ENDCG
        }
    }
}
