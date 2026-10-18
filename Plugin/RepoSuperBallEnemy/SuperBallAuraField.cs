using UnityEngine;

namespace RepoSuperBallEnemy
{
    public sealed class SuperBallAuraField : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private Renderer outerShell;
        private Renderer midShell;
        private Renderer distortionShell;
        private Light glowLight;
        private float idleAlpha = 0.04f;
        private float chargeAlpha = 0.22f;
        private float pulseSpeedMin = 1.0f;
        private float pulseSpeedMax = 7.0f;
        private float maxScale = 1.9f;
        private float emission = 3.5f;
        private float phase;
        private bool disableHardShell = true;
        private bool wasActive;

        public void Configure(
            Renderer newOuterShell,
            Renderer newMidShell,
            Renderer newDistortionShell,
            Light newGlowLight,
            float newIdleAlpha,
            float newChargeAlpha,
            float newPulseSpeedMin,
            float newPulseSpeedMax,
            float newMaxScale,
            float newEmission,
            bool newDisableHardShell)
        {
            outerShell = newOuterShell;
            midShell = newMidShell;
            distortionShell = newDistortionShell;
            glowLight = newGlowLight;
            idleAlpha = newIdleAlpha;
            chargeAlpha = newChargeAlpha;
            pulseSpeedMin = newPulseSpeedMin;
            pulseSpeedMax = Mathf.Max(newPulseSpeedMin, newPulseSpeedMax);
            maxScale = newMaxScale;
            emission = newEmission;
            disableHardShell = newDisableHardShell;
            phase = Random.Range(0.0f, 100.0f);
            SetAura(0.0f, false, false);
            Debug.Log($"[RepoSuperBallEnemy] Super Ball aura field created: idleAlpha={idleAlpha:0.00}, chargeAlpha={chargeAlpha:0.00}, maxScale={maxScale:0.00}, hardOuterShellEnabled={!disableHardShell}.");
        }

        public void SetAura(float intensity01, bool active, bool launch)
        {
            float intensity = Mathf.Clamp01(intensity01);
            bool visible = active && (intensity > 0.01f || (!launch && idleAlpha > 0.001f));
            if (visible != wasActive)
            {
                wasActive = visible;
                Debug.Log($"[RepoSuperBallEnemy] Super Ball aura field {(visible ? "enabled" : "disabled")}.");
            }

            SetRendererEnabled(outerShell, visible && !disableHardShell);
            SetRendererEnabled(midShell, visible);
            SetRendererEnabled(distortionShell, visible);

            if (!visible)
            {
                if (glowLight != null)
                {
                    glowLight.enabled = false;
                }
                return;
            }

            float pulseSpeed = Mathf.Lerp(pulseSpeedMin, pulseSpeedMax, intensity);
            float pulse = (Mathf.Sin((Time.time + phase) * pulseSpeed) + 1.0f) * 0.5f;
            float snap = launch ? 0.82f : 1.0f;
            float outerScale = Mathf.Lerp(1.06f, maxScale, intensity) * Mathf.Lerp(0.98f, 1.05f, pulse) * snap;
            float midScale = Mathf.Lerp(1.03f, Mathf.Max(1.08f, maxScale * 0.82f), intensity) * Mathf.Lerp(0.99f, 1.035f, 1.0f - pulse) * snap;
            float distortionScale = Mathf.Lerp(1.10f, maxScale * 1.02f, intensity) * Mathf.Lerp(0.985f, 1.055f, pulse) * snap;

            SetScale(outerShell, outerScale);
            SetScale(midShell, midScale);
            SetScale(distortionShell, distortionScale);

            float baseAlpha = Mathf.Lerp(idleAlpha, chargeAlpha, intensity);
            ApplyShell(outerShell, new Color(0.06f, 1.0f, 0.04f, baseAlpha * Mathf.Lerp(0.10f, 0.26f, pulse)), emission * Mathf.Lerp(0.10f, 0.38f, intensity));
            ApplyShell(midShell, new Color(0.55f, 1.0f, 0.08f, baseAlpha * Mathf.Lerp(0.10f, 0.34f, 1.0f - pulse)), emission * Mathf.Lerp(0.10f, 0.55f, intensity));
            ApplyShell(distortionShell, new Color(0.72f, 1.0f, 0.35f, baseAlpha * Mathf.Lerp(0.04f, 0.14f, pulse)), emission * Mathf.Lerp(0.06f, 0.36f, intensity));

            if (glowLight != null)
            {
                glowLight.enabled = true;
                glowLight.intensity = Mathf.Lerp(0.05f, 1.35f, intensity) * Mathf.Lerp(0.82f, 1.18f, pulse);
                glowLight.range = Mathf.Lerp(0.75f, 1.75f, intensity);
            }
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private static void SetScale(Renderer renderer, float scale)
        {
            if (renderer != null)
            {
                renderer.transform.localScale = Vector3.one * scale;
            }
        }

        private static void ApplyShell(Renderer renderer, Color color, float emission)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.material;
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }
            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, color);
            }
            if (material.HasProperty(EmissionColorProperty))
            {
                material.SetColor(EmissionColorProperty, new Color(0.12f, 1.0f, 0.03f, 1.0f) * emission);
                material.EnableKeyword("_EMISSION");
            }
        }
    }
}
