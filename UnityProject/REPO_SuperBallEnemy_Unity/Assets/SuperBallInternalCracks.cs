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
public sealed class SuperBallInternalCracks : MonoBehaviour
{
    [Header("Distribution")]
    public SuperBallCrackDistributionMode DistributionMode = SuperBallCrackDistributionMode.FullSphereShell;
    [Range(1, 64)] public int ClusterCount = 56;
    [Range(1, 10)] public int SegmentsPerCluster = 4;
    [Range(0f, 1f)] public float FrontHemisphereClusterShare = 0.86f;
    public bool UseManualShellRadius = true;
    [Range(0.01f, 1.25f)] public float ManualShellRadius = 0.98f;
    public bool AutoDeriveSphereRadius = true;
    public bool NormalizeUnitSphereMeshRadius;
    [Min(0.01f)] public float SphereRadius = 0.98f;
    [Min(0.01f)] public float EffectiveSphereRadius = 0.98f;
    [Min(0.01f)] public float EffectiveGenerationRadius = 0.97f;
    [Min(0.01f)] public float EffectiveProjectedRadius = 0.965f;
    [Min(0.01f)] public float EffectiveRadiusDebug = 0.97f;
    [Min(0.01f)] public float EffectiveDiskHorizontalRadius = 0.965f;
    [Min(0.01f)] public float EffectiveDiskVerticalRadius = 0.965f;
    [Min(0.01f)] public float FullShellCoverageRadius = 0.965f;
    [Min(0.01f)] public float EffectiveFullShellCoverageRadius = 0.965f;
    public Vector3 RendererBoundsSize;
    [Min(0f)] public float RendererBoundsRadius;
    [Min(0f)] public float RendererLocalRawRadius;
    [Min(0f)] public float RendererAutoRadius;
    public string ShellRadiusSource = "ManualShellRadius";
    [Min(0f)] public float SurfaceInset = 0.010f;
    [Range(0.1f, 0.98f)] public float DiskHorizontalCoverage = 0.82f;
    [Range(0.1f, 0.98f)] public float DiskVerticalCoverage = 0.82f;
    [Range(0f, 0.5f)] public float CenterAvoidance = 0.10f;
    [Range(0f, 0.35f)] public float EdgeAvoidance = 0.08f;
    [Min(0f)] public float MinClusterDistance = 0.22f;
    [Min(0f)] public float DepthJitter = 0.018f;
    [Range(0.01f, 1.25f)] public float MinLocalRadius = 0.08f;
    [Range(0.01f, 1.25f)] public float MaxLocalRadius = 0.965f;
    [Range(0.02f, 0.98f)] public float FrontDepthMin = 0.18f;
    [Range(0.02f, 0.99f)] public float FrontDepthMax = 0.92f;
    [Range(0.45f, 0.995f)] public float HemisphereCoverage = 0.985f;
    [Range(0.2f, 1.15f)] public float VerticalSpread = 1.00f;
    [Range(0.2f, 1.15f)] public float HorizontalSpread = 1.00f;
    [Min(0.01f)] public float MinArcLength = 0.18f;
    [Min(0.01f)] public float MaxArcLength = 0.46f;
    [Range(0f, 1f)] public float BranchChance = 0.48f;
    [Range(0.1f, 1.25f)] public float BranchLengthMultiplier = 0.52f;
    public int RandomSeed = 93019;

    [Header("Rendering")]
    [Range(0f, 1f)] public float CrackAlphaMin = 0.16f;
    [Range(0f, 1f)] public float CrackAlphaMax = 0.44f;
    [Min(0f)] public float CrackEmissionMin = 0.75f;
    [Min(0f)] public float CrackEmissionMax = 2.10f;
    [Range(0f, 2f)] public float IdleVisibility = 0.18f;
    [Range(0f, 2f)] public float ChargeVisibility = 1.18f;
    [Range(0f, 3f)] public float LaunchFlashVisibility = 1.75f;
    public Color crackColor = new Color(0.48f, 1f, 0.06f, 1f);
    [Min(0f)] public float emissionIntensity = 1.35f;
    [Min(0.001f)] public float minThickness = 0.0024f;
    [Min(0.001f)] public float maxThickness = 0.0095f;
    [Min(0.01f)] public float StateTransitionSpeed = 4.8f;

    [Header("Debug Validation")]
    public bool DebugVisiblePlacement;
    public Color DebugPlacementColor = Color.cyan;
    [Range(1f, 20f)] public float DebugThicknessMultiplier = 8f;
    [Min(1f)] public float DebugEmissionIntensity = 8f;
    public bool DrawDebugGizmos;
    public bool DrawDebugHemisphereGuide;
    public bool LogGenerationSummary = true;

    [SerializeField, HideInInspector] private Material crackMaterial;
    [SerializeField, HideInInspector] private Renderer targetRenderer;
    [SerializeField, HideInInspector] private Material debugPlacementMaterial;
    [SerializeField, HideInInspector] private List<Vector3> debugClusterCenters = new List<Vector3>();
    [SerializeField, HideInInspector] private List<Vector3> debugClusterAnchors = new List<Vector3>();
    [SerializeField, HideInInspector] private int debugSegmentCount;
    [SerializeField, HideInInspector] private int debugRejectedAnchorCount;
    [SerializeField, HideInInspector] private float debugMinPointRadius;
    [SerializeField, HideInInspector] private float debugMaxPointRadius;
    [SerializeField, HideInInspector] private Vector3 debugMinGeneratedLocal;
    [SerializeField, HideInInspector] private Vector3 debugMaxGeneratedLocal;
    [SerializeField, HideInInspector] private bool debugAnyPointExceedsShell;

    private const float GoldenAngle = 2.3999631f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");

    private Material fallbackMaterial;

    [ContextMenu("Regenerate Internal Cracks")]
    public void RegenerateCracks()
    {
        ClampSettings();
        ClearChildren();
        debugClusterCenters.Clear();
        debugClusterAnchors.Clear();
        debugSegmentCount = 0;
        debugRejectedAnchorCount = 0;
        debugMinPointRadius = float.MaxValue;
        debugMaxPointRadius = 0f;
        debugMinGeneratedLocal = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        debugMaxGeneratedLocal = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        debugAnyPointExceedsShell = false;
        ConfigurePulse(true);

        Material lineMaterial = ResolveMaterial();
        System.Random random = new System.Random(RandomSeed);
        float shellRadius = ResolveEffectiveGenerationRadius();
        float shellCoverageRadius = ResolveFullShellCoverageRadius(shellRadius);
        EffectiveProjectedRadius = shellCoverageRadius;
        EffectiveDiskHorizontalRadius = shellCoverageRadius;
        EffectiveDiskVerticalRadius = shellCoverageRadius;
        int clusterCount = Mathf.Clamp(ClusterCount, 1, 64);
        int targetCount = Mathf.Clamp(clusterCount * SegmentsPerCluster, clusterCount, 256);
        List<Vector3> shellNormals = GenerateShellNormals(clusterCount, random, shellRadius, shellCoverageRadius);

        List<CrackCluster> clusters = new List<CrackCluster>(clusterCount);
        int segmentIndex = 0;

        for (int i = 0; i < clusterCount; i++)
        {
            CrackCluster cluster = CreateCluster(i, random, shellNormals[i], shellRadius);
            clusters.Add(cluster);
            CreatePrimarySegment(cluster, lineMaterial, random, ref segmentIndex);
        }

        int cursor = 0;
        while (segmentIndex < targetCount)
        {
            CrackCluster cluster = clusters[(cursor * 11 + cursor / Mathf.Max(1, clusters.Count)) % clusters.Count];
            cursor++;

            float roll = Next01(random);

            if (roll < 0.22f)
            {
                CreateSparkSegment(cluster, lineMaterial, random, ref segmentIndex);
            }
            else if (cluster.PrimaryPaths.Count > 0 && roll < 0.22f + BranchChance)
            {
                CreateBranchSegment(cluster, lineMaterial, random, ref segmentIndex);
            }
            else
            {
                CreatePrimarySegment(cluster, lineMaterial, random, ref segmentIndex);
            }
        }

        debugSegmentCount = segmentIndex;
        LogGenerationStats();
        ConfigurePulse(true);
    }

    public void SetCrackMaterial(Material material)
    {
        crackMaterial = material;
    }

    public void SetTargetRenderer(Renderer renderer)
    {
        targetRenderer = renderer;
        RefreshEffectiveRadiusReadout();
        ClampSettings();
    }

