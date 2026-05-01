using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways]
public sealed class SuperBallInternalVeins : MonoBehaviour
{
    [Header("Distribution")]
    [Range(1, 64)] public int VeinCount = 32;
    [Range(3, 32)] public int PointsPerVein = 5;
    public bool UseManualShellRadius = true;
    [Range(0.01f, 1.25f)] public float ManualShellRadius = 0.98f;
    public bool AutoDeriveSphereRadius = true;
    [Min(0.01f)] public float SphereRadius = 0.98f;
    [Min(0.01f)] public float EffectiveShellRadius = 0.98f;
    [Min(0.01f)] public float EffectiveGenerationRadius = 0.965f;
    [Min(0f)] public float SurfaceInset = 0.015f;
    [Range(0f, 1f)] public float FrontHemisphereShare = 0.84f;
    [Min(0.01f)] public float MinArcLength = 0.16f;
    [Min(0.01f)] public float MaxArcLength = 0.38f;
    [Range(0f, 0.35f)] public float CurveNoise = 0.065f;
    [Range(0f, 1f)] public float BranchChance = 0.14f;
    [Range(0.1f, 1f)] public float BranchLengthMultiplier = 0.34f;
    public int RandomSeed = 84017;

    [Header("Rendering")]
    [Range(0f, 2f)] public float IdleVisibility = 0.08f;
    [Range(0f, 2f)] public float ChargeVisibility = 0.82f;
    [Range(0f, 3f)] public float LaunchFlashVisibility = 1.18f;
    [Range(0f, 1f)] public float VeinAlphaMin = 0.045f;
    [Range(0f, 1f)] public float VeinAlphaMax = 0.18f;
    [Min(0f)] public float VeinEmissionMin = 0.42f;
    [Min(0f)] public float VeinEmissionMax = 1.30f;
    [Min(0f)] public float EmissionIntensity = 0.92f;
    [Min(0.0005f)] public float MinThickness = 0.0012f;
    [Min(0.0005f)] public float MaxThickness = 0.0048f;
    public Color VeinColor = new Color(0.48f, 1f, 0.06f, 1f);
    [Min(0.01f)] public float IdlePulseSpeed = 0.28f;
    [Min(0.01f)] public float ChargePulseSpeed = 2.40f;
    [Min(0.01f)] public float FlashDecaySpeed = 5.20f;
    [Min(0.01f)] public float StateTransitionSpeed = 4.8f;
    [Range(-8f, 8f)] public float FlowSpeed = 0.18f;
    [Range(0f, 1f)] public float NoiseStrength = 0.03f;
    [Range(0f, 1f)] public float FlickerAmount = 0.06f;

    [Header("Debug Validation")]
    public bool DebugVisiblePlacement;
    public bool DrawDebugGizmos;
    public bool LogGenerationSummary = true;
    public Vector3 RendererBoundsSize;
    [Min(0f)] public float RendererBoundsRadius;
    [Min(0f)] public float RendererLocalRawRadius;
    public string ShellRadiusSource = "ManualShellRadius";
    public Vector3 GeneratedLocalMin;
    public Vector3 GeneratedLocalMax;
    [Min(0f)] public float MaxGeneratedPointRadius;
    public bool AnyPointExceedsShell;
    [Min(0)] public int GeneratedVeinCount;
    [Min(0)] public int TotalLineRenderers;

    [SerializeField, HideInInspector] private Material veinMaterial;
    [SerializeField, HideInInspector] private Renderer targetRenderer;
    [SerializeField, HideInInspector] private List<Vector3> debugVeinCenters = new List<Vector3>();

    private const float GoldenAngle = 2.3999631f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int FlickerAmountId = Shader.PropertyToID("_FlickerAmount");

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private MaterialPropertyBlock block;
    private Material fallbackMaterial;
    private float targetChargeProgress;
    private float currentChargeProgress;
    private float flashAmount;

    [ContextMenu("Regenerate Internal Veins")]
    public void RegenerateInternalVeins()
    {
        RegenerateVeins();
    }

