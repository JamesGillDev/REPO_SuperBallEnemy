using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class SuperBallCrackPulse : MonoBehaviour
{
    [Range(0f, 2f)] public float idleVisibility = 0.18f;
    [Range(0f, 2f)] public float chargeVisibility = 1.18f;
    [Range(0f, 3f)] public float launchFlashVisibility = 1.75f;
    [Min(0f)] public float idleEmission = 0.78f;
    [Min(0f)] public float chargeEmission = 2.05f;
    [Min(0f)] public float flashEmission = 3.35f;
    [Min(0.01f)] public float idleFlickerSpeed = 0.75f;
    [Min(0.01f)] public float chargeFlickerSpeed = 4.80f;
    [Min(0.01f)] public float flashDecaySpeed = 5.5f;
    [Min(0.01f)] public float stateTransitionSpeed = 4.8f;

    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
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
        RebuildLineCache();
        currentChargeProgress = targetChargeProgress;
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
        currentChargeProgress = Mathf.MoveTowards(
            currentChargeProgress,
            targetChargeProgress,
            deltaTime * Mathf.Max(stateTransitionSpeed, 0.01f));
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
        float threat = Mathf.Clamp01(currentChargeProgress);
        float pressure = SmoothRange(0.34f, 0.92f, threat);
        float flickerSpeed = Mathf.Lerp(idleFlickerSpeed, chargeFlickerSpeed, pressure);
        float flicker = 0.90f + Mathf.PerlinNoise(time * flickerSpeed, 0.37f) * 0.18f;
        float visibility = Mathf.Max(0.02f, Mathf.Lerp(idleVisibility, chargeVisibility, pressure) * flicker);
        float emission = Mathf.Lerp(idleEmission, chargeEmission, pressure) * flicker;

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
            float lineFlicker = 0.94f + Mathf.Sin(time * (flickerSpeed * 0.55f) + linePhase) * 0.06f;

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

    private static double UnityEditorSafeTime()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }
}
