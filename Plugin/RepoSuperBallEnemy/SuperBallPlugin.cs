using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using REPOLib.Modules;
using REPOLib.Objects.Sdk;
using UnityEngine;
using UnityEngine.AI;

namespace RepoSuperBallEnemy
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class SuperBallPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "James.RepoSuperBallEnemy";
        public const string PluginName = "REPO Super Ball Enemy";
        public const string PluginVersion = "0.2.1";

        private const string EnemySetupName = "SuperBallEnemySetup";
        private const string EnemyPrefabName = "SuperBallEnemy";
        private const string EnemyDisplayName = "Super Ball";
        private const string SphereVisualName = "SuperBallChromeSphere";
        private const string AuraVisualName = "SuperBallAuraSphere";
        private const string ConceptRootName = "SuperBallConceptVisuals";
        private const string InnerCoreVisualName = "SuperBallInnerCore";
        private const string CrackShellVisualName = "SuperBallCrackShell";
        private const string FaceRootName = "SuperBallHellFace";
        private const string LeftEyeVisualName = "SuperBallLeftEye";
        private const string RightEyeVisualName = "SuperBallRightEye";
        private const string GrinVisualName = "SuperBallGrin";
        private const string HighlightVisualName = "SuperBallChromeHighlight";
        private const string SpawnDebugSphereName = "SuperBallF8VisibilitySphere";
        private const string StandaloneDebugSphereName = "SuperBallStandaloneDebugSphere";
        private const string GlowLightName = "SuperBallGreenGlow";
        private const string PrefabTemplateContainerName = "SuperBallPrefabTemplates";
        private const float SpawnCooldownSeconds = 1.0f;

        private static ManualLogSource Log;

        private ConfigEntry<bool> enableSuperBall;
        private ConfigEntry<float> superBallDiameter;
        private ConfigEntry<KeyCode> spawnTestKey;
        private ConfigEntry<bool> enableSpawnPoolInjection;
        private ConfigEntry<float> emissionIntensity;
        private ConfigEntry<float> spawnDistance;
        private ConfigEntry<bool> enableBounceVisuals;
        private ConfigEntry<float> superBallAlpha;
        private ConfigEntry<bool> enableConceptFace;
        private ConfigEntry<bool> enableInternalCracks;
        private ConfigEntry<bool> enableChromeHighlights;
        private ConfigEntry<float> faceGlowIntensity;
        private ConfigEntry<float> crackGlowIntensity;
        private ConfigEntry<float> crackLayerAlpha;
        private ConfigEntry<float> innerCoreAlpha;
        private ConfigEntry<bool> enableFallbackDebugSphere;
        private ConfigEntry<bool> enablePhysicalBlockingCollider;
        private ConfigEntry<bool> disableInheritedBaseAttacks;
        private ConfigEntry<bool> enableCustomSuperBallBehavior;
        private ConfigEntry<int> contactDamage;
        private ConfigEntry<int> chargedDamage;
        private ConfigEntry<float> chargeWarningSeconds;
        private ConfigEntry<float> chargeCooldownSeconds;
        private ConfigEntry<float> bounceVisualHeight;
        private ConfigEntry<float> bounceVisualSpeed;
        private ConfigEntry<float> chargeSpinMaxSpeed;
        private ConfigEntry<float> chargeAuraScale;
        private ConfigEntry<bool> auraEnabled;
        private ConfigEntry<float> auraAlpha;
        private ConfigEntry<float> auraScaleMultiplier;
        private ConfigEntry<float> roamSpeed;
        private ConfigEntry<float> idleBounceAmplitude;
        private ConfigEntry<float> idleBounceFrequency;
        private ConfigEntry<float> chargeBounceAmplitudeMin;
        private ConfigEntry<float> chargeBounceAmplitudeMax;
        private ConfigEntry<float> chargeSpinSpeedMin;
        private ConfigEntry<float> chargeSpinSpeedMax;
        private ConfigEntry<float> chargeSpeed;
        private ConfigEntry<int> maxRicochetCount;
        private ConfigEntry<float> recoveryDuration;

        private EnemySetup superBallSetup;
        private GameObject superBallPrefab;
        private GameObject prefabTemplateContainer;
        private bool registrationAttempted;
        private bool registrationSucceeded;
        private float nextSpawnAllowedTime;

        private void Awake()
        {
            Log = Logger;

            enableSuperBall = Config.Bind("General", "EnableSuperBall", true, "Enable creation and test spawning of the Super Ball enemy.");
            superBallDiameter = Config.Bind("Visuals", "SuperBallDiameter", 0.55f, "Runtime sphere diameter in meters. Clamped to 0.40-0.75m for v0.2.x.");
            emissionIntensity = Config.Bind("Visuals", "MainEmission", 3.15f, "Main sphere green emission multiplier.");
            superBallAlpha = Config.Bind("Visuals", "MainAlpha", 0.38f, "Main sphere alpha for the translucent glass-rubber Super Ball look.");
            enableConceptFace = Config.Bind("Visuals", "EnableConceptFace", true, "Enable the evil glowing face decal inspired by the concept sheet.");
            enableInternalCracks = Config.Bind("Visuals", "EnableInternalCracks", true, "Enable procedural internal green crack/glass veins.");
            enableChromeHighlights = Config.Bind("Visuals", "EnableChromeHighlights", true, "Enable subtle chrome-like white/green highlight decals.");
            faceGlowIntensity = Config.Bind("Visuals", "FaceGlowIntensity", 5.5f, "Emission multiplier for the evil face decals.");
            crackGlowIntensity = Config.Bind("Visuals", "CrackGlowIntensity", 4.75f, "Emission multiplier for the internal crack layer.");
            crackLayerAlpha = Config.Bind("Visuals", "CrackLayerAlpha", 0.72f, "Alpha for the procedural crack/glass vein layer.");
            innerCoreAlpha = Config.Bind("Visuals", "InnerCoreAlpha", 0.26f, "Alpha for the darker inner glass core.");
            auraEnabled = Config.Bind("Visuals", "AuraEnabled", true, "Enable the pulsing charge aura sphere.");
            auraAlpha = Config.Bind("Visuals", "AuraAlpha", 0.24f, "Maximum aura alpha during charge warning.");
            auraScaleMultiplier = Config.Bind("Visuals", "AuraScaleMultiplier", 1.70f, "Aura sphere scale multiplier relative to the main sphere.");
            enableBounceVisuals = Config.Bind("Visuals", "EnableBounceVisuals", true, "Enable visual bobbing and rolling on the sphere body.");
            enableFallbackDebugSphere = Config.Bind("Diagnostics", "EnableFallbackDebugSphere", true, "Create a standalone fallback sphere only if the spawned enemy hierarchy is inactive or invisible.");
            enablePhysicalBlockingCollider = Config.Bind("Physics", "EnablePhysicalBlockingCollider", true, "Use a non-trigger sphere collider for early physical blocking tests.");
            disableInheritedBaseAttacks = Config.Bind("Behavior", "DisableInheritedBaseAttacks", true, "Disable suspicious inherited base enemy attack components such as Animal/zap/ranged scripts.");
            enableCustomSuperBallBehavior = Config.Bind("Behavior", "EnableCustomSuperBallBehavior", true, "Attach the safe Super Ball v0.2 behavior scaffold.");
            contactDamage = Config.Bind("Behavior", "ContactDamage", 5, "Logged normal contact damage amount. The actual player damage API is not wired yet.");
            chargedDamage = Config.Bind("Behavior", "ChargedDamage", 10, "Logged charged contact damage amount. The actual player damage API is not wired yet.");
            chargeWarningSeconds = Config.Bind("Behavior", "ChargeWarningDuration", 3.0f, "Seconds of visible charge warning before launch.");
            chargeCooldownSeconds = Config.Bind("Behavior", "ChargeCooldownSeconds", 5.0f, "Seconds before another charge warning can start.");
            bounceVisualHeight = Config.Bind("Behavior", "BounceVisualHeight", 0.12f, "Legacy visual bounce height fallback when custom behavior is disabled.");
            bounceVisualSpeed = Config.Bind("Behavior", "BounceVisualSpeed", 1.8f, "Legacy visual bounce speed fallback when custom behavior is disabled.");
            chargeSpinMaxSpeed = Config.Bind("Behavior", "ChargeSpinMaxSpeed", 1080.0f, "Legacy maximum visual spin speed during charge warning.");
            chargeAuraScale = Config.Bind("Behavior", "ChargeAuraScale", 2.25f, "Aura/light multiplier used during charge warning.");
            roamSpeed = Config.Bind("Behavior", "RoamSpeed", 1.25f, "Idle roam movement speed.");
            idleBounceAmplitude = Config.Bind("Behavior", "IdleBounceAmplitude", 0.06f, "Idle roam bounce amplitude.");
            idleBounceFrequency = Config.Bind("Behavior", "IdleBounceFrequency", 1.55f, "Idle roam bounce frequency.");
            chargeBounceAmplitudeMin = Config.Bind("Behavior", "ChargeBounceAmplitudeMin", 0.04f, "Charge warning starting bounce amplitude.");
            chargeBounceAmplitudeMax = Config.Bind("Behavior", "ChargeBounceAmplitudeMax", 0.22f, "Charge warning maximum bounce amplitude.");
            chargeSpinSpeedMin = Config.Bind("Behavior", "ChargeSpinSpeedMin", 120.0f, "Charge warning starting spin speed.");
            chargeSpinSpeedMax = Config.Bind("Behavior", "ChargeSpinSpeedMax", 1320.0f, "Charge warning maximum spin speed.");
            chargeSpeed = Config.Bind("Behavior", "ChargeSpeed", 9.5f, "Physical charge launch movement speed.");
            maxRicochetCount = Config.Bind("Behavior", "MaxRicochetCount", 3, "Maximum wall ricochets during one charge launch.");
            recoveryDuration = Config.Bind("Behavior", "RecoveryDuration", 1.25f, "Brief calm-down duration after a charge burst.");
            spawnTestKey = Config.Bind("Testing", "SpawnTestKey", KeyCode.F8, "Press this key as host/single-player to spawn Super Ball near the local player.");
            spawnDistance = Config.Bind("Testing", "SpawnDistance", 4.0f, "Meters in front of the local player/camera for test spawning.");
            enableSpawnPoolInjection = Config.Bind("Spawning", "EnableSpawnPoolInjection", false, "If true, also inject Super Ball into the vanilla enemy director list using the base enemy difficulty.");

            SuperBallBehavior.SetLogger(Log);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
            Log.LogInfo($"Runtime v0.2 uses REPOLib, a cloned vanilla enemy prefab, owned Super Ball visuals, and inherited attack suppression. Test spawn key: {spawnTestKey.Value}.");
        }

        private IEnumerator Start()
        {
            if (!enableSuperBall.Value)
            {
                Log.LogWarning("Super Ball is disabled by config.");
                yield break;
            }

            yield return RegisterWhenEnemyDirectorReady();
        }

        private void Update()
        {
            if (!enableSuperBall.Value)
            {
                return;
            }

            if (Input.GetKeyDown(spawnTestKey.Value))
            {
                if (Time.unscaledTime < nextSpawnAllowedTime)
                {
                    Log.LogInfo($"Detected {spawnTestKey.Value}, but Super Ball test spawn is cooling down.");
                    return;
                }

                nextSpawnAllowedTime = Time.unscaledTime + SpawnCooldownSeconds;
                Log.LogInfo($"Detected {spawnTestKey.Value}; attempting Super Ball test spawn.");
                SpawnSuperBallNearPlayer();
            }
        }

        private IEnumerator RegisterWhenEnemyDirectorReady()
        {
            if (registrationAttempted)
            {
                yield break;
            }

            registrationAttempted = true;

            while (EnemyDirector.instance == null)
            {
                Log.LogInfo("Waiting for EnemyDirector before building Super Ball.");
                yield return new WaitForSeconds(2.0f);
            }

            EnemySetup baseSetup = null;
            EnemyParent baseParent = null;

            while (baseSetup == null)
            {
                baseSetup = FindBestBaseEnemySetup(out baseParent);
                if (baseSetup == null)
                {
                    Log.LogInfo("Waiting for vanilla enemy setup data before building Super Ball.");
                    yield return new WaitForSeconds(2.0f);
                }
            }

            registrationSucceeded = TryCreateAndRegisterSuperBall(baseSetup, baseParent);
            if (registrationSucceeded)
            {
                Log.LogInfo("Super Ball registration completed. Press the configured test key to spawn it.");
            }
        }

        private EnemySetup FindBestBaseEnemySetup(out EnemyParent enemyParent)
        {
            enemyParent = null;

            List<EnemySetup> setups = GetDirectorEnemySetups();
            if (setups.Count == 0)
            {
                return null;
            }

            string[] preferredTokens = new[] { "animal", "runner", "slow walker", "slow", "hunter", "duck" };
            for (int i = 0; i < preferredTokens.Length; i++)
            {
                string token = preferredTokens[i];
                for (int j = 0; j < setups.Count; j++)
                {
                    EnemyParent parent;
                    if (!TryGetEnemyParent(setups[j], out parent))
                    {
                        continue;
                    }

                    string setupName = setups[j].name ?? string.Empty;
                    string enemyName = parent.enemyName ?? string.Empty;
                    string haystack = setupName + " " + enemyName;
                    if (haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        enemyParent = parent;
                        Log.LogInfo($"Selected base enemy '{enemyName}' from setup '{setupName}' using token '{token}'.");
                        return setups[j];
                    }
                }
            }

            for (int i = 0; i < setups.Count; i++)
            {
                EnemyParent parent;
                if (TryGetEnemyParent(setups[i], out parent))
                {
                    enemyParent = parent;
                    Log.LogWarning($"Animal-like base enemy was not found; falling back to '{parent.enemyName}' from setup '{setups[i].name}'.");
                    return setups[i];
                }
            }

            return null;
        }

        private List<EnemySetup> GetDirectorEnemySetups()
        {
            List<EnemySetup> setups = new List<EnemySetup>();
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
            {
                return setups;
            }

            AddSetups(setups, director.enemiesDifficulty1);
            AddSetups(setups, director.enemiesDifficulty2);
            AddSetups(setups, director.enemiesDifficulty3);
            return setups;
        }

        private static void AddSetups(List<EnemySetup> destination, List<EnemySetup> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && !destination.Contains(source[i]))
                {
                    destination.Add(source[i]);
                }
            }
        }

        private bool TryCreateAndRegisterSuperBall(EnemySetup baseSetup, EnemyParent baseParent)
        {
            try
            {
                GameObject basePrefab = GetFirstSpawnPrefab(baseSetup);
                if (basePrefab == null)
                {
                    Log.LogError("Could not create Super Ball because the base enemy has no usable spawn prefab.");
                    return false;
                }

                if (baseParent == null)
                {
                    TryGetEnemyParent(baseSetup, out baseParent);
                }

                superBallPrefab = CloneBaseEnemyPrefab(basePrefab);
                EnemyParent superBallParent = superBallPrefab.GetComponent<EnemyParent>();
                if (superBallParent == null)
                {
                    superBallParent = superBallPrefab.GetComponentInChildren<EnemyParent>(true);
                }

                if (superBallParent == null)
                {
                    Log.LogError("Could not create Super Ball because the cloned prefab has no EnemyParent component.");
                    UnityEngine.Object.Destroy(superBallPrefab);
                    return false;
                }

                superBallParent.enemyName = EnemyDisplayName;
                if (baseParent != null)
                {
                    superBallParent.difficulty = baseParent.difficulty;
                }

                BuildSphereVisual(superBallPrefab);
                superBallPrefab.SetActive(true);

                superBallSetup = ScriptableObject.CreateInstance<EnemySetup>();
                superBallSetup.name = EnemySetupName;
                superBallSetup.levelsCompletedCondition = baseSetup.levelsCompletedCondition;
                superBallSetup.levelsCompletedMin = baseSetup.levelsCompletedMin;
                superBallSetup.levelsCompletedMax = baseSetup.levelsCompletedMax;
                superBallSetup.rarityPreset = baseSetup.rarityPreset;
                superBallSetup.runsPlayed = baseSetup.runsPlayed;

                EnemyContent enemyContent = ScriptableObject.CreateInstance<EnemyContent>();
                SetPrivateField(enemyContent, "_setup", superBallSetup);
                SetPrivateField(enemyContent, "_spawnObjects", new List<GameObject> { superBallPrefab });

                Enemies.RegisterEnemy(enemyContent);

                if (superBallSetup.spawnObjects == null || superBallSetup.spawnObjects.Count == 0)
                {
                    Log.LogError("REPOLib did not assign any spawn objects to Super Ball; registration failed.");
                    return false;
                }

                if (enableSpawnPoolInjection.Value)
                {
                    EnsureSuperBallInEnemyDirector(superBallParent.difficulty);
                }
                else
                {
                    Log.LogInfo("Spawn pool injection is disabled; Super Ball is available through the F8 test spawn only.");
                }

                Log.LogInfo($"Registered {EnemyDisplayName} using prefab '{superBallPrefab.name}' and setup '{superBallSetup.name}'.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Error while creating/registering Super Ball: {ex}");
                return false;
            }
        }

        private GameObject CloneBaseEnemyPrefab(GameObject basePrefab)
        {
            bool baseWasActive = basePrefab.activeSelf;
            GameObject clone;

            try
            {
                basePrefab.SetActive(false);
                clone = UnityEngine.Object.Instantiate(basePrefab);
            }
            finally
            {
                basePrefab.SetActive(baseWasActive);
            }

            clone.name = EnemyPrefabName;
            clone.transform.SetParent(GetPrefabTemplateContainer().transform, false);
            clone.SetActive(true);
            return clone;
        }

        private GameObject GetPrefabTemplateContainer()
        {
            if (prefabTemplateContainer != null)
            {
                return prefabTemplateContainer;
            }

            prefabTemplateContainer = new GameObject(PrefabTemplateContainerName);
            prefabTemplateContainer.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(prefabTemplateContainer);
            return prefabTemplateContainer;
        }

        private void BuildSphereVisual(GameObject prefab)
        {
            RendererCleanupResult cleanup = DisableInheritedRenderers(prefab, "Prefab visual build");
            AttackCleanupResult attackCleanup = DisableInheritedAttackComponents(prefab, "Prefab visual build");
            RemoveDuplicateOwnedSpheres(prefab, null, "Prefab visual build");

            float diameter = GetConfiguredDiameter();
            float radius = diameter * 0.5f;
            Vector3 visibleCenter = GetSafeSphereLocalCenter(diameter);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = SphereVisualName;
            sphere.transform.SetParent(prefab.transform, false);
            sphere.transform.localPosition = visibleCenter;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.layer = prefab.layer;
            sphere.SetActive(true);

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateSuperBallMaterial();
                renderer.enabled = true;
            }

            SphereCollider sphereCollider = EnsureSphereCollider(sphere, diameter, "Prefab visual build");
            Rigidbody rigidbody = EnsureSuperBallRigidbody(sphere);
            Light light = EnsurePointLight(sphere.transform, diameter);
            Renderer auraRenderer = EnsureAuraVisual(sphere.transform, diameter);
            SuperBallConceptVisuals conceptVisuals = EnsureConceptVisuals(prefab, sphere.transform, diameter);
            ColliderCleanupResult colliderCleanup = DisableInheritedColliders(prefab, sphereCollider, "Prefab visual build");
            EnsureVisualMotion(prefab, sphere.transform, visibleCenter, diameter);
            SuperBallBehavior behavior = EnsureSuperBallBehavior(prefab, sphere.transform, auraRenderer == null ? null : auraRenderer.transform, auraRenderer, light, sphereCollider, rigidbody, diameter);

            LogVisualStats(prefab, "Prefab visual build");
            Log.LogInfo($"Prefab visual build renderer cleanup: inheritedRenderersFound={cleanup.InheritedRenderersFound}, disabled={cleanup.DisabledCount}, disabledObjects=[{string.Join(", ", cleanup.DisabledRendererNames.ToArray())}], keptSuperBallRenderer='{GetObjectName(renderer)}'.");
            Log.LogInfo($"Prefab visual build collider cleanup: totalCollidersFound={colliderCleanup.TotalFound}, disabled={colliderCleanup.DisabledCount}, kept={colliderCleanup.KeptCount}, activeAfterCleanup={colliderCleanup.ActiveAfterCleanup}, keptColliders=[{string.Join(", ", colliderCleanup.KeptColliderNames.ToArray())}], disabledColliders=[{string.Join(", ", colliderCleanup.DisabledColliderNames.ToArray())}].");
            Log.LogInfo($"Prefab visual build attack cleanup: attackLikeComponentsFound={attackCleanup.FoundCount}, disabled={attackCleanup.DisabledCount}, disabledComponents=[{string.Join(", ", attackCleanup.DisabledComponentNames.ToArray())}], keptComponents=[{string.Join(", ", attackCleanup.KeptComponentNames.ToArray())}].");
            Log.LogInfo($"Built runtime sphere visual. Diameter={diameter:0.00}m, colliderEffectiveRadius={radius:0.00}m, localCenter={visibleCenter}, materialColor={FormatColor(GetSuperBallBodyColor())}, emission={GetDebugEmissionIntensity():0.00}, alpha={GetSuperBallAlpha():0.00}, transparencyMode='{GetMaterialTransparencyMode()}', auraRenderer={(auraRenderer != null)}, conceptVisuals={(conceptVisuals != null && conceptVisuals.enabled)}, behaviorAttached={behavior != null}.");
        }

        private Material CreateSuperBallMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            Material material = new Material(shader);
            Color bodyColor = GetSuperBallBodyColor();
            Color emissionColor = GetSuperBallEmissionColor() * GetDebugEmissionIntensity();

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", bodyColor);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", bodyColor);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.30f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 1.0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 1.0f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
            }

            ApplyTransparencySettings(material);
            return material;
        }

        private Material CreateInnerCoreMaterial()
        {
            Material material = CreateTransparentStandardMaterial(
                "SuperBallInnerCoreMaterial",
                new Color(0.01f, 0.20f, 0.035f, Mathf.Clamp(innerCoreAlpha.Value, 0.05f, 0.65f)),
                new Color(0.0f, 0.85f, 0.08f, 1.0f) * Mathf.Clamp(crackGlowIntensity.Value * 0.32f, 0.2f, 3.5f),
                CreateInnerCoreTexture(),
                0.05f,
                0.92f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 4;
            return material;
        }

        private Material CreateCrackShellMaterial()
        {
            float alpha = Mathf.Clamp(crackLayerAlpha.Value, 0.10f, 1.0f);
            Material material = CreateTransparentStandardMaterial(
                "SuperBallCrackShellMaterial",
                new Color(0.20f, 1.0f, 0.03f, alpha),
                new Color(0.04f, 1.0f, 0.02f, 1.0f) * Mathf.Clamp(crackGlowIntensity.Value, 0.5f, 12.0f),
                CreateCrackTexture(),
                0.0f,
                0.98f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 18;
            return material;
        }

        private Material CreateFaceDecalMaterial(Texture2D texture, Color tint, float glowIntensity)
        {
            Material material = CreateTransparentStandardMaterial(
                "SuperBallFaceDecalMaterial",
                tint,
                tint * Mathf.Clamp(glowIntensity, 1.0f, 12.0f),
                texture,
                0.0f,
                0.2f);
            SetCullOff(material);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 40;
            return material;
        }

        private Material CreateHighlightMaterial()
        {
            Material material = CreateTransparentStandardMaterial(
                "SuperBallChromeHighlightMaterial",
                new Color(0.85f, 1.0f, 0.70f, 0.30f),
                new Color(0.35f, 1.0f, 0.12f, 1.0f) * 1.55f,
                CreateHighlightTexture(),
                0.0f,
                1.0f);
            SetCullOff(material);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 45;
            return material;
        }

        private Material CreateTransparentStandardMaterial(string materialName, Color color, Color emission, Texture2D texture, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            Material material = new Material(shader);
            material.name = materialName;

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (texture != null)
            {
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                }
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                }
                if (material.HasProperty("_EmissionMap"))
                {
                    material.SetTexture("_EmissionMap", texture);
                }
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
            }

            ApplyTransparencySettings(material);
            return material;
        }

        private void SetCullOff(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
        }

        private Renderer EnsureAuraVisual(Transform sphereTransform, float diameter)
        {
            if (sphereTransform == null)
            {
                return null;
            }

            Transform auraTransform = sphereTransform.Find(AuraVisualName);
            GameObject auraObject;
            if (auraTransform == null)
            {
                auraObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                auraObject.name = AuraVisualName;
                auraObject.transform.SetParent(sphereTransform, false);
                auraTransform = auraObject.transform;
            }
            else
            {
                auraObject = auraTransform.gameObject;
            }

            auraObject.layer = sphereTransform.gameObject.layer;
            auraTransform.localPosition = Vector3.zero;
            auraTransform.localRotation = Quaternion.identity;
            auraTransform.localScale = Vector3.one * Mathf.Clamp(auraScaleMultiplier.Value, 1.05f, 2.5f);

            Collider[] auraColliders = auraObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < auraColliders.Length; i++)
            {
                if (auraColliders[i] != null)
                {
                    auraColliders[i].enabled = false;
                }
            }

            Renderer renderer = auraObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateAuraMaterial(0.0f);
                renderer.enabled = false;
            }

            Log.LogInfo($"Aura visual setup: auraEnabled={auraEnabled.Value}, object='{AuraVisualName}', diameter={diameter:0.00}, localScale={FormatVector3(auraTransform.localScale)}, maxAlpha={Mathf.Clamp(auraAlpha.Value, 0.02f, 0.45f):0.00}.");
            return renderer;
        }

        private SuperBallConceptVisuals EnsureConceptVisuals(GameObject root, Transform sphereTransform, float diameter)
        {
            if (root == null || sphereTransform == null)
            {
                return null;
            }

            float radius = diameter * 0.5f;
            bool faceEnabled = enableConceptFace.Value;
            bool cracksEnabled = enableInternalCracks.Value;
            bool highlightsEnabled = enableChromeHighlights.Value;

            Renderer innerCoreRenderer = EnsurePrimitiveRenderer(
                sphereTransform,
                InnerCoreVisualName,
                PrimitiveType.Sphere,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 0.74f,
                CreateInnerCoreMaterial(),
                root.layer);
            SetRendererEnabled(innerCoreRenderer, cracksEnabled);

            Renderer crackRenderer = EnsurePrimitiveRenderer(
                sphereTransform,
                CrackShellVisualName,
                PrimitiveType.Sphere,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 1.012f,
                CreateCrackShellMaterial(),
                root.layer);
            SetRendererEnabled(crackRenderer, cracksEnabled);

            Transform conceptRoot = FindChildTransform(root.transform, ConceptRootName);
            if (conceptRoot == null)
            {
                GameObject conceptObject = new GameObject(ConceptRootName);
                conceptRoot = conceptObject.transform;
                conceptRoot.SetParent(root.transform, false);
            }

            conceptRoot.localPosition = GetSafeSphereLocalCenter(diameter);
            conceptRoot.localRotation = Quaternion.identity;
            conceptRoot.localScale = Vector3.one;
            conceptRoot.gameObject.layer = root.layer;
            conceptRoot.gameObject.SetActive(true);

            Transform faceRoot = FindChildTransform(conceptRoot, FaceRootName);
            if (faceRoot == null)
            {
                GameObject faceObject = new GameObject(FaceRootName);
                faceRoot = faceObject.transform;
                faceRoot.SetParent(conceptRoot, false);
            }

            faceRoot.localPosition = Vector3.zero;
            faceRoot.localRotation = Quaternion.identity;
            faceRoot.localScale = Vector3.one;
            faceRoot.gameObject.layer = root.layer;
            faceRoot.gameObject.SetActive(faceEnabled);

            float faceZ = radius * 1.035f;
            Renderer leftEye = EnsurePrimitiveRenderer(
                faceRoot,
                LeftEyeVisualName,
                PrimitiveType.Quad,
                new Vector3(-radius * 0.32f, radius * 0.17f, faceZ),
                Quaternion.identity,
                new Vector3(radius * 0.58f, radius * 0.23f, 1.0f),
                CreateFaceDecalMaterial(CreateEyeTexture(false), new Color(0.66f, 1.0f, 0.05f, 1.0f), Mathf.Clamp(faceGlowIntensity.Value, 1.0f, 12.0f)),
                root.layer);
            Renderer rightEye = EnsurePrimitiveRenderer(
                faceRoot,
                RightEyeVisualName,
                PrimitiveType.Quad,
                new Vector3(radius * 0.32f, radius * 0.17f, faceZ),
                Quaternion.identity,
                new Vector3(radius * 0.58f, radius * 0.23f, 1.0f),
                CreateFaceDecalMaterial(CreateEyeTexture(true), new Color(0.66f, 1.0f, 0.05f, 1.0f), Mathf.Clamp(faceGlowIntensity.Value, 1.0f, 12.0f)),
                root.layer);
            Renderer grin = EnsurePrimitiveRenderer(
                faceRoot,
                GrinVisualName,
                PrimitiveType.Quad,
                new Vector3(0.0f, -radius * 0.22f, faceZ + 0.006f),
                Quaternion.identity,
                new Vector3(radius * 1.15f, radius * 0.34f, 1.0f),
                CreateFaceDecalMaterial(CreateGrinTexture(), new Color(0.62f, 1.0f, 0.02f, 1.0f), Mathf.Clamp(faceGlowIntensity.Value * 0.85f, 1.0f, 12.0f)),
                root.layer);

            Renderer highlightLarge = EnsurePrimitiveRenderer(
                faceRoot,
                HighlightVisualName + "Large",
                PrimitiveType.Quad,
                new Vector3(-radius * 0.24f, radius * 0.37f, faceZ + 0.011f),
                Quaternion.Euler(0.0f, 0.0f, -20.0f),
                new Vector3(radius * 0.40f, radius * 0.17f, 1.0f),
                CreateHighlightMaterial(),
                root.layer);
            Renderer highlightSmall = EnsurePrimitiveRenderer(
                faceRoot,
                HighlightVisualName + "Small",
                PrimitiveType.Quad,
                new Vector3(radius * 0.22f, radius * 0.32f, faceZ + 0.012f),
                Quaternion.Euler(0.0f, 0.0f, 24.0f),
                new Vector3(radius * 0.24f, radius * 0.09f, 1.0f),
                CreateHighlightMaterial(),
                root.layer);

            SetRendererEnabled(leftEye, faceEnabled);
            SetRendererEnabled(rightEye, faceEnabled);
            SetRendererEnabled(grin, faceEnabled);
            SetRendererEnabled(highlightLarge, highlightsEnabled);
            SetRendererEnabled(highlightSmall, highlightsEnabled);

            Light faceLight = EnsureFaceLight(faceRoot, diameter);
            faceLight.enabled = faceEnabled;

            SuperBallConceptVisuals visuals = root.GetComponent<SuperBallConceptVisuals>();
            if (visuals == null)
            {
                visuals = root.AddComponent<SuperBallConceptVisuals>();
            }

            visuals.Configure(
                faceRoot,
                new[] { leftEye, rightEye, grin },
                new[] { highlightLarge, highlightSmall },
                crackRenderer,
                innerCoreRenderer,
                faceLight,
                faceEnabled,
                cracksEnabled,
                highlightsEnabled,
                Mathf.Clamp(faceGlowIntensity.Value, 1.0f, 12.0f),
                Mathf.Clamp(crackGlowIntensity.Value, 0.5f, 12.0f),
                Mathf.Clamp(crackLayerAlpha.Value, 0.10f, 1.0f),
                Mathf.Clamp(innerCoreAlpha.Value, 0.05f, 0.65f));

            Log.LogInfo($"Concept visual setup: faceEnabled={faceEnabled}, cracksEnabled={cracksEnabled}, highlightsEnabled={highlightsEnabled}, faceRoot='{GetHierarchyPath(faceRoot, root.transform)}', crackShell='{GetObjectName(crackRenderer)}', innerCore='{GetObjectName(innerCoreRenderer)}', faceGlow={Mathf.Clamp(faceGlowIntensity.Value, 1.0f, 12.0f):0.00}, crackGlow={Mathf.Clamp(crackGlowIntensity.Value, 0.5f, 12.0f):0.00}.");
            return visuals;
        }

        private Renderer EnsurePrimitiveRenderer(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material, int layer)
        {
            Transform child = parent.Find(name);
            GameObject childObject;
            if (child == null)
            {
                childObject = GameObject.CreatePrimitive(primitiveType);
                childObject.name = name;
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }
            else
            {
                childObject = child.gameObject;
            }

            childObject.layer = layer;
            child.localPosition = localPosition;
            child.localRotation = localRotation;
            child.localScale = localScale;
            childObject.SetActive(true);
            DisableAllColliders(childObject);

            Renderer renderer = childObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = material;
                renderer.enabled = true;
            }

            return renderer;
        }

        private Light EnsureFaceLight(Transform faceRoot, float diameter)
        {
            const string faceLightName = "SuperBallFaceGlowLight";
            Transform lightTransform = faceRoot.Find(faceLightName);
            GameObject lightObject;
            if (lightTransform == null)
            {
                lightObject = new GameObject(faceLightName);
                lightTransform = lightObject.transform;
                lightTransform.SetParent(faceRoot, false);
            }
            else
            {
                lightObject = lightTransform.gameObject;
            }

            lightObject.layer = faceRoot.gameObject.layer;
            lightTransform.localPosition = new Vector3(0.0f, 0.02f, diameter * 0.46f);
            lightTransform.localRotation = Quaternion.identity;
            lightTransform.localScale = Vector3.one;

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = LightType.Point;
            light.color = new Color(0.58f, 1.0f, 0.02f, 1.0f);
            light.range = Mathf.Clamp(diameter * 2.4f, 0.75f, 2.4f);
            light.intensity = Mathf.Clamp(faceGlowIntensity.Value * 0.28f, 0.15f, 2.2f);
            return light;
        }

        private Material CreateAuraMaterial(float alpha)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            Material material = new Material(shader);
            Color auraColor = new Color(0.10f, 1.0f, 0.05f, Mathf.Clamp(alpha, 0.0f, 0.45f));
            Color emissionColor = new Color(0.0f, 1.0f, 0.06f, 1.0f) * Mathf.Clamp(GetDebugEmissionIntensity() * 1.35f, 0.5f, 7.0f);

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", auraColor);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", auraColor);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.98f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.98f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
            }

            ApplyTransparencySettings(material);
            return material;
        }

        private static Texture2D CreateEyeTexture(bool mirror)
        {
            const int width = 96;
            const int height = 48;
            Color[] pixels = CreateClearPixels(width, height);
            Vector2[] polygon =
            {
                new Vector2(0.05f, 0.35f),
                new Vector2(0.96f, 0.54f),
                new Vector2(0.78f, 0.86f),
                new Vector2(0.16f, 0.67f)
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    if (mirror)
                    {
                        u = 1.0f - u;
                    }

                    Vector2 p = new Vector2(u, v);
                    float edgeDistance = DistanceToPolygonEdges(p, polygon);
                    if (PointInPolygon(p, polygon))
                    {
                        float centerGlow = Mathf.Clamp01(1.0f - edgeDistance * 7.0f);
                        float verticalGlow = Mathf.Clamp01((v - 0.30f) * 2.5f);
                        Color color = Color.Lerp(new Color(0.10f, 0.95f, 0.02f, 0.72f), new Color(0.92f, 1.0f, 0.20f, 1.0f), Mathf.Max(centerGlow, verticalGlow));
                        BlendPixel(pixels, width, x, y, color);
                    }
                    else if (edgeDistance < 0.10f)
                    {
                        float alpha = Mathf.Clamp01(1.0f - edgeDistance / 0.10f) * 0.35f;
                        BlendPixel(pixels, width, x, y, new Color(0.12f, 1.0f, 0.03f, alpha));
                    }
                }
            }

            return CreateTexture("SuperBallEyeTexture", width, height, pixels);
        }

        private static Texture2D CreateGrinTexture()
        {
            const int width = 192;
            const int height = 72;
            Color[] pixels = CreateClearPixels(width, height);
            Color line = new Color(0.70f, 1.0f, 0.02f, 0.95f);
            Color glow = new Color(0.05f, 1.0f, 0.02f, 0.45f);
            Vector2 previous = new Vector2(0.08f, 0.62f);

            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20.0f;
                float curve = 1.0f - Mathf.Pow(Mathf.Abs(t - 0.5f) * 2.0f, 1.7f);
                Vector2 next = new Vector2(Mathf.Lerp(0.08f, 0.92f, t), 0.52f - curve * 0.22f);
                DrawGlowLine(pixels, width, height, previous, next, line, 1.25f, glow, 5.0f);
                previous = next;
            }

            for (int i = 0; i < 8; i++)
            {
                float t = (i + 0.5f) / 8.0f;
                float x = Mathf.Lerp(0.14f, 0.86f, t);
                float curve = 1.0f - Mathf.Pow(Mathf.Abs(t - 0.5f) * 2.0f, 1.7f);
                float top = 0.48f - curve * 0.20f;
                float bottom = top - UnityEngine.Random.Range(0.12f, 0.28f);
                DrawGlowLine(pixels, width, height, new Vector2(x, top), new Vector2(x + UnityEngine.Random.Range(-0.035f, 0.035f), bottom), line, 1.05f, glow, 4.0f);
            }

            return CreateTexture("SuperBallGrinTexture", width, height, pixels);
        }

        private static Texture2D CreateHighlightTexture()
        {
            const int width = 96;
            const int height = 48;
            Color[] pixels = CreateClearPixels(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    float dx = (u - 0.5f) / 0.43f;
                    float dy = (v - 0.5f) / 0.22f;
                    float outer = dx * dx + dy * dy;
                    float inner = ((u - 0.43f) / 0.35f) * ((u - 0.43f) / 0.35f) + ((v - 0.47f) / 0.16f) * ((v - 0.47f) / 0.16f);
                    if (outer <= 1.0f && inner >= 0.62f)
                    {
                        float alpha = Mathf.Clamp01(1.0f - outer) * 0.55f + 0.10f;
                        BlendPixel(pixels, width, x, y, new Color(0.95f, 1.0f, 0.78f, alpha));
                    }
                }
            }

            return CreateTexture("SuperBallHighlightTexture", width, height, pixels);
        }

        private static Texture2D CreateCrackTexture()
        {
            const int width = 256;
            const int height = 128;
            Color[] pixels = CreateClearPixels(width, height);
            System.Random random = new System.Random(7731);
            Color hot = new Color(0.74f, 1.0f, 0.02f, 0.95f);
            Color glow = new Color(0.0f, 1.0f, 0.03f, 0.42f);

            for (int crack = 0; crack < 18; crack++)
            {
                Vector2 current = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                float angle = Mathf.Lerp(-2.8f, 2.8f, (float)random.NextDouble());
                int segments = random.Next(3, 7);
                for (int segment = 0; segment < segments; segment++)
                {
                    float length = Mathf.Lerp(0.035f, 0.12f, (float)random.NextDouble());
                    angle += Mathf.Lerp(-0.75f, 0.75f, (float)random.NextDouble());
                    Vector2 next = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                    next.x = Mathf.Repeat(next.x, 1.0f);
                    next.y = Mathf.Clamp01(next.y);
                    DrawGlowLine(pixels, width, height, current, next, hot, 0.75f, glow, 4.0f);

                    if (random.NextDouble() > 0.54)
                    {
                        float branchAngle = angle + Mathf.Lerp(-1.15f, 1.15f, (float)random.NextDouble());
                        Vector2 branch = current + new Vector2(Mathf.Cos(branchAngle), Mathf.Sin(branchAngle)) * length * Mathf.Lerp(0.35f, 0.70f, (float)random.NextDouble());
                        branch.x = Mathf.Repeat(branch.x, 1.0f);
                        branch.y = Mathf.Clamp01(branch.y);
                        DrawGlowLine(pixels, width, height, current, branch, hot, 0.52f, glow, 3.0f);
                    }

                    current = next;
                }
            }

            return CreateTexture("SuperBallCrackTexture", width, height, pixels);
        }

        private static Texture2D CreateInnerCoreTexture()
        {
            const int width = 128;
            const int height = 128;
            Color[] pixels = CreateClearPixels(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    float noise = Mathf.PerlinNoise(u * 5.2f + 11.0f, v * 5.2f + 27.0f);
                    float veins = Mathf.PerlinNoise(u * 16.0f + 6.0f, v * 16.0f + 19.0f);
                    float alpha = Mathf.Clamp01(0.10f + noise * 0.22f + Mathf.Pow(veins, 5.0f) * 0.45f);
                    Color color = Color.Lerp(new Color(0.0f, 0.12f, 0.02f, alpha), new Color(0.04f, 0.90f, 0.04f, alpha), Mathf.Pow(veins, 4.0f));
                    BlendPixel(pixels, width, x, y, color);
                }
            }

            return CreateTexture("SuperBallInnerCoreTexture", width, height, pixels);
        }

        private static Color[] CreateClearPixels(int width, int height)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            return pixels;
        }

        private static Texture2D CreateTexture(string name, int width, int height, Color[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawGlowLine(Color[] pixels, int width, int height, Vector2 a, Vector2 b, Color line, float lineThicknessPixels, Color glow, float glowThicknessPixels)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                    float distancePixels = DistanceToSegment(p, a, b) * Mathf.Max(width, height);
                    if (distancePixels <= glowThicknessPixels)
                    {
                        float alpha = Mathf.Clamp01(1.0f - distancePixels / glowThicknessPixels) * glow.a;
                        BlendPixel(pixels, width, x, y, new Color(glow.r, glow.g, glow.b, alpha));
                    }
                    if (distancePixels <= lineThicknessPixels)
                    {
                        float alpha = Mathf.Clamp01(1.0f - distancePixels / Mathf.Max(0.01f, lineThicknessPixels)) * line.a;
                        BlendPixel(pixels, width, x, y, new Color(line.r, line.g, line.b, alpha));
                    }
                }
            }
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = Vector2.Dot(ab, ab);
            if (denominator < 0.0001f)
            {
                return Vector2.Distance(p, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denominator);
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                if (((polygon[i].y > p.y) != (polygon[j].y > p.y)) &&
                    (p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y + 0.0001f) + polygon[i].x))
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        private static float DistanceToPolygonEdges(Vector2 p, Vector2[] polygon)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                distance = Mathf.Min(distance, DistanceToSegment(p, a, b));
            }

            return distance;
        }

        private static void BlendPixel(Color[] pixels, int width, int x, int y, Color source)
        {
            int index = y * width + x;
            Color destination = pixels[index];
            float outAlpha = source.a + destination.a * (1.0f - source.a);
            if (outAlpha <= 0.0001f)
            {
                pixels[index] = Color.clear;
                return;
            }

            pixels[index] = new Color(
                (source.r * source.a + destination.r * destination.a * (1.0f - source.a)) / outAlpha,
                (source.g * source.a + destination.g * destination.a * (1.0f - source.a)) / outAlpha,
                (source.b * source.a + destination.b * destination.a * (1.0f - source.a)) / outAlpha,
                outAlpha);
        }

        private void ApplyTransparencySettings(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 2.0f);
            }
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1.0f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0.0f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0.0f);
            }

            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private Color GetSuperBallBodyColor()
        {
            return new Color(0.10f, 1.0f, 0.10f, GetSuperBallAlpha());
        }

        private Color GetSuperBallEmissionColor()
        {
            return new Color(0.02f, 1.0f, 0.05f, 1.0f);
        }

        private float GetSuperBallAlpha()
        {
            return Mathf.Clamp(superBallAlpha.Value, 0.30f, 0.75f);
        }

        private string GetMaterialTransparencyMode()
        {
            return GetSuperBallAlpha() < 0.98f ? "transparent/fade" : "opaque";
        }

        private void EnsureSuperBallInEnemyDirector(EnemyParent.Difficulty difficulty)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null || superBallSetup == null)
            {
                return;
            }

            List<EnemySetup> list = null;
            switch (difficulty)
            {
                case EnemyParent.Difficulty.Difficulty1:
                    list = director.enemiesDifficulty1;
                    break;
                case EnemyParent.Difficulty.Difficulty2:
                    list = director.enemiesDifficulty2;
                    break;
                case EnemyParent.Difficulty.Difficulty3:
                    list = director.enemiesDifficulty3;
                    break;
            }

            if (list == null)
            {
                Log.LogWarning("Could not inject Super Ball into spawn pool because the difficulty list was unavailable.");
                return;
            }

            if (!list.Contains(superBallSetup))
            {
                list.Add(superBallSetup);
                Log.LogInfo($"Injected Super Ball into EnemyDirector list for {difficulty}.");
            }
        }

        private void SpawnSuperBallNearPlayer()
        {
            if (!registrationSucceeded || superBallSetup == null)
            {
                Log.LogWarning("Super Ball is not registered yet. Try again after entering a level.");
                return;
            }

            try
            {
                if (!SemiFunc.IsMasterClientOrSingleplayer())
                {
                    Log.LogWarning("Super Ball test spawn is only available to the host or in single-player.");
                    return;
                }

                Vector3 position = GetTestSpawnPosition();
                List<EnemyParent> spawned = Enemies.SpawnEnemy(superBallSetup, position, Quaternion.identity, false);
                int count = spawned == null ? 0 : spawned.Count;
                Log.LogInfo($"Super Ball test spawn requested at {position}. Spawned objects: {count}.");
                LogSpawnedObjects(spawned, position);
                Debug.Log($"[RepoSuperBallEnemy] Spawned {EnemyDisplayName} test enemy at {position}.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error while test spawning Super Ball: {ex}");
            }
        }

        private Vector3 GetTestSpawnPosition()
        {
            float diameter = GetConfiguredDiameter();
            float radius = diameter * 0.5f;
            float groundOffset = radius + 0.03f;
            float fallbackOffset = radius + 0.08f;
            float distance = Mathf.Clamp(spawnDistance.Value, 1.0f, 20.0f);

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 cameraPosition = mainCamera.transform.position;
                Vector3 cameraForward = mainCamera.transform.forward;
                Vector3 desiredPoint = cameraPosition + cameraForward * distance;
                Vector3 rayOrigin = desiredPoint + Vector3.up * 3.0f;
                Vector3 fallbackCenter = desiredPoint + Vector3.up * fallbackOffset;
                RaycastHit hit;

                Log.LogInfo($"F8 spawn placement input: cameraPosition={cameraPosition}, cameraForward={cameraForward}, desiredPoint={desiredPoint}, rayOrigin={rayOrigin}.");

                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 10.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    string hitObjectName = hit.collider == null ? "<none>" : hit.collider.name;
                    Log.LogInfo($"F8 spawn placement raycast: hitObject='{hitObjectName}', hitPoint={hit.point}, hitNormal={hit.normal}.");

                    if (hit.normal.y < 0.5f)
                    {
                        Log.LogWarning($"F8 spawn placement rejected hit '{hitObjectName}' because hitNormal.y={hit.normal.y:0.00} is not floor-like. finalSpawnCenter={fallbackCenter}.");
                        return fallbackCenter;
                    }

                    if (hit.point.y > desiredPoint.y + 0.75f)
                    {
                        Log.LogWarning($"F8 spawn placement rejected hit '{hitObjectName}' because hitPoint.y={hit.point.y:0.00} is too far above desiredPoint.y={desiredPoint.y:0.00}. finalSpawnCenter={fallbackCenter}.");
                        return fallbackCenter;
                    }

                    Vector3 spawnCenter = hit.point + Vector3.up * groundOffset;
                    Log.LogInfo($"F8 spawn placement result: finalSpawnCenter={spawnCenter}.");
                    return spawnCenter;
                }

                Log.LogWarning($"F8 spawn placement raycast: no hit. finalSpawnCenter={fallbackCenter}.");
                return fallbackCenter;
            }

            try
            {
                PlayerAvatar localPlayer = SemiFunc.PlayerAvatarLocal();
                if (localPlayer != null)
                {
                    Vector3 desiredPoint = localPlayer.transform.position + localPlayer.transform.forward * distance;
                    Vector3 fallback = desiredPoint + Vector3.up * fallbackOffset;
                    Log.LogWarning($"F8 spawn placement: no camera available; using player-forward fallback finalSpawnCenter={fallback}.");
                    return fallback;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not get local player for spawn position: {ex.Message}");
            }

            Vector3 finalFallback = Vector3.up * (radius + 1.0f);
            Log.LogWarning($"F8 spawn placement: no camera/player available; using final fallback center {finalFallback}.");
            return finalFallback;
        }

        private void LogSpawnedObjects(List<EnemyParent> spawned, Vector3 requestedCenter)
        {
            if (spawned == null || spawned.Count == 0)
            {
                Log.LogWarning("F8 spawn diagnostics: no spawned objects were returned by REPOLib.");
                return;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                EnemyParent enemyParent = spawned[i];
                if (enemyParent == null)
                {
                    Log.LogWarning($"F8 spawn diagnostics [{i}]: EnemyParent was null.");
                    continue;
                }

                GameObject root = enemyParent.gameObject;
                SpawnVisibilityDiagnostics diagnostics = EnsureSpawnedVisibility(root, requestedCenter);
                Transform visibleSphere = diagnostics.VisibleSphere;
                LogSpawnedRuntimeDiagnostics(root, enemyParent, i, requestedCenter);

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

                int enabledRenderers = CountEnabledRenderers(renderers);
                int activeColliders = CountActiveColliders(colliders);
                bool pointLightEnabled = diagnostics.PointLight != null && diagnostics.PointLight.enabled;
                bool behaviorAttached = diagnostics.Behavior != null && diagnostics.Behavior.enabled;

                Log.LogInfo($"F8 spawn diagnostics [{i}]: root='{root.name}', rootActiveSelf={root.activeSelf}, rootActiveInHierarchy={root.activeInHierarchy}, rootPosition={FormatVector3(root.transform.position)}.");
                Log.LogInfo($"F8 spawn diagnostics [{i}]: visibleSphere='{visibleSphere.name}', worldPosition={FormatVector3(visibleSphere.position)}, localPosition={FormatVector3(visibleSphere.localPosition)}, localScale={FormatVector3(visibleSphere.localScale)}, diameter={diagnostics.Diameter:0.00}.");
                Log.LogInfo($"F8 spawn diagnostics [{i}]: rendererCount={renderers.Length}, enabledRenderers={enabledRenderers}, inheritedRenderersDisabled={diagnostics.RendererCleanup.DisabledCount}, colliderCount={colliders.Length}, activeColliders={activeColliders}, intentionalActiveColliders={diagnostics.ColliderCleanup.ActiveAfterCleanup}, pointLightEnabled={pointLightEnabled}, fallbackStandaloneCreated={diagnostics.FallbackStandaloneCreated}, fallbackReason='{diagnostics.FallbackReason}'.");
                Log.LogInfo($"F8 spawn diagnostics [{i}]: colliderCleanup totalFound={diagnostics.ColliderCleanup.TotalFound}, disabled={diagnostics.ColliderCleanup.DisabledCount}, kept={diagnostics.ColliderCleanup.KeptCount}, keptColliders=[{string.Join(", ", diagnostics.ColliderCleanup.KeptColliderNames.ToArray())}], disabledColliders=[{string.Join(", ", diagnostics.ColliderCleanup.DisabledColliderNames.ToArray())}].");
                Log.LogInfo($"F8 spawn diagnostics [{i}]: activeSuperBallVisibleSphereCount={diagnostics.ActiveVisibleSphereCount}, visibleSphereObjects=[{string.Join(", ", diagnostics.VisibleSphereNames.ToArray())}], auraObject='{GetComponentPath(diagnostics.AuraRenderer, root)}', auraEnabled={diagnostics.AuraRenderer != null && diagnostics.AuraRenderer.enabled}, conceptVisualsEnabled={diagnostics.ConceptVisuals != null && diagnostics.ConceptVisuals.enabled}, attackComponentsDisabled={diagnostics.AttackCleanup.DisabledCount}, SuperBallBehaviorAttached={behaviorAttached}.");
                Log.LogInfo($"F8 spawn diagnostics [{i}]: colliderObject='{GetComponentPath(diagnostics.Collider, root)}', colliderCenter={FormatVector3(diagnostics.Collider == null ? Vector3.zero : diagnostics.Collider.center)}, colliderRadius={diagnostics.ColliderEffectiveRadius:0.00}, colliderIsTrigger={diagnostics.ColliderIsTrigger}, groundedAlignment='{diagnostics.GroundedAlignment}', rigidbodyPresent={diagnostics.Rigidbody != null}, rigidbodyIsKinematic={diagnostics.RigidbodyIsKinematic}, layer={visibleSphere.gameObject.layer}('{LayerMask.LayerToName(visibleSphere.gameObject.layer)}').");
            }
        }

        private void LogSpawnedRuntimeDiagnostics(GameObject root, EnemyParent returnedEnemyParent, int index, Vector3 requestedCenter)
        {
            if (root == null)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}]: spawned root was null.");
                return;
            }

            Log.LogInfo($"F8 AI diagnostics [{index}]: begin runtime object inspection for root='{root.name}', path='{GetHierarchyPath(root.transform, root.transform)}', requestedCenter={FormatVector3(requestedCenter)}, rootPosition={FormatVector3(root.transform.position)}.");
            LogMonoBehaviourComponents(root, index);
            LogAiRelatedScripts(root, index);
            LogNavMeshAgentsAndRecover(root, index, requestedCenter);
            LogRigidbodies(root, index);
            LogNetworkComponents(root, index);
            LogEnemySetupReferences(root, returnedEnemyParent, index);
            Log.LogInfo($"F8 AI diagnostics [{index}]: end runtime object inspection; finalRootPosition={FormatVector3(root.transform.position)}.");
        }

        private void LogMonoBehaviourComponents(GameObject root, int index)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            Log.LogInfo($"F8 AI diagnostics [{index}] MonoBehaviour list: count={behaviours.Length}.");

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] MonoBehaviour[{i}]: missing script/null component.");
                    continue;
                }

                Type type = behaviour.GetType();
                Log.LogInfo($"F8 AI diagnostics [{index}] MonoBehaviour[{i}]: path='{GetHierarchyPath(behaviour.transform, root.transform)}', type='{type.FullName}', enabled={behaviour.enabled}, activeSelf={behaviour.gameObject.activeSelf}, activeInHierarchy={behaviour.gameObject.activeInHierarchy}.");
            }
        }

        private void LogAiRelatedScripts(GameObject root, int index)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            int aiRelatedCount = 0;
            int enabledCount = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                if (!IsAiRelatedScript(type))
                {
                    continue;
                }

                aiRelatedCount++;
                if (behaviour.enabled)
                {
                    enabledCount++;
                }

                Log.LogInfo($"F8 AI diagnostics [{index}] AI-related script[{aiRelatedCount - 1}]: path='{GetHierarchyPath(behaviour.transform, root.transform)}', type='{type.FullName}', enabled={behaviour.enabled}, activeInHierarchy={behaviour.gameObject.activeInHierarchy}.");
            }

            if (aiRelatedCount == 0)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] AI-related scripts: none found. This strongly suggests the clone has visuals/parent data but no active enemy brain to chase or attack.");
            }
            else if (enabledCount == 0)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] AI-related scripts: found {aiRelatedCount}, but none are enabled. This explains an idle spawn unless the game enables them later.");
            }
            else
            {
                Log.LogInfo($"F8 AI diagnostics [{index}] AI-related scripts: found {aiRelatedCount}, enabled={enabledCount}.");
            }
        }

        private void LogNavMeshAgentsAndRecover(GameObject root, int index, Vector3 requestedCenter)
        {
            LogEnemyNavMeshAgentWrappers(root, index);

            NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
            if (agents.Length == 0)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent: none found. If the cloned Animal AI expects nav movement, this is a likely reason it will not chase.");
                TryWarpTransformToNearestNavMesh(root.transform, index, requestedCenter, "missing NavMeshAgent");
                return;
            }

            bool anyOnNavMesh = false;
            for (int i = 0; i < agents.Length; i++)
            {
                NavMeshAgent agent = agents[i];
                if (agent == null)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent[{i}]: null component.");
                    continue;
                }

                bool isOnNavMesh;
                string isOnNavMeshError;
                bool readIsOnNavMesh = TryReadAgentIsOnNavMesh(agent, out isOnNavMesh, out isOnNavMeshError);
                anyOnNavMesh |= readIsOnNavMesh && isOnNavMesh;

                string isOnNavMeshText = readIsOnNavMesh ? isOnNavMesh.ToString() : isOnNavMeshError;
                Log.LogInfo($"F8 AI diagnostics [{index}] NavMeshAgent[{i}]: path='{GetHierarchyPath(agent.transform, root.transform)}', enabled={agent.enabled}, activeInHierarchy={agent.gameObject.activeInHierarchy}, isOnNavMesh={isOnNavMeshText}, speed={SafeRead(() => agent.speed)}, destination={SafeRead(() => agent.destination)}, remainingDistance={SafeRead(() => agent.remainingDistance)}.");

                if (!readIsOnNavMesh || !isOnNavMesh)
                {
                    TryWarpAgentToNearestNavMesh(agent, root, index, requestedCenter, i);
                }
            }

            if (!anyOnNavMesh)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent summary: no agent is currently on the navmesh. This is a likely reason the cloned enemy is idle.");
            }
        }

        private void LogEnemyNavMeshAgentWrappers(GameObject root, int index)
        {
            EnemyNavMeshAgent[] wrappers = root.GetComponentsInChildren<EnemyNavMeshAgent>(true);
            if (wrappers.Length == 0)
            {
                Log.LogInfo($"F8 AI diagnostics [{index}] EnemyNavMeshAgent wrapper: none found.");
                return;
            }

            for (int i = 0; i < wrappers.Length; i++)
            {
                EnemyNavMeshAgent wrapper = wrappers[i];
                if (wrapper == null)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] EnemyNavMeshAgent[{i}]: null wrapper.");
                    continue;
                }

                NavMeshAgent wrappedAgent = GetWrappedUnityNavMeshAgent(wrapper);
                string wrappedStatus = wrappedAgent == null
                    ? "wrappedUnityAgent=null"
                    : $"wrappedUnityAgent='{GetHierarchyPath(wrappedAgent.transform, root.transform)}', enabled={wrappedAgent.enabled}, isOnNavMesh={SafeRead(() => wrappedAgent.isOnNavMesh)}, speed={SafeRead(() => wrappedAgent.speed)}, destination={SafeRead(() => wrappedAgent.destination)}, remainingDistance={SafeRead(() => wrappedAgent.remainingDistance)}";
                Log.LogInfo($"F8 AI diagnostics [{index}] EnemyNavMeshAgent[{i}]: path='{GetHierarchyPath(wrapper.transform, root.transform)}', enabled={wrapper.enabled}, activeInHierarchy={wrapper.gameObject.activeInHierarchy}, {wrappedStatus}.");
            }
        }

        private static NavMeshAgent GetWrappedUnityNavMeshAgent(EnemyNavMeshAgent wrapper)
        {
            if (wrapper == null)
            {
                return null;
            }

            FieldInfo field = wrapper.GetType().GetField("Agent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(wrapper) as NavMeshAgent;
            }
            catch
            {
                return null;
            }
        }

        private void LogRigidbodies(GameObject root, int index)
        {
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            if (bodies.Length == 0)
            {
                Log.LogInfo($"F8 AI diagnostics [{index}] Rigidbody: none found.");
                return;
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] Rigidbody[{i}]: null component.");
                    continue;
                }

                Log.LogInfo($"F8 AI diagnostics [{index}] Rigidbody[{i}]: path='{GetHierarchyPath(body.transform, root.transform)}', activeInHierarchy={body.gameObject.activeInHierarchy}, isKinematic={body.isKinematic}, useGravity={body.useGravity}, constraints={body.constraints}.");
            }
        }

        private void LogNetworkComponents(GameObject root, int index)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            int networkCount = 0;

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (!IsNetworkIdentityComponent(type))
                {
                    continue;
                }

                bool valid = IsNetworkComponentValid(component, out string validityDetails);
                Log.LogInfo($"F8 AI diagnostics [{index}] Network identity[{networkCount}]: path='{GetHierarchyPath(component.transform, root.transform)}', type='{type.FullName}', activeInHierarchy={component.gameObject.activeInHierarchy}, valid={valid}, details={validityDetails}.");
                networkCount++;
            }

            if (networkCount == 0)
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] Network identity: no PhotonView/network identity component found. Network-driven enemy initialization may not run for this clone.");
            }
        }

        private void LogEnemySetupReferences(GameObject root, EnemyParent returnedEnemyParent, int index)
        {
            EnemyParent rootEnemyParent = root.GetComponent<EnemyParent>();
            EnemyParent[] enemyParents = root.GetComponentsInChildren<EnemyParent>(true);
            int spawnObjectCount = superBallSetup == null || superBallSetup.spawnObjects == null ? -1 : superBallSetup.spawnObjects.Count;
            bool setupContainsTemplatePrefab = superBallSetup != null && superBallPrefab != null && SetupContainsPrefab(superBallSetup, superBallPrefab);
            GameObject firstSetupPrefab = superBallSetup == null ? null : GetFirstSpawnPrefab(superBallSetup);

            Log.LogInfo($"F8 AI diagnostics [{index}] required enemy setup refs: setupExists={superBallSetup != null}, setupName='{GetObjectName(superBallSetup)}', setupSpawnObjectCount={spawnObjectCount}, firstSetupPrefab='{GetObjectName(firstSetupPrefab)}', setupContainsTemplatePrefab={setupContainsTemplatePrefab}, templatePrefab='{GetObjectName(superBallPrefab)}'.");
            Log.LogInfo($"F8 AI diagnostics [{index}] required enemy setup refs: rootHasEnemyParent={rootEnemyParent != null}, enemyParentCountInChildren={enemyParents.Length}, returnedEnemyParentNull={returnedEnemyParent == null}, returnedEnemyParentPath='{GetComponentPath(returnedEnemyParent, root)}'.");

            for (int i = 0; i < enemyParents.Length; i++)
            {
                EnemyParent enemyParent = enemyParents[i];
                if (enemyParent == null)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] EnemyParent[{i}]: null component.");
                    continue;
                }

                string enabledText = enemyParent is Behaviour behaviour ? behaviour.enabled.ToString() : "<not Behaviour>";
                Log.LogInfo($"F8 AI diagnostics [{index}] EnemyParent[{i}]: path='{GetHierarchyPath(enemyParent.transform, root.transform)}', enabled={enabledText}, activeInHierarchy={enemyParent.gameObject.activeInHierarchy}, enemyName='{enemyParent.enemyName}', difficulty={enemyParent.difficulty}, isReturned={ReferenceEquals(enemyParent, returnedEnemyParent)}.");
                LogUnityObjectReferenceFields(enemyParent, root, index, $"EnemyParent[{i}]");
            }
        }

        private bool TryWarpAgentToNearestNavMesh(NavMeshAgent agent, GameObject root, int index, Vector3 requestedCenter, int agentIndex)
        {
            Vector3 sampleOrigin = agent == null ? root.transform.position : agent.transform.position;
            if (!TryFindNearestNavMeshPoint(sampleOrigin, requestedCenter, out NavMeshHit hit, out string source))
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent[{agentIndex}] warp recovery: no navmesh point found near agent/root/requested spawn.");
                return false;
            }

            if (agent != null && agent.enabled && agent.gameObject.activeInHierarchy)
            {
                bool warped = false;
                try
                {
                    warped = agent.Warp(hit.position);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent[{agentIndex}] warp recovery threw {ex.GetType().Name}: {ex.Message}");
                }

                Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent[{agentIndex}] warp recovery: attempted agent.Warp to {FormatVector3(hit.position)} from {source}, success={warped}, agentPosition={FormatVector3(agent.transform.position)}, rootPosition={FormatVector3(root.transform.position)}.");
                return warped;
            }

            Vector3 beforeRoot = root.transform.position;
            Vector3 delta = hit.position - sampleOrigin;
            root.transform.position += delta;
            Log.LogWarning($"F8 AI diagnostics [{index}] NavMeshAgent[{agentIndex}] warp recovery: agent was disabled or inactive, moved root by {FormatVector3(delta)} from {FormatVector3(beforeRoot)} to {FormatVector3(root.transform.position)} using {source}.");
            return true;
        }

        private bool TryWarpTransformToNearestNavMesh(Transform root, int index, Vector3 requestedCenter, string reason)
        {
            if (root == null)
            {
                return false;
            }

            if (!TryFindNearestNavMeshPoint(root.position, requestedCenter, out NavMeshHit hit, out string source))
            {
                Log.LogWarning($"F8 AI diagnostics [{index}] NavMesh recovery ({reason}): no navmesh point found near root/requested spawn.");
                return false;
            }

            Vector3 before = root.position;
            root.position = hit.position;
            Log.LogWarning($"F8 AI diagnostics [{index}] NavMesh recovery ({reason}): moved root from {FormatVector3(before)} to nearest navmesh point {FormatVector3(root.position)} using {source}.");
            return true;
        }

        private static bool TryFindNearestNavMeshPoint(Vector3 primary, Vector3 requestedCenter, out NavMeshHit hit, out string source)
        {
            const float sampleRadius = 8.0f;

            if (NavMesh.SamplePosition(primary, out hit, sampleRadius, NavMesh.AllAreas))
            {
                source = $"primary={FormatVector3(primary)}, radius={sampleRadius:0.0}";
                return true;
            }

            if (NavMesh.SamplePosition(requestedCenter, out hit, sampleRadius, NavMesh.AllAreas))
            {
                source = $"requestedCenter={FormatVector3(requestedCenter)}, radius={sampleRadius:0.0}";
                return true;
            }

            Vector3 groundBiased = new Vector3(primary.x, primary.y - 2.0f, primary.z);
            if (NavMesh.SamplePosition(groundBiased, out hit, sampleRadius * 1.5f, NavMesh.AllAreas))
            {
                source = $"groundBiased={FormatVector3(groundBiased)}, radius={(sampleRadius * 1.5f):0.0}";
                return true;
            }

            source = "<none>";
            return false;
        }

        private static bool TryReadAgentIsOnNavMesh(NavMeshAgent agent, out bool isOnNavMesh, out string error)
        {
            try
            {
                isOnNavMesh = agent != null && agent.isOnNavMesh;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                isOnNavMesh = false;
                error = $"<error {ex.GetType().Name}: {ex.Message}>";
                return false;
            }
        }

        private static bool IsAiRelatedScript(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.FullName ?? type.Name;
            string[] tokens = new[]
            {
                "animal",
                "enemy",
                ".ai",
                " ai",
                "attack",
                "chase",
                "state",
                "target",
                "nav",
                "path",
                "vision",
                "move",
                "roam",
                "health",
                "hurt",
                "player"
            };

            for (int i = 0; i < tokens.Length; i++)
            {
                if (name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNetworkIdentityComponent(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.FullName ?? type.Name;
            return name.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Netcode", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNetworkComponentValid(Component component, out string details)
        {
            List<string> detailParts = new List<string>();
            string[] memberNames = new[]
            {
                "ViewID",
                "viewID",
                "OwnerActorNr",
                "ownerActorNr",
                "CreatorActorNr",
                "creatorActorNr",
                "InstantiationId",
                "instantiationId",
                "IsMine",
                "isMine",
                "Owner",
                "owner",
                "Controller",
                "controller",
                "ObservedComponents",
                "observedComponents"
            };

            object viewId = null;
            bool foundViewId = false;
            for (int i = 0; i < memberNames.Length; i++)
            {
                object value;
                if (!TryGetMemberValue(component, memberNames[i], out value))
                {
                    continue;
                }

                if (string.Equals(memberNames[i], "ViewID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(memberNames[i], "viewID", StringComparison.OrdinalIgnoreCase))
                {
                    viewId = value;
                    foundViewId = true;
                }

                detailParts.Add(memberNames[i] + "=" + FormatDiagnosticValue(value));
            }

            bool valid = component != null && component.gameObject.activeInHierarchy;
            if (foundViewId && viewId is int intViewId)
            {
                valid &= intViewId > 0;
            }

            details = detailParts.Count == 0 ? "<no known identity members found>" : string.Join(", ", detailParts.ToArray());
            return valid;
        }

        private static bool TryGetMemberValue(object target, string memberName, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    value = property.GetValue(target, null);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    value = field.GetValue(target);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static void LogUnityObjectReferenceFields(Component component, GameObject root, int index, string label)
        {
            if (component == null)
            {
                return;
            }

            int referenceCount = 0;
            Type current = component.GetType();
            while (current != null
                && current != typeof(MonoBehaviour)
                && current != typeof(Behaviour)
                && current != typeof(Component)
                && current != typeof(UnityEngine.Object))
            {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }

                    object value = null;
                    string valueText;
                    try
                    {
                        value = field.GetValue(component);
                        valueText = FormatDiagnosticValue(value);
                    }
                    catch (Exception ex)
                    {
                        valueText = $"<error {ex.GetType().Name}: {ex.Message}>";
                    }

                    bool isNull = value == null;
                    UnityEngine.Object unityObject = value as UnityEngine.Object;
                    if (!ReferenceEquals(unityObject, null))
                    {
                        isNull = unityObject == null;
                    }

                    Log.LogInfo($"F8 AI diagnostics [{index}] {label} reference field: {current.Name}.{field.Name} ({field.FieldType.Name}) = {valueText}, isNull={isNull}.");
                    referenceCount++;
                }

                current = current.BaseType;
            }

            if (referenceCount == 0)
            {
                Log.LogInfo($"F8 AI diagnostics [{index}] {label} reference fields: no direct UnityEngine.Object fields found on '{component.GetType().FullName}'.");
            }
        }

        private static bool SetupContainsPrefab(EnemySetup setup, GameObject prefab)
        {
            if (setup == null || prefab == null || setup.spawnObjects == null)
            {
                return false;
            }

            for (int i = 0; i < setup.spawnObjects.Count; i++)
            {
                PrefabRef prefabRef = setup.spawnObjects[i];
                if (prefabRef != null && ReferenceEquals(prefabRef.Prefab, prefab))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SafeRead<T>(Func<T> reader)
        {
            try
            {
                return FormatDiagnosticValue(reader());
            }
            catch (Exception ex)
            {
                return $"<error {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private static string FormatDiagnosticValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is Vector3 vector)
            {
                return FormatVector3(vector);
            }

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (!ReferenceEquals(unityObject, null))
            {
                return unityObject == null ? "null(UnityDestroyed)" : $"'{unityObject.name}' ({unityObject.GetType().FullName})";
            }

            ICollection collection = value as ICollection;
            if (collection != null && !(value is string))
            {
                return $"{value.GetType().Name}(count={collection.Count})";
            }

            return value.ToString();
        }

        private static string GetComponentPath(Component component, GameObject root)
        {
            if (component == null || root == null)
            {
                return "<null>";
            }

            return GetHierarchyPath(component.transform, root.transform);
        }

        private static string GetHierarchyPath(Transform transform, Transform root)
        {
            if (transform == null)
            {
                return "<null>";
            }

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string GetObjectName(UnityEngine.Object unityObject)
        {
            if (ReferenceEquals(unityObject, null))
            {
                return "<null>";
            }

            return unityObject == null ? "<destroyed>" : unityObject.name;
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
        }

        private SpawnVisibilityDiagnostics EnsureSpawnedVisibility(GameObject root, Vector3 requestedCenter)
        {
            float diameter = GetConfiguredDiameter();
            float radius = diameter * 0.5f;
            root.SetActive(true);

            RendererCleanupResult rendererCleanup = DisableInheritedRenderers(root, "F8 spawn visibility");
            AttackCleanupResult attackCleanup = DisableInheritedAttackComponents(root, "F8 spawn visibility");
            Vector3 safeLocalCenter = GetSafeSphereLocalCenter(diameter);

            Transform sphereTransform = FindOwnedSphereTransform(root);
            GameObject sphere;
            if (sphereTransform == null)
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = SphereVisualName;
                sphere.transform.SetParent(root.transform, false);
                sphereTransform = sphere.transform;
                Log.LogWarning($"F8 spawn visibility: created missing owned sphere '{SphereVisualName}' under '{root.name}'.");
            }
            else
            {
                sphere = sphereTransform.gameObject;
                sphere.name = SphereVisualName;
            }

            sphere.SetActive(true);
            sphere.layer = root.layer;
            sphereTransform.localPosition = safeLocalCenter;
            sphereTransform.localRotation = Quaternion.identity;
            sphereTransform.localScale = Vector3.one * diameter;

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = sphere.AddComponent<MeshRenderer>();
            }
            renderer.enabled = true;
            renderer.material = CreateSuperBallMaterial();

            SphereCollider sphereCollider = EnsureSphereCollider(sphere, diameter, "F8 spawn visibility");
            Rigidbody rigidbody = EnsureSuperBallRigidbody(sphere);
            Renderer auraRenderer = EnsureAuraVisual(sphereTransform, diameter);
            SuperBallConceptVisuals conceptVisuals = EnsureConceptVisuals(root, sphereTransform, diameter);
            ColliderCleanupResult colliderCleanup = DisableInheritedColliders(root, sphereCollider, "F8 spawn visibility");

            Light pointLight = EnsurePointLight(sphereTransform, diameter);
            EnsureVisualMotion(root, sphereTransform, safeLocalCenter, diameter);
            SuperBallBehavior behavior = EnsureSuperBallBehavior(root, sphereTransform, auraRenderer == null ? null : auraRenderer.transform, auraRenderer, pointLight, sphereCollider, rigidbody, diameter);
            RemoveDuplicateOwnedSpheres(root, sphereTransform, "F8 spawn visibility");

            bool fallbackCreated = false;
            string fallbackReason = GetFallbackReason(root, sphere, renderer);
            if (fallbackReason == null)
            {
                DestroyFallbackDebugSpheres("F8 spawn visibility: fallback not needed");
            }
            else if (enableFallbackDebugSphere.Value)
            {
                fallbackCreated = CreateStandaloneDebugSphere(requestedCenter, diameter, fallbackReason);
            }
            else
            {
                Log.LogWarning($"F8 spawn visibility: fallback sphere was needed but disabled by config. reason='{fallbackReason}'.");
                DestroyFallbackDebugSpheres("F8 spawn visibility: fallback disabled by config");
            }

            List<string> visibleSphereNames = GetActiveOwnedSphereRendererNames(root, fallbackCreated);
            return new SpawnVisibilityDiagnostics
            {
                VisibleSphere = sphereTransform,
                PointLight = pointLight,
                FallbackStandaloneCreated = fallbackCreated,
                FallbackReason = fallbackReason ?? "<none>",
                RendererCleanup = rendererCleanup,
                AttackCleanup = attackCleanup,
                ColliderCleanup = colliderCleanup,
                Collider = sphereCollider,
                ColliderEffectiveRadius = radius,
                ColliderIsTrigger = sphereCollider != null && sphereCollider.isTrigger,
                Rigidbody = rigidbody,
                RigidbodyIsKinematic = rigidbody != null && rigidbody.isKinematic,
                Behavior = behavior,
                Diameter = diameter,
                ActiveVisibleSphereCount = visibleSphereNames.Count,
                VisibleSphereNames = visibleSphereNames,
                AuraRenderer = auraRenderer,
                ConceptVisuals = conceptVisuals,
                GroundedAlignment = GetGroundedAlignmentText(root, sphereTransform, sphereCollider, diameter)
            };
        }

        private Light EnsurePointLight(Transform parent, float diameter)
        {
            Transform lightTransform = parent.Find(GlowLightName);
            GameObject lightObject;
            if (lightTransform == null)
            {
                lightObject = new GameObject(GlowLightName);
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.localPosition = Vector3.zero;
            }
            else
            {
                lightObject = lightTransform.gameObject;
            }

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.enabled = true;
            light.type = LightType.Point;
            light.color = GetSuperBallEmissionColor();
            light.range = Mathf.Clamp(diameter * 3.0f, 2.0f, 4.5f);
            light.intensity = GetDebugEmissionIntensity();
            return light;
        }

        private bool CreateStandaloneDebugSphere(Vector3 center, float diameter, string reason)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = StandaloneDebugSphereName;
            sphere.transform.position = center;
            sphere.transform.rotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.SetActive(true);

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.material = CreateSuperBallMaterial();
            }

            SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
            }

            EnsurePointLight(sphere.transform, diameter);
            EnsureVisualMotion(sphere, sphere.transform, center, diameter);
            Log.LogWarning($"Created fallback standalone debug sphere '{StandaloneDebugSphereName}' at {FormatVector3(center)} because {reason}.");
            return true;
        }

        private void EnsureVisualMotion(GameObject root, Transform visualRoot, Vector3 baseLocalPosition, float diameter)
        {
            SuperBallVisualMotion motion = root.GetComponent<SuperBallVisualMotion>();
            if (motion == null)
            {
                motion = root.AddComponent<SuperBallVisualMotion>();
            }

            motion.VisualRoot = visualRoot;
            motion.Enabled = enableBounceVisuals.Value && !enableCustomSuperBallBehavior.Value;
            motion.BaseLocalPosition = baseLocalPosition;
            motion.BobAmplitude = Mathf.Clamp(diameter * 0.08f, 0.05f, 0.22f);
            motion.BobFrequency = 2.4f;
            motion.RollDegreesPerSecond = 210.0f;
        }

        private SphereCollider EnsureSphereCollider(GameObject sphere, float diameter, string context)
        {
            SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                sphereCollider = sphere.AddComponent<SphereCollider>();
            }

            sphereCollider.enabled = true;
            sphereCollider.radius = 0.5f;
            sphereCollider.center = Vector3.zero;
            sphereCollider.isTrigger = !enablePhysicalBlockingCollider.Value;

            Log.LogInfo($"{context} collider setup: colliderObject='{sphere.name}', effectiveRadius={(diameter * 0.5f):0.00}, localRadius={sphereCollider.radius:0.00}, isTrigger={sphereCollider.isTrigger}, layer={sphere.layer}('{LayerMask.LayerToName(sphere.layer)}').");
            return sphereCollider;
        }

        private Rigidbody EnsureSuperBallRigidbody(GameObject sphere)
        {
            Rigidbody rigidbody = sphere.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = sphere.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return rigidbody;
        }

        private SuperBallBehavior EnsureSuperBallBehavior(GameObject root, Transform visualRoot, Transform auraRoot, Renderer auraRenderer, Light auraLight, Collider contactCollider, Rigidbody rigidbody, float diameter)
        {
            SuperBallBehavior behavior = visualRoot.GetComponent<SuperBallBehavior>();
            if (behavior == null && enableCustomSuperBallBehavior.Value)
            {
                behavior = visualRoot.gameObject.AddComponent<SuperBallBehavior>();
            }

            if (behavior == null)
            {
                return null;
            }

            behavior.enabled = enableCustomSuperBallBehavior.Value;
            behavior.Configure(
                root.transform,
                visualRoot,
                auraRoot,
                auraRenderer,
                auraLight,
                contactCollider,
                rigidbody,
                enableCustomSuperBallBehavior.Value,
                contactDamage.Value,
                chargedDamage.Value,
                Mathf.Clamp(chargeWarningSeconds.Value, 1.0f, 8.0f),
                Mathf.Clamp(chargeCooldownSeconds.Value, 1.0f, 20.0f),
                Mathf.Clamp(roamSpeed.Value, 0.4f, 4.0f),
                Mathf.Clamp(idleBounceAmplitude.Value, 0.0f, 0.25f),
                Mathf.Clamp(idleBounceFrequency.Value, 0.4f, 5.0f),
                Mathf.Clamp(chargeBounceAmplitudeMin.Value, 0.0f, 0.3f),
                Mathf.Clamp(chargeBounceAmplitudeMax.Value, 0.02f, 0.55f),
                Mathf.Clamp(chargeSpinSpeedMin.Value, 0.0f, 900.0f),
                Mathf.Clamp(chargeSpinSpeedMax.Value, 180.0f, 2400.0f),
                Mathf.Clamp(chargeSpeed.Value, 2.0f, 20.0f),
                Mathf.Clamp(maxRicochetCount.Value, 0, 8),
                Mathf.Clamp(recoveryDuration.Value, 0.25f, 5.0f),
                Mathf.Clamp(chargeAuraScale.Value, 1.0f, 3.0f),
                auraEnabled.Value,
                Mathf.Clamp(auraAlpha.Value, 0.02f, 0.45f),
                Mathf.Clamp(auraScaleMultiplier.Value, 1.05f, 2.5f),
                diameter);
            return behavior;
        }

        private RendererCleanupResult DisableInheritedRenderers(GameObject root, string context)
        {
            RendererCleanupResult result = new RendererCleanupResult();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (IsOwnedSuperBallRenderer(renderer, root.transform))
                {
                    result.KeptRendererName = renderer.gameObject.name;
                    continue;
                }

                result.InheritedRenderersFound++;
                result.DisabledRendererNames.Add(GetHierarchyPath(renderer.transform, root.transform));
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    result.DisabledCount++;
                }
            }

            Log.LogInfo($"{context} inherited renderer cleanup: found={result.InheritedRenderersFound}, disabled={result.DisabledCount}, disabledObjects=[{string.Join(", ", result.DisabledRendererNames.ToArray())}], keptSuperBallRenderer='{result.KeptRendererName ?? "<none>"}'.");
            return result;
        }

        private ColliderCleanupResult DisableInheritedColliders(GameObject root, Collider keepCollider, string context)
        {
            ColliderCleanupResult result = new ColliderCleanupResult();
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            result.TotalFound = colliders.Length;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                string path = GetHierarchyPath(collider.transform, root.transform);
                if (keepCollider != null && ReferenceEquals(collider, keepCollider))
                {
                    collider.enabled = true;
                    result.KeptCount++;
                    result.KeptColliderNames.Add(path);
                    continue;
                }

                if (collider.enabled)
                {
                    collider.enabled = false;
                    result.DisabledCount++;
                }

                result.DisabledColliderNames.Add(path);
            }

            Collider[] after = root.GetComponentsInChildren<Collider>(true);
            result.ActiveAfterCleanup = CountActiveColliders(after);
            Log.LogInfo($"{context} inherited collider cleanup: totalFound={result.TotalFound}, disabled={result.DisabledCount}, kept={result.KeptCount}, activeAfterCleanup={result.ActiveAfterCleanup}, keptColliders=[{string.Join(", ", result.KeptColliderNames.ToArray())}], disabledColliders=[{string.Join(", ", result.DisabledColliderNames.ToArray())}].");
            return result;
        }

        private static void DisableAllColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private AttackCleanupResult DisableInheritedAttackComponents(GameObject root, string context)
        {
            AttackCleanupResult result = new AttackCleanupResult();
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                if (!IsSuspiciousInheritedAttackComponent(type))
                {
                    continue;
                }

                string componentName = $"{GetHierarchyPath(behaviour.transform, root.transform)}:{type.FullName}";
                result.FoundCount++;

                if (IsCoreEnemyLifecycleComponent(type) || !disableInheritedBaseAttacks.Value)
                {
                    string reason = IsCoreEnemyLifecycleComponent(type) ? "core lifecycle" : "config disabled";
                    result.KeptComponentNames.Add(componentName + $" ({reason})");
                    continue;
                }

                if (behaviour.enabled)
                {
                    behaviour.enabled = false;
                    result.DisabledCount++;
                }

                result.DisabledComponentNames.Add(componentName);
            }

            Log.LogInfo($"{context} inherited attack cleanup: found={result.FoundCount}, disabled={result.DisabledCount}, disabledComponents=[{string.Join(", ", result.DisabledComponentNames.ToArray())}], keptComponents=[{string.Join(", ", result.KeptComponentNames.ToArray())}].");
            return result;
        }

        private static bool IsSuspiciousInheritedAttackComponent(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.FullName ?? type.Name;
            string[] tokens = new[]
            {
                "Animal",
                "Attack",
                "Zap",
                "Beam",
                "Lightning",
                "Tongue",
                "Mouth",
                "Projectile",
                "Shoot",
                "Ranged"
            };

            for (int i = 0; i < tokens.Length; i++)
            {
                if (name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCoreEnemyLifecycleComponent(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.Name;
            return name == "Enemy"
                || name == "EnemyParent"
                || name == "EnemyNavMeshAgent"
                || name == "EnemyRigidbody"
                || name == "EnemyVision"
                || name == "EnemyPlayerDistance"
                || name == "EnemyOnScreen"
                || name == "EnemyPlayerRoom"
                || name == "EnemyGrounded"
                || name == "EnemyJump"
                || name == "EnemyHealth"
                || name == "EnemyStateSpawn"
                || name == "EnemyStateDespawn"
                || name == "EnemyStateStunned";
        }

        private static bool IsOwnedSuperBallRenderer(Renderer renderer, Transform root)
        {
            if (renderer == null)
            {
                return false;
            }

            Transform current = renderer.transform;
            while (current != null && current != root)
            {
                if (current.name.IndexOf("SuperBall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private Transform FindOwnedSphereTransform(GameObject root)
        {
            Transform preferred = FindChildTransform(root.transform, SphereVisualName);
            if (preferred != null)
            {
                return preferred;
            }

            return FindChildTransform(root.transform, SpawnDebugSphereName);
        }

        private static Transform FindChildTransform(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && string.Equals(transforms[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private void RemoveDuplicateOwnedSpheres(GameObject root, Transform keep, string context)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            int removed = 0;
            List<string> removedNames = new List<string>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == root.transform || candidate == keep)
                {
                    continue;
                }

                bool isDuplicate = string.Equals(candidate.name, SphereVisualName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.name, SpawnDebugSphereName, StringComparison.OrdinalIgnoreCase);
                if (!isDuplicate)
                {
                    continue;
                }

                removedNames.Add(GetHierarchyPath(candidate, root.transform));
                Renderer[] duplicateRenderers = candidate.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < duplicateRenderers.Length; j++)
                {
                    if (duplicateRenderers[j] != null)
                    {
                        duplicateRenderers[j].enabled = false;
                    }
                }
                candidate.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(candidate.gameObject);
                removed++;
            }

            if (removed > 0)
            {
                Log.LogWarning($"{context}: removed duplicate Super Ball sphere objects count={removed}, names=[{string.Join(", ", removedNames.ToArray())}].");
            }
        }

        private string GetFallbackReason(GameObject root, GameObject sphere, Renderer renderer)
        {
            if (root == null)
            {
                return "spawned root is null";
            }
            if (!root.activeSelf)
            {
                return "spawned root activeSelf is false";
            }
            if (!root.activeInHierarchy)
            {
                return "spawned root activeInHierarchy is false";
            }
            if (sphere == null)
            {
                return "owned sphere is missing";
            }
            if (!sphere.activeSelf)
            {
                return "owned sphere activeSelf is false";
            }
            if (!sphere.activeInHierarchy)
            {
                return "owned sphere activeInHierarchy is false";
            }
            if (renderer == null)
            {
                return "owned sphere renderer is missing";
            }
            if (!renderer.enabled)
            {
                return "owned sphere renderer is disabled";
            }

            return null;
        }

        private void DestroyFallbackDebugSpheres(string reason)
        {
            int destroyed = 0;
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !string.Equals(candidate.name, StandaloneDebugSphereName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] != null)
                    {
                        renderers[j].enabled = false;
                    }
                }
                candidate.SetActive(false);
                UnityEngine.Object.Destroy(candidate);
                destroyed++;
            }

            if (destroyed > 0)
            {
                Log.LogInfo($"{reason}: destroyed active fallback debug spheres count={destroyed}.");
            }
        }

        private List<string> GetActiveOwnedSphereRendererNames(GameObject root, bool fallbackCreated)
        {
            List<string> names = new List<string>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (string.Equals(renderer.gameObject.name, SphereVisualName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(renderer.gameObject.name, SpawnDebugSphereName, StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(GetHierarchyPath(renderer.transform, root.transform));
                }
            }

            if (fallbackCreated)
            {
                names.Add(StandaloneDebugSphereName);
            }

            return names;
        }

        private static int CountActiveColliders(Collider[] colliders)
        {
            int active = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                {
                    active++;
                }
            }

            return active;
        }

        private static int CountEnabledRenderers(Renderer[] renderers)
        {
            int enabled = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    enabled++;
                }
            }

            return enabled;
        }

        private static Vector3 GetSafeSphereLocalCenter(float diameter)
        {
            return Vector3.zero;
        }

        private float GetConfiguredDiameter()
        {
            return Mathf.Clamp(superBallDiameter.Value, 0.40f, 0.75f);
        }

        private string GetGroundedAlignmentText(GameObject root, Transform sphereTransform, SphereCollider sphereCollider, float diameter)
        {
            if (root == null || sphereTransform == null || sphereCollider == null)
            {
                return "missing root/sphere/collider";
            }

            float radius = diameter * 0.5f;
            float visualRootDelta = Vector3.Distance(root.transform.position, sphereTransform.position);
            bool localCentered = Mathf.Abs(sphereTransform.localPosition.y) <= 0.02f;
            bool colliderCentered = sphereCollider.center.sqrMagnitude <= 0.0004f;
            float visualBottomY = sphereTransform.position.y - radius;

            return $"localCentered={localCentered}, colliderCentered={colliderCentered}, visualRootDelta={visualRootDelta:0.000}, visualBottomY={visualBottomY:0.00}, rootY={root.transform.position.y:0.00}";
        }

        private float GetDebugEmissionIntensity()
        {
            return Mathf.Clamp(emissionIntensity.Value, 0.5f, 8.0f);
        }

        private static void LogVisualStats(GameObject root, string context)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            Log.LogInfo($"{context}: root='{root.name}', rendererCount={renderers.Length}, enabledRenderers={CountEnabledRenderers(renderers)}, colliderCount={colliders.Length}, activeInHierarchy={root.activeInHierarchy}.");
        }

        private static string FormatColor(Color color)
        {
            return $"({color.r:0.00}, {color.g:0.00}, {color.b:0.00}, {color.a:0.00})";
        }

        private sealed class RendererCleanupResult
        {
            public int InheritedRenderersFound;
            public int DisabledCount;
            public string KeptRendererName;
            public readonly List<string> DisabledRendererNames = new List<string>();
        }

        private sealed class AttackCleanupResult
        {
            public int FoundCount;
            public int DisabledCount;
            public readonly List<string> DisabledComponentNames = new List<string>();
            public readonly List<string> KeptComponentNames = new List<string>();
        }

        private sealed class ColliderCleanupResult
        {
            public int TotalFound;
            public int DisabledCount;
            public int KeptCount;
            public int ActiveAfterCleanup;
            public readonly List<string> DisabledColliderNames = new List<string>();
            public readonly List<string> KeptColliderNames = new List<string>();
        }

        private sealed class SpawnVisibilityDiagnostics
        {
            public Transform VisibleSphere;
            public Light PointLight;
            public bool FallbackStandaloneCreated;
            public string FallbackReason;
            public RendererCleanupResult RendererCleanup;
            public AttackCleanupResult AttackCleanup;
            public ColliderCleanupResult ColliderCleanup;
            public SphereCollider Collider;
            public float ColliderEffectiveRadius;
            public bool ColliderIsTrigger;
            public Rigidbody Rigidbody;
            public bool RigidbodyIsKinematic;
            public SuperBallBehavior Behavior;
            public float Diameter;
            public int ActiveVisibleSphereCount;
            public List<string> VisibleSphereNames = new List<string>();
            public Renderer AuraRenderer;
            public SuperBallConceptVisuals ConceptVisuals;
            public string GroundedAlignment;
        }

        private static GameObject GetFirstSpawnPrefab(EnemySetup setup)
        {
            if (setup == null || setup.spawnObjects == null)
            {
                return null;
            }

            for (int i = 0; i < setup.spawnObjects.Count; i++)
            {
                PrefabRef prefabRef = setup.spawnObjects[i];
                if (prefabRef == null)
                {
                    continue;
                }

                GameObject prefab = prefabRef.Prefab;
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static bool TryGetEnemyParent(EnemySetup setup, out EnemyParent enemyParent)
        {
            enemyParent = null;
            if (setup == null || setup.spawnObjects == null)
            {
                return false;
            }

            for (int i = 0; i < setup.spawnObjects.Count; i++)
            {
                PrefabRef prefabRef = setup.spawnObjects[i];
                if (prefabRef == null)
                {
                    continue;
                }

                GameObject prefab = prefabRef.Prefab;
                if (prefab == null)
                {
                    continue;
                }

                enemyParent = prefab.GetComponent<EnemyParent>();
                if (enemyParent == null)
                {
                    enemyParent = prefab.GetComponentInChildren<EnemyParent>(true);
                }

                if (enemyParent != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