    public void RegenerateVeins()
    {
        ClampSettings();
        ClearChildren();
        debugVeinCenters.Clear();
        ResetGeneratedStats();

        Material lineMaterial = ResolveMaterial();
        System.Random random = new System.Random(RandomSeed);
        float generationRadius = ResolveEffectiveGenerationRadius();
        int count = Mathf.Clamp(VeinCount, 1, 64);
        int pointsPerVein = Mathf.Clamp(PointsPerVein, 3, 32);
        List<Vector3> centers = GenerateShellCenters(count, random, generationRadius);
        int lineIndex = 0;

        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 centerNormal = centers[i].sqrMagnitude < 0.0001f ? Vector3.back : centers[i].normalized;
            float arcLength = RandomRange(random, MinArcLength, MaxArcLength);
            float thickness = RandomRange(random, MinThickness, MaxThickness);
            float alpha = RandomRange(random, VeinAlphaMin, VeinAlphaMax);
            float emission = RandomRange(random, VeinEmissionMin, VeinEmissionMax);
            Color color = RandomVeinColor(random);
            Vector3[] points = BuildAngularLightningPath(centerNormal, generationRadius, arcLength, pointsPerVein, CurveNoise, random);

            CreateVeinLine(
                "SuperBall_EnergyVein_" + lineIndex.ToString("000"),
                points,
                lineMaterial,
                thickness,
                alpha,
                emission,
                color);
            lineIndex++;
            GeneratedVeinCount++;
            debugVeinCenters.Add(centerNormal * generationRadius);

            if (Next01(random) < BranchChance)
            {
                int branchPoints = Mathf.Clamp(Mathf.RoundToInt(pointsPerVein * BranchLengthMultiplier), 3, pointsPerVein);
                int startIndex = Mathf.Clamp(random.Next(1, points.Length - 1), 1, points.Length - 2);
                Vector3 branchNormal = points[startIndex].normalized;
                float branchLength = arcLength * BranchLengthMultiplier * RandomRange(random, 0.72f, 1.16f);
                Vector3[] branch = BuildLightningBranchPath(branchNormal, generationRadius, branchLength, branchPoints, CurveNoise * 0.7f, random);

                CreateVeinLine(
                    "SuperBall_EnergyVein_Branch_" + lineIndex.ToString("000"),
                    branch,
                    lineMaterial,
                    thickness * RandomRange(random, 0.55f, 0.78f),
                    alpha * RandomRange(random, 0.62f, 0.88f),
                    emission * RandomRange(random, 0.82f, 1.08f),
                    color);
                lineIndex++;
            }
        }

