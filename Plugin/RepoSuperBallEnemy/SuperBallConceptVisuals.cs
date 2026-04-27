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
        private Renderer[] highlightRenderers = new Renderer[0];
        private Renderer crackRenderer;
        private Renderer innerCoreRenderer;
        private Light faceLight;
        private SuperBallBehavior behavior;
        private bool faceEnabled;
        private bool cracksEnabled;
        private bool highlightsEnabled;
        private float faceGlowIntensity = 5.5f;
        private float crackGlowIntensity = 4.75f;
        private float crackAlpha = 0.72f;
        private float innerCoreAlpha = 0.26f;
        private float phase;

        public void Configure(
            Transform newFaceRoot,
            Renderer[] newFaceRenderers,
            Renderer[] newHighlightRenderers,
            Renderer newCrackRenderer,
            Renderer newInnerCoreRenderer,
            Light newFaceLight,
            bool newFaceEnabled,
            bool newCracksEnabled,
            bool newHighlightsEnabled,
            float newFaceGlowIntensity,
            float newCrackGlowIntensity,
            float newCrackAlpha,
            float newInnerCoreAlpha)
        {
            faceRoot = newFaceRoot;
            faceRenderers = newFaceRenderers ?? new Renderer[0];
            highlightRenderers = newHighlightRenderers ?? new Renderer[0];
            crackRenderer = newCrackRenderer;
            innerCoreRenderer = newInnerCoreRenderer;
            faceLight = newFaceLight;
            faceEnabled = newFaceEnabled;
            cracksEnabled = newCracksEnabled;
            highlightsEnabled = newHighlightsEnabled;
            faceGlowIntensity = newFaceGlowIntensity;
            crackGlowIntensity = newCrackGlowIntensity;
            crackAlpha = newCrackAlpha;
            innerCoreAlpha = newInnerCoreAlpha;
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
            float slowPulse = (Mathf.Sin((Time.time + phase) * Mathf.Lerp(2.1f, 11.0f, charge)) + 1.0f) * 0.5f;
            float flicker = (Mathf.Sin((Time.time + phase) * 17.0f) + 1.0f) * 0.5f;
            float facePulse = Mathf.Lerp(0.70f, 1.20f, slowPulse) + charge * Mathf.Lerp(0.10f, 0.34f, flicker);
            float crackPulse = Mathf.Lerp(0.55f, 1.15f, slowPulse) + charge * Mathf.Lerp(0.08f, 0.28f, flicker);

            if (faceRoot != null)
            {
                float snarlScale = 1.0f + charge * 0.045f + slowPulse * charge * 0.025f;
                faceRoot.localScale = new Vector3(snarlScale, Mathf.Lerp(1.0f, 0.96f, charge * slowPulse), 1.0f);
            }

            ApplyFacePulse(facePulse, charge);
            ApplyCrackPulse(crackPulse, charge);
            ApplyHighlightPulse(slowPulse, charge);
        }

        private void ApplyEnabledState()
        {
            if (faceRoot != null)
            {
                faceRoot.gameObject.SetActive(faceEnabled);
            }

            SetRendererArrayEnabled(faceRenderers, faceEnabled);
            SetRendererArrayEnabled(highlightRenderers, highlightsEnabled);
            SetRendererEnabled(crackRenderer, cracksEnabled);
            SetRendererEnabled(innerCoreRenderer, cracksEnabled);
            if (faceLight != null)
            {
                faceLight.enabled = faceEnabled;
            }
        }

        private void ApplyFacePulse(float pulse, float charge)
        {
            if (!faceEnabled)
            {
                return;
            }

            Color faceColor = new Color(0.66f, 1.0f, 0.03f, Mathf.Lerp(0.86f, 1.0f, charge));
            Color emission = new Color(0.54f, 1.0f, 0.01f, 1.0f) * faceGlowIntensity * pulse;
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                SetMaterialColors(faceRenderers[i], faceColor, emission);
            }

            if (faceLight != null)
            {
                faceLight.intensity = Mathf.Clamp(faceGlowIntensity * Mathf.Lerp(0.22f, 0.48f, charge) * pulse, 0.15f, 3.5f);
                faceLight.range = Mathf.Lerp(0.9f, 1.8f, charge);
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
                new Color(0.01f, 0.22f, 0.035f, Mathf.Clamp01(innerCoreAlpha * Mathf.Lerp(0.75f, 1.20f, charge))),
                new Color(0.0f, 0.85f, 0.06f, 1.0f) * Mathf.Lerp(0.55f, 1.45f, charge) * pulse);
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
