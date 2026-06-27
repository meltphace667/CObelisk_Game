Shader "Obelisk/UI/Black Square Corruption Halo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room Texture", 2D) = "white" {}
        _EntityCenter ("Entity Center", Vector) = (0.5,0.5,0,0)
        _EntitySize ("Entity Size", Vector) = (0.2,0.15,0,0)
        _CorruptionStrength ("Corruption Strength", Range(0,2)) = 0.8
        _RadiusMultiplier ("Radius Multiplier", Range(0.4,4)) = 1.8
        _HaloAlpha ("Halo Alpha", Range(0,1)) = 0.8
        _Blockiness ("Blockiness", Range(4,120)) = 40
        _Seed ("Seed", Float) = 1
        _ObeliskTime ("Obelisk Time", Float) = 0
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
            float4 _EntityCenter;
            float4 _EntitySize;
            float _CorruptionStrength;
            float _RadiusMultiplier;
            float _HaloAlpha;
            float _Blockiness;
            float _Seed;
            float _ObeliskTime;

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
                return frac(sin(dot(p, float2(269.5, 183.3)) + _Seed * 41.31) * 43758.5453);
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
                float2 size = max(_EntitySize.xy, float2(0.01, 0.01)) * _RadiusMultiplier;

                float2 d = abs(uv - center) / (size * 0.5);
                float rectDistance = max(d.x, d.y);
                float halo = 1.0 - smoothstep(0.82, 1.55, rectDistance);

                float2 blocks = floor(uv * _Blockiness);
                float blockRnd = hash(blocks + floor(_ObeliskTime * 7.0));
                float sliceRnd = hash(float2(floor(uv.y * 48.0), floor(_ObeliskTime * 11.0)));

                float2 offset = 0;
                offset.x += (sliceRnd - 0.5) * 0.035 * _CorruptionStrength * step(0.58, sliceRnd);
                offset.y += (blockRnd - 0.5) * 0.012 * _CorruptionStrength * step(0.82, blockRnd);

                float2 uvR = uv + offset + float2(0.006, 0.0) * _CorruptionStrength * step(0.72, blockRnd);
                float2 uvG = uv + offset;
                float2 uvB = uv + offset - float2(0.007, 0.0) * _CorruptionStrength * step(0.66, blockRnd);

                fixed4 cR = tex2D(_MainTex, uvR);
                fixed4 cG = tex2D(_MainTex, uvG);
                fixed4 cB = tex2D(_MainTex, uvB);

                float3 col = float3(cR.r, cG.g, cB.b);

                float quant = 7.0 + floor(blockRnd * 5.0);
                col = floor(col * quant) / quant;

                float tear = step(0.965, sliceRnd);
                col = lerp(col, 1.0 - col, tear * 0.22 * _CorruptionStrength);
                col *= 0.82 + blockRnd * 0.18;

                float alpha = halo * _HaloAlpha * saturate(0.45 + _CorruptionStrength * 0.55);
                alpha *= step(0.04, halo);

                return fixed4(col, saturate(alpha)) * i.color;
            }
            ENDCG
        }
    }
}