        RebuildLineCache();
        ApplyPulse();
        LogSummary();
    }

    public void SetVeinMaterial(Material material)
    {
        veinMaterial = material;
    }

    public void SetTargetRenderer(Renderer renderer)
    {
        targetRenderer = renderer;
        RefreshEffectiveRadiusReadout();
    }

    public void SetIdle()
    {
        targetChargeProgress = 0f;
        flashAmount = 0f;
        if (!Application.isPlaying)
        {
            currentChargeProgress = 0f;
        }
    }

    public void SetRecovery()
    {
        SetIdle();
    }

    public void SetCharge(float progress)
    {
        targetChargeProgress = Mathf.Clamp01(progress);
        if (!Application.isPlaying)
        {
            currentChargeProgress = targetChargeProgress;
        }
    }

    public void Flash(float intensity)
    {
        flashAmount = Mathf.Max(flashAmount, Mathf.Max(0f, intensity));
    }

    private void OnEnable()
    {
        EnsureBlock();
        RefreshEffectiveRadiusReadout();
        RebuildLineCache();
        currentChargeProgress = targetChargeProgress;

        if (lines.Count == 0 && transform.childCount == 0)
        {
            RegenerateVeins();
        }
        else
        {
            ApplyPulse();
        }
    }

    private void OnValidate()
    {
        ClampSettings();
        RefreshEffectiveRadiusReadout();
        ApplyPulse();
    }

    private void OnTransformChildrenChanged()
    {
        RebuildLineCache();
        ApplyPulse();
    }

    private void Update()
    {
        if (lines.Count == 0 && transform.childCount > 0)
        {
            RebuildLineCache();
        }

        float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 30f;
        currentChargeProgress = Mathf.MoveTowards(
            currentChargeProgress,
            targetChargeProgress,
            deltaTime * Mathf.Max(StateTransitionSpeed, 0.01f));
        flashAmount = Mathf.MoveTowards(flashAmount, 0f, deltaTime * FlashDecaySpeed);
        ApplyPulse();
    }

    private void ClampSettings()
    {
        VeinCount = Mathf.Clamp(VeinCount, 1, 64);
        PointsPerVein = Mathf.Clamp(PointsPerVein, 3, 32);
        ManualShellRadius = Mathf.Clamp(ManualShellRadius, 0.01f, 1.25f);
        SphereRadius = Mathf.Clamp(SphereRadius, 0.01f, 1.25f);
        EffectiveShellRadius = Mathf.Max(0.01f, EffectiveShellRadius);
        EffectiveGenerationRadius = Mathf.Max(0.01f, EffectiveGenerationRadius);
        float radiusLimit = UseManualShellRadius ? ManualShellRadius : Mathf.Max(SphereRadius, RendererLocalRawRadius);
        SurfaceInset = Mathf.Clamp(SurfaceInset, 0f, Mathf.Max(0f, radiusLimit - 0.01f));
        FrontHemisphereShare = Mathf.Clamp01(FrontHemisphereShare);
        MinArcLength = Mathf.Max(0.01f, MinArcLength);
        MaxArcLength = Mathf.Max(MinArcLength, MaxArcLength);
        CurveNoise = Mathf.Clamp(CurveNoise, 0f, 0.35f);
        BranchChance = Mathf.Clamp01(BranchChance);
        BranchLengthMultiplier = Mathf.Clamp(BranchLengthMultiplier, 0.1f, 1f);
        IdleVisibility = Mathf.Clamp(IdleVisibility, 0f, 2f);
        ChargeVisibility = Mathf.Clamp(ChargeVisibility, 0f, 2f);
        LaunchFlashVisibility = Mathf.Clamp(LaunchFlashVisibility, 0f, 3f);
        VeinAlphaMin = Mathf.Clamp01(VeinAlphaMin);
        VeinAlphaMax = Mathf.Clamp(Mathf.Max(VeinAlphaMin, VeinAlphaMax), 0f, 1f);
        VeinEmissionMin = Mathf.Max(0f, VeinEmissionMin);
        VeinEmissionMax = Mathf.Max(VeinEmissionMin, VeinEmissionMax);
        EmissionIntensity = Mathf.Max(0f, EmissionIntensity);
        MinThickness = Mathf.Max(0.0005f, MinThickness);
        MaxThickness = Mathf.Max(MinThickness, MaxThickness);
        IdlePulseSpeed = Mathf.Max(0.01f, IdlePulseSpeed);
        ChargePulseSpeed = Mathf.Max(IdlePulseSpeed, ChargePulseSpeed);
        FlashDecaySpeed = Mathf.Max(0.01f, FlashDecaySpeed);
        StateTransitionSpeed = Mathf.Max(0.01f, StateTransitionSpeed);
        FlowSpeed = Mathf.Clamp(FlowSpeed, -8f, 8f);
        NoiseStrength = Mathf.Clamp01(NoiseStrength);
        FlickerAmount = Mathf.Clamp01(FlickerAmount);
    }

    private float ResolveEffectiveGenerationRadius()
    {
        return RefreshEffectiveRadiusReadout();
    }

    private float RefreshEffectiveRadiusReadout()
    {
        Renderer resolvedRenderer = targetRenderer != null ? targetRenderer : GetComponentInParent<Renderer>();
        RefreshRendererBoundsReadout(resolvedRenderer);
        RendererLocalRawRadius = CalculateLocalMeshRadius(resolvedRenderer, transform);

        float rendererRadius = AutoDeriveSphereRadius ? RendererLocalRawRadius : 0f;
        if (rendererRadius <= 0.01f)
        {
            rendererRadius = SphereRadius;
        }

        if (UseManualShellRadius)
        {
            EffectiveShellRadius = Mathf.Max(0.01f, ManualShellRadius);
            ShellRadiusSource = "ManualShellRadius";
        }
        else
        {
            EffectiveShellRadius = Mathf.Max(0.01f, rendererRadius);
            ShellRadiusSource = AutoDeriveSphereRadius ? "AutoDerivedRendererRaw" : "SphereRadius";
        }

        EffectiveGenerationRadius = Mathf.Max(0.01f, EffectiveShellRadius - SurfaceInset);
        return EffectiveGenerationRadius;
    }

    private void RefreshRendererBoundsReadout(Renderer renderer)
    {
        if (renderer == null)
        {
            RendererBoundsSize = Vector3.zero;
            RendererBoundsRadius = 0f;
            return;
        }

        Bounds bounds = renderer.bounds;
        RendererBoundsSize = bounds.size;
        RendererBoundsRadius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
    }

    private List<Vector3> GenerateShellCenters(int count, System.Random random, float shellRadius)
    {
        List<Vector3> centers = new List<Vector3>(count);
        int frontTarget = Mathf.Clamp(Mathf.RoundToInt(count * FrontHemisphereShare), 0, count);
        float frontOffset = RandomRange(random, 0f, 360f);
        float rearOffset = RandomRange(random, 0f, 360f);
        int radialBands;
        int angularSectors;
        ResolvePlacementGrid(count, out radialBands, out angularSectors);

        for (int i = 0; i < count; i++)
        {
            bool front = i < frontTarget;
            int groupIndex = front ? i : i - frontTarget;
            int groupCount = front ? Mathf.Max(1, frontTarget) : Mathf.Max(1, count - frontTarget);
            Vector3 normal = GenerateProjectedShellNormal(
                groupIndex,
                groupCount,
                front,
                shellRadius,
                radialBands,
                angularSectors,
                front ? frontOffset : rearOffset,
                random);

            normal = JitterNormal(normal, random, 6f);
            centers.Add(normal.normalized);
        }

        Shuffle(centers, random);
        return centers;
    }

    private static void ResolvePlacementGrid(int count, out int radialBands, out int angularSectors)
    {
        radialBands = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * 0.72f)), 3, 7);
        angularSectors = Mathf.Max(6, Mathf.CeilToInt(count / (float)radialBands));

        while (radialBands * angularSectors < count)
        {
            angularSectors++;
        }
    }

    private Vector3 GenerateProjectedShellNormal(
        int index,
        int count,
        bool frontHemisphere,
        float shellRadius,
        int radialBands,
        int angularSectors,
        float azimuthOffset,
        System.Random random)
    {
        int band = index % radialBands;
        int sector = (index / radialBands) % angularSectors;
        float coverageRadius = Mathf.Max(0.01f, shellRadius * 0.98f);
        float innerArea = 0.12f * 0.12f;
        float outerArea = 0.99f * 0.99f;
        float bandStart = band / (float)radialBands;
        float bandEnd = (band + 1f) / radialBands;
        float areaT = Mathf.Lerp(innerArea, outerArea, RandomRange(random, bandStart, bandEnd));

        if (band == radialBands - 1)
        {
            areaT = Mathf.Lerp(Mathf.Max(areaT, outerArea * 0.80f), outerArea, RandomRange(random, 0.45f, 0.96f));
        }

        float diskRadius = Mathf.Sqrt(Mathf.Clamp01(areaT)) * coverageRadius;
        float sectorWidth = (Mathf.PI * 2f) / angularSectors;
        float angle = (sector + RandomRange(random, 0.12f, 0.88f)) * sectorWidth + azimuthOffset * Mathf.Deg2Rad;
        Vector2 disk = new Vector2(Mathf.Cos(angle) * diskRadius, Mathf.Sin(angle) * diskRadius);
        if (frontHemisphere)
        {
            disk = ApplyFaceSupportBias(disk, index, count, coverageRadius, random);
        }

        disk = Vector2.ClampMagnitude(disk, coverageRadius);

        float safeRadius = Mathf.Max(0.01f, shellRadius);
        float zMagnitude = Mathf.Sqrt(Mathf.Max(0.0001f, safeRadius * safeRadius - disk.sqrMagnitude));
        float z = frontHemisphere ? -zMagnitude : zMagnitude;
        return new Vector3(disk.x, disk.y, z).normalized;
    }

    private static Vector2 ApplyFaceSupportBias(Vector2 disk, int index, int count, float radius, System.Random random)
    {
        float supportShare = Mathf.Clamp01(0.58f - index / Mathf.Max(1f, count) * 0.14f);
        if (Next01(random) > supportShare)
        {
            return disk;
        }

        Vector2[] anchors =
        {
            new Vector2(-0.34f, 0.34f),
            new Vector2(0.34f, 0.34f),
            new Vector2(-0.48f, 0.03f),
            new Vector2(0.48f, 0.03f),
            new Vector2(-0.24f, -0.32f),
            new Vector2(0.24f, -0.32f),
        };

        Vector2 anchor = anchors[index % anchors.Length] * radius;
        anchor += RandomInsideUnitCircle(random) * radius * 0.09f;
        return Vector2.Lerp(disk, anchor, RandomRange(random, 0.42f, 0.72f));
    }

    private Vector3[] BuildAngularLightningPath(
        Vector3 centerNormal,
        float radius,
        float arcLength,
        int pointCount,
        float angularJitter,
        System.Random random)
    {
        Vector3 tangent;
        Vector3 bitangent;
        BuildTangentBasis(centerNormal, out tangent, out bitangent);

        float orientation = RandomRange(random, 0f, Mathf.PI * 2f);
        Vector3 flowDirection = (tangent * Mathf.Cos(orientation) + bitangent * Mathf.Sin(orientation)).normalized;
        Vector3 axis = Vector3.Cross(flowDirection, centerNormal).normalized;
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = bitangent;
        }

        float arcAngle = Mathf.Clamp(arcLength / Mathf.Max(0.01f, radius), 0.035f, Mathf.PI * 0.42f);
        float jitter = Mathf.Clamp(angularJitter, 0f, 0.20f);
        Vector3[] points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : i / (float)(pointCount - 1);
            float centered = t - 0.5f;
            float angularOffset = centered * arcAngle;
            Vector3 normal = (Quaternion.AngleAxis(angularOffset * Mathf.Rad2Deg, axis) * centerNormal).normalized;

            if (i > 0 && i < pointCount - 1)
            {
                Vector3 localTangent;
                Vector3 localBitangent;
                BuildTangentBasis(normal, out localTangent, out localBitangent);
                float zigzag = (i % 2 == 0 ? -1f : 1f) * RandomRange(random, jitter * 0.45f, jitter);
                float kink = RandomRange(random, -jitter * 0.35f, jitter * 0.35f);
                normal = (normal + localBitangent * zigzag + localTangent * kink).normalized;
            }

            points[i] = RegisterGeneratedPoint(normal * radius);
        }

        return points;
    }

    private Vector3[] BuildLightningBranchPath(
        Vector3 startNormal,
        float radius,
        float arcLength,
        int pointCount,
        float angularJitter,
        System.Random random)
    {
        Vector3 tangent;
        Vector3 bitangent;
        BuildTangentBasis(startNormal, out tangent, out bitangent);

        float orientation = RandomRange(random, 0f, Mathf.PI * 2f);
        Vector3 flowDirection = (tangent * Mathf.Cos(orientation) + bitangent * Mathf.Sin(orientation)).normalized;
        Vector3 axis = Vector3.Cross(flowDirection, startNormal).normalized;
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = bitangent;
        }

        float arcAngle = Mathf.Clamp(arcLength / Mathf.Max(0.01f, radius), 0.02f, Mathf.PI * 0.28f);
        float jitter = Mathf.Clamp(angularJitter, 0f, 0.16f);
        Vector3[] points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : i / (float)(pointCount - 1);
            float angularOffset = t * arcAngle;
            Vector3 normal = (Quaternion.AngleAxis(angularOffset * Mathf.Rad2Deg, axis) * startNormal).normalized;

            if (i > 0 && i < pointCount - 1)
            {
                Vector3 localTangent;
                Vector3 localBitangent;
                BuildTangentBasis(normal, out localTangent, out localBitangent);
                float zigzag = (i % 2 == 0 ? -1f : 1f) * RandomRange(random, jitter * 0.35f, jitter);
                normal = (normal + localBitangent * zigzag + localTangent * RandomRange(random, -jitter * 0.25f, jitter * 0.25f)).normalized;
            }

            points[i] = RegisterGeneratedPoint(normal * radius);
        }

        return points;
    }

    private void CreateVeinLine(
        string lineName,
        Vector3[] points,
        Material material,
        float thickness,
        float alpha,
        float emission,
        Color color)
    {
        GameObject lineObject = new GameObject(lineName);
        Transform lineTransform = lineObject.transform;
        lineTransform.SetParent(transform, false);
        lineTransform.localPosition = Vector3.zero;
        lineTransform.localRotation = Quaternion.identity;
        lineTransform.localScale = Vector3.one;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.allowOcclusionWhenDynamic = false;
        line.widthMultiplier = thickness;
        line.widthCurve = CreateWidthCurve();

        Color start = color;
        Color end = Color.Lerp(color, new Color(0.64f, 1f, 0.05f, 1f), 0.25f);
        start.a = 0.72f;
        end.a = 0.55f;
        line.startColor = start;
        line.endColor = end;

        EnsureBlock();
        line.GetPropertyBlock(block);
        block.SetColor(ColorId, Color.white);
        block.SetFloat(AlphaId, alpha);
        block.SetFloat(EmissionIntensityId, EmissionIntensity * emission);
        block.SetFloat(VisibilityId, DebugVisiblePlacement ? 1f : IdleVisibility);
        block.SetFloat(PulseId, Hash01(lineName));
        block.SetFloat(ScrollSpeedId, FlowSpeed * Mathf.Lerp(0.85f, 1.15f, Hash01(lineName + "_flow")));
        block.SetFloat(NoiseStrengthId, NoiseStrength);
        block.SetFloat(FlickerAmountId, FlickerAmount);
        line.SetPropertyBlock(block);

        TotalLineRenderers++;
    }

    private void ApplyPulse()
    {
        EnsureBlock();

        if (lines.Count == 0)
        {
            return;
        }

        float time = Application.isPlaying ? Time.time : (float)UnityEditorSafeTime();
        float threat = Mathf.Clamp01(currentChargeProgress);
        float feed = SmoothRange(0.22f, 0.88f, threat);
        float speed = Mathf.Lerp(IdlePulseSpeed, ChargePulseSpeed, feed);
        float slowPulse = 0.88f + Mathf.PerlinNoise(time * speed, 0.71f) * 0.12f;
        float baseVisibility = Mathf.Lerp(IdleVisibility, ChargeVisibility, feed);
        float visibility = DebugVisiblePlacement ? 1f : baseVisibility * slowPulse;
        float emissionScale = Mathf.Lerp(0.72f, 1.20f, feed);

        if (flashAmount > 0f)
        {
            float flash = Mathf.Clamp01(flashAmount);
            visibility = Mathf.Max(visibility, LaunchFlashVisibility * flash);
            emissionScale = Mathf.Max(emissionScale, Mathf.Lerp(1.00f, 1.55f, flash));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
            {
                continue;
            }

            float lineSeed = Hash01(line.name);
            float linePulse = Mathf.Repeat(time * (0.08f + lineSeed * 0.10f) + lineSeed, 1f);
            float lineFlicker = 0.94f + Mathf.Sin(time * speed * (0.35f + lineSeed * 0.20f) + lineSeed * 6.28318f) * 0.06f;
            line.GetPropertyBlock(block);
            block.SetFloat(VisibilityId, visibility * lineFlicker);
            block.SetFloat(EmissionIntensityId, EmissionIntensity * Mathf.Lerp(VeinEmissionMin, VeinEmissionMax, lineSeed) * emissionScale);
            block.SetFloat(PulseId, linePulse);
            block.SetFloat(AlphaId, Mathf.Lerp(VeinAlphaMin, VeinAlphaMax, lineSeed));
            block.SetFloat(ScrollSpeedId, FlowSpeed * Mathf.Lerp(0.85f, 1.15f, Hash01(line.name + "_flow")));
            block.SetFloat(NoiseStrengthId, NoiseStrength);
            block.SetFloat(FlickerAmountId, FlickerAmount);
            line.SetPropertyBlock(block);
        }
    }

    private Material ResolveMaterial()
    {
        if (veinMaterial != null)
        {
            return veinMaterial;
        }

        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("REPO/SuperBallEnergyVein");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            fallbackMaterial = new Material(shader)
            {
                name = "M_SuperBall_EnergyVeins_Runtime"
            };
            ConfigureMaterialProperties(fallbackMaterial);
        }

        return fallbackMaterial;
    }

    private void ConfigureMaterialProperties(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, VeinColor);
        }

        if (material.HasProperty(VisibilityId))
        {
            material.SetFloat(VisibilityId, IdleVisibility);
        }

        if (material.HasProperty(EmissionIntensityId))
        {
            material.SetFloat(EmissionIntensityId, EmissionIntensity);
        }

        if (material.HasProperty(PulseId))
        {
            material.SetFloat(PulseId, 0f);
        }

        if (material.HasProperty(AlphaId))
        {
            material.SetFloat(AlphaId, 1f);
        }

        if (material.HasProperty(ScrollSpeedId))
        {
            material.SetFloat(ScrollSpeedId, FlowSpeed);
        }

        if (material.HasProperty(NoiseStrengthId))
        {
            material.SetFloat(NoiseStrengthId, NoiseStrength);
        }

        if (material.HasProperty(FlickerAmountId))
        {
            material.SetFloat(FlickerAmountId, FlickerAmount);
        }
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        lines.Clear();
    }

    private void RebuildLineCache()
    {
        lines.Clear();
        GetComponentsInChildren(true, lines);
        TotalLineRenderers = lines.Count;
    }

    private Vector3 RegisterGeneratedPoint(Vector3 point)
    {
        if (GeneratedLocalMin.x == float.MaxValue)
        {
            GeneratedLocalMin = point;
            GeneratedLocalMax = point;
        }
        else
        {
            GeneratedLocalMin = Vector3.Min(GeneratedLocalMin, point);
            GeneratedLocalMax = Vector3.Max(GeneratedLocalMax, point);
        }

        float radius = point.magnitude;
        MaxGeneratedPointRadius = Mathf.Max(MaxGeneratedPointRadius, radius);
        if (radius > EffectiveGenerationRadius + 0.0005f)
        {
            AnyPointExceedsShell = true;
        }

        return point;
    }

    private void ResetGeneratedStats()
    {
        GeneratedLocalMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        GeneratedLocalMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        MaxGeneratedPointRadius = 0f;
        AnyPointExceedsShell = false;
        GeneratedVeinCount = 0;
        TotalLineRenderers = 0;
    }

    private void LogSummary()
    {
        if (!LogGenerationSummary)
        {
            return;
        }

        Vector3 min = GeneratedLocalMin.x == float.MaxValue ? Vector3.zero : GeneratedLocalMin;
        Vector3 max = GeneratedLocalMax.x == float.MinValue ? Vector3.zero : GeneratedLocalMax;
        Debug.Log(
            $"SuperBallInternalVeins generated: veinCount={GeneratedVeinCount}, lineRenderers={TotalLineRenderers}, pointsPerVein={PointsPerVein}, rendererBoundsSize={FormatVector(RendererBoundsSize)}, rendererBoundsRadius={RendererBoundsRadius:F4}, rendererLocalRawRadius={RendererLocalRawRadius:F4}, shellRadiusSource={ShellRadiusSource}, selectedShellRadius={EffectiveShellRadius:F4}, effectiveGenerationRadius={EffectiveGenerationRadius:F4}, surfaceInset={SurfaceInset:F4}, generatedLocalMin={FormatVector(min)}, generatedLocalMax={FormatVector(max)}, maxPointRadius={MaxGeneratedPointRadius:F4}, pointsExceedShell={AnyPointExceedsShell}, parentPosition={FormatVector(transform.localPosition)}, parentScale={FormatVector(transform.localScale)}, debugVisible={DebugVisiblePlacement}, drawDebugGizmos={DrawDebugGizmos}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!DrawDebugGizmos)
        {
            return;
        }

        float radius = RefreshEffectiveRadiusReadout();
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.7f, 1f, 0.05f, 0.32f);
        Gizmos.DrawWireSphere(Vector3.zero, radius);
        Gizmos.color = new Color(0.42f, 1f, 0.08f, 0.70f);

        float centerSize = Mathf.Max(0.008f, radius * 0.012f);
        for (int i = 0; i < debugVeinCenters.Count; i++)
        {
            Gizmos.DrawSphere(debugVeinCenters[i], centerSize);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    public static void InstallIntoSampleScene()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        const string materialPath = "Assets/M_SuperBall_EnergyVeins.mat";

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject innerCore = GameObject.Find("SuperBall_InnerCore");

        if (innerCore == null)
        {
            throw new InvalidOperationException("Could not find SuperBall_InnerCore in SampleScene.");
        }

        Transform veinsRoot = innerCore.transform.Find("SuperBall_InternalVeins");
        if (veinsRoot == null)
        {
            GameObject veinsObject = new GameObject("SuperBall_InternalVeins");
            veinsRoot = veinsObject.transform;
            veinsRoot.SetParent(innerCore.transform, false);
        }

        veinsRoot.localPosition = Vector3.zero;
        veinsRoot.localRotation = Quaternion.identity;
        veinsRoot.localScale = Vector3.one;

        Transform cracksRoot = innerCore.transform.Find("SuperBall_InternalCracks");
        if (cracksRoot != null)
        {
            veinsRoot.SetSiblingIndex(cracksRoot.GetSiblingIndex() + 1);
        }

        SuperBallInternalVeins veins = veinsRoot.GetComponent<SuperBallInternalVeins>();
        if (veins == null)
        {
            veins = veinsRoot.gameObject.AddComponent<SuperBallInternalVeins>();
        }

        Material material = EnsureMaterialAsset(materialPath);
        Renderer renderer = innerCore.GetComponent<Renderer>();

        veins.VeinCount = 32;
        veins.PointsPerVein = 5;
        veins.UseManualShellRadius = true;
        veins.ManualShellRadius = 0.98f;
        veins.AutoDeriveSphereRadius = true;
        veins.SphereRadius = 0.98f;
        veins.SurfaceInset = 0.015f;
        veins.FrontHemisphereShare = 0.84f;
        veins.MinArcLength = 0.16f;
        veins.MaxArcLength = 0.38f;
        veins.CurveNoise = 0.065f;
        veins.BranchChance = 0.14f;
        veins.BranchLengthMultiplier = 0.34f;
        veins.IdleVisibility = 0.08f;
        veins.ChargeVisibility = 0.82f;
        veins.LaunchFlashVisibility = 1.18f;
        veins.VeinAlphaMin = 0.045f;
        veins.VeinAlphaMax = 0.18f;
        veins.VeinEmissionMin = 0.42f;
        veins.VeinEmissionMax = 1.30f;
        veins.EmissionIntensity = 0.92f;
        veins.MinThickness = 0.0012f;
        veins.MaxThickness = 0.0048f;
        veins.RandomSeed = 84017;
        veins.VeinColor = new Color(0.48f, 1f, 0.06f, 1f);
        veins.IdlePulseSpeed = 0.28f;
        veins.ChargePulseSpeed = 2.40f;
        veins.FlashDecaySpeed = 5.20f;
        veins.StateTransitionSpeed = 4.8f;
        veins.FlowSpeed = 0.18f;
        veins.NoiseStrength = 0.03f;
        veins.FlickerAmount = 0.06f;
        veins.DebugVisiblePlacement = false;
        veins.DrawDebugGizmos = false;
        veins.LogGenerationSummary = true;
        veins.SetVeinMaterial(material);
        veins.SetTargetRenderer(renderer);
        veins.RegenerateInternalVeins();

        EditorUtility.SetDirty(veins);
        EditorUtility.SetDirty(veinsRoot.gameObject);
        EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Material EnsureMaterialAsset(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("REPO/SuperBallEnergyVein");
        if (shader == null)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SuperBallEnergyVein.shader");
        }

        if (shader == null)
        {
            throw new InvalidOperationException("Could not find REPO/SuperBallEnergyVein shader.");
        }

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "M_SuperBall_EnergyVeins"
            };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialDefaults(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialDefaults(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetColorIfPresent(material, ColorId, new Color(0.48f, 1f, 0.06f, 1f));
        SetFloatIfPresent(material, VisibilityId, 0.08f);
        SetFloatIfPresent(material, EmissionIntensityId, 0.92f);
        SetFloatIfPresent(material, PulseId, 0f);
        SetFloatIfPresent(material, AlphaId, 1f);
        SetFloatIfPresent(material, ScrollSpeedId, 0.18f);
        SetFloatIfPresent(material, NoiseStrengthId, 0.03f);
        SetFloatIfPresent(material, FlickerAmountId, 0.06f);
    }

    private static void SetColorIfPresent(Material material, int id, Color value)
    {
        if (material.HasProperty(id))
        {
            material.SetColor(id, value);
        }
    }

    private static void SetFloatIfPresent(Material material, int id, float value)
    {
        if (material.HasProperty(id))
        {
            material.SetFloat(id, value);
        }
    }
#endif

    private static AnimationCurve CreateWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.10f, 0.70f),
            new Keyframe(0.50f, 1f),
            new Keyframe(0.90f, 0.65f),
            new Keyframe(1f, 0f));
    }

    private static float CalculateLocalMeshRadius(Renderer renderer, Transform localSpace)
    {
        if (renderer == null || localSpace == null)
        {
            return 0f;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return CalculateBoundsRadiusInLocalSpace(meshFilter.sharedMesh.bounds, renderer.transform, localSpace);
        }

        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
        {
            return CalculateBoundsRadiusInLocalSpace(skinnedMeshRenderer.sharedMesh.bounds, renderer.transform, localSpace);
        }

        Bounds worldBounds = renderer.bounds;
        Bounds localBounds = new Bounds(localSpace.InverseTransformPoint(worldBounds.center), Vector3.zero);
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = worldBounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                    localBounds.Encapsulate(localSpace.InverseTransformPoint(worldCorner));
                }
            }
        }

        Vector3 localExtents = localBounds.extents;
        return Mathf.Min(localExtents.x, Mathf.Min(localExtents.y, localExtents.z));
    }

    private static float CalculateBoundsRadiusInLocalSpace(Bounds sourceBounds, Transform sourceTransform, Transform localSpace)
    {
        Vector3 extents = sourceBounds.extents;
        Bounds localBounds = new Bounds(localSpace.InverseTransformPoint(sourceTransform.TransformPoint(sourceBounds.center)), Vector3.zero);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    Vector3 worldCorner = sourceTransform.TransformPoint(sourceBounds.center + localCorner);
                    localBounds.Encapsulate(localSpace.InverseTransformPoint(worldCorner));
                }
            }
        }

        Vector3 localExtents = localBounds.extents;
        return Mathf.Min(localExtents.x, Mathf.Min(localExtents.y, localExtents.z));
    }

    private static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        normal = normal.sqrMagnitude < 0.0001f ? Vector3.back : normal.normalized;
        Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.86f ? Vector3.right : Vector3.up;
        tangent = Vector3.Cross(reference, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static Vector3 JitterNormal(Vector3 normal, System.Random random, float maxAngleDegrees)
    {
        Vector3 tangent;
        Vector3 bitangent;
        BuildTangentBasis(normal, out tangent, out bitangent);
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float amount = RandomRange(random, 0f, maxAngleDegrees) * Mathf.Deg2Rad;
        Vector3 sideways = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
        return (normal * Mathf.Cos(amount) + sideways * Mathf.Sin(amount)).normalized;
    }

    private Color RandomVeinColor(System.Random random)
    {
        Color toxicGreen = new Color(0.20f, 0.88f, 0.05f, 1f);
        Color yellowGreen = new Color(0.56f, 1.00f, 0.05f, 1f);
        Color deepGreen = new Color(0.04f, 0.55f, 0.04f, 1f);
        float roll = Next01(random);

        if (roll < 0.58f)
        {
            return Color.Lerp(toxicGreen, yellowGreen, RandomRange(random, 0.05f, 0.35f));
        }

        if (roll < 0.86f)
        {
            return Color.Lerp(deepGreen, toxicGreen, RandomRange(random, 0.35f, 0.95f));
        }

        return Color.Lerp(toxicGreen, yellowGreen, RandomRange(random, 0.35f, 0.55f));
    }

    private static void Shuffle<T>(IList<T> items, System.Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temp = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = temp;
        }
    }

    private static float Next01(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, Next01(random));
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

    private static Vector2 RandomInsideUnitCircle(System.Random random)
    {
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Next01(random));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static float Hash01(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }

    private static string FormatVector(Vector3 vector)
    {
        return string.Format("({0:F4}, {1:F4}, {2:F4})", vector.x, vector.y, vector.z);
    }

    private void EnsureBlock()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
    }

    private static double UnityEditorSafeTime()
    {
#if UNITY_EDITOR
        return EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SuperBallInternalVeins))]
