using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class SuperBallCrackPulse : MonoBehaviour
{
    [Range(0f, 2f)] public float idleVisibility = 0.30f;
    [Range(0f, 2f)] public float chargeVisibility = 1.00f;
    [Range(0f, 3f)] public float launchFlashVisibility = 1.35f;
    [Min(0f)] public float idleEmission = 0.45f;
    [Min(0f)] public float chargeEmission = 1.00f;
    [Min(0f)] public float flashEmission = 1.60f;
    [Min(0.01f)] public float idleFlickerSpeed = 0.95f;
    [Min(0.01f)] public float chargeFlickerSpeed = 7.25f;
    [Min(0.01f)] public float flashDecaySpeed = 5.5f;

    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private MaterialPropertyBlock block;
    private float chargeProgress;
    private float flashAmount;
    private bool charging;

    public void SetIdle()
    {
        charging = false;
        chargeProgress = 0f;
    }

    public void SetCharge(float progress)
    {
        charging = true;
        chargeProgress = Mathf.Clamp01(progress);
    }

    public void Flash(float intensity)
    {
        flashAmount = Mathf.Max(flashAmount, Mathf.Max(0f, intensity));
    }

    private void OnEnable()
    {
        EnsureBlock();
        RebuildLineCache();
        ApplyPulse();
    }

    private void OnTransformChildrenChanged()
    {
        RebuildLineCache();
        ApplyPulse();
    }

    private void Update()
    {
        if (lines.Count == 0)
        {
            RebuildLineCache();
        }

        float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 30f;
        flashAmount = Mathf.MoveTowards(flashAmount, 0f, deltaTime * flashDecaySpeed);
        ApplyPulse();
    }

    private void RebuildLineCache()
    {
        lines.Clear();
        GetComponentsInChildren(true, lines);
    }

    private void ApplyPulse()
    {
        EnsureBlock();

        float time = Application.isPlaying ? Time.time : (float)UnityEditorSafeTime();
        float flickerSpeed = charging ? chargeFlickerSpeed : idleFlickerSpeed;
        float flicker = 0.84f + Mathf.PerlinNoise(time * flickerSpeed, 0.37f) * 0.28f;
        float visibility = charging
            ? Mathf.Lerp(idleVisibility, chargeVisibility, chargeProgress) * flicker
            : Mathf.Max(0.08f, idleVisibility * flicker);

        float emission = charging
            ? Mathf.Lerp(idleEmission, chargeEmission, chargeProgress) * flicker
            : idleEmission * flicker;

        if (flashAmount > 0f)
        {
            float flash = Mathf.Clamp01(flashAmount);
            visibility = Mathf.Max(visibility, launchFlashVisibility * flash);
            emission = Mathf.Max(emission, Mathf.Lerp(chargeEmission, flashEmission, flash));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
            {
                continue;
            }

            float linePhase = Hash01(line.name) * 6.28318f;
            float lineFlicker = 0.90f + Mathf.Sin(time * (flickerSpeed * 0.7f) + linePhase) * 0.10f;

            line.GetPropertyBlock(block);
            block.SetFloat(VisibilityId, visibility * lineFlicker);
            block.SetFloat(EmissionIntensityId, emission * (0.86f + Hash01(line.name + "_e") * 0.34f));
            line.SetPropertyBlock(block);
        }
    }

    private void EnsureBlock()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
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

    private static double UnityEditorSafeTime()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }
}
