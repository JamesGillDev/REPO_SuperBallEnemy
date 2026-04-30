using UnityEngine;

namespace RepoSuperBallEnemy
{
    public sealed class SuperBallLightningArcs : MonoBehaviour
    {
        private const string ArcRootName = "SuperBallLightning";

        private LineRenderer[] arcs = new LineRenderer[0];
        private Material arcMaterial;
        private bool lightningEnabled = true;
        private bool idleVisible;
        private int arcCount = 4;
        private float emission = 4.0f;
        private float baseRadius = 0.62f;
        private float maxThickness = 0.018f;
        private float jitterAmount = 0.18f;
        private float refreshRate = 0.07f;
        private float intensity;
        private float burstTimer;
        private float nextJitterTime;
        private bool wasVisible;

        public void Configure(bool enabled, int count, float newEmission, bool newIdleVisible, float newThickness, float newJitter, float newRefreshRate)
        {
            lightningEnabled = enabled;
            idleVisible = newIdleVisible;
            arcCount = Mathf.Clamp(count, 1, 10);
            emission = Mathf.Clamp(newEmission, 0.5f, 12.0f);
            maxThickness = Mathf.Clamp(newThickness, 0.004f, 0.05f);
            jitterAmount = Mathf.Clamp(newJitter, 0.02f, 0.5f);
            refreshRate = Mathf.Clamp(newRefreshRate, 0.03f, 0.20f);
            EnsureArcs();
            SetIntensity(0.0f, false);
            Debug.Log($"[RepoSuperBallEnemy] Super Ball lightning system created: enabled={lightningEnabled}, idleVisible={idleVisible}, arcCount={arcCount}, emission={emission:0.00}, thickness={maxThickness:0.000}, jitter={jitterAmount:0.00}, refreshRate={refreshRate:0.00}.");
        }

        public void SetIntensity(float intensity01, bool launch)
        {
            intensity = Mathf.Clamp01(intensity01);
            bool visible = lightningEnabled && (idleVisible || intensity > 0.01f || burstTimer > 0.0f);
            if (visible != wasVisible)
            {
                wasVisible = visible;
                Debug.Log($"[RepoSuperBallEnemy] Super Ball lightning {(visible ? "enabled" : "disabled")}.");
            }

            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i] != null)
                {
                    arcs[i].enabled = visible;
                    float width = Mathf.Lerp(maxThickness * 0.22f, launch ? maxThickness : maxThickness * 0.72f, Mathf.Max(intensity, burstTimer > 0.0f ? 1.0f : 0.0f));
                    arcs[i].startWidth = width;
                    arcs[i].endWidth = width * 0.45f;
                    Color start = Color.Lerp(new Color(0.32f, 1.0f, 0.04f, 0.20f), new Color(0.98f, 1.0f, 0.42f, 0.92f), Mathf.Max(intensity, burstTimer > 0.0f ? 1.0f : 0.0f));
                    Color end = new Color(0.18f, 1.0f, 0.06f, Mathf.Lerp(0.10f, 0.75f, Mathf.Max(intensity, burstTimer > 0.0f ? 1.0f : 0.0f)));
                    arcs[i].startColor = start;
                    arcs[i].endColor = end;
                }
            }
        }

        public void Burst()
        {
            if (!lightningEnabled)
            {
                return;
            }

            burstTimer = 0.22f;
            SetIntensity(1.0f, true);
            JitterArcs(true);
        }

        private void Update()
        {
            if (burstTimer > 0.0f)
            {
                burstTimer -= Time.deltaTime;
            }

            if (!wasVisible)
            {
                return;
            }

            if (Time.time >= nextJitterTime)
            {
                nextJitterTime = Time.time + refreshRate;
                JitterArcs(burstTimer > 0.0f);
            }

            if (burstTimer <= 0.0f && intensity <= 0.01f)
            {
                SetIntensity(0.0f, false);
            }
        }

        private void EnsureArcs()
        {
            Transform root = transform.Find(ArcRootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(ArcRootName);
                root = rootObject.transform;
                root.SetParent(transform, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }

            arcMaterial = CreateArcMaterial();
            arcs = new LineRenderer[arcCount];
            for (int i = 0; i < arcCount; i++)
            {
                Transform child = root.Find("Arc" + i);
                GameObject childObject;
                if (child == null)
                {
                    childObject = new GameObject("Arc" + i);
                    child = childObject.transform;
                    child.SetParent(root, false);
                }
                else
                {
                    childObject = child.gameObject;
                }

                LineRenderer line = childObject.GetComponent<LineRenderer>();
                if (line == null)
                {
                    line = childObject.AddComponent<LineRenderer>();
                }

                line.useWorldSpace = false;
                line.positionCount = 5;
                line.material = arcMaterial;
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.enabled = false;
                arcs[i] = line;
            }

            JitterArcs(false);
        }

        private Material CreateArcMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            Material material = new Material(shader);
            material.name = "SuperBallLightningArcMaterial";
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.75f, 1.0f, 0.12f, 0.85f) * emission);
            }

            return material;
        }

        private void JitterArcs(bool burst)
        {
            float activeIntensity = Mathf.Max(intensity, burst ? 1.0f : 0.0f);
            for (int i = 0; i < arcs.Length; i++)
            {
                LineRenderer line = arcs[i];
                if (line == null)
                {
                    continue;
                }

                Vector3 start = RandomOnShell(baseRadius * Mathf.Lerp(0.72f, 0.98f, activeIntensity));
                Vector3 end = RandomOnShell(baseRadius * Mathf.Lerp(0.88f, 1.18f, activeIntensity));
                Vector3 midpoint = (start + end) * 0.5f;
                Vector3 normal = midpoint.sqrMagnitude < 0.01f ? Random.onUnitSphere : midpoint.normalized;
                float jitter = Mathf.Lerp(jitterAmount * 0.30f, jitterAmount, activeIntensity);
                line.SetPosition(0, start);
                line.SetPosition(1, Vector3.Lerp(start, end, 0.28f) + Random.onUnitSphere * jitter + normal * 0.08f);
                line.SetPosition(2, midpoint + Random.onUnitSphere * jitter + normal * 0.16f);
                line.SetPosition(3, Vector3.Lerp(start, end, 0.72f) + Random.onUnitSphere * jitter + normal * 0.08f);
                line.SetPosition(4, end);
            }
        }

        private static Vector3 RandomOnShell(float radius)
        {
            Vector3 point = Random.onUnitSphere;
            point.y *= 0.72f;
            if (point.sqrMagnitude < 0.01f)
            {
                point = Vector3.forward;
            }

            return point.normalized * radius;
        }
    }
}