public sealed class SuperBallInternalVeinsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty useManualRadius = serializedObject.FindProperty("UseManualShellRadius");
        bool manualRadius = useManualRadius != null && useManualRadius.boolValue;
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            bool readOnly = property.name == "m_Script"
                || (manualRadius && property.name == "SphereRadius")
                || property.name == "EffectiveShellRadius"
                || property.name == "EffectiveGenerationRadius"
                || property.name == "RendererBoundsSize"
                || property.name == "RendererBoundsRadius"
                || property.name == "RendererLocalRawRadius"
                || property.name == "ShellRadiusSource"
                || property.name == "GeneratedLocalMin"
                || property.name == "GeneratedLocalMax"
                || property.name == "MaxGeneratedPointRadius"
                || property.name == "AnyPointExceedsShell"
                || property.name == "GeneratedVeinCount"
                || property.name == "TotalLineRenderers";

            using (new EditorGUI.DisabledScope(readOnly))
            {
                EditorGUILayout.PropertyField(property, true);
            }

            enterChildren = false;
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate Internal Veins"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                SuperBallInternalVeins veins = targets[i] as SuperBallInternalVeins;
                if (veins == null)
                {
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(veins.gameObject, "Regenerate Internal Veins");
                veins.RegenerateInternalVeins();
                EditorUtility.SetDirty(veins);
                EditorUtility.SetDirty(veins.gameObject);

                if (veins.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(veins.gameObject.scene);
                }
            }
        }
    }
}
#endif
