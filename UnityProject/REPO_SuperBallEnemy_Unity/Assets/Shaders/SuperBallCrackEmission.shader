Shader "REPO/SuperBallCrackEmission"
{
    Properties
    {
        _Color ("Crack Color", Color) = (0.78, 1.0, 0.08, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 12.0)) = 3.4
        _Visibility ("Visibility", Range(0.0, 3.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+10"
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
            Name "CrackEmission"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            fixed4 _Color;
            half _EmissionIntensity;
            half _Visibility;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half alpha = saturate(i.color.a * _Visibility);
                fixed3 glow = i.color.rgb * _EmissionIntensity * _Visibility;
                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
