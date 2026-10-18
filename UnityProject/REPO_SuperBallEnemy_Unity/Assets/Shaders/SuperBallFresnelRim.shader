Shader "REPO/SuperBallFresnelRim"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (0.584, 0.984, 0.043, 1)
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0.0, 10.0)) = 2.0
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100

        // This pass is an unlit transparent shell: it draws no solid center,
        // writes no depth, and only blends the Fresnel edge into the scene.
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "FresnelRim"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.viewDirWS = UnityWorldSpaceViewDir(worldPos);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 normalWS = normalize(i.normalWS);
                float3 viewDirWS = normalize(i.viewDirWS);

                // Fresnel rim: front-facing pixels produce a dot product near 1,
                // while silhouette pixels produce a dot product near 0.
                float viewFacing = saturate(dot(normalWS, viewDirWS));
                float rim = pow(1.0 - viewFacing, max(_RimPower, 0.0001));

                // Rim controls both brightness and alpha, leaving the sphere center transparent.
                float3 glow = _RimColor.rgb * _RimIntensity * rim;
                float alpha = saturate(rim * _Alpha * _RimColor.a);

                return float4(glow, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
