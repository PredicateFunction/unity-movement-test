Shader "Lit/CheckerboardWithDistanceBlend"
{
    Properties
    {
        _Density ("Density", Range(2,5000)) = 30
        _DarkColor ("Dark Color", Color) = (0.3, 0.3, 0.3, 1)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _BlendStartDistance ("Blend Start Distance", Float) = 10
        _BlendEndDistance ("Blend End Distance", Float) = 30
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            float _Density;
            float4 _DarkColor;
            float4 _LightColor;
            float _BlendStartDistance;
            float _BlendEndDistance;


            v2f vert (float4 pos : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(pos);
                o.uv = uv * _Density;
                o.worldPos = mul(unity_ObjectToWorld, pos).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv;
                c = floor(c) / 2;
                float checker = frac(c.x + c.y);

                fixed4 checkerColor = lerp(_DarkColor, _LightColor, checker);

                float dist = distance(i.worldPos, _WorldSpaceCameraPos);

                float blend = saturate((dist - _BlendStartDistance) / (_BlendEndDistance - _BlendStartDistance));

                fixed4 blendedColor = lerp(_DarkColor, _LightColor, 0.5);

                fixed4 finalColor = lerp(checkerColor, blendedColor, blend);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
