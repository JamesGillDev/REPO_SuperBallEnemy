Shader "REPO/SuperBallEnergyVein"
{
    Properties
    {
        _Color ("Vein Color", Color) = (0.48, 1.0, 0.06, 1.0)
        _Visibility ("Visibility", Range(0.0, 3.0)) = 0.08
        _EmissionIntensity ("Emission Intensity", Range(0.0, 8.0)) = 0.92
        _Pulse ("Pulse", Range(0.0, 1.0)) = 0.0
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.10
        _ScrollSpeed ("Pulse Speed", Range(-8.0, 8.0)) = 0.18
        _NoiseStrength ("Shimmer Strength", Range(0.0, 1.0)) = 0.03
        _FlickerAmount ("Flicker Amount", Range(0.0, 1.0)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+12"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "EnergyVeinEmission"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Visibility;
            half _EmissionIntensity;
            half _Pulse;
            half _Alpha;
            half _ScrollSpeed;
            half _NoiseStrength;
            half _FlickerAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half pulse = sin(_Time.y * _ScrollSpeed + _Pulse * 6.28318h) * 0.5h + 0.5h;
                half shimmer = lerp(1.0h, 0.94h + pulse * 0.12h, saturate(_NoiseStrength));
                half flicker = 1.0h + (pulse - 0.5h) * _FlickerAmount;
                half visibility = max(0.0h, _Visibility) * shimmer * flicker;
                half alpha = saturate(i.color.a * _Alpha * visibility);
                fixed3 glow = i.color.rgb * _EmissionIntensity * visibility;
                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
