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

namespace RepoSuperBallEnemy
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class SuperBallPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "James.RepoSuperBallEnemy";
        public const string PluginName = "REPO Super Ball Enemy";
        public const string PluginVersion = "0.1.0";

        private const string EnemySetupName = "SuperBallEnemySetup";
        private const string EnemyPrefabName = "SuperBallEnemy";
        private const string EnemyDisplayName = "Super Ball";

        private static ManualLogSource Log;

        private ConfigEntry<bool> enableSuperBall;
        private ConfigEntry<float> superBallDiameter;
        private ConfigEntry<KeyCode> spawnTestKey;
        private ConfigEntry<bool> enableSpawnPoolInjection;
        private ConfigEntry<float> emissionIntensity;
        private ConfigEntry<float> spawnDistance;
        private ConfigEntry<bool> enableBounceVisuals;

        private EnemySetup superBallSetup;
        private GameObject superBallPrefab;
        private bool registrationAttempted;
        private bool registrationSucceeded;

        private void Awake()
        {
            Log = Logger;

            enableSuperBall = Config.Bind("General", "EnableSuperBall", true, "Enable creation and test spawning of the Super Ball enemy.");
            superBallDiameter = Config.Bind("Visuals", "SuperBallDiameter", 1.8f, "Runtime sphere diameter in meters.");
            emissionIntensity = Config.Bind("Visuals", "EmissionIntensity", 2.8f, "Green emission multiplier for the chrome sphere material.");
            enableBounceVisuals = Config.Bind("Visuals", "EnableBounceVisuals", true, "Enable visual bobbing and rolling on the sphere body.");
            spawnTestKey = Config.Bind("Testing", "SpawnTestKey", KeyCode.F8, "Press this key as host/single-player to spawn Super Ball near the local player.");
            spawnDistance = Config.Bind("Testing", "SpawnDistance", 4.0f, "Meters in front of the local player/camera for test spawning.");
            enableSpawnPoolInjection = Config.Bind("Spawning", "EnableSpawnPoolInjection", false, "If true, also inject Super Ball into the vanilla enemy director list using the base enemy difficulty.");

            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
            Log.LogInfo($"Runtime v1 uses REPOLib and a cloned vanilla enemy prefab. Test spawn key: {spawnTestKey.Value}.");
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
            clone.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(clone);
            return clone;
        }

        private void BuildSphereVisual(GameObject prefab)
        {
            DisableExistingRenderers(prefab);

            float diameter = Mathf.Clamp(superBallDiameter.Value, 0.5f, 6.0f);
            float radius = diameter * 0.5f;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SuperBallChromeSphere";
            sphere.transform.SetParent(prefab.transform, false);
            sphere.transform.localPosition = new Vector3(0.0f, radius, 0.0f);
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.layer = prefab.layer;

            SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
            }

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateSuperBallMaterial();
                renderer.enabled = true;
            }

            GameObject lightObject = new GameObject("SuperBallGreenGlow");
            lightObject.transform.SetParent(sphere.transform, false);
            lightObject.transform.localPosition = Vector3.zero;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.0f, 1.0f, 0.35f, 1.0f);
            light.range = Mathf.Max(3.5f, diameter * 2.5f);
            light.intensity = Mathf.Clamp(emissionIntensity.Value, 0.5f, 8.0f);

            SuperBallVisualMotion motion = prefab.AddComponent<SuperBallVisualMotion>();
            motion.VisualRoot = sphere.transform;
            motion.Enabled = enableBounceVisuals.Value;
            motion.BaseLocalPosition = sphere.transform.localPosition;
            motion.BobAmplitude = Mathf.Clamp(diameter * 0.08f, 0.05f, 0.22f);
            motion.BobFrequency = 2.4f;
            motion.RollDegreesPerSecond = 210.0f;

            Log.LogInfo($"Built runtime sphere visual. Diameter={diameter:0.00}m, emission={emissionIntensity.Value:0.00}.");
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
            Color bodyColor = new Color(0.03f, 0.72f, 0.24f, 1.0f);
            Color emissionColor = new Color(0.0f, 1.0f, 0.35f, 1.0f) * Mathf.Clamp(emissionIntensity.Value, 0.0f, 12.0f);

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
                material.SetFloat("_Metallic", 1.0f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.96f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.96f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
            }

            return material;
        }

        private static void DisableExistingRenderers(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
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
                Debug.Log($"[RepoSuperBallEnemy] Spawned {EnemyDisplayName} test enemy at {position}.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error while test spawning Super Ball: {ex}");
            }
        }

        private Vector3 GetTestSpawnPosition()
        {
            float distance = Mathf.Clamp(spawnDistance.Value, 1.0f, 20.0f);

            try
            {
                PlayerAvatar localPlayer = SemiFunc.PlayerAvatarLocal();
                if (localPlayer != null)
                {
                    return localPlayer.transform.position + localPlayer.transform.forward * distance + Vector3.up * 0.25f;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not get local player for spawn position: {ex.Message}");
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position + mainCamera.transform.forward * distance;
            }

            return Vector3.up;
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
