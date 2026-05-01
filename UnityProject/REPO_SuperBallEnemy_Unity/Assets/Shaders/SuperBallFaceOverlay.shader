Shader "REPO/SuperBallFaceOverlay"
{
    Properties
    {
        _MainTex ("Face Cutout", 2D) = "black" {}
        _TintColor ("Tint Color", Color) = (0.74, 1.0, 0.08, 1.0)
        _CoreTint ("Core Tint", Color) = (0.05, 0.17, 0.07, 1.0)
        _Visibility ("Visibility", Range(0.0, 2.0)) = 0.04
        _EmissionIntensity ("Emission Intensity", Range(0.0, 8.0)) = 0.35
        _Alpha ("Alpha", Range(0.0, 1.0)) = 1.0
        _TextureDetailStrength ("Texture Detail Strength", Range(0.0, 2.0)) = 1.46
        _TintStrength ("Tint Strength", Range(0.0, 1.0)) = 0.58
        _AlphaStrength ("Alpha Strength", Range(0.0, 2.0)) = 1.12
        _Contrast ("Contrast", Range(0.2, 3.0)) = 1.82
        _Brightness ("Brightness", Range(0.0, 2.0)) = 1.14
        _Gamma ("Gamma", Range(0.35, 2.5)) = 0.80
        _EdgeDefinition ("Edge Definition", Range(0.0, 2.0)) = 1.55
        _FeatureThreshold ("Feature Threshold", Range(0.0, 1.0)) = 0.16
        _EyeBoost ("Eye Boost", Range(0.0, 3.0)) = 0.0
        _GrinBoost ("Grin Boost", Range(0.0, 3.0)) = 0.0
        _FaceScale ("Face Scale", Range(0.2, 3.0)) = 1.60
        _FaceWidthScale ("Face Width Scale", Range(0.2, 3.0)) = 1.15
        _FaceHeightScale ("Face Height Scale", Range(0.2, 3.0)) = 1.06
        _FaceVerticalOffset ("Face Vertical Offset", Range(-0.7, 0.7)) = 0.04
        _FaceHorizontalOffset ("Face Horizontal Offset", Range(-0.7, 0.7)) = 0.0
        _FaceSoftness ("Projected Edge Softness", Range(0.001, 0.25)) = 0.065
        _FrontMaskStrength ("Front Hemisphere Mask Strength", Range(0.0, 1.0)) = 1.0
        _HemisphereSoftness ("Hemisphere Mask Softness", Range(0.001, 0.75)) = 0.22
        _AlphaCutoff ("Alpha Cutoff", Range(0.0, 0.4)) = 0.012
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.25)) = 0.045
        _CoreTintStrength ("Core Tint Strength", Range(0.0, 2.0)) = 0.52
        _EmbeddedBlend ("Embedded Blend", Range(0.0, 1.0)) = 0.48
        _DepthBlend ("Depth Blend", Range(0.0, 1.0)) = 0.30
        _Pulse ("Pulse", Range(0.0, 1.0)) = 0.0
        _PulseSpeed ("Pulse Speed", Range(0.0, 20.0)) = 0.8
        _FlickerAmount ("Flicker Amount", Range(0.0, 0.5)) = 0.015
        [HideInInspector] _ProjectionForward ("Projection Forward", Vector) = (0, 0, -1, 0)
        [HideInInspector] _ProjectionRight ("Projection Right", Vector) = (1, 0, 0, 0)
        [HideInInspector] _ProjectionUp ("Projection Up", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+24"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100

        // Alpha-blended sphere overlay. The texture is projected from
        // local-space sphere direction, keeping the expression curved,
        // readable, and embedded inside the dark inner sphere instead of
        // appearing as a flat card.
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FaceOverlayEmission"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            fixed4 _CoreTint;
            half _Visibility;
            half _EmissionIntensity;
            half _Alpha;
            half _TextureDetailStrength;
            half _TintStrength;
            half _AlphaStrength;
            half _Contrast;
            half _Brightness;
            half _Gamma;
            half _EdgeDefinition;
            half _FeatureThreshold;
            half _EyeBoost;
            half _GrinBoost;
            half _FaceScale;
            half _FaceWidthScale;
            half _FaceHeightScale;
            half _FaceVerticalOffset;
            half _FaceHorizontalOffset;
            half _FaceSoftness;
            half _FrontMaskStrength;
            half _HemisphereSoftness;
            half _AlphaCutoff;
            half _EdgeSoftness;
            half _CoreTintStrength;
            half _EmbeddedBlend;
            half _DepthBlend;
            half _Pulse;
            half _PulseSpeed;
            half _FlickerAmount;
            float4 _ProjectionForward;
            float4 _ProjectionRight;
            float4 _ProjectionUp;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 sphereDir = normalize(i.localPos);
                float3 faceCenter = dot(_ProjectionForward.xyz, _ProjectionForward.xyz) > 0.0001 ? normalize(_ProjectionForward.xyz) : float3(0.0, 0.0, -1.0);
                float3 faceRight = dot(_ProjectionRight.xyz, _ProjectionRight.xyz) > 0.0001 ? normalize(_ProjectionRight.xyz) : float3(1.0, 0.0, 0.0);
                float3 faceUp = dot(_ProjectionUp.xyz, _ProjectionUp.xyz) > 0.0001 ? normalize(_ProjectionUp.xyz) : float3(0.0, 1.0, 0.0);

                float2 projected = float2(dot(sphereDir, faceRight), dot(sphereDir, faceUp));
                float2 faceScale = max(float2(_FaceScale * _FaceWidthScale, _FaceScale * _FaceHeightScale), 0.0001h);
                float2 faceUv = (projected - float2(_FaceHorizontalOffset, _FaceVerticalOffset)) / faceScale + 0.5h;
                float2 uvInside = step(float2(0.0, 0.0), faceUv) * step(faceUv, float2(1.0, 1.0));
                half rectMask = uvInside.x * uvInside.y;
                half edgeDistance = min(min(faceUv.x, 1.0h - faceUv.x), min(faceUv.y, 1.0h - faceUv.y));
                half projectedEdgeMask = smoothstep(0.0h, max(_FaceSoftness, 0.0001h), edgeDistance);

                half frontFacing = dot(sphereDir, faceCenter);
                half hemisphereMask = smoothstep(0.0h, max(_HemisphereSoftness, 0.0001h), frontFacing);
                hemisphereMask = lerp(1.0h, hemisphereMask, saturate(_FrontMaskStrength));

                fixed4 face = tex2D(_MainTex, TRANSFORM_TEX(saturate(faceUv), _MainTex));

                half lowerEdge = max(0.0h, _AlphaCutoff - _EdgeSoftness);
                half upperEdge = min(1.0h, _AlphaCutoff + _EdgeSoftness);
                half alphaMask = smoothstep(lowerEdge, upperEdge, face.a);
                half mask = alphaMask * rectMask * projectedEdgeMask * hemisphereMask;
                clip(mask - 0.0005h);

                half luminance = dot(face.rgb, half3(0.299h, 0.587h, 0.114h));
                half detail = saturate(luminance * _Brightness);
                detail = pow(max(detail, 0.0001h), max(_Gamma, 0.0001h));
                detail = saturate((detail - 0.5h) * _Contrast + 0.5h);
                half detailBlend = saturate(_TextureDetailStrength);
                half featureMask = saturate((detail - _FeatureThreshold) * (2.0h + _EdgeDefinition * 6.0h));
                half edgeMask = saturate((detail - (_FeatureThreshold * 0.7h)) * (1.25h + _EdgeDefinition * 4.0h));
                half eyeBand = smoothstep(0.50h, 0.64h, faceUv.y) * (1.0h - smoothstep(0.74h, 0.92h, faceUv.y));
                half grinBand = 1.0h - smoothstep(0.35h, 0.60h, faceUv.y);
                half eyeMask = featureMask * eyeBand;
                half grinMask = edgeMask * grinBand;
                half projectedRadius = saturate(length((faceUv - 0.5h) * 2.0h));
                half embeddedMask = saturate(projectedRadius * 0.8h + (1.0h - saturate(frontFacing)) * 0.35h);

                fixed3 shapedTexture = lerp(detail.xxx, face.rgb, detailBlend);
                fixed3 tintedTexture = lerp(shapedTexture, shapedTexture * _TintColor.rgb, saturate(_TintStrength));
                half aggressionBoost = 1.0h + eyeMask * _EyeBoost + grinMask * _GrinBoost;
                half definitionBoost = 1.0h + edgeMask * (_EdgeDefinition * 0.35h);
                half coreTintBlend = saturate(_CoreTintStrength * (0.2h + embeddedMask * _EmbeddedBlend));
                fixed3 embeddedColor = lerp(tintedTexture, tintedTexture * _CoreTint.rgb, coreTintBlend);
                fixed3 finalTexture = embeddedColor * aggressionBoost * definitionBoost;
                half detailAlpha = lerp(1.0h, saturate(face.a + detail * 0.45h + edgeMask * 0.25h), detailBlend);

                half pulseWave = 0.5h + 0.5h * sin((_Time.y * max(_PulseSpeed, 0.01h) + _Pulse) * 6.28318h);
                half pulse = 1.0h + pulseWave * _FlickerAmount;
                half visible = max(0.0h, _Visibility * _Alpha * _AlphaStrength);
                half embeddedAlpha = lerp(1.0h, saturate(1.0h - embeddedMask * (0.25h + _DepthBlend * 0.55h)), saturate(_EmbeddedBlend));
                half alpha = saturate(mask * _TintColor.a * visible * detailAlpha * embeddedAlpha);
                fixed3 glow = finalTexture * (_EmissionIntensity * pulse) * lerp(0.8h, 1.05h, saturate(1.0h - projectedRadius * 0.75h));

                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
