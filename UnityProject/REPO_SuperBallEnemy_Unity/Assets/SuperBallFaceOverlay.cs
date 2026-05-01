using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class SuperBallFaceOverlay : MonoBehaviour
{
    private const float LockedFaceScale = 1.40f;

    private const string FaceObjectName = "SuperBall_Face";
    private const string MainTexName = "_MainTex";
    private const int FaceSortingOrder = 24;
    private const float DefaultFaceObjectScale = 1.006f;
    private static readonly Vector2 DefaultFaceTextureScale = new Vector2(0.94f, 0.80f);
    private static readonly Vector2 DefaultFaceTextureOffset = new Vector2(0.03f, 0.10f);

#if UNITY_EDITOR
    private const string DarkCoreObjectName = "SuperBall_InnerCoreDark";
    private const string InnerCoreObjectName = "SuperBall_InnerCore";
    private const string FaceShaderName = "REPO/SuperBallFaceOverlay";
    private const string FaceShaderPath = "Assets/Shaders/SuperBallFaceOverlay.shader";
    private const string FaceMaterialPath = "Assets/M_SuperBall_Face.mat";
    private const string FaceTexturePath = "Assets/Textures/SuperBall/SuperBall_Face_Cutout.png";
#endif

    [Header("Visibility")]
    [Tooltip("Base face visibility in the idle state.")]
    [Range(0f, 1f)] public float IdleVisibility = 0.04f;
    [Tooltip("Target visibility reached as the charge state ramps toward 100%.")]
    [Range(0f, 1.5f)] public float ChargeVisibility = 1.22f;
    [Tooltip("Extra visibility spike applied when Flash(intensity) is triggered.")]
    [Range(1f, 3f)] public float FlashVisibilityMultiplier = 1.75f;
    [Tooltip("How quickly SetIdle and SetCharge blend between their target states.")]
    [Min(0.01f)] public float StateTransitionSpeed = 4.8f;

    [Space(4)]
    [Header("Emission")]
    [Tooltip("Base face emission while the Super Ball is idle.")]
    [Min(0f)] public float IdleEmission = 0.35f;
    [Tooltip("Emission reached at full charge.")]
    [Min(0f)] public float ChargeEmission = 3.45f;
    [Tooltip("Maximum emission response allowed for Flash(intensity).")]
    [Min(0f)] public float FlashEmission = 5.25f;
    [Tooltip("How quickly the flash spike settles back toward charge or idle.")]
    [Min(0.01f)] public float FlashDecaySpeed = 6.0f;
    [Tooltip("Supernatural tint applied to the embedded face.")]
    public Color FaceColor = new Color(0.78f, 1.0f, 0.10f, 1f);

    [Space(4)]
    [Header("Texture Detail")]
    [Tooltip("Preserves the source artwork detail so the eyes and grin do not collapse into a flat blob.")]
    [Range(0f, 2f)] public float TextureDetailStrength = 1.46f;
    [Tooltip("How strongly the tint color influences the source texture.")]
    [Range(0f, 1f)] public float TintStrength = 0.58f;
    [Tooltip("Overall alpha response of the projected face.")]
    [Range(0f, 2f)] public float AlphaStrength = 1.12f;
    [Tooltip("Contrast shaping applied after texture luminance is sampled.")]
    [Range(0.2f, 3f)] public float Contrast = 1.82f;
    [Tooltip("Brightness applied before contrast shaping.")]
    [Range(0f, 2f)] public float Brightness = 1.14f;
    [Tooltip("Gamma correction applied before contrast.")]
    [Range(0.35f, 2.5f)] public float Gamma = 0.80f;
    [Tooltip("Extra sharpening weight applied to bright texture features.")]
    [Range(0f, 2f)] public float EdgeDefinition = 1.55f;
    [Tooltip("Luminance threshold used to isolate important facial features.")]
    [Range(0f, 1f)] public float FeatureThreshold = 0.16f;

    [Space(4)]
    [Header("Placement / Projection")]
    [Tooltip("Scales the projected face footprint across the inner sphere.")]
    [Range(0.2f, LockedFaceScale)] public float FaceScale = LockedFaceScale;
    [Tooltip("Additional horizontal footprint scale. Keeps the grin wide without forcing the whole texture taller.")]
    [Range(0.2f, 3f)] public float FaceWidthScale = 1.15f;
    [Tooltip("Additional vertical footprint scale. Compensates for transparent texture padding while preserving the curved projection.")]
    [Range(0.2f, 3f)] public float FaceHeightScale = 1.06f;
    [Tooltip("Local pitch/yaw/roll offset applied to the default front-facing projection direction.")]
    public Vector3 ProjectionEuler = Vector3.zero;
    [Tooltip("Moves the projected face up or down without turning it into a billboard.")]
    [Range(-0.7f, 0.7f)] public float FaceVerticalOffset = 0.04f;
    [Tooltip("Moves the projected face left or right on the sphere.")]
    [Range(-0.7f, 0.7f)] public float FaceHorizontalOffset = 0f;
    [Tooltip("Softens the outer edge of the spherical projection footprint.")]
    [Range(0.001f, 0.25f)] public float FaceSoftness = 0.065f;
    [Tooltip("Keeps the face concentrated on the readable/front hemisphere.")]
    [Range(0f, 1f)] public float FrontMaskStrength = 1f;
    [Tooltip("Soft fade from the readable front side into the hidden hemisphere.")]
    [Range(0.001f, 0.75f)] public float HemisphereSoftness = 0.22f;

    [Space(4)]
    [Header("Embedded Depth")]
    [Tooltip("Minimum alpha retained before the texture edge softening takes over.")]
    [Range(0f, 0.4f)] public float AlphaCutoff = 0.012f;
    [Tooltip("Softens alpha edges so the expression blends into the dark core.")]
    [Range(0.001f, 0.25f)] public float TextureEdgeSoftness = 0.045f;
    [Tooltip("Dark core tint used to sink the face into the orb instead of reading like a sticker.")]
    public Color CoreTint = new Color(0.05f, 0.17f, 0.07f, 1f);
    [Tooltip("How strongly the dark core tint influences the face color.")]
    [Range(0f, 2f)] public float CoreTintStrength = 0.52f;
    [Tooltip("How much the face is pushed back into the inner sphere instead of floating on top.")]
    [Range(0f, 1f)] public float EmbeddedBlend = 0.48f;
    [Tooltip("Additional depth fade applied across the face footprint and hidden hemisphere.")]
    [Range(0f, 1f)] public float DepthBlend = 0.30f;

    [Space(4)]
    [Header("Animation Response")]
    [Tooltip("Subtle idle pulse amount. Keeps the face feeling alive at rest.")]
    [Range(0f, 0.5f)] public float IdleFlicker = 0.015f;
    [Tooltip("Pulse amount used while charging or under flash pressure.")]
    [Range(0f, 0.5f)] public float ChargeFlicker = 0.20f;
    [Tooltip("Idle pulse speed in cycles per second.")]
    [Min(0f)] public float IdlePulseSpeed = 0.8f;
    [Tooltip("Charge pulse speed in cycles per second.")]
    [Min(0f)] public float ChargePulseSpeed = 2.4f;
    [Tooltip("Extra eye emphasis unlocked during aggressive moments.")]
    [Range(0f, 3f)] public float EyeAggressionBoost = 1.60f;
    [Tooltip("Extra grin emphasis unlocked during aggressive moments.")]
    [Range(0f, 3f)] public float GrinAggressionBoost = 1.10f;
    [Tooltip("Additional sharpening applied during charge and flash.")]
    [Range(0f, 1.5f)] public float AggressiveSharpnessBoost = 0.55f;
    [Tooltip("Charge progress where the eyes begin to emerge.")]
    [Range(0f, 1f)] public float EyeRevealStart = 0.08f;
    [Tooltip("Charge progress where the eyes are fully emphasized.")]
    [Range(0f, 1f)] public float EyeRevealEnd = 0.42f;
    [Tooltip("Charge progress where the grin begins to emerge after the eyes.")]
    [Range(0f, 1f)] public float GrinRevealStart = 0.38f;
    [Tooltip("Charge progress where the grin reaches full threat emphasis.")]
    [Range(0f, 1f)] public float GrinRevealEnd = 0.86f;

    [Space(4)]
    [Header("Debug / Regeneration")]
    [Tooltip("Draw editor-only front/left/right/high/low preview anchors around the face for readability checks.")]
    public bool DrawEditorPreviewAnchors = false;
    [Tooltip("Distance of the editor-only preview anchors from the face object.")]
    [Min(0.25f)] public float PreviewDistance = 2.6f;
    [Tooltip("Yaw offset used for the left and right preview anchors.")]
    [Range(1f, 45f)] public float PreviewYawAngle = 18f;
    [Tooltip("Pitch offset used for the above and below preview anchors.")]
    [Range(1f, 35f)] public float PreviewPitchAngle = 12f;
    [Tooltip("Editor-only color used for preview anchor markers.")]
    public Color PreviewAnchorColor = new Color(0.8f, 1f, 0.25f, 0.9f);
    [Min(0f)] public float PreviewSphereRadius = 1f;
    [TextArea(2, 4)] public string GroundContactNote =
        "SampleScene keeps the Super Ball visual root centered at y=0; runtime spawning should place the root center at groundY + sphereRadius.";

    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int TextureDetailStrengthId = Shader.PropertyToID("_TextureDetailStrength");
    private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
    private static readonly int AlphaStrengthId = Shader.PropertyToID("_AlphaStrength");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int GammaId = Shader.PropertyToID("_Gamma");
    private static readonly int EdgeDefinitionId = Shader.PropertyToID("_EdgeDefinition");
    private static readonly int FeatureThresholdId = Shader.PropertyToID("_FeatureThreshold");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int FlickerAmountId = Shader.PropertyToID("_FlickerAmount");
    private static readonly int EyeBoostId = Shader.PropertyToID("_EyeBoost");
    private static readonly int GrinBoostId = Shader.PropertyToID("_GrinBoost");
    private static readonly int FaceScaleId = Shader.PropertyToID("_FaceScale");
    private static readonly int FaceWidthScaleId = Shader.PropertyToID("_FaceWidthScale");
    private static readonly int FaceHeightScaleId = Shader.PropertyToID("_FaceHeightScale");
    private static readonly int FaceVerticalOffsetId = Shader.PropertyToID("_FaceVerticalOffset");
    private static readonly int FaceHorizontalOffsetId = Shader.PropertyToID("_FaceHorizontalOffset");
    private static readonly int FaceSoftnessId = Shader.PropertyToID("_FaceSoftness");
    private static readonly int FrontMaskStrengthId = Shader.PropertyToID("_FrontMaskStrength");
    private static readonly int HemisphereSoftnessId = Shader.PropertyToID("_HemisphereSoftness");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int CoreTintId = Shader.PropertyToID("_CoreTint");
    private static readonly int CoreTintStrengthId = Shader.PropertyToID("_CoreTintStrength");
    private static readonly int EmbeddedBlendId = Shader.PropertyToID("_EmbeddedBlend");
    private static readonly int DepthBlendId = Shader.PropertyToID("_DepthBlend");
    private static readonly int ProjectionForwardId = Shader.PropertyToID("_ProjectionForward");
    private static readonly int ProjectionRightId = Shader.PropertyToID("_ProjectionRight");
    private static readonly int ProjectionUpId = Shader.PropertyToID("_ProjectionUp");

    private static Mesh runtimeSphereMesh;

    private MeshRenderer faceRenderer;
    private MeshFilter faceFilter;
    private MaterialPropertyBlock block;
    private float targetChargeProgress;
    private float currentChargeProgress;
    private float flashAmount;

    public void SetIdle()
    {
        targetChargeProgress = 0f;
        flashAmount = 0f;
        if (!Application.isPlaying)
        {
            currentChargeProgress = 0f;
        }

        ApplyState();
    }

    public void SetRecovery()
    {
        SetIdle();
    }

    public void SetCharge(float chargeProgress)
    {
        targetChargeProgress = Mathf.Clamp01(chargeProgress);
        if (!Application.isPlaying)
        {
            currentChargeProgress = targetChargeProgress;
        }

        ApplyState();
    }

    public void Flash(float intensity)
    {
        flashAmount = Mathf.Max(flashAmount, Mathf.Clamp(intensity, 0f, 2f));
        ApplyState();
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Visual State/Idle")]
    public void PreviewVisualIdle()
    {
        ApplyLinkedPreviewState(0f, 0f, false);
    }

    [ContextMenu("Preview Visual State/Charge 25%")]
    public void PreviewVisualCharge25()
    {
        ApplyLinkedPreviewState(0.25f, 0f, false);
    }

    [ContextMenu("Preview Visual State/Charge 50%")]
    public void PreviewVisualCharge50()
    {
        ApplyLinkedPreviewState(0.50f, 0f, false);
    }

    [ContextMenu("Preview Visual State/Charge 75%")]
    public void PreviewVisualCharge75()
    {
        ApplyLinkedPreviewState(0.75f, 0f, false);
    }

    [ContextMenu("Preview Visual State/Full Charge")]
    public void PreviewVisualFullCharge()
    {
        ApplyLinkedPreviewState(1f, 0f, false);
    }

    [ContextMenu("Preview Visual State/Flash")]
    public void PreviewVisualFlash()
    {
        ApplyLinkedPreviewState(1f, 1f, false);
    }

    [ContextMenu("Preview Visual State/Recovery")]
    public void PreviewVisualRecovery()
    {
        ApplyLinkedPreviewState(0f, 0f, true);
    }
#endif

    [ContextMenu("Regenerate/Refresh Face Overlay")]
    public void RegenerateFaceOverlay()
    {
        CacheComponents();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            RegenerateFaceOverlayEditor();
            return;
        }
#endif

        EnsureSphereMesh();
        ConfigureRenderer();
        ApplyState();
        Debug.Log(
            "SuperBallFaceOverlay regenerated at runtime: hierarchy=" + GetHierarchyPath(transform) +
            ", mesh=" + (faceFilter != null && faceFilter.sharedMesh != null ? faceFilter.sharedMesh.name : "<missing>") +
            ", material=" + (faceRenderer != null && faceRenderer.sharedMaterial != null ? faceRenderer.sharedMaterial.name : "<missing>"));
    }

    private void OnEnable()
    {
        CacheComponents();
        EnsureSphereMesh();
        currentChargeProgress = targetChargeProgress;
        ApplyState();
    }

    private void OnValidate()
    {
        CacheComponents();
        EnsureSphereMesh();
        FaceScale = Mathf.Min(FaceScale, LockedFaceScale);
        targetChargeProgress = Mathf.Clamp01(targetChargeProgress);
        if (!Application.isPlaying)
        {
            currentChargeProgress = targetChargeProgress;
        }

        ApplyState();
    }

    private void Update()
    {
        float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 30f;
        currentChargeProgress = Mathf.MoveTowards(currentChargeProgress, targetChargeProgress, deltaTime * Mathf.Max(StateTransitionSpeed, 0.01f));
        flashAmount = Mathf.MoveTowards(flashAmount, 0f, deltaTime * FlashDecaySpeed);
        ApplyState();
    }

    private Quaternion GetProjectionRotation()
    {
        return Quaternion.Euler(ProjectionEuler);
    }

    private void ApplyProjectionVectors(MaterialPropertyBlock propertyBlock)
    {
        Quaternion projectionRotation = GetProjectionRotation();
        Vector3 projectionForward = projectionRotation * Vector3.back;
        Vector3 projectionRight = projectionRotation * Vector3.right;
        Vector3 projectionUp = projectionRotation * Vector3.up;

        propertyBlock.SetVector(ProjectionForwardId, new Vector4(projectionForward.x, projectionForward.y, projectionForward.z, 0f));
        propertyBlock.SetVector(ProjectionRightId, new Vector4(projectionRight.x, projectionRight.y, projectionRight.z, 0f));
        propertyBlock.SetVector(ProjectionUpId, new Vector4(projectionUp.x, projectionUp.y, projectionUp.z, 0f));
    }

    private void ApplyProjectionVectors(Material material)
    {
        Quaternion projectionRotation = GetProjectionRotation();
        Vector3 projectionForward = projectionRotation * Vector3.back;
        Vector3 projectionRight = projectionRotation * Vector3.right;
        Vector3 projectionUp = projectionRotation * Vector3.up;

        material.SetVector(ProjectionForwardId, new Vector4(projectionForward.x, projectionForward.y, projectionForward.z, 0f));
        material.SetVector(ProjectionRightId, new Vector4(projectionRight.x, projectionRight.y, projectionRight.z, 0f));
        material.SetVector(ProjectionUpId, new Vector4(projectionUp.x, projectionUp.y, projectionUp.z, 0f));
    }

    private void CacheComponents()
    {
        if (faceRenderer == null)
        {
            faceRenderer = GetComponent<MeshRenderer>();
        }

        if (faceFilter == null)
        {
            faceFilter = GetComponent<MeshFilter>();
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
    }

    private void EnsureSphereMesh()
    {
        if (faceFilter == null || faceFilter.sharedMesh != null)
        {
            return;
        }

        if (runtimeSphereMesh == null)
        {
            runtimeSphereMesh = CreateRuntimeSphereMesh();
        }

        faceFilter.sharedMesh = runtimeSphereMesh;
    }

    private void ConfigureRenderer()
    {
        if (faceRenderer == null)
        {
            return;
        }

        faceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        faceRenderer.receiveShadows = false;
        faceRenderer.lightProbeUsage = LightProbeUsage.Off;
        faceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        faceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        faceRenderer.sortingOrder = FaceSortingOrder;
    }

    private static Mesh CreateRuntimeSphereMesh()
    {
        const int latitudeSegments = 16;
        const int longitudeSegments = 32;
        List<Vector3> vertices = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
        List<Vector3> normals = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
        List<Vector2> uvs = new List<Vector2>((latitudeSegments + 1) * (longitudeSegments + 1));
        List<int> triangles = new List<int>(latitudeSegments * longitudeSegments * 6);

        for (int y = 0; y <= latitudeSegments; y++)
        {
            float v = y / (float)latitudeSegments;
            float theta = v * Mathf.PI;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            for (int x = 0; x <= longitudeSegments; x++)
            {
                float u = x / (float)longitudeSegments;
                float phi = u * Mathf.PI * 2f;
                Vector3 normal = new Vector3(
                    Mathf.Cos(phi) * sinTheta,
                    cosTheta,
                    Mathf.Sin(phi) * sinTheta);

                vertices.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(u, v));
            }
        }

        int row = longitudeSegments + 1;
        for (int y = 0; y < latitudeSegments; y++)
        {
            for (int x = 0; x < longitudeSegments; x++)
            {
                int a = y * row + x;
                int b = a + row;
                int c = b + 1;
                int d = a + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(d);
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "SuperBall_Face_Sphere_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ApplyState()
    {
        CacheComponents();

        if (faceRenderer == null)
        {
            return;
        }

        float t = Mathf.Clamp01(Application.isPlaying ? currentChargeProgress : targetChargeProgress);
        float faceReveal = SmoothRange(0.16f, 0.92f, t);
        float eyeReveal = SmoothRange(EyeRevealStart, EyeRevealEnd, t);
        float grinReveal = SmoothRange(GrinRevealStart, GrinRevealEnd, t);
        float pressureReveal = Mathf.Max(faceReveal, eyeReveal * 0.62f);
        float visibility = Mathf.Lerp(IdleVisibility, ChargeVisibility, faceReveal);
        float emission = Mathf.Lerp(IdleEmission, ChargeEmission, pressureReveal);
        float pulseAmount = Mathf.Lerp(IdleFlicker, ChargeFlicker, pressureReveal);
        float pulseSpeed = Mathf.Lerp(IdlePulseSpeed, ChargePulseSpeed, pressureReveal);
        float aggressiveMoment = Mathf.Clamp01(Mathf.Max(Mathf.Max(eyeReveal, grinReveal), flashAmount));
        float eyeBoost = eyeReveal * EyeAggressionBoost;
        float grinBoost = grinReveal * GrinAggressionBoost;
        float edgeDefinition = EdgeDefinition + Mathf.Max(grinReveal, flashAmount) * AggressiveSharpnessBoost;

        if (flashAmount > 0f)
        {
            float flashT = Mathf.Clamp01(flashAmount);
            float flashScale = Mathf.Lerp(1f, FlashVisibilityMultiplier, flashT);
            visibility = Mathf.Max(visibility, ChargeVisibility * flashScale);
            emission = Mathf.Max(emission, Mathf.Lerp(ChargeEmission, FlashEmission, flashT));
            pulseAmount = Mathf.Max(pulseAmount, ChargeFlicker * Mathf.Lerp(1f, 1.25f, flashT));
            pulseSpeed = Mathf.Max(pulseSpeed, ChargePulseSpeed * Mathf.Lerp(1f, 1.2f, flashT));
            eyeBoost = Mathf.Max(eyeBoost, EyeAggressionBoost * flashT);
            grinBoost = Mathf.Max(grinBoost, GrinAggressionBoost * flashT);
        }

        float pulse = Application.isPlaying ? Mathf.Repeat(Time.time * Mathf.Max(pulseSpeed, 0.01f), 1f) : 0.35f;

        faceRenderer.GetPropertyBlock(block);
        block.SetColor(TintColorId, FaceColor);
        block.SetColor(CoreTintId, CoreTint);
        block.SetFloat(VisibilityId, Mathf.Max(0f, visibility));
        block.SetFloat(EmissionIntensityId, Mathf.Max(0f, emission));
        block.SetFloat(TextureDetailStrengthId, Mathf.Max(0f, TextureDetailStrength));
        block.SetFloat(TintStrengthId, Mathf.Clamp01(TintStrength));
        block.SetFloat(AlphaStrengthId, Mathf.Max(0f, AlphaStrength));
        block.SetFloat(ContrastId, Mathf.Max(0.01f, Contrast));
        block.SetFloat(BrightnessId, Mathf.Max(0f, Brightness));
        block.SetFloat(GammaId, Mathf.Max(0.01f, Gamma));
        block.SetFloat(EdgeDefinitionId, Mathf.Max(0f, edgeDefinition));
        block.SetFloat(FeatureThresholdId, Mathf.Clamp01(FeatureThreshold));
        block.SetFloat(PulseId, pulse);
        block.SetFloat(PulseSpeedId, Mathf.Max(0f, pulseSpeed));
        block.SetFloat(FlickerAmountId, Mathf.Clamp01(pulseAmount));
        block.SetFloat(EyeBoostId, Mathf.Max(0f, eyeBoost));
        block.SetFloat(GrinBoostId, Mathf.Max(0f, grinBoost));
        block.SetFloat(FaceScaleId, Mathf.Min(FaceScale, LockedFaceScale));
        block.SetFloat(FaceWidthScaleId, FaceWidthScale);
        block.SetFloat(FaceHeightScaleId, FaceHeightScale);
        block.SetFloat(FaceVerticalOffsetId, FaceVerticalOffset);
        block.SetFloat(FaceHorizontalOffsetId, FaceHorizontalOffset);
        block.SetFloat(FaceSoftnessId, FaceSoftness);
        block.SetFloat(FrontMaskStrengthId, FrontMaskStrength);
        block.SetFloat(HemisphereSoftnessId, HemisphereSoftness);
        block.SetFloat(AlphaCutoffId, AlphaCutoff);
        block.SetFloat(EdgeSoftnessId, TextureEdgeSoftness);
        block.SetFloat(CoreTintStrengthId, Mathf.Max(0f, CoreTintStrength));
        block.SetFloat(EmbeddedBlendId, Mathf.Clamp01(EmbeddedBlend));
        block.SetFloat(DepthBlendId, Mathf.Clamp01(DepthBlend));
        ApplyProjectionVectors(block);
        faceRenderer.SetPropertyBlock(block);
    }

    private static float SmoothRange(float start, float end, float value)
    {
        if (Mathf.Approximately(start, end))
        {
            return value >= end ? 1f : 0f;
        }

        float min = Mathf.Min(start, end);
        float max = Mathf.Max(start, end);
        float t = Mathf.InverseLerp(min, max, Mathf.Clamp01(value));
        return t * t * (3f - 2f * t);
    }

    private void SyncMaterialFromInspector(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.SetColor(TintColorId, FaceColor);
        material.SetTextureScale(MainTexName, DefaultFaceTextureScale);
        material.SetTextureOffset(MainTexName, DefaultFaceTextureOffset);
        material.SetFloat(VisibilityId, Mathf.Max(0f, IdleVisibility));
        material.SetFloat(EmissionIntensityId, Mathf.Max(0f, IdleEmission));
        material.SetFloat(AlphaId, 1f);
        material.SetFloat(TextureDetailStrengthId, Mathf.Max(0f, TextureDetailStrength));
        material.SetFloat(TintStrengthId, Mathf.Clamp01(TintStrength));
        material.SetFloat(AlphaStrengthId, Mathf.Max(0f, AlphaStrength));
        material.SetFloat(ContrastId, Mathf.Max(0.01f, Contrast));
        material.SetFloat(BrightnessId, Mathf.Max(0f, Brightness));
        material.SetFloat(GammaId, Mathf.Max(0.01f, Gamma));
        material.SetFloat(EdgeDefinitionId, Mathf.Max(0f, EdgeDefinition));
        material.SetFloat(FeatureThresholdId, Mathf.Clamp01(FeatureThreshold));
        material.SetFloat(FaceScaleId, Mathf.Min(FaceScale, LockedFaceScale));
        material.SetFloat(FaceWidthScaleId, FaceWidthScale);
        material.SetFloat(FaceHeightScaleId, FaceHeightScale);
        material.SetFloat(FaceVerticalOffsetId, FaceVerticalOffset);
        material.SetFloat(FaceHorizontalOffsetId, FaceHorizontalOffset);
        material.SetFloat(FaceSoftnessId, FaceSoftness);
        material.SetFloat(FrontMaskStrengthId, FrontMaskStrength);
        material.SetFloat(HemisphereSoftnessId, HemisphereSoftness);
        material.SetFloat(AlphaCutoffId, AlphaCutoff);
        material.SetFloat(EdgeSoftnessId, TextureEdgeSoftness);
        material.SetColor(CoreTintId, CoreTint);
        material.SetFloat(CoreTintStrengthId, Mathf.Max(0f, CoreTintStrength));
        material.SetFloat(EmbeddedBlendId, Mathf.Clamp01(EmbeddedBlend));
        material.SetFloat(DepthBlendId, Mathf.Clamp01(DepthBlend));
        material.SetFloat(PulseId, 0f);
        material.SetFloat(PulseSpeedId, Mathf.Max(0f, IdlePulseSpeed));
        material.SetFloat(FlickerAmountId, Mathf.Clamp01(IdleFlicker));
        material.SetFloat(EyeBoostId, 0f);
        material.SetFloat(GrinBoostId, 0f);
        ApplyProjectionVectors(material);
        material.renderQueue = 3000 + FaceSortingOrder;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

#if UNITY_EDITOR
    public static void InstallIntoSampleScene()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";

        EnsureFaceTextureImport(FaceTexturePath);
        Material material = EnsureFaceMaterial(FaceMaterialPath, FaceShaderPath, FaceTexturePath);

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject darkCore = GameObject.Find(DarkCoreObjectName);
        GameObject innerCore = GameObject.Find(InnerCoreObjectName);
        GameObject faceObject = GameObject.Find(FaceObjectName);
        bool reusedPlaceholder = false;

        if (faceObject == null)
        {
            faceObject = GameObject.Find("SuperBall_Face_AttackOnly");
            reusedPlaceholder = faceObject != null;
        }

        if (faceObject == null)
        {
            faceObject = new GameObject(FaceObjectName);
        }
        else
        {
            faceObject.name = FaceObjectName;
        }

        Transform parent = darkCore != null ? darkCore.transform : innerCore != null ? innerCore.transform : null;
        if (parent == null)
        {
            throw new InvalidOperationException("Could not find SuperBall_InnerCoreDark or SuperBall_InnerCore in SampleScene.");
        }

        faceObject.transform.SetParent(parent, false);
        faceObject.transform.localPosition = Vector3.zero;
        faceObject.transform.localRotation = Quaternion.identity;
        faceObject.transform.localScale = Vector3.one * DefaultFaceObjectScale;

        MeshFilter meshFilter = faceObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = faceObject.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = ResolveSphereMesh(darkCore, innerCore);

        MeshRenderer meshRenderer = faceObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = faceObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.sharedMaterial = material;

        SuperBallFaceOverlay controller = faceObject.GetComponent<SuperBallFaceOverlay>();
        if (controller == null)
        {
            controller = faceObject.AddComponent<SuperBallFaceOverlay>();
        }

        controller.IdleVisibility = 0.04f;
        controller.ChargeVisibility = 1.22f;
        controller.FlashVisibilityMultiplier = 1.75f;
        controller.StateTransitionSpeed = 4.8f;
        controller.IdleEmission = 0.35f;
        controller.ChargeEmission = 3.45f;
        controller.FlashEmission = 5.25f;
        controller.FlashDecaySpeed = 6.0f;
        controller.IdleFlicker = 0.015f;
        controller.ChargeFlicker = 0.20f;
        controller.IdlePulseSpeed = 0.8f;
        controller.ChargePulseSpeed = 2.4f;
        controller.EyeAggressionBoost = 1.60f;
        controller.GrinAggressionBoost = 1.10f;
        controller.AggressiveSharpnessBoost = 0.55f;
        controller.EyeRevealStart = 0.08f;
        controller.EyeRevealEnd = 0.42f;
        controller.GrinRevealStart = 0.38f;
        controller.GrinRevealEnd = 0.86f;
        controller.FaceColor = new Color(0.78f, 1f, 0.10f, 1f);
        controller.TextureDetailStrength = 1.46f;
        controller.TintStrength = 0.58f;
        controller.AlphaStrength = 1.12f;
        controller.Contrast = 1.82f;
        controller.Brightness = 1.14f;
        controller.Gamma = 0.80f;
        controller.EdgeDefinition = 1.55f;
        controller.FeatureThreshold = 0.16f;
        controller.FaceScale = LockedFaceScale;
        controller.FaceWidthScale = 1.15f;
        controller.FaceHeightScale = 1.06f;
        controller.ProjectionEuler = Vector3.zero;
        controller.FaceVerticalOffset = 0.04f;
        controller.FaceHorizontalOffset = 0f;
        controller.FaceSoftness = 0.065f;
        controller.FrontMaskStrength = 1f;
        controller.HemisphereSoftness = 0.22f;
        controller.AlphaCutoff = 0.012f;
        controller.TextureEdgeSoftness = 0.045f;
        controller.CoreTint = new Color(0.05f, 0.17f, 0.07f, 1f);
        controller.CoreTintStrength = 0.52f;
        controller.EmbeddedBlend = 0.48f;
        controller.DepthBlend = 0.30f;
        controller.DrawEditorPreviewAnchors = false;
        controller.PreviewDistance = 2.6f;
        controller.PreviewYawAngle = 18f;
        controller.PreviewPitchAngle = 12f;
        controller.PreviewAnchorColor = new Color(0.8f, 1f, 0.25f, 0.9f);
        controller.PreviewSphereRadius = 1f;
        controller.GroundContactNote =
            "SampleScene keeps the Super Ball visual root centered at y=0; runtime spawning should place the root center at groundY + sphereRadius.";
        controller.SetIdle();
        controller.RegenerateFaceOverlay();

        Transform veins = innerCore != null ? innerCore.transform.Find("SuperBall_InternalVeins") : null;
        if (veins == null)
        {
            throw new InvalidOperationException("SuperBall_InternalVeins was not found after face integration.");
        }

        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(faceObject);
        EditorUtility.SetDirty(meshRenderer);
        EditorUtility.SetDirty(meshFilter);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "SuperBallFaceOverlay installed: reusedPlaceholder=" + reusedPlaceholder +
            ", hierarchy=" + GetHierarchyPath(faceObject.transform) +
            ", mesh=" + meshFilter.sharedMesh.name +
            ", material=" + FaceMaterialPath +
            ", texture=" + FaceTexturePath);
    }

    private void RegenerateFaceOverlayEditor()
    {
        bool warningRaised = false;

        if (gameObject.name != FaceObjectName)
        {
            gameObject.name = FaceObjectName;
        }

        GameObject darkCore = GameObject.Find(DarkCoreObjectName);
        GameObject innerCore = GameObject.Find(InnerCoreObjectName);

        if (faceFilter == null)
        {
            faceFilter = gameObject.GetComponent<MeshFilter>();
        }

        if (faceFilter == null)
        {
            faceFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (faceRenderer == null)
        {
            faceRenderer = gameObject.GetComponent<MeshRenderer>();
        }

        if (faceRenderer == null)
        {
            faceRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (darkCore == null)
        {
            warningRaised = true;
            Debug.LogWarning(
                "SuperBallFaceOverlay regenerate warning: missing required scene object '" + DarkCoreObjectName +
                "'. Keeping the current parent and using the best available sphere mesh fallback.",
                this);
        }
        else if (transform.parent != darkCore.transform)
        {
            transform.SetParent(darkCore.transform, false);
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * DefaultFaceObjectScale;

        Mesh resolvedMesh = null;
        try
        {
            resolvedMesh = ResolveSphereMesh(darkCore, innerCore);
        }
        catch (Exception ex)
        {
            warningRaised = true;
            Debug.LogWarning(
                "SuperBallFaceOverlay regenerate warning: could not resolve project sphere mesh. " +
                "Using runtime sphere fallback if needed. Details: " + ex.Message,
                this);
        }

        if (resolvedMesh != null)
        {
            faceFilter.sharedMesh = resolvedMesh;
        }
        else if (faceFilter.sharedMesh == null || IsLikelyQuadMesh(faceFilter.sharedMesh))
        {
            if (runtimeSphereMesh == null)
            {
                runtimeSphereMesh = CreateRuntimeSphereMesh();
            }

            faceFilter.sharedMesh = runtimeSphereMesh;
        }

        Material material = null;
        try
        {
            EnsureFaceTextureImport(FaceTexturePath);
            material = EnsureFaceMaterial(FaceMaterialPath, FaceShaderPath, FaceTexturePath);
        }
        catch (Exception ex)
        {
            warningRaised = true;
            Debug.LogWarning(
                "SuperBallFaceOverlay regenerate warning: could not rebind face shader/material/texture. Details: " +
                ex.Message,
                this);
        }

        if (material != null)
        {
            SyncMaterialFromInspector(material);
            faceRenderer.sharedMaterial = material;
            EditorUtility.SetDirty(material);
        }
        else if (faceRenderer.sharedMaterial == null)
        {
            warningRaised = true;
            Debug.LogWarning(
                "SuperBallFaceOverlay regenerate warning: face renderer has no material and the project face material could not be loaded.",
                this);
        }

        ConfigureRenderer();
        ApplyState();

        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(gameObject);
        if (faceRenderer != null)
        {
            EditorUtility.SetDirty(faceRenderer);
        }

        if (faceFilter != null)
        {
            EditorUtility.SetDirty(faceFilter);
        }

        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            SceneView.RepaintAll();
        }

        Debug.Log(
            "SuperBallFaceOverlay regenerated successfully: hierarchy=" + GetHierarchyPath(transform) +
            ", mesh=" + (faceFilter != null && faceFilter.sharedMesh != null ? faceFilter.sharedMesh.name : "<missing>") +
            ", material=" + (faceRenderer != null && faceRenderer.sharedMaterial != null ? faceRenderer.sharedMaterial.name : "<missing>") +
            ", texture=" + FaceTexturePath +
            ", projectionScale=" + FaceScale.ToString("0.###") +
            ", widthScale=" + FaceWidthScale.ToString("0.###") +
            ", heightScale=" + FaceHeightScale.ToString("0.###") +
            ", verticalOffset=" + FaceVerticalOffset.ToString("0.###") +
            (warningRaised ? ", warnings=true" : ", warnings=false"),
            this);
    }

    private static void EnsureFaceTextureImport(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Could not find face texture importer at " + texturePath);
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Material EnsureFaceMaterial(string materialPath, string shaderPath, string texturePath)
    {
        Shader shader = Shader.Find(FaceShaderName);
        if (shader == null)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        }

        if (shader == null)
        {
            throw new InvalidOperationException("Could not find " + FaceShaderName + " shader.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "M_SuperBall_Face"
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            throw new InvalidOperationException("Could not load face texture at " + texturePath);
        }

        material.shader = shader;
        material.SetTexture(MainTexId, texture);
        material.SetTextureScale(MainTexName, DefaultFaceTextureScale);
        material.SetTextureOffset(MainTexName, DefaultFaceTextureOffset);
        material.SetColor("_TintColor", new Color(0.78f, 1f, 0.10f, 1f));
        material.SetFloat("_Visibility", 0.04f);
        material.SetFloat("_EmissionIntensity", 0.35f);
        material.SetFloat("_Alpha", 1f);
        material.SetFloat("_TextureDetailStrength", 1.46f);
        material.SetFloat("_TintStrength", 0.58f);
        material.SetFloat("_AlphaStrength", 1.12f);
        material.SetFloat("_Contrast", 1.82f);
        material.SetFloat("_Brightness", 1.14f);
        material.SetFloat("_Gamma", 0.80f);
        material.SetFloat("_EdgeDefinition", 1.55f);
        material.SetFloat("_FeatureThreshold", 0.16f);
        material.SetFloat("_FaceScale", LockedFaceScale);
        material.SetFloat("_FaceWidthScale", 1.15f);
        material.SetFloat("_FaceHeightScale", 1.06f);
        material.SetFloat("_FaceVerticalOffset", 0.04f);
        material.SetFloat("_FaceHorizontalOffset", 0f);
        material.SetFloat("_FaceSoftness", 0.065f);
        material.SetFloat("_FrontMaskStrength", 1f);
        material.SetFloat("_HemisphereSoftness", 0.22f);
        material.SetFloat("_AlphaCutoff", 0.012f);
        material.SetFloat("_EdgeSoftness", 0.045f);
        material.SetColor("_CoreTint", new Color(0.05f, 0.17f, 0.07f, 1f));
        material.SetFloat("_CoreTintStrength", 0.52f);
        material.SetFloat("_EmbeddedBlend", 0.48f);
        material.SetFloat("_DepthBlend", 0.30f);
        material.SetFloat("_Pulse", 0f);
        material.SetFloat("_PulseSpeed", 0.8f);
        material.SetFloat("_FlickerAmount", 0.015f);
        material.SetFloat("_EyeBoost", 0f);
        material.SetFloat("_GrinBoost", 0f);
        material.SetVector("_ProjectionForward", new Vector4(0f, 0f, -1f, 0f));
        material.SetVector("_ProjectionRight", new Vector4(1f, 0f, 0f, 0f));
        material.SetVector("_ProjectionUp", new Vector4(0f, 1f, 0f, 0f));
        material.renderQueue = 3024;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool IsLikelyQuadMesh(Mesh mesh)
    {
        return mesh != null
            && (mesh.name.IndexOf("quad", StringComparison.OrdinalIgnoreCase) >= 0
                || (mesh.vertexCount <= 4 && mesh.triangles != null && mesh.triangles.Length <= 6));
    }

    private static Mesh ResolveSphereMesh(GameObject darkCore, GameObject innerCore)
    {
        Mesh mesh = GetSharedMesh(darkCore);
        if (mesh != null)
        {
            return mesh;
        }

        mesh = GetSharedMesh(innerCore);
        if (mesh != null)
        {
            return mesh;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/SuperBall_HighPolySphere.fbx");
        for (int i = 0; i < assets.Length; i++)
        {
            mesh = assets[i] as Mesh;
            if (mesh != null)
            {
                return mesh;
            }
        }

        throw new InvalidOperationException("Could not resolve a sphere mesh for SuperBall_Face.");
    }

    private static Mesh GetSharedMesh(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        MeshFilter meshFilter = source.GetComponent<MeshFilter>();
        return meshFilter != null ? meshFilter.sharedMesh : null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!DrawEditorPreviewAnchors)
        {
            return;
        }

        Quaternion worldProjection = transform.rotation * GetProjectionRotation();
        Vector3 forward = worldProjection * Vector3.back;
        Vector3 right = worldProjection * Vector3.right;
        Vector3 up = worldProjection * Vector3.up;
        Vector3 origin = transform.position;
        float previewDistance = Mathf.Max(PreviewDistance, 0.25f);
        float markerRadius = Mathf.Max(PreviewSphereRadius * 0.07f, 0.04f);

        Vector3[] previewPositions =
        {
            origin + forward * previewDistance,
            origin + (Quaternion.AngleAxis(-PreviewYawAngle, up) * forward) * previewDistance,
            origin + (Quaternion.AngleAxis(PreviewYawAngle, up) * forward) * previewDistance,
            origin + (Quaternion.AngleAxis(-PreviewPitchAngle, right) * forward) * previewDistance,
            origin + (Quaternion.AngleAxis(PreviewPitchAngle, right) * forward) * previewDistance,
        };

        string[] previewLabels = { "Front", "Left", "Right", "Above", "Below" };

        Gizmos.color = PreviewAnchorColor;
        for (int i = 0; i < previewPositions.Length; i++)
        {
            Gizmos.DrawLine(origin, previewPositions[i]);
            Gizmos.DrawWireSphere(previewPositions[i], markerRadius);
            Handles.Label(previewPositions[i] + up * (markerRadius * 1.6f), previewLabels[i]);
        }
    }

    private void ApplyLinkedPreviewState(float chargeProgress, float flashIntensity, bool recovery)
    {
        if (recovery)
        {
            SetRecovery();
        }
        else if (chargeProgress <= 0f && flashIntensity <= 0f)
        {
            SetIdle();
        }
        else
        {
            SetCharge(chargeProgress);
        }

        if (flashIntensity > 0f)
        {
            Flash(flashIntensity);
        }

        SuperBallInternalCracks[] cracks = FindObjectsOfType<SuperBallInternalCracks>(true);
        for (int i = 0; i < cracks.Length; i++)
        {
            SuperBallInternalCracks crackSystem = cracks[i];
            if (crackSystem == null || crackSystem.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (recovery)
            {
                crackSystem.SetRecovery();
            }
            else if (chargeProgress <= 0f && flashIntensity <= 0f)
            {
                crackSystem.SetIdle();
            }
            else
            {
                crackSystem.SetCharge(chargeProgress);
            }

            if (flashIntensity > 0f)
            {
                crackSystem.Flash(flashIntensity);
            }

            EditorUtility.SetDirty(crackSystem);
        }

        SuperBallInternalVeins[] veins = FindObjectsOfType<SuperBallInternalVeins>(true);
        for (int i = 0; i < veins.Length; i++)
        {
            SuperBallInternalVeins veinSystem = veins[i];
            if (veinSystem == null || veinSystem.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (recovery)
            {
                veinSystem.SetRecovery();
            }
            else if (chargeProgress <= 0f && flashIntensity <= 0f)
            {
                veinSystem.SetIdle();
            }
            else
            {
                veinSystem.SetCharge(chargeProgress);
            }

            if (flashIntensity > 0f)
            {
                veinSystem.Flash(flashIntensity);
            }

            EditorUtility.SetDirty(veinSystem);
        }

        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    public static void CaptureCohesionValidation()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SuperBallFaceOverlay faceOverlay = FindObjectOfType<SuperBallFaceOverlay>(true);
        if (faceOverlay == null)
        {
            throw new InvalidOperationException("Could not find SuperBallFaceOverlay in " + scenePath);
        }

        Selection.activeObject = null;

        string outputDirectory = Path.Combine("Logs", "SuperBallCohesionValidation");
        Directory.CreateDirectory(outputDirectory);

        GameObject cameraObject = new GameObject("Codex_CohesionValidationCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 50f;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        RenderTexture renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
        {
            name = "SuperBallCohesionValidationRT"
        };
        Texture2D image = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        List<string> savedFiles = new List<string>();

        VisualCaptureCase[] cases =
        {
            new VisualCaptureCase("idle_front", 0f, 0f, true, 0f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge25_front", 0.25f, 0f, false, 0f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge50_front", 0.50f, 0f, false, 0f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge75_front", 0.75f, 0f, false, 0f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge75_left", 0.75f, 0f, false, -18f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge75_right", 0.75f, 0f, false, 18f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("charge75_above", 0.75f, 0f, false, 0f, -14f, 2.8f, 1.25f),
            new VisualCaptureCase("gameplay_charge", 0.85f, 0f, false, 0f, 0f, 5.2f, 2.25f),
            new VisualCaptureCase("flash_front", 1f, 1f, false, 0f, 0f, 2.8f, 1.25f),
            new VisualCaptureCase("recovery_front", 0f, 0f, true, 0f, 0f, 2.8f, 1.25f),
        };

        try
        {
            for (int i = 0; i < cases.Length; i++)
            {
                VisualCaptureCase captureCase = cases[i];
                faceOverlay.ApplyLinkedPreviewState(captureCase.ChargeProgress, captureCase.FlashIntensity, captureCase.Recovery);
                CaptureCase(camera, renderTexture, image, faceOverlay.transform.position, captureCase, outputDirectory, savedFiles);
            }

            faceOverlay.ApplyLinkedPreviewState(0f, 0f, true);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            renderTexture.Release();
            DestroyImmediate(renderTexture);
            DestroyImmediate(image);
            DestroyImmediate(cameraObject);
        }

        Debug.Log(
            "SuperBall cohesion validation captures saved: " + string.Join(", ", savedFiles.ToArray()) +
            ", scene=" + scene.path);
    }

    private static void CaptureCase(
        Camera camera,
        RenderTexture renderTexture,
        Texture2D image,
        Vector3 target,
        VisualCaptureCase captureCase,
        string outputDirectory,
        List<string> savedFiles)
    {
        Vector3 viewDirection = Quaternion.Euler(captureCase.Pitch, captureCase.Yaw, 0f) * Vector3.back;
        camera.transform.position = target + viewDirection.normalized * captureCase.Distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        camera.orthographicSize = captureCase.OrthographicSize;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        image.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
        image.Apply(false, false);
        RenderTexture.active = previousActive;

        string outputPath = Path.Combine(outputDirectory, captureCase.Name + ".png");
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        savedFiles.Add(outputPath);
    }

    private struct VisualCaptureCase
    {
        public readonly string Name;
        public readonly float ChargeProgress;
        public readonly float FlashIntensity;
        public readonly bool Recovery;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly float Distance;
        public readonly float OrthographicSize;

        public VisualCaptureCase(
            string name,
            float chargeProgress,
            float flashIntensity,
            bool recovery,
            float yaw,
            float pitch,
            float distance,
            float orthographicSize)
        {
            Name = name;
            ChargeProgress = chargeProgress;
            FlashIntensity = flashIntensity;
            Recovery = recovery;
            Yaw = yaw;
            Pitch = pitch;
            Distance = distance;
            OrthographicSize = orthographicSize;
        }
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SuperBallFaceOverlay))]
public sealed class SuperBallFaceOverlayEditor : Editor
{
    private static readonly string[] VisibilityFields =
    {
        nameof(SuperBallFaceOverlay.IdleVisibility),
        nameof(SuperBallFaceOverlay.ChargeVisibility),
        nameof(SuperBallFaceOverlay.FlashVisibilityMultiplier),
        nameof(SuperBallFaceOverlay.StateTransitionSpeed),
    };

    private static readonly string[] EmissionFields =
    {
        nameof(SuperBallFaceOverlay.IdleEmission),
        nameof(SuperBallFaceOverlay.ChargeEmission),
        nameof(SuperBallFaceOverlay.FlashEmission),
        nameof(SuperBallFaceOverlay.FlashDecaySpeed),
        nameof(SuperBallFaceOverlay.FaceColor),
    };

    private static readonly string[] TextureDetailFields =
    {
        nameof(SuperBallFaceOverlay.TextureDetailStrength),
        nameof(SuperBallFaceOverlay.TintStrength),
        nameof(SuperBallFaceOverlay.AlphaStrength),
        nameof(SuperBallFaceOverlay.Contrast),
        nameof(SuperBallFaceOverlay.Brightness),
        nameof(SuperBallFaceOverlay.Gamma),
        nameof(SuperBallFaceOverlay.EdgeDefinition),
        nameof(SuperBallFaceOverlay.FeatureThreshold),
    };

    private static readonly string[] PlacementFields =
    {
        nameof(SuperBallFaceOverlay.FaceScale),
        nameof(SuperBallFaceOverlay.FaceWidthScale),
        nameof(SuperBallFaceOverlay.FaceHeightScale),
        nameof(SuperBallFaceOverlay.ProjectionEuler),
        nameof(SuperBallFaceOverlay.FaceVerticalOffset),
        nameof(SuperBallFaceOverlay.FaceHorizontalOffset),
        nameof(SuperBallFaceOverlay.FaceSoftness),
        nameof(SuperBallFaceOverlay.FrontMaskStrength),
        nameof(SuperBallFaceOverlay.HemisphereSoftness),
    };

    private static readonly string[] EmbeddedDepthFields =
    {
        nameof(SuperBallFaceOverlay.AlphaCutoff),
        nameof(SuperBallFaceOverlay.TextureEdgeSoftness),
        nameof(SuperBallFaceOverlay.CoreTint),
        nameof(SuperBallFaceOverlay.CoreTintStrength),
        nameof(SuperBallFaceOverlay.EmbeddedBlend),
        nameof(SuperBallFaceOverlay.DepthBlend),
    };

    private static readonly string[] AnimationFields =
    {
        nameof(SuperBallFaceOverlay.IdleFlicker),
        nameof(SuperBallFaceOverlay.ChargeFlicker),
        nameof(SuperBallFaceOverlay.IdlePulseSpeed),
        nameof(SuperBallFaceOverlay.ChargePulseSpeed),
        nameof(SuperBallFaceOverlay.EyeAggressionBoost),
        nameof(SuperBallFaceOverlay.GrinAggressionBoost),
        nameof(SuperBallFaceOverlay.AggressiveSharpnessBoost),
        nameof(SuperBallFaceOverlay.EyeRevealStart),
        nameof(SuperBallFaceOverlay.EyeRevealEnd),
        nameof(SuperBallFaceOverlay.GrinRevealStart),
        nameof(SuperBallFaceOverlay.GrinRevealEnd),
    };

    private static readonly string[] DebugFields =
    {
        nameof(SuperBallFaceOverlay.DrawEditorPreviewAnchors),
        nameof(SuperBallFaceOverlay.PreviewDistance),
        nameof(SuperBallFaceOverlay.PreviewYawAngle),
        nameof(SuperBallFaceOverlay.PreviewPitchAngle),
        nameof(SuperBallFaceOverlay.PreviewAnchorColor),
        nameof(SuperBallFaceOverlay.PreviewSphereRadius),
        nameof(SuperBallFaceOverlay.GroundContactNote),
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SuperBallFaceOverlay faceOverlay = target as SuperBallFaceOverlay;
        DrawRegenerateButton();

        EditorGUILayout.Space();
        DrawReferencesSection(faceOverlay);
        DrawPropertyGroup("Placement / Projection", PlacementFields);
        EditorGUILayout.HelpBox(
            "ProjectionEuler rotates the face around the sphere without turning it into a billboard. If future arenas need a different readable side, drive ProjectionEuler and the offsets from gameplay instead of attaching a UI card.",
            MessageType.None);
        DrawPropertyGroup("Visibility", VisibilityFields);
        DrawPropertyGroup("Emission", EmissionFields);
        DrawPropertyGroup("Texture Detail", TextureDetailFields);
        DrawPropertyGroup("Embedded Depth", EmbeddedDepthFields);
        DrawPropertyGroup("Animation Response", AnimationFields);
        DrawPropertyGroup("Debug / Regeneration", DebugFields);
        DrawRuntimeHookupSection();

        serializedObject.ApplyModifiedProperties();

        DrawPreviewSection();

        EditorGUILayout.Space();
        DrawRegenerateButton();
    }

    private void DrawReferencesSection(SuperBallFaceOverlay faceOverlay)
    {
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regeneration re-applies sphere mesh, material defaults, and safe texture import settings for the face overlay only. Cracks and veins are not regenerated.",
            MessageType.None);

        MeshRenderer meshRenderer = faceOverlay != null ? faceOverlay.GetComponent<MeshRenderer>() : null;
        MeshFilter meshFilter = faceOverlay != null ? faceOverlay.GetComponent<MeshFilter>() : null;
        Material material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
        Texture texture = material != null ? material.mainTexture : null;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Face Renderer", meshRenderer, typeof(MeshRenderer), true);
            EditorGUILayout.ObjectField("Sphere Mesh", meshFilter != null ? meshFilter.sharedMesh : null, typeof(Mesh), false);
            EditorGUILayout.ObjectField("Face Material", material, typeof(Material), false);
            EditorGUILayout.ObjectField("Face Texture", texture, typeof(Texture), false);
        }
    }

    private void DrawPropertyGroup(string title, string[] propertyNames)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    private void DrawRuntimeHookupSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Hookup Notes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Idle -> SetIdle()\nChargeWarning -> SetCharge(progress)\nChargeLaunch -> Flash(intensity) while optionally continuing SetCharge(progress)\nRicochetFlash -> Flash(intensity)\nRecovery -> SetRecovery() or SetIdle()",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "Eyes and grin emphasis are parameter-driven in the shader. The gameplay state machine should call SetIdle, SetCharge(progress), Flash(intensity), and recovery/idle on the face, internal cracks, and internal veins together instead of swapping textures or materials.",
            MessageType.None);
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visual State Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Editor-only preview buttons drive the face, internal cracks, and internal veins together so idle, charge, flash, and recovery can be judged without selected-object gizmos.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPreviewButton("Idle", overlay => overlay.PreviewVisualIdle());
            DrawPreviewButton("Charge 25%", overlay => overlay.PreviewVisualCharge25());
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPreviewButton("Charge 50%", overlay => overlay.PreviewVisualCharge50());
            DrawPreviewButton("Charge 75%", overlay => overlay.PreviewVisualCharge75());
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPreviewButton("Full Charge", overlay => overlay.PreviewVisualFullCharge());
            DrawPreviewButton("Flash", overlay => overlay.PreviewVisualFlash());
            DrawPreviewButton("Recovery", overlay => overlay.PreviewVisualRecovery());
        }
    }

    private void DrawPreviewButton(string label, Action<SuperBallFaceOverlay> previewAction)
    {
        if (!GUILayout.Button(label))
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            SuperBallFaceOverlay faceOverlay = targets[i] as SuperBallFaceOverlay;
            if (faceOverlay == null)
            {
                continue;
            }

            Undo.RecordObject(faceOverlay, "Preview SuperBall Visual State");
            previewAction(faceOverlay);
            EditorUtility.SetDirty(faceOverlay);
        }
    }

    private void DrawRegenerateButton()
    {
        if (!GUILayout.Button("Regenerate Face Overlay"))
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            SuperBallFaceOverlay faceOverlay = targets[i] as SuperBallFaceOverlay;
            if (faceOverlay == null)
            {
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(faceOverlay.gameObject, "Regenerate Face Overlay");
            faceOverlay.RegenerateFaceOverlay();
            EditorUtility.SetDirty(faceOverlay);
            EditorUtility.SetDirty(faceOverlay.gameObject);

            if (faceOverlay.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(faceOverlay.gameObject.scene);
            }
        }
    }
}
#endif
