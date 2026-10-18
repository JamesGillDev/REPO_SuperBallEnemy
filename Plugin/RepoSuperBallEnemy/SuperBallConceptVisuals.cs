using UnityEngine;

namespace RepoSuperBallEnemy
{
    public sealed class SuperBallConceptVisuals : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private Transform faceRoot;
        private Renderer[] faceRenderers = new Renderer[0];
        private Renderer[] toothRenderers = new Renderer[0];
        private Renderer[] highlightRenderers = new Renderer[0];
        private Renderer crackRenderer;
        private Renderer innerCoreRenderer;
        private Light faceLight;
        private SuperBallBehavior behavior;
        private bool faceEnabled;
        private bool cracksEnabled;
        private bool highlightsEnabled;
        private bool faceWasVisible;
        private bool faceRendererDetailsLogged;
        private bool teethEnabled = true;
        private float faceEmissionMin = 1.0f;
        private float faceEmissionMax = 6.0f;
        private float faceMaxAlpha = 0.95f;
        private Color eyeColor = new Color(0.88f, 1.0f, 0.10f, 1.0f);
        private Color mouthColor = new Color(0.95f, 1.0f, 0.38f, 1.0f);
        private float crackGlowIntensity = 4.75f;
        private float crackAlpha = 0.72f;
        private float innerCoreAlpha = 0.26f;
        private float innerGlowAlpha = 0.20f;
        private float phase;

        public void Configure(
            Transform newFaceRoot,
            Renderer[] newFaceRenderers,
            Renderer[] newToothRenderers,
            Renderer[] newHighlightRenderers,
            Renderer newCrackRenderer,
            Renderer newInnerCoreRenderer,
            Light newFaceLight,
            bool newFaceEnabled,
            bool newCracksEnabled,
            bool newHighlightsEnabled,
            float newFaceAppearAtChargeProgress,
            float newFaceEmissionMin,
            float newFaceEmissionMax,
            bool newTeethEnabled,
            float newFaceMaxAlpha,
            Color newEyeColor,
            Color newMouthColor,
            float newCrackGlowIntensity,
            float newCrackAlpha,
            float newInnerCoreAlpha,
            float newInnerGlowAlpha)
        {
            faceRoot = newFaceRoot;
            faceRenderers = newFaceRenderers ?? new Renderer[0];
            toothRenderers = newToothRenderers ?? new Renderer[0];
            highlightRenderers = newHighlightRenderers ?? new Renderer[0];
            crackRenderer = newCrackRenderer;
            innerCoreRenderer = newInnerCoreRenderer;
            faceLight = newFaceLight;
            faceEnabled = newFaceEnabled;
            cracksEnabled = newCracksEnabled;
            highlightsEnabled = newHighlightsEnabled;
            faceEmissionMin = newFaceEmissionMin;
            faceEmissionMax = newFaceEmissionMax;
            teethEnabled = newTeethEnabled;
            faceMaxAlpha = newFaceMaxAlpha;
            eyeColor = newEyeColor;
            mouthColor = newMouthColor;
            crackGlowIntensity = newCrackGlowIntensity;
            crackAlpha = newCrackAlpha;
            innerCoreAlpha = newInnerCoreAlpha;
            innerGlowAlpha = newInnerGlowAlpha;
            behavior = GetComponentInChildren<SuperBallBehavior>(true);
            phase = Random.Range(0.0f, 100.0f);
            ApplyEnabledState();
        }

        private void OnEnable()
        {
            if (behavior == null)
            {
                behavior = GetComponentInChildren<SuperBallBehavior>(true);
            }
        }

        private void Update()
        {
            if (behavior == null)
            {
                behavior = GetComponentInChildren<SuperBallBehavior>(true);
            }

            float charge = behavior == null ? 0.0f : behavior.VisualChargeIntensity;
            float faceIntensity = behavior == null ? 0.0f : behavior.VisualFaceIntensity;
            float slowPulse = (Mathf.Sin((Time.time + phase) * Mathf.Lerp(2.1f, 11.0f, charge)) + 1.0f) * 0.5f;
            float flicker = (Mathf.Sin((Time.time + phase) * 17.0f) + 1.0f) * 0.5f;
            float facePulse = Mathf.Lerp(0.70f, 1.20f, slowPulse) + faceIntensity * Mathf.Lerp(0.10f, 0.34f, flicker);
            float crackPulse = Mathf.Lerp(0.55f, 1.15f, slowPulse) + charge * Mathf.Lerp(0.08f, 0.28f, flicker);

            if (faceRoot != null)
            {
                float scaleMultiplier = behavior == null ? 1.0f : behavior.VisualScaleMultiplier;
                float snarlScale = scaleMultiplier * (1.0f + faceIntensity * 0.055f + slowPulse * faceIntensity * 0.035f);
                faceRoot.localScale = new Vector3(snarlScale, scaleMultiplier * Mathf.Lerp(1.0f, 0.94f, faceIntensity * slowPulse), 1.0f);
                FaceCameraOrPlayer();
            }

            ApplyFacePulse(facePulse, faceIntensity);
            ApplyCrackPulse(crackPulse, charge);
            ApplyHighlightPulse(slowPulse, charge);
        }

