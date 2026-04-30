Shader "REPO/SuperBallEnergyVein"
{
    Properties
    {
        _Color ("Vein Color", Color) = (0.50, 1.0, 0.05, 1.0)
        _Visibility ("Visibility", Range(0.0, 3.0)) = 0.18
        _EmissionIntensity ("Emission Intensity", Range(0.0, 8.0)) = 0.9
        _Pulse ("Pulse", Range(0.0, 1.0)) = 0.0
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.18
        _ScrollSpeed ("Scroll Speed", Range(-8.0, 8.0)) = 0.35
        _NoiseStrength ("Noise Strength", Range(0.0, 1.0)) = 0.22
        _FlickerAmount ("Flicker Amount", Range(0.0, 1.0)) = 0.18
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
                half flow = i.uv.x * 8.0h + _Time.y * _ScrollSpeed + _Pulse * 6.28318h;
                half slowWave = sin(flow) * 0.5h + 0.5h;
                half fineWave = sin(flow * 2.37h + 1.91h) * 0.5h + 0.5h;
                half shimmer = lerp(1.0h, 0.78h + slowWave * 0.34h + fineWave * 0.18h, saturate(_NoiseStrength));
                half flicker = 1.0h + (slowWave - 0.5h) * _FlickerAmount;
                half visibility = saturate(_Visibility) * shimmer * flicker;
                half alpha = saturate(i.color.a * _Alpha * visibility);
                fixed3 glow = i.color.rgb * _EmissionIntensity * visibility;
                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