    public void SetIdle()
    {
        SuperBallCrackPulse pulse = ResolvePulseForState();
        if (pulse != null)
        {
            pulse.SetIdle();
        }
    }

    public void SetRecovery()
    {
        SetIdle();
    }

    public void SetCharge(float progress)
    {
        SuperBallCrackPulse pulse = ResolvePulseForState();
        if (pulse != null)
        {
            pulse.SetCharge(progress);
        }
    }

    public void Flash(float intensity)
    {
        SuperBallCrackPulse pulse = ResolvePulseForState();
        if (pulse != null)
        {
            pulse.Flash(intensity);
        }
    }

    private void Awake()
    {
        if (Application.isPlaying && transform.childCount == 0)
        {
            RegenerateCracks();
        }
    }

    private void OnEnable()
    {
        ConfigurePulse(true);

        if (!Application.isPlaying && transform.childCount == 0)
        {
            RegenerateCracks();
        }
    }

    private void OnValidate()
    {
        ClampSettings();
        RefreshEffectiveRadiusReadout();
        ConfigurePulse(false);
    }

    private SuperBallCrackPulse ResolvePulseForState()
    {
        ConfigurePulse(true);
        return GetComponent<SuperBallCrackPulse>();
    }

    private void ClampSettings()
    {
        ClusterCount = Mathf.Clamp(ClusterCount, 1, 64);
        SegmentsPerCluster = Mathf.Clamp(SegmentsPerCluster, 1, 10);
        FrontHemisphereClusterShare = Mathf.Clamp01(FrontHemisphereClusterShare);
        ManualShellRadius = Mathf.Clamp(ManualShellRadius, 0.01f, 1.25f);
        SphereRadius = Mathf.Max(0.01f, SphereRadius);
        EffectiveSphereRadius = Mathf.Max(0.01f, EffectiveSphereRadius);
        EffectiveGenerationRadius = Mathf.Max(0.01f, EffectiveGenerationRadius);
        EffectiveProjectedRadius = Mathf.Max(0.01f, EffectiveProjectedRadius);
        EffectiveRadiusDebug = Mathf.Max(0.01f, EffectiveRadiusDebug);
        EffectiveDiskHorizontalRadius = Mathf.Max(0.01f, EffectiveDiskHorizontalRadius);
        EffectiveDiskVerticalRadius = Mathf.Max(0.01f, EffectiveDiskVerticalRadius);
        RendererBoundsRadius = Mathf.Max(0f, RendererBoundsRadius);
        RendererLocalRawRadius = Mathf.Max(0f, RendererLocalRawRadius);
        RendererAutoRadius = Mathf.Max(0f, RendererAutoRadius);
        float autoLimitRadius = Mathf.Max(SphereRadius, Mathf.Max(EffectiveSphereRadius, RendererLocalRawRadius));
        float limitRadius = UseManualShellRadius ? ManualShellRadius : (AutoDeriveSphereRadius ? autoLimitRadius : SphereRadius);
        SurfaceInset = Mathf.Clamp(SurfaceInset, 0f, limitRadius - 0.01f);
        float fullShellLimit = Mathf.Max(0.01f, limitRadius - SurfaceInset);
        FullShellCoverageRadius = Mathf.Clamp(FullShellCoverageRadius, 0.01f, fullShellLimit);
        EffectiveFullShellCoverageRadius = Mathf.Max(0.01f, EffectiveFullShellCoverageRadius);
        DiskHorizontalCoverage = Mathf.Clamp(DiskHorizontalCoverage, 0.1f, 0.98f);
        DiskVerticalCoverage = Mathf.Clamp(DiskVerticalCoverage, 0.1f, 0.98f);
        CenterAvoidance = Mathf.Clamp(CenterAvoidance, 0f, 0.5f);
        EdgeAvoidance = Mathf.Clamp(EdgeAvoidance, 0f, 0.35f);
        MinClusterDistance = Mathf.Max(0f, MinClusterDistance);
        DepthJitter = Mathf.Max(0f, DepthJitter);
        MaxLocalRadius = Mathf.Clamp(MaxLocalRadius, 0.01f, limitRadius - SurfaceInset);
        MinLocalRadius = Mathf.Clamp(MinLocalRadius, 0.01f, MaxLocalRadius);
        FrontDepthMin = Mathf.Clamp(FrontDepthMin, 0.02f, 0.98f);
        FrontDepthMax = Mathf.Clamp(FrontDepthMax, FrontDepthMin + 0.01f, 0.99f);
        HemisphereCoverage = Mathf.Clamp(HemisphereCoverage, 0.45f, 0.995f);
        VerticalSpread = Mathf.Clamp(VerticalSpread, 0.2f, 1.15f);
        HorizontalSpread = Mathf.Clamp(HorizontalSpread, 0.2f, 1.15f);
        MinArcLength = Mathf.Max(0.01f, MinArcLength);
        MaxArcLength = Mathf.Max(MinArcLength, MaxArcLength);
        BranchChance = Mathf.Clamp01(BranchChance);
        BranchLengthMultiplier = Mathf.Clamp(BranchLengthMultiplier, 0.1f, 1.25f);
        CrackAlphaMin = Mathf.Clamp01(CrackAlphaMin);
        CrackAlphaMax = Mathf.Clamp(CrackAlphaMax, CrackAlphaMin, 1f);
        CrackEmissionMin = Mathf.Max(0f, CrackEmissionMin);
        CrackEmissionMax = Mathf.Max(CrackEmissionMin, CrackEmissionMax);
        IdleVisibility = Mathf.Clamp(IdleVisibility, 0f, 2f);
        ChargeVisibility = Mathf.Clamp(ChargeVisibility, 0f, 2f);
        LaunchFlashVisibility = Mathf.Clamp(LaunchFlashVisibility, 0f, 3f);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        minThickness = Mathf.Max(0.001f, minThickness);
        maxThickness = Mathf.Max(minThickness, maxThickness);
        StateTransitionSpeed = Mathf.Max(0.01f, StateTransitionSpeed);
        DebugThicknessMultiplier = Mathf.Clamp(DebugThicknessMultiplier, 1f, 20f);
        DebugEmissionIntensity = Mathf.Max(1f, DebugEmissionIntensity);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            child.SetParent(null, false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
                continue;
            }
#endif

            Destroy(child.gameObject);
        }
    }

    private CrackCluster CreateCluster(int index, System.Random random, Vector3 normal, float shellRadius)
    {
        GameObject clusterObject = new GameObject(string.Format("SuperBall_CrackCluster_{0:00}", index + 1));
        clusterObject.transform.SetParent(transform, false);
        clusterObject.transform.localPosition = Vector3.zero;
        clusterObject.transform.localRotation = Quaternion.identity;
        clusterObject.transform.localScale = Vector3.one;

        normal = normal.sqrMagnitude < 0.0001f ? Vector3.up : normal.normalized;
        float depthOffset = RandomRange(random, 0f, DepthJitter);
        float clusterRadius = Mathf.Max(0.01f, shellRadius - depthOffset);
        Vector3 localCenter = normal * clusterRadius;
        Vector3 tangent;
        Vector3 bitangent;
        BuildTangentBasis(normal, out tangent, out bitangent);

        debugClusterAnchors.Add(normal);
        debugClusterCenters.Add(localCenter);
        return new CrackCluster(clusterObject.transform, normal, tangent, bitangent, clusterRadius, localCenter);
    }

    private void CreatePrimarySegment(CrackCluster cluster, Material lineMaterial, System.Random random, ref int segmentIndex)
    {
        Vector2 start = RandomInsideUnitCircle(random) * RandomRange(random, 0.006f, 0.024f);
        Vector2 tangent = RandomInsideUnitCircle(random).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector2.right;
        }

        float length = RandomRange(random, MinArcLength, MaxArcLength) * RandomRange(random, 0.78f, 1.16f);
        int pointCount = random.Next(3, 6);
        Vector3[] points = BuildVeinPath(cluster, start, tangent, length, pointCount, false, random);

        cluster.PrimaryPaths.Add(points);
        CreateLineObject(cluster.Root, points, lineMaterial, random, ref segmentIndex, CrackSegmentKind.Primary);
    }

    private void CreateBranchSegment(CrackCluster cluster, Material lineMaterial, System.Random random, ref int segmentIndex)
    {
        Vector3[] sourcePath = cluster.PrimaryPaths[random.Next(cluster.PrimaryPaths.Count)];
        int sourceIndex = Mathf.Clamp(random.Next(1, sourcePath.Length - 1), 1, sourcePath.Length - 2);
        Vector2 start = ShellPointToTangent(cluster, sourcePath[sourceIndex]);
        Vector2 sourceDelta = ShellPointToTangent(cluster, sourcePath[sourceIndex + 1]) - ShellPointToTangent(cluster, sourcePath[sourceIndex - 1]);

        if (sourceDelta.sqrMagnitude < 0.0001f)
        {
            sourceDelta = RandomInsideUnitCircle(random).normalized;
        }

        float branchAngle = (Next01(random) < 0.5f ? -1f : 1f) * RandomRange(random, 25f, 70f);
        Vector2 tangent = Rotate2D(sourceDelta.normalized, branchAngle);
        float length = RandomRange(random, MinArcLength * 0.50f, MaxArcLength * 0.92f) * BranchLengthMultiplier;
        int pointCount = random.Next(2, 5);
        Vector3[] points = BuildVeinPath(cluster, start, tangent, length, pointCount, true, random);

        points[0] = sourcePath[sourceIndex];
        CreateLineObject(cluster.Root, points, lineMaterial, random, ref segmentIndex, CrackSegmentKind.Branch);
    }

    private void CreateSparkSegment(CrackCluster cluster, Material lineMaterial, System.Random random, ref int segmentIndex)
    {
        Vector2 start = RandomInsideUnitCircle(random) * RandomRange(random, 0.010f, 0.032f);
        Vector2 tangent = RandomInsideUnitCircle(random).normalized;
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector2.up;
        }

        float length = RandomRange(random, MinArcLength * 0.20f, MinArcLength * 0.62f);
        int pointCount = random.Next(2, 4);
        Vector3[] points = BuildVeinPath(cluster, start, tangent, length, pointCount, false, random);

        CreateLineObject(cluster.Root, points, lineMaterial, random, ref segmentIndex, CrackSegmentKind.Spark);
    }

    private void CreateLineObject(Transform parent, Vector3[] points, Material lineMaterial, System.Random random, ref int segmentIndex, CrackSegmentKind kind)
    {
        segmentIndex++;

        GameObject segmentObject = new GameObject(string.Format("SuperBall_CrackSegment_{0:000}", segmentIndex));
        segmentObject.transform.SetParent(parent, false);
        segmentObject.transform.localPosition = Vector3.zero;
        segmentObject.transform.localRotation = Quaternion.identity;
        segmentObject.transform.localScale = Vector3.one;

        LineRenderer line = segmentObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = lineMaterial;
        line.positionCount = points.Length;
        line.SetPositions(points);
        for (int i = 0; i < points.Length; i++)
        {
            RecordGeneratedPoint(points[i]);
        }

        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        line.sortingOrder = DebugVisiblePlacement ? 100 : 0;

        float thickness = RandomRange(random, minThickness, maxThickness);
        if (kind == CrackSegmentKind.Branch)
        {
            thickness *= RandomRange(random, 0.54f, 0.78f);
        }
        else if (kind == CrackSegmentKind.Spark)
        {
            thickness *= RandomRange(random, 0.34f, 0.58f);
        }

        if (DebugVisiblePlacement)
        {
            thickness = Mathf.Max(thickness * DebugThicknessMultiplier, maxThickness * 2.6f);
        }

        line.widthMultiplier = thickness;
        line.widthCurve = DebugVisiblePlacement
            ? AnimationCurve.Linear(0f, 1f, 1f, 1f)
            : new AnimationCurve(
                new Keyframe(0f, RandomRange(random, 0.26f, 0.58f)),
                new Keyframe(0.46f, RandomRange(random, 0.72f, 1.08f)),
                new Keyframe(1f, RandomRange(random, 0.22f, 0.58f)));

        Color veinColor = DebugVisiblePlacement ? DebugPlacementColor : RandomVeinColor(random);
        float alphaBase = DebugVisiblePlacement ? 1f : RandomRange(random, CrackAlphaMin, CrackAlphaMax);
        float emission = DebugVisiblePlacement ? 1f : RandomRange(random, CrackEmissionMin, CrackEmissionMax);

        if (!DebugVisiblePlacement && kind == CrackSegmentKind.Branch)
        {
            alphaBase *= RandomRange(random, 0.60f, 0.82f);
            emission *= RandomRange(random, 0.68f, 0.92f);
        }
        else if (!DebugVisiblePlacement && kind == CrackSegmentKind.Spark)
        {
            alphaBase *= RandomRange(random, 0.36f, 0.60f);
            emission *= RandomRange(random, 0.92f, 1.28f);
        }

        Color dimColor = DebugVisiblePlacement
            ? ScaleColor(veinColor, 1.0f, 1f)
            : ScaleColor(veinColor, emission * 0.62f, alphaBase * 0.65f);
        Color hotColor = DebugVisiblePlacement
            ? ScaleColor(veinColor, 1.35f, 1f)
            : ScaleColor(veinColor, emission * 1.08f, alphaBase);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(dimColor, 0f),
                new GradientColorKey(hotColor, RandomRange(random, 0.30f, 0.70f)),
                new GradientColorKey(dimColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(dimColor.a, 0f),
                new GradientAlphaKey(hotColor.a, 0.5f),
                new GradientAlphaKey(dimColor.a, 1f)
            });

        line.colorGradient = gradient;
    }

    private void RecordGeneratedPoint(Vector3 point)
    {
        debugMinGeneratedLocal = Vector3.Min(debugMinGeneratedLocal, point);
        debugMaxGeneratedLocal = Vector3.Max(debugMaxGeneratedLocal, point);

        float pointRadius = point.magnitude;
        debugMinPointRadius = Mathf.Min(debugMinPointRadius, pointRadius);
        debugMaxPointRadius = Mathf.Max(debugMaxPointRadius, pointRadius);

        if (pointRadius > EffectiveGenerationRadius + 0.0005f)
        {
            debugAnyPointExceedsShell = true;
        }
    }

    private Vector3[] BuildVeinPath(CrackCluster cluster, Vector2 start, Vector2 tangent, float length, int pointCount, bool forwardOnly, System.Random random)
    {
        tangent = tangent.sqrMagnitude < 0.0001f ? Vector2.right : tangent.normalized;
        Vector2 side = new Vector2(-tangent.y, tangent.x);
        float kink = RandomRange(random, 0.006f, 0.020f);
        float drift = RandomRange(random, -0.006f, 0.006f);
        Vector3[] points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount == 1 ? 0f : i / (pointCount - 1f);
            float signedT = forwardOnly ? t : t - 0.5f;
            float fade = Mathf.Sin(t * Mathf.PI);
            float zigzag = 0f;
            if (i > 0 && i < pointCount - 1)
            {
                zigzag = (i % 2 == 0 ? -1f : 1f) * RandomRange(random, kink * 0.42f, kink);
                zigzag += RandomRange(random, -0.003f, 0.003f);
            }

            float forwardKink = i > 0 && i < pointCount - 1 ? RandomRange(random, -0.010f, 0.010f) * fade : 0f;
            Vector2 disk = start + tangent * (signedT * length + forwardKink) + side * (zigzag + drift * signedT);
            float inwardJitter = RandomRange(random, 0f, DepthJitter * 0.18f) * fade;
            points[i] = TangentOffsetToShellPoint(cluster, disk, inwardJitter);
        }

        return points;
    }

    private Vector3 TangentOffsetToShellPoint(CrackCluster cluster, Vector2 offset, float inwardJitter)
    {
        float pointRadius = Mathf.Clamp(cluster.Radius - inwardJitter, 0.01f, EffectiveGenerationRadius);
        float arcDistance = offset.magnitude;

        if (arcDistance < 0.0001f)
        {
            return cluster.Normal * pointRadius;
        }

        Vector3 tangentDirection = (cluster.Tangent * offset.x + cluster.Bitangent * offset.y).normalized;
        float angle = arcDistance / Mathf.Max(0.01f, pointRadius);
        Vector3 shellDirection = (cluster.Normal * Mathf.Cos(angle) + tangentDirection * Mathf.Sin(angle)).normalized;
        return shellDirection * pointRadius;
    }

    private static Vector2 ShellPointToTangent(CrackCluster cluster, Vector3 point)
    {
        Vector3 offset = point - cluster.Center;
        return new Vector2(Vector3.Dot(offset, cluster.Tangent), Vector3.Dot(offset, cluster.Bitangent));
    }

    private List<Vector3> GenerateShellNormals(int count, System.Random random, float shellRadius, float shellCoverageRadius)
    {
        List<Vector3> normals = new List<Vector3>(count);
        int frontHemisphereTarget = DistributionMode == SuperBallCrackDistributionMode.FullSphereShell
            ? Mathf.Clamp(Mathf.RoundToInt(count * FrontHemisphereClusterShare), 0, count)
            : count;
        float frontAzimuthOffset = RandomRange(random, 0f, 360f);
        float rearAzimuthOffset = RandomRange(random, 0f, 360f);
        float minDistance = Mathf.Max(0f, MinClusterDistance);

        for (int i = 0; i < count; i++)
        {
            Vector3 bestNormal = Vector3.up;
            float bestDistance = -1f;
            bool accepted = false;

            for (int attempt = 0; attempt < 18; attempt++)
            {
                Vector3 candidate = GenerateShellCandidate(
                    i,
                    count,
                    attempt,
                    random,
                    shellRadius,
                    shellCoverageRadius,
                    frontHemisphereTarget,
                    frontAzimuthOffset,
                    rearAzimuthOffset);
                float nearestDistance = NearestAnchorDistance(candidate, normals, shellRadius);

                if (nearestDistance > bestDistance)
                {
                    bestDistance = nearestDistance;
                    bestNormal = candidate;
                }

                if (nearestDistance >= minDistance || normals.Count == 0)
                {
                    normals.Add(candidate);
                    accepted = true;
                    break;
                }

                debugRejectedAnchorCount++;
            }

            if (!accepted)
            {
                normals.Add(bestNormal);
            }
        }

        Shuffle(normals, random);
        return normals;
    }

    private Vector3 GenerateShellCandidate(
        int index,
        int count,
        int attempt,
        System.Random random,
        float shellRadius,
        float shellCoverageRadius,
        int frontHemisphereTarget,
        float frontAzimuthOffset,
        float rearAzimuthOffset)
    {
        switch (DistributionMode)
        {
            case SuperBallCrackDistributionMode.FrontHemisphereShell:
                return GenerateProjectedShellNormal(index, count, true, shellRadius, shellCoverageRadius, frontAzimuthOffset, random, attempt);
            case SuperBallCrackDistributionMode.ViewFacingDisk:
                return GenerateViewFacingDiskNormal(index, count, random);
            default:
                return GenerateFrontWeightedShellNormal(
                    index,
                    count,
                    frontHemisphereTarget,
                    shellRadius,
                    shellCoverageRadius,
                    frontAzimuthOffset,
                    rearAzimuthOffset,
                    random,
                    attempt);
        }
    }

    private Vector3 GenerateFrontWeightedShellNormal(
        int index,
        int count,
        int frontHemisphereTarget,
        float shellRadius,
        float shellCoverageRadius,
        float frontAzimuthOffset,
        float rearAzimuthOffset,
        System.Random random,
        int attempt)
    {
        if (frontHemisphereTarget <= 0)
        {
            return GenerateProjectedShellNormal(index, count, false, shellRadius, shellCoverageRadius, rearAzimuthOffset, random, attempt);
        }

        if (frontHemisphereTarget >= count)
        {
            return GenerateProjectedShellNormal(index, count, true, shellRadius, shellCoverageRadius, frontAzimuthOffset, random, attempt);
        }

        if (index < frontHemisphereTarget)
        {
            return GenerateProjectedShellNormal(index, frontHemisphereTarget, true, shellRadius, shellCoverageRadius, frontAzimuthOffset, random, attempt);
        }

        int rearIndex = index - frontHemisphereTarget;
        int rearCount = Mathf.Max(1, count - frontHemisphereTarget);
        return GenerateProjectedShellNormal(rearIndex, rearCount, false, shellRadius, shellCoverageRadius, rearAzimuthOffset, random, attempt);
    }

    private Vector3 GenerateProjectedShellNormal(
        int index,
        int count,
        bool frontHemisphere,
        float shellRadius,
        float shellCoverageRadius,
        float azimuthOffset,
        System.Random random,
        int attempt)
    {
        int radialBands;
        int angularSectors;
        ResolveShellProjectionGrid(count, out radialBands, out angularSectors);

        int band = index % radialBands;
        int sector = (index / radialBands) % angularSectors;
        Vector2 disk = GetFullShellCoverageCandidate(
            band,
            sector,
            radialBands,
            angularSectors,
            random,
            shellCoverageRadius,
            attempt);
        disk = Rotate2D(disk, azimuthOffset);
        if (frontHemisphere)
        {
            disk = ApplyFaceSupportBias(disk, index, count, shellCoverageRadius, random);
        }

        disk = ClampProjectedCircle(disk, shellCoverageRadius);

        float safeShellRadius = Mathf.Max(0.01f, shellRadius);
        float zMagnitude = Mathf.Sqrt(Mathf.Max(0.0001f, safeShellRadius * safeShellRadius - disk.sqrMagnitude));
        float z = frontHemisphere ? -zMagnitude : zMagnitude;
        return new Vector3(disk.x, disk.y, z).normalized;
    }

    private static Vector2 ApplyFaceSupportBias(Vector2 disk, int index, int count, float radius, System.Random random)
    {
        float supportShare = Mathf.Clamp01(0.62f - index / Mathf.Max(1f, count) * 0.18f);
        if (Next01(random) > supportShare)
        {
            return disk;
        }

        Vector2[] anchors =
        {
            new Vector2(-0.36f, 0.34f),
            new Vector2(0.36f, 0.34f),
            new Vector2(-0.50f, 0.02f),
            new Vector2(0.50f, 0.02f),
            new Vector2(-0.28f, -0.34f),
            new Vector2(0.28f, -0.34f),
        };

        Vector2 anchor = anchors[index % anchors.Length] * radius;
        anchor += RandomInsideUnitCircle(random) * radius * 0.10f;
        float blend = RandomRange(random, 0.36f, 0.68f);
        return Vector2.Lerp(disk, anchor, blend);
    }

    private Vector3 GenerateViewFacingDiskNormal(int index, int count, System.Random random)
    {
        int radialBands;
        int angularSectors;
        ResolveFrontDiskGrid(count, out radialBands, out angularSectors);
        int band = index % radialBands;
        int sector = (index / radialBands) % angularSectors;
        Vector2 radii = ResolveVisibleDiskRadii(EffectiveGenerationRadius);
        Vector2 disk = GetFrontDiskCandidate(band, sector, radialBands, angularSectors, random, EffectiveGenerationRadius, radii);
        float z = -Mathf.Sqrt(Mathf.Max(0.0001f, EffectiveGenerationRadius * EffectiveGenerationRadius - disk.sqrMagnitude));
        return new Vector3(disk.x, disk.y, z).normalized;
    }

    private static Vector3 FibonacciSphereDirection(int index, int count)
    {
        float t = (index + 0.5f) / Mathf.Max(1, count);
        float y = 1f - 2f * t;
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        float angle = index * GoldenAngle;
        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius).normalized;
    }

    private static Vector3 FibonacciHemisphereDirection(int index, int count, Vector3 pole)
    {
        float t = (index + 0.5f) / Mathf.Max(1, count);
        float z = Mathf.Lerp(0.04f, 1f, t);
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        float angle = index * GoldenAngle;
        Vector3 local = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, pole.normalized);
        return (rotation * local).normalized;
    }

    private static Vector3 JitterNormal(Vector3 normal, System.Random random, int attempt)
    {
        float maxAngle = Mathf.Lerp(3f, 13f, Mathf.Clamp01(attempt / 17f)) * Mathf.Deg2Rad;
        Vector3 tangent;
        Vector3 bitangent;
        BuildTangentBasis(normal, out tangent, out bitangent);
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float amount = RandomRange(random, 0f, maxAngle);
        Vector3 sideways = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
        return (normal * Mathf.Cos(amount) + sideways * Mathf.Sin(amount)).normalized;
    }

    private void ResolveFrontDiskGrid(int count, out int radialBands, out int angularSectors)
    {
        radialBands = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * 0.52f)), 3, 8);
        angularSectors = Mathf.Max(6, Mathf.CeilToInt(count / (float)radialBands));

        while (radialBands * angularSectors < count)
        {
            angularSectors++;
        }
    }

    private void ResolveShellProjectionGrid(int count, out int radialBands, out int angularSectors)
    {
        radialBands = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * 0.62f)), 3, 8);
        angularSectors = Mathf.Max(8, Mathf.CeilToInt(count / (float)radialBands));

        while (radialBands * angularSectors < count)
        {
            angularSectors++;
        }
    }

    private Vector2 GetFullShellCoverageCandidate(
        int band,
        int sector,
        int radialBands,
        int angularSectors,
        System.Random random,
        float coverageRadius,
        int attempt)
    {
        float safeCoverageRadius = Mathf.Max(0.01f, coverageRadius);
        float innerNormalized = Mathf.Clamp(CenterAvoidance, 0f, 0.45f);
        float outerNormalized = 0.998f;
        float innerArea = innerNormalized * innerNormalized;
        float outerArea = outerNormalized * outerNormalized;
        float bandStart = band / (float)radialBands;
        float bandEnd = (band + 1f) / radialBands;
        float areaT = Mathf.Lerp(innerArea, outerArea, RandomRange(random, bandStart, bandEnd));

        if (band == radialBands - 1)
        {
            areaT = Mathf.Lerp(Mathf.Max(areaT, outerArea * 0.86f), outerArea, RandomRange(random, 0.55f, 0.98f));
        }

        float radiusNormalized = Mathf.Sqrt(Mathf.Clamp01(areaT));
        float sectorWidth = (Mathf.PI * 2f) / angularSectors;
        float angle = (sector + RandomRange(random, 0.12f, 0.88f)) * sectorWidth;
        float attemptJitter = Mathf.Min(0.22f, attempt * 0.018f);
        angle += RandomRange(random, -sectorWidth * (0.10f + attemptJitter), sectorWidth * (0.10f + attemptJitter));

        float radialJitter = RandomRange(random, -0.018f, 0.018f) * Mathf.Clamp01(attempt / 6f);
        radiusNormalized = Mathf.Clamp(radiusNormalized + radialJitter, innerNormalized, outerNormalized);

        Vector2 disk = new Vector2(
            Mathf.Cos(angle) * radiusNormalized * safeCoverageRadius,
            Mathf.Sin(angle) * radiusNormalized * safeCoverageRadius);

        return ClampProjectedCircle(disk, safeCoverageRadius);
    }

    private Vector2 GetFrontDiskCandidate(int band, int sector, int radialBands, int angularSectors, System.Random random, float shellRadius, Vector2 visibleDiskRadii)
    {
        float minDiskRadius = Mathf.Max(0.01f, Mathf.Min(visibleDiskRadii.x, visibleDiskRadii.y));
        float innerNormalized = Mathf.Clamp01(Mathf.Max(CenterAvoidance, MinLocalRadius / minDiskRadius));
        float outerNormalized = Mathf.Clamp01(1f - EdgeAvoidance);
        outerNormalized = Mathf.Clamp01(Mathf.Max(innerNormalized + 0.03f, outerNormalized));
        float innerArea = innerNormalized * innerNormalized;
        float outerArea = outerNormalized * outerNormalized;
        float bandStart = band / (float)radialBands;
        float bandEnd = (band + 1f) / radialBands;
        float areaT = Mathf.Lerp(innerArea, outerArea, RandomRange(random, bandStart, bandEnd));
        float radiusNormalized = Mathf.Sqrt(Mathf.Clamp01(areaT));
        float sectorWidth = (Mathf.PI * 2f) / angularSectors;
        float angle = (sector + RandomRange(random, 0.16f, 0.84f)) * sectorWidth;
        angle += RandomRange(random, -sectorWidth * 0.10f, sectorWidth * 0.10f);

        Vector2 disk = new Vector2(
            Mathf.Cos(angle) * radiusNormalized * visibleDiskRadii.x,
            Mathf.Sin(angle) * radiusNormalized * visibleDiskRadii.y);

        return ClampProjectedEllipse(disk, visibleDiskRadii);
    }

    private Vector2 ClampProjectedEllipse(Vector2 disk, Vector2 radii)
    {
        float radiusX = Mathf.Max(0.01f, radii.x);
        float radiusY = Mathf.Max(0.01f, radii.y);
        Vector2 normalized = new Vector2(disk.x / radiusX, disk.y / radiusY);

        if (normalized.sqrMagnitude <= 1f)
        {
            return disk;
        }

        normalized.Normalize();
        return new Vector2(normalized.x * radiusX, normalized.y * radiusY);
    }

    private static Vector2 ClampProjectedCircle(Vector2 disk, float radius)
    {
        float safeRadius = Mathf.Max(0.01f, radius);

        if (disk.sqrMagnitude <= safeRadius * safeRadius)
        {
            return disk;
        }

        return disk.normalized * safeRadius;
    }

    private float ResolveEffectiveGenerationRadius()
    {
        return RefreshEffectiveRadiusReadout();
    }

    private float ResolveFullShellCoverageRadius(float shellRadius)
    {
        float safeShellRadius = Mathf.Max(0.01f, shellRadius);
        FullShellCoverageRadius = Mathf.Clamp(FullShellCoverageRadius, 0.01f, safeShellRadius);
        EffectiveFullShellCoverageRadius = FullShellCoverageRadius;
        return EffectiveFullShellCoverageRadius;
    }

    private Vector2 ResolveVisibleDiskRadii(float shellRadius)
    {
        if (DistributionMode != SuperBallCrackDistributionMode.ViewFacingDisk)
        {
            float radius = Mathf.Max(0.01f, shellRadius);
            EffectiveDiskHorizontalRadius = radius;
            EffectiveDiskVerticalRadius = radius;
            return new Vector2(radius, radius);
        }

        float safeRadius = Mathf.Max(0.01f, shellRadius);
        float horizontalRadius = Mathf.Clamp(safeRadius * DiskHorizontalCoverage, 0.01f, safeRadius * 0.99f);
        float verticalRadius = Mathf.Clamp(safeRadius * DiskVerticalCoverage, 0.01f, safeRadius * 0.99f);
        EffectiveDiskHorizontalRadius = horizontalRadius;
        EffectiveDiskVerticalRadius = verticalRadius;
        return new Vector2(horizontalRadius, verticalRadius);
    }

    private float RefreshEffectiveRadiusReadout()
    {
        Renderer resolvedRenderer = targetRenderer != null ? targetRenderer : GetComponentInParent<Renderer>();
        RefreshRendererBoundsReadout(resolvedRenderer);
        RendererLocalRawRadius = CalculateLocalMeshRadius(resolvedRenderer, transform, false);
        float rendererRadius = AutoDeriveSphereRadius ? CalculateLocalMeshRadius(resolvedRenderer, transform, NormalizeUnitSphereMeshRadius) : 0f;

        if (rendererRadius <= 0.01f)
        {
            rendererRadius = SphereRadius;
        }

        RendererAutoRadius = Mathf.Max(0.01f, rendererRadius);
        if (UseManualShellRadius)
        {
            EffectiveSphereRadius = Mathf.Max(0.01f, ManualShellRadius);
            ShellRadiusSource = "ManualShellRadius";
        }
        else
        {
            EffectiveSphereRadius = RendererAutoRadius;
            ShellRadiusSource = AutoDeriveSphereRadius
                ? (NormalizeUnitSphereMeshRadius ? "AutoDerivedRendererNormalized" : "AutoDerivedRendererRaw")
                : "SphereRadius";
        }

        EffectiveGenerationRadius = Mathf.Max(0.01f, EffectiveSphereRadius - SurfaceInset);
        ResolveFullShellCoverageRadius(EffectiveGenerationRadius);
        EffectiveRadiusDebug = EffectiveGenerationRadius;
        Vector2 diskRadii = DistributionMode == SuperBallCrackDistributionMode.ViewFacingDisk
            ? ResolveVisibleDiskRadii(EffectiveGenerationRadius)
            : new Vector2(EffectiveFullShellCoverageRadius, EffectiveFullShellCoverageRadius);
        EffectiveProjectedRadius = Mathf.Max(diskRadii.x, diskRadii.y);
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

    private Color RandomVeinColor(System.Random random)
    {
        Color toxicGreen = new Color(0.02f, 0.62f, 0.16f, 1f);
        Color neonGreen = new Color(0.12f, 0.95f, 0.08f, 1f);
        Color yellowGreen = new Color(0.42f, 1.00f, 0.10f, 1f);

        float roll = Next01(random);
        if (roll < 0.50f)
        {
            return Color.Lerp(toxicGreen, neonGreen, Next01(random));
        }

        if (roll < 0.86f)
        {
            return Color.Lerp(neonGreen, yellowGreen, RandomRange(random, 0.04f, 0.55f));
        }

        return Color.Lerp(toxicGreen, yellowGreen, RandomRange(random, 0.10f, 0.34f));
    }

    private void ConfigurePulse(bool createIfMissing)
    {
        SuperBallCrackPulse pulse = GetComponent<SuperBallCrackPulse>();
        if (pulse == null)
        {
            if (!createIfMissing)
            {
                return;
            }

            pulse = gameObject.AddComponent<SuperBallCrackPulse>();
        }

        pulse.enabled = !DebugVisiblePlacement;
        pulse.idleVisibility = IdleVisibility;
        pulse.chargeVisibility = ChargeVisibility;
        pulse.launchFlashVisibility = LaunchFlashVisibility;
        pulse.idleEmission = Mathf.Max(0.04f, emissionIntensity * 0.58f);
        pulse.chargeEmission = Mathf.Max(pulse.idleEmission, emissionIntensity * 1.55f);
        pulse.flashEmission = Mathf.Max(pulse.chargeEmission, emissionIntensity * 2.55f);
        pulse.idleFlickerSpeed = 0.75f;
        pulse.chargeFlickerSpeed = 4.80f;
        pulse.flashDecaySpeed = 5.5f;
        pulse.stateTransitionSpeed = StateTransitionSpeed;

        if (DebugVisiblePlacement)
        {
            ClearLinePropertyBlocks();
        }
    }

    private Material ResolveMaterial()
    {
        if (DebugVisiblePlacement)
        {
            return ResolveDebugPlacementMaterial();
        }

        if (crackMaterial != null)
        {
            ApplyMaterialSettings(crackMaterial);
            return crackMaterial;
        }

        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("REPO/SuperBallCrackEmission");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            fallbackMaterial = new Material(shader)
            {
                name = "M_SuperBall_CrackGlow_Runtime",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        ApplyMaterialSettings(fallbackMaterial);
        return fallbackMaterial;
    }

    private Material ResolveDebugPlacementMaterial()
    {
        if (debugPlacementMaterial == null)
        {
            Shader shader = Shader.Find("REPO/SuperBallCrackEmission");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            debugPlacementMaterial = new Material(shader)
            {
                name = "M_SuperBall_CrackGlow_DebugPlacement",
                hideFlags = HideFlags.HideInHierarchy
            };
        }

        ApplyDebugPlacementMaterialSettings(debugPlacementMaterial);
        return debugPlacementMaterial;
    }

    private void ApplyMaterialSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        bool changed = false;
        if (material.HasProperty(ColorId))
        {
            Color currentColor = material.GetColor(ColorId);
            if (currentColor != crackColor)
            {
                material.SetColor(ColorId, crackColor);
                changed = true;
            }
        }

        if (material.HasProperty(EmissionIntensityId))
        {
            float currentEmission = material.GetFloat(EmissionIntensityId);
            if (!Mathf.Approximately(currentEmission, emissionIntensity))
            {
                material.SetFloat(EmissionIntensityId, emissionIntensity);
                changed = true;
            }
        }

        if (material.HasProperty(VisibilityId))
        {
            float currentVisibility = material.GetFloat(VisibilityId);
            if (!Mathf.Approximately(currentVisibility, IdleVisibility))
            {
                material.SetFloat(VisibilityId, IdleVisibility);
                changed = true;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && changed)
        {
            EditorUtility.SetDirty(material);
        }
#endif
    }

    private void ApplyDebugPlacementMaterialSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        bool changed = false;
        if (material.HasProperty(ColorId))
        {
            Color currentColor = material.GetColor(ColorId);
            if (currentColor != Color.white)
            {
                material.SetColor(ColorId, Color.white);
                changed = true;
            }
        }

        if (material.HasProperty(EmissionIntensityId))
        {
            float currentEmission = material.GetFloat(EmissionIntensityId);
            if (!Mathf.Approximately(currentEmission, DebugEmissionIntensity))
            {
                material.SetFloat(EmissionIntensityId, DebugEmissionIntensity);
                changed = true;
            }
        }

        if (material.HasProperty(VisibilityId))
        {
            float currentVisibility = material.GetFloat(VisibilityId);
            if (!Mathf.Approximately(currentVisibility, 1f))
            {
                material.SetFloat(VisibilityId, 1f);
                changed = true;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && changed)
        {
            EditorUtility.SetDirty(material);
        }
#endif
    }

    private void ClearLinePropertyBlocks()
    {
        List<LineRenderer> lines = new List<LineRenderer>();
        GetComponentsInChildren(true, lines);

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null)
            {
                lines[i].SetPropertyBlock(null);
            }
        }
    }

    private void LogGenerationStats()
    {
        if (!LogGenerationSummary)
        {
            return;
        }

        int centerCount = debugClusterCenters.Count;
        if (centerCount == 0)
        {
            Debug.Log(string.Format(
                "SuperBallInternalCracks generated: mode={0}, clusterCenters=0, lineSegments={1}, rendererBoundsSize={2}, rendererBoundsRadius={3:F4}, rendererLocalRawRadius={4:F4}, shellRadiusSource={5}, selectedShellRadius={6:F4}, effectiveGenerationRadius={7:F4}, fullShellCoverageRadius={8:F4}, generatedLocalMin={9}, generatedLocalMax={10}, maxPointRadius={11:F4}, clusterCenterRadiusMin=0.0000, clusterCenterRadiusMax=0.0000, pointsExceedShell={12}, rejectedAnchors={13}, parentScale={14}, debugVisible={15}",
                DistributionMode,
                debugSegmentCount,
                FormatVector(RendererBoundsSize),
                RendererBoundsRadius,
                RendererLocalRawRadius,
                ShellRadiusSource,
                EffectiveSphereRadius,
                EffectiveGenerationRadius,
                EffectiveFullShellCoverageRadius,
                FormatVector(Vector3.zero),
                FormatVector(Vector3.zero),
                debugMaxPointRadius,
                debugAnyPointExceedsShell,
                debugRejectedAnchorCount,
                FormatVector(transform.localScale),
                DebugVisiblePlacement));
            return;
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        Vector3 sum = Vector3.zero;
        int upperLeft = 0;
        int upperRight = 0;
        int lowerLeft = 0;
        int lowerRight = 0;
        int frontHemisphere = 0;
        int rearHemisphere = 0;
        int centerBand = 0;
        int midBand = 0;
        int edgeBand = 0;
        int[] octants = new int[8];
        float clusterCenterMinRadius = float.MaxValue;
        float clusterCenterMaxRadius = 0f;
        float nearestMin;
        float nearestAverage;
        float nearestMax;
        CalculateNearestStats(debugClusterCenters, out nearestMin, out nearestAverage, out nearestMax);

        for (int i = 0; i < centerCount; i++)
        {
            Vector3 center = debugClusterCenters[i];
            min = Vector3.Min(min, center);
            max = Vector3.Max(max, center);
            sum += center;
            float centerRadius = center.magnitude;
            clusterCenterMinRadius = Mathf.Min(clusterCenterMinRadius, centerRadius);
            clusterCenterMaxRadius = Mathf.Max(clusterCenterMaxRadius, centerRadius);

            if (center.x < 0f && center.y >= 0f)
            {
                upperLeft++;
            }
            else if (center.x >= 0f && center.y >= 0f)
            {
                upperRight++;
            }
            else if (center.x < 0f)
            {
                lowerLeft++;
            }
            else
            {
                lowerRight++;
            }

            int octant = (center.x >= 0f ? 1 : 0) | (center.y >= 0f ? 2 : 0) | (center.z >= 0f ? 4 : 0);
            octants[octant]++;

            if (Vector3.Dot(center.normalized, Vector3.back) >= 0f)
            {
                frontHemisphere++;
            }
            else
            {
                rearHemisphere++;
            }

            float normalizedRadius = center.magnitude / Mathf.Max(0.01f, EffectiveGenerationRadius);
            if (normalizedRadius < 0.90f)
            {
                centerBand++;
            }
            else if (normalizedRadius < 0.97f)
            {
                midBand++;
            }
            else
            {
                edgeBand++;
            }
        }

        Vector3 average = sum / centerCount;
        Vector3 generatedLocalMin = debugMinGeneratedLocal.x == float.MaxValue ? Vector3.zero : debugMinGeneratedLocal;
        Vector3 generatedLocalMax = debugMaxGeneratedLocal.x == float.MinValue ? Vector3.zero : debugMaxGeneratedLocal;
        Debug.Log(
            $"SuperBallInternalCracks generated: mode={DistributionMode}, clusterCenters={centerCount}, lineSegments={debugSegmentCount}, frontWeightedShare={FrontHemisphereClusterShare:F2}, rendererBoundsSize={FormatVector(RendererBoundsSize)}, rendererBoundsRadius={RendererBoundsRadius:F4}, rendererLocalRawRadius={RendererLocalRawRadius:F4}, rendererAutoRadius={RendererAutoRadius:F4}, shellRadiusSource={ShellRadiusSource}, selectedShellRadius={EffectiveSphereRadius:F4}, effectiveGenerationRadius={EffectiveGenerationRadius:F4}, fullShellCoverageRadius={EffectiveFullShellCoverageRadius:F4}, generatedLocalMin={FormatVector(generatedLocalMin)}, generatedLocalMax={FormatVector(generatedLocalMax)}, pointRadiusMin={debugMinPointRadius:F4}, maxPointRadius={debugMaxPointRadius:F4}, clusterCenterRadiusMin={clusterCenterMinRadius:F4}, clusterCenterRadiusMax={clusterCenterMaxRadius:F4}, pointsExceedShell={debugAnyPointExceedsShell}, clusterLocalMin={FormatVector(min)}, clusterLocalMax={FormatVector(max)}, clusterLocalAverage={FormatVector(average)}, shellHemispheres(front={frontHemisphere}, rear={rearHemisphere}), quadrants(UL={upperLeft}, UR={upperRight}, LL={lowerLeft}, LR={lowerRight}), octants(-X-Y-Z={octants[0]}, +X-Y-Z={octants[1]}, -X+Y-Z={octants[2]}, +X+Y-Z={octants[3]}, -X-Y+Z={octants[4]}, +X-Y+Z={octants[5]}, -X+Y+Z={octants[6]}, +X+Y+Z={octants[7]}), radiusBands(deep={centerBand}, mid={midBand}, shell={edgeBand}), nearestDistance(min={nearestMin:F4}, avg={nearestAverage:F4}, max={nearestMax:F4}), rejectedAnchors={debugRejectedAnchorCount}, parentScale={FormatVector(transform.localScale)}, debugVisible={DebugVisiblePlacement}");
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!DrawDebugGizmos)
        {
            return;
        }

        float shellRadius = RefreshEffectiveRadiusReadout();

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (DrawDebugHemisphereGuide)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.42f);
            Gizmos.DrawWireSphere(Vector3.zero, shellRadius);
            DrawLocalCircle(shellRadius, 0f, new Color(0f, 1f, 1f, 0.60f));
            DrawLocalCircleYZ(shellRadius, 0f, new Color(0f, 1f, 1f, 0.35f));
            DrawLocalCircleXZ(shellRadius, 0f, new Color(0f, 1f, 1f, 0.35f));
        }

        float centerSize = Mathf.Max(0.008f, shellRadius * 0.012f);
        Gizmos.color = DebugPlacementColor;
        for (int i = 0; i < debugClusterCenters.Count; i++)
        {
            Gizmos.DrawSphere(debugClusterCenters[i], centerSize);
        }

        Gizmos.color = Color.white;
        float anchorSize = Mathf.Max(0.006f, shellRadius * 0.009f);
        for (int i = 0; i < debugClusterAnchors.Count; i++)
        {
            Vector3 anchor = debugClusterAnchors[i].normalized * shellRadius;
            Gizmos.DrawWireSphere(anchor, anchorSize);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawLocalCircle(float radius, float z, Color color)
    {
        DrawLocalEllipse(radius, radius, z, color);
    }

    private static void DrawLocalCircleYZ(float radius, float x, Color color)
    {
        radius = Mathf.Max(0.01f, radius);
        Gizmos.color = color;
        const int steps = 96;
        Vector3 previous = new Vector3(x, radius, 0f);

        for (int i = 1; i <= steps; i++)
        {
            float angle = (i / (float)steps) * Mathf.PI * 2f;
            Vector3 next = new Vector3(x, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private static void DrawLocalCircleXZ(float radius, float y, Color color)
    {
        radius = Mathf.Max(0.01f, radius);
        Gizmos.color = color;
        const int steps = 96;
        Vector3 previous = new Vector3(radius, y, 0f);

        for (int i = 1; i <= steps; i++)
        {
            float angle = (i / (float)steps) * Mathf.PI * 2f;
            Vector3 next = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private static void DrawLocalEllipse(float radiusX, float radiusY, float z, Color color)
    {
        radiusX = Mathf.Max(0.01f, radiusX);
        radiusY = Mathf.Max(0.01f, radiusY);
        Gizmos.color = color;
        const int steps = 96;
        Vector3 previous = new Vector3(radiusX, 0f, z);

        for (int i = 1; i <= steps; i++)
        {
            float angle = (i / (float)steps) * Mathf.PI * 2f;
            Vector3 next = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, z);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
    #endif

    private static string FormatVector(Vector3 vector)
    {
        return string.Format("({0:F4}, {1:F4}, {2:F4})", vector.x, vector.y, vector.z);
    }

    private static float NearestAnchorDistance(Vector2 candidate, List<Vector2> anchors)
    {
        if (anchors.Count == 0)
        {
            return float.MaxValue;
        }

        float nearestSqr = float.MaxValue;
        for (int i = 0; i < anchors.Count; i++)
        {
            float sqrDistance = (candidate - anchors[i]).sqrMagnitude;
            if (sqrDistance < nearestSqr)
            {
                nearestSqr = sqrDistance;
            }
        }

        return Mathf.Sqrt(nearestSqr);
    }

    private static float NearestAnchorDistance(Vector3 candidate, List<Vector3> anchors, float radius)
    {
        if (anchors.Count == 0)
        {
            return float.MaxValue;
        }

        float nearestSqr = float.MaxValue;
        Vector3 scaledCandidate = candidate.normalized * radius;
        for (int i = 0; i < anchors.Count; i++)
        {
            float sqrDistance = (scaledCandidate - anchors[i].normalized * radius).sqrMagnitude;
            if (sqrDistance < nearestSqr)
            {
                nearestSqr = sqrDistance;
            }
        }

        return Mathf.Sqrt(nearestSqr);
    }

    private static void CalculateNearestStats(List<Vector3> points, out float nearestMin, out float nearestAverage, out float nearestMax)
    {
        nearestMin = 0f;
        nearestAverage = 0f;
        nearestMax = 0f;

        if (points.Count < 2)
        {
            return;
        }

        nearestMin = float.MaxValue;
        float sum = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            float nearest = float.MaxValue;
            for (int j = 0; j < points.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, Vector3.Distance(points[i], points[j]));
            }

            nearestMin = Mathf.Min(nearestMin, nearest);
            nearestMax = Mathf.Max(nearestMax, nearest);
            sum += nearest;
        }

        nearestAverage = sum / points.Count;
    }

    private static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        normal = normal.sqrMagnitude < 0.0001f ? Vector3.up : normal.normalized;
        Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.86f ? Vector3.right : Vector3.up;
        tangent = Vector3.Cross(reference, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static Quaternion RandomRotation(System.Random random)
    {
        return Quaternion.AngleAxis(RandomRange(random, 0f, 360f), RandomUnitVector(random));
    }

    private static Vector3 RandomUnitVector(System.Random random)
    {
        float z = RandomRange(random, -1f, 1f);
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z).normalized;
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

    private static float CalculateLocalMeshRadius(Renderer renderer, Transform localSpace, bool normalizeUnitSphereMeshRadius)
    {
        if (renderer == null || localSpace == null)
        {
            return 0f;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return NormalizeDerivedMeshRadius(
                CalculateBoundsRadiusInLocalSpace(meshFilter.sharedMesh.bounds, renderer.transform, localSpace),
                normalizeUnitSphereMeshRadius);
        }

        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
        {
            return NormalizeDerivedMeshRadius(
                CalculateBoundsRadiusInLocalSpace(skinnedMeshRenderer.sharedMesh.bounds, renderer.transform, localSpace),
                normalizeUnitSphereMeshRadius);
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
        return NormalizeDerivedMeshRadius(
            Mathf.Min(localExtents.x, Mathf.Min(localExtents.y, localExtents.z)),
            normalizeUnitSphereMeshRadius);
    }

    private static float NormalizeDerivedMeshRadius(float radius, bool normalizeUnitSphereMeshRadius)
    {
        if (!normalizeUnitSphereMeshRadius)
        {
            return radius;
        }

        // The authored Super Ball sphere mesh reports a radius near 1 while the scene scale
        // treats scale 1 as a unit-diameter sphere. Normalize that common unit-sphere asset
        // convention so a 0.98 InnerCore scale yields an internal shell radius near 0.49.
        if (radius > 0.75f && radius < 1.25f)
        {
            return radius * 0.5f;
        }

        return radius;
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
                    Vector3 sourceCorner = sourceBounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                    localBounds.Encapsulate(localSpace.InverseTransformPoint(sourceTransform.TransformPoint(sourceCorner)));
                }
            }
        }

        Vector3 localExtents = localBounds.extents;
        return Mathf.Min(localExtents.x, Mathf.Min(localExtents.y, localExtents.z));
    }

    private static Vector2 RandomInsideUnitCircle(System.Random random)
    {
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Next01(random));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static Vector2 Rotate2D(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private static Color ScaleColor(Color color, float multiplier, float alpha)
    {
        return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, alpha);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, Next01(random));
    }

    private static float Next01(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private sealed class CrackCluster
    {
        public readonly Transform Root;
        public readonly Vector3 Normal;
        public readonly Vector3 Tangent;
        public readonly Vector3 Bitangent;
        public readonly float Radius;
        public readonly Vector3 Center;
        public readonly List<Vector3[]> PrimaryPaths = new List<Vector3[]>();

        public CrackCluster(Transform root, Vector3 normal, Vector3 tangent, Vector3 bitangent, float radius, Vector3 center)
        {
            Root = root;
            Normal = normal;
            Tangent = tangent;
            Bitangent = bitangent;
            Radius = radius;
            Center = center;
        }
    }

    private enum CrackSegmentKind
    {
        Primary,
        Branch,
        Spark
    }

#if UNITY_EDITOR
    public static void InstallIntoSampleScene()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        const string crackMaterialPath = "Assets/M_SuperBall_CrackGlow.mat";

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject innerCore = GameObject.Find("SuperBall_InnerCore");

        if (innerCore == null)
        {
            throw new InvalidOperationException("Could not find SuperBall_InnerCore in SampleScene.");
        }

        Transform cracksRoot = innerCore.transform.Find("SuperBall_InternalCracks");

        if (cracksRoot == null)
        {
            GameObject cracksObject = new GameObject("SuperBall_InternalCracks");
            cracksRoot = cracksObject.transform;
            cracksRoot.SetParent(innerCore.transform, false);
        }

        cracksRoot.localPosition = Vector3.zero;
        cracksRoot.localRotation = Quaternion.identity;
        cracksRoot.localScale = Vector3.one;

        SuperBallInternalCracks cracks = cracksRoot.GetComponent<SuperBallInternalCracks>();
        if (cracks == null)
        {
            cracks = cracksRoot.gameObject.AddComponent<SuperBallInternalCracks>();
        }

        Renderer renderer = innerCore.GetComponent<Renderer>();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(crackMaterialPath);

        cracks.DistributionMode = SuperBallCrackDistributionMode.FullSphereShell;
        cracks.ClusterCount = 56;
        cracks.SegmentsPerCluster = 4;
        cracks.FrontHemisphereClusterShare = 0.86f;
        cracks.UseManualShellRadius = true;
        cracks.ManualShellRadius = 0.98f;
        cracks.AutoDeriveSphereRadius = true;
        cracks.NormalizeUnitSphereMeshRadius = false;
        cracks.SurfaceInset = 0.010f;
        cracks.FullShellCoverageRadius = 0.965f;
        cracks.EffectiveFullShellCoverageRadius = 0.965f;
        cracks.DiskHorizontalCoverage = 0.82f;
        cracks.DiskVerticalCoverage = 0.82f;
        cracks.CenterAvoidance = 0.10f;
        cracks.EdgeAvoidance = 0.08f;
        cracks.MinClusterDistance = 0.22f;
        cracks.DepthJitter = 0.018f;
        cracks.SphereRadius = 0.98f;
        cracks.MinLocalRadius = 0.08f;
        cracks.MaxLocalRadius = 0.965f;
        cracks.FrontDepthMin = 0.18f;
        cracks.FrontDepthMax = 0.92f;
        cracks.HemisphereCoverage = 0.985f;
        cracks.VerticalSpread = 1.00f;
        cracks.HorizontalSpread = 1.00f;
        cracks.MinArcLength = 0.18f;
        cracks.MaxArcLength = 0.46f;
        cracks.BranchChance = 0.48f;
        cracks.BranchLengthMultiplier = 0.52f;
        cracks.RandomSeed = 93019;
        cracks.CrackAlphaMin = 0.16f;
        cracks.CrackAlphaMax = 0.44f;
        cracks.CrackEmissionMin = 0.75f;
        cracks.CrackEmissionMax = 2.10f;
        cracks.IdleVisibility = 0.18f;
        cracks.ChargeVisibility = 1.18f;
        cracks.LaunchFlashVisibility = 1.75f;
        cracks.crackColor = new Color(0.48f, 1f, 0.06f, 1f);
        cracks.emissionIntensity = 1.35f;
        cracks.minThickness = 0.0024f;
        cracks.maxThickness = 0.0095f;
        cracks.StateTransitionSpeed = 4.8f;
        cracks.DebugVisiblePlacement = false;
        cracks.DebugPlacementColor = Color.cyan;
        cracks.DebugThicknessMultiplier = 8f;
        cracks.DebugEmissionIntensity = 8f;
        cracks.DrawDebugGizmos = false;
        cracks.DrawDebugHemisphereGuide = false;
        cracks.LogGenerationSummary = true;
        ConfigureCrackMaterial(material);
        cracks.SetCrackMaterial(material);
        cracks.SetTargetRenderer(renderer);
        cracks.RegenerateCracks();

        EditorUtility.SetDirty(cracks);
        EditorUtility.SetDirty(cracksRoot.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureCrackMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        bool changed = false;
        Shader shader = Shader.Find("REPO/SuperBallCrackEmission");
        if (shader != null && material.shader != shader)
        {
            material.shader = shader;
            changed = true;
        }

        Color targetColor = new Color(0.48f, 1f, 0.06f, 1f);
        if (material.HasProperty(ColorId))
        {
            Color currentColor = material.GetColor(ColorId);
            if (currentColor != targetColor)
            {
                material.SetColor(ColorId, targetColor);
                changed = true;
            }
        }

        if (material.HasProperty(EmissionIntensityId))
        {
            float currentEmission = material.GetFloat(EmissionIntensityId);
            if (!Mathf.Approximately(currentEmission, 1.35f))
            {
                material.SetFloat(EmissionIntensityId, 1.35f);
                changed = true;
            }
        }

        if (material.HasProperty(VisibilityId))
        {
            float currentVisibility = material.GetFloat(VisibilityId);
            if (!Mathf.Approximately(currentVisibility, 0.18f))
            {
                material.SetFloat(VisibilityId, 0.18f);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(material);
        }
    }
#endif
}

public enum SuperBallCrackDistributionMode
{
    FullSphereShell,
    FrontHemisphereShell,
    ViewFacingDisk
}

#if UNITY_EDITOR
[CustomEditor(typeof(SuperBallInternalCracks))]
public sealed class SuperBallInternalCracksEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty autoRadius = serializedObject.FindProperty("AutoDeriveSphereRadius");
        bool autoDeriveRadius = autoRadius != null && autoRadius.boolValue;
        SerializedProperty manualRadius = serializedObject.FindProperty("UseManualShellRadius");
        bool useManualRadius = manualRadius != null && manualRadius.boolValue;
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            bool readOnly = property.name == "m_Script"
                || ((autoDeriveRadius || useManualRadius) && property.name == "SphereRadius")
                || property.name == "EffectiveSphereRadius"
                || property.name == "EffectiveGenerationRadius"
                || property.name == "EffectiveProjectedRadius"
                || property.name == "EffectiveRadiusDebug"
                || property.name == "EffectiveDiskHorizontalRadius"
                || property.name == "EffectiveDiskVerticalRadius"
                || property.name == "EffectiveFullShellCoverageRadius"
                || property.name == "RendererBoundsSize"
                || property.name == "RendererBoundsRadius"
                || property.name == "RendererLocalRawRadius"
                || property.name == "RendererAutoRadius"
                || property.name == "ShellRadiusSource";

            using (new EditorGUI.DisabledScope(readOnly))
            {
                EditorGUILayout.PropertyField(property, true);
            }

            enterChildren = false;
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate Internal Cracks"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                SuperBallInternalCracks cracks = targets[i] as SuperBallInternalCracks;
                if (cracks == null)
                {
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(cracks.gameObject, "Regenerate Internal Cracks");
                cracks.RegenerateCracks();
                EditorUtility.SetDirty(cracks);
                EditorUtility.SetDirty(cracks.gameObject);

                if (cracks.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(cracks.gameObject.scene);
                }
            }
        }
    }
}
#endif