        private void ApplyEnabledState()
        {
            if (faceRoot != null)
            {
                faceRoot.gameObject.SetActive(faceEnabled || highlightsEnabled);
            }

            SetRendererArrayEnabled(faceRenderers, false);
            SetRendererArrayEnabled(toothRenderers, false);
            SetRendererArrayEnabled(highlightRenderers, highlightsEnabled);
            SetRendererEnabled(crackRenderer, cracksEnabled);
            SetRendererEnabled(innerCoreRenderer, cracksEnabled);
            if (faceLight != null)
            {
                faceLight.enabled = false;
            }
        }

        private void ApplyFacePulse(float pulse, float faceIntensity)
        {
            bool visible = faceEnabled && faceIntensity > 0.01f;
            if (visible != faceWasVisible)
            {
                faceWasVisible = visible;
                faceRendererDetailsLogged = false;
                Debug.Log($"[RepoSuperBallEnemy] Super Ball face {(visible ? "activated" : "deactivated")}.");
            }

            if (faceRoot != null)
            {
                faceRoot.gameObject.SetActive(visible || highlightsEnabled);
            }
            SetRendererArrayEnabled(faceRenderers, visible);
            SetRendererArrayEnabled(toothRenderers, visible && teethEnabled);
            if (faceLight != null)
            {
                faceLight.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            float emissionStrength = Mathf.Lerp(faceEmissionMin, faceEmissionMax, faceIntensity) * pulse;
            Color faceColor = new Color(eyeColor.r, eyeColor.g, eyeColor.b, Mathf.Lerp(0.18f, faceMaxAlpha, faceIntensity));
            Color emission = new Color(eyeColor.r, eyeColor.g, eyeColor.b, 1.0f) * emissionStrength;
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                Color color = i == 2 ? new Color(mouthColor.r, mouthColor.g, mouthColor.b, faceColor.a) : faceColor;
                Color rendererEmission = i == 2 ? new Color(mouthColor.r, mouthColor.g, mouthColor.b, 1.0f) * emissionStrength * 0.92f : emission;
                SetMaterialColors(faceRenderers[i], color, rendererEmission);
            }
            for (int i = 0; i < toothRenderers.Length; i++)
            {
                SetMaterialColors(toothRenderers[i], new Color(mouthColor.r, mouthColor.g, mouthColor.b, Mathf.Lerp(0.25f, faceMaxAlpha, faceIntensity)), new Color(mouthColor.r, mouthColor.g, mouthColor.b, 1.0f) * emissionStrength * 1.12f);
            }

            if (faceLight != null)
            {
                faceLight.intensity = Mathf.Clamp(emissionStrength * 0.33f, 0.10f, 3.5f);
                faceLight.range = Mathf.Lerp(0.8f, 1.9f, faceIntensity);
            }

            if (visible && !faceRendererDetailsLogged && faceIntensity > 0.50f)
            {
                faceRendererDetailsLogged = true;
                Debug.Log($"[RepoSuperBallEnemy] Super Ball face renderers active: eyes/mouth={faceRenderers.Length}, teethEnabled={teethEnabled}, teeth={toothRenderers.Length}.");
            }
        }

        private void ApplyCrackPulse(float pulse, float charge)
        {
            if (!cracksEnabled)
            {
                return;
            }

            SetMaterialColors(
                crackRenderer,
                new Color(0.19f, 1.0f, 0.02f, Mathf.Clamp01(crackAlpha * Mathf.Lerp(0.70f, 1.0f, charge))),
                new Color(0.03f, 1.0f, 0.02f, 1.0f) * crackGlowIntensity * pulse);
            SetMaterialColors(
                innerCoreRenderer,
                new Color(0.01f, 0.22f, 0.035f, Mathf.Clamp01(Mathf.Max(innerCoreAlpha, innerGlowAlpha) * Mathf.Lerp(0.75f, 1.20f, charge))),
                new Color(0.0f, 0.85f, 0.06f, 1.0f) * Mathf.Lerp(0.55f, 1.45f, charge) * pulse);
        }

        private void FaceCameraOrPlayer()
        {
            if (faceRoot == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 delta = camera.transform.position - faceRoot.position;
            if (delta.sqrMagnitude <= 0.01f)
            {
                return;
            }

            faceRoot.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private void ApplyHighlightPulse(float pulse, float charge)
        {
            if (!highlightsEnabled)
            {
                return;
            }

            Color color = new Color(0.88f, 1.0f, 0.72f, Mathf.Lerp(0.18f, 0.34f, pulse));
            Color emission = new Color(0.28f, 1.0f, 0.10f, 1.0f) * Mathf.Lerp(0.75f, 1.45f, charge);
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                SetMaterialColors(highlightRenderers[i], color, emission);
            }
        }

        private static void SetRendererArrayEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SetRendererEnabled(renderers[i], enabled);
            }
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private static void SetMaterialColors(Renderer renderer, Color color, Color emission)
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
                material.SetColor(EmissionColorProperty, emission);
                material.EnableKeyword("_EMISSION");
            }
        }
    }
}
