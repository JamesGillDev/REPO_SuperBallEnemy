using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.AI;

namespace RepoSuperBallEnemy
{
    public sealed class SuperBallBehavior : MonoBehaviour
    {
        public enum SuperBallState
        {
            IdleRoam,
            ChargeWarning,
            ChargeLaunch,
            Recovery
        }

        private const float RoamTargetMinSeconds = 3.5f;
        private const float RoamTargetMaxSeconds = 7.0f;
        private const float ChargeRange = 9.0f;
        private const float ChargeLaunchDuration = 1.25f;
        private const float ChargeWarningProgressLogStep = 0.25f;
        private const float SelfHitGraceDistance = 0.08f;

        private static ManualLogSource Log;

        public Transform RootTransform;
        public Transform VisualRoot;
        public Transform AuraRoot;
        public Renderer AuraRenderer;
        public Light AuraLight;
        public Collider ContactCollider;
        public Rigidbody Body;
        public bool BehaviorEnabled = true;
        public int ContactDamage = 5;
        public int ChargedDamage = 10;
        public float ChargeWarningSeconds = 3.0f;
        public float ChargeCooldownSeconds = 5.0f;
        public float RoamSpeed = 1.25f;
        public float IdleBounceAmplitude = 0.06f;
        public float IdleBounceFrequency = 1.55f;
        public float ChargeBounceAmplitudeMin = 0.04f;
        public float ChargeBounceAmplitudeMax = 0.22f;
        public float ChargeSpinSpeedMin = 120.0f;
        public float ChargeSpinSpeedMax = 1320.0f;
        public float ChargeSpeed = 9.5f;
        public int MaxRicochetCount = 3;
        public float RecoveryDuration = 1.25f;
        public float ChargeAuraScale = 2.25f;
        public bool AuraEnabled = true;
        public float AuraAlpha = 0.18f;
        public float AuraScaleMultiplier = 1.55f;
        public float Diameter = 0.55f;

        private SuperBallState state = SuperBallState.IdleRoam;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Vector3 roamTarget;
        private Vector3 launchDirection = Vector3.forward;
        private Vector3 lastKnownPlayerPosition;
        private float restCenterY;
        private float baseLightIntensity;
        private float baseLightRange;
        private float stateTimer;
        private float roamTargetTimer;
        private float phase;
        private float nextChargeAllowedTime;
        private float contactLogCooldown;
        private float nextProgressLogThreshold;
        private int ricochetCount;
        private float squashTimer;
        private Vector3 squashScale = Vector3.one;
        private readonly List<Collider> selfColliders = new List<Collider>();

        public SuperBallState CurrentState
        {
            get { return state; }
        }

        public float VisualChargeIntensity
        {
            get
            {
                if (state == SuperBallState.ChargeWarning)
                {
                    return ChargeWarningSeconds <= 0.0f ? 1.0f : Mathf.Clamp01(stateTimer / ChargeWarningSeconds);
                }

                if (state == SuperBallState.ChargeLaunch)
                {
                    return 1.0f;
                }

                if (state == SuperBallState.Recovery)
                {
                    float progress = RecoveryDuration <= 0.0f ? 1.0f : Mathf.Clamp01(stateTimer / RecoveryDuration);
                    return 1.0f - progress;
                }

                return 0.0f;
            }
        }

        public static void SetLogger(ManualLogSource logger)
        {
            Log = logger;
        }

        public void Configure(
            Transform rootTransform,
            Transform visualRoot,
            Transform auraRoot,
            Renderer auraRenderer,
            Light auraLight,
            Collider contactCollider,
            Rigidbody body,
            bool behaviorEnabled,
            int contactDamage,
            int chargedDamage,
            float chargeWarningSeconds,
            float chargeCooldownSeconds,
            float roamSpeed,
            float idleBounceAmplitude,
            float idleBounceFrequency,
            float chargeBounceAmplitudeMin,
            float chargeBounceAmplitudeMax,
            float chargeSpinSpeedMin,
            float chargeSpinSpeedMax,
            float chargeSpeed,
            int maxRicochetCount,
            float recoveryDuration,
            float chargeAuraScale,
            bool auraEnabled,
            float auraAlpha,
            float auraScaleMultiplier,
            float diameter)
        {
            RootTransform = rootTransform;
            VisualRoot = visualRoot == null ? transform : visualRoot;
            AuraRoot = auraRoot;
            AuraRenderer = auraRenderer;
            AuraLight = auraLight;
            ContactCollider = contactCollider;
            Body = body;
            BehaviorEnabled = behaviorEnabled;
            ContactDamage = contactDamage;
            ChargedDamage = chargedDamage;
            ChargeWarningSeconds = chargeWarningSeconds;
            ChargeCooldownSeconds = chargeCooldownSeconds;
            RoamSpeed = roamSpeed;
            IdleBounceAmplitude = idleBounceAmplitude;
            IdleBounceFrequency = idleBounceFrequency;
            ChargeBounceAmplitudeMin = chargeBounceAmplitudeMin;
            ChargeBounceAmplitudeMax = Mathf.Max(chargeBounceAmplitudeMin, chargeBounceAmplitudeMax);
            ChargeSpinSpeedMin = chargeSpinSpeedMin;
            ChargeSpinSpeedMax = Mathf.Max(chargeSpinSpeedMin, chargeSpinSpeedMax);
            ChargeSpeed = chargeSpeed;
            MaxRicochetCount = maxRicochetCount;
            RecoveryDuration = recoveryDuration;
            ChargeAuraScale = chargeAuraScale;
            AuraEnabled = auraEnabled;
            AuraAlpha = auraAlpha;
            AuraScaleMultiplier = auraScaleMultiplier;
            Diameter = diameter;

            CaptureBaseVisuals();
            CacheSelfColliders();
            SetAura(0.0f, 0.0f, false);
        }

        private void Awake()
        {
            if (RootTransform == null)
            {
                RootTransform = transform.root;
            }

            if (VisualRoot == null)
            {
                VisualRoot = transform;
            }

            if (ContactCollider == null)
            {
                ContactCollider = GetComponent<Collider>();
            }

            if (Body == null)
            {
                Body = GetComponent<Rigidbody>();
            }

            phase = UnityEngine.Random.value * 10.0f;
            CaptureBaseVisuals();
            CacheSelfColliders();
        }

        private void OnEnable()
        {
            CaptureBaseVisuals();
            CacheSelfColliders();
            PickRoamTarget("initial enable");
            EnterState(SuperBallState.IdleRoam);
        }

        private void Update()
        {
            if (!BehaviorEnabled || RootTransform == null || VisualRoot == null)
            {
                return;
            }

            stateTimer += Time.deltaTime;
            roamTargetTimer -= Time.deltaTime;
            if (contactLogCooldown > 0.0f)
            {
                contactLogCooldown -= Time.deltaTime;
            }
            if (squashTimer > 0.0f)
            {
                squashTimer -= Time.deltaTime;
            }

            switch (state)
            {
                case SuperBallState.IdleRoam:
                    UpdateIdleRoam();
                    break;
                case SuperBallState.ChargeWarning:
                    UpdateChargeWarning();
                    break;
                case SuperBallState.ChargeLaunch:
                    UpdateChargeLaunch();
                    break;
                case SuperBallState.Recovery:
                    UpdateRecovery();
                    break;
            }
        }

        private void EnterState(SuperBallState nextState)
        {
            if (state != nextState)
            {
                WriteLog($"Super Ball state change: {state} -> {nextState}.");
            }

            state = nextState;
            stateTimer = 0.0f;

            if (nextState == SuperBallState.IdleRoam)
            {
                ricochetCount = 0;
                SetAura(0.0f, 0.0f, false);
                RestoreVisualScale();
                if (roamTarget == Vector3.zero || Vector3.Distance(GetFlatPosition(RootTransform.position), GetFlatPosition(roamTarget)) < 0.45f)
                {
                    PickRoamTarget("enter roam");
                }
            }
            else if (nextState == SuperBallState.ChargeWarning)
            {
                nextProgressLogThreshold = ChargeWarningProgressLogStep;
                lastKnownPlayerPosition = TryGetPlayerPosition(out Vector3 playerPosition) ? playerPosition : RootTransform.position + RootTransform.forward * 4.0f;
                WriteLog($"Super Ball charge warning started. target={FormatVector3(lastKnownPlayerPosition)}, duration={ChargeWarningSeconds:0.00}s.");
                WriteSoundHook("charge warning rising hum / pressure wobble");
            }
            else if (nextState == SuperBallState.ChargeLaunch)
            {
                if (!TryGetLaunchDirection(out launchDirection))
                {
                    launchDirection = RootTransform.forward;
                    launchDirection.y = 0.0f;
                    if (launchDirection.sqrMagnitude < 0.01f)
                    {
                        launchDirection = Vector3.forward;
                    }
                    launchDirection.Normalize();
                    WriteLog($"Super Ball charge launch target missing; using forward launch direction {FormatVector3(launchDirection)}.");
                }

                ricochetCount = 0;
                Squash(new Vector3(1.18f, 0.84f, 1.18f), 0.16f);
                WriteLog($"Super Ball charge launch started. direction={FormatVector3(launchDirection)}, speed={ChargeSpeed:0.00}, maxRicochets={MaxRicochetCount}, damageOnContact={ChargedDamage}.");
                WriteSoundHook("launch snap / whip burst");
            }
            else if (nextState == SuperBallState.Recovery)
            {
                nextChargeAllowedTime = Time.time + ChargeCooldownSeconds;
                SetAura(0.0f, 0.0f, false);
                RestoreVisualScale();
                WriteLog($"Super Ball recovery started. recoveryDuration={RecoveryDuration:0.00}s, nextChargeAllowedIn={ChargeCooldownSeconds:0.00}s.");
                WriteSoundHook("cooldown pressure release");
            }
        }

        private void UpdateIdleRoam()
        {
            if (roamTargetTimer <= 0.0f || Vector3.Distance(GetFlatPosition(RootTransform.position), GetFlatPosition(roamTarget)) < 0.45f)
            {
                PickRoamTarget("roam timer/arrival");
            }

            MoveRootToward(roamTarget, RoamSpeed);
            float bounce = Mathf.Abs(Mathf.Sin((Time.time + phase) * IdleBounceFrequency)) * IdleBounceAmplitude;
            ApplyVerticalBounce(bounce);
            ApplySpin(Mathf.Lerp(80.0f, 190.0f, Mathf.Clamp01(RoamSpeed / 2.0f)));
            SetAura(0.0f, 0.0f, false);
            ApplySquashRecovery();

            if (Time.time >= nextChargeAllowedTime && TryGetPlayerPosition(out Vector3 playerPosition))
            {
                float distance = Vector3.Distance(GetFlatPosition(RootTransform.position), GetFlatPosition(playerPosition));
                if (distance <= ChargeRange)
                {
                    lastKnownPlayerPosition = playerPosition;
                    EnterState(SuperBallState.ChargeWarning);
                }
            }
        }

        private void UpdateChargeWarning()
        {
            if (TryGetPlayerPosition(out Vector3 playerPosition))
            {
                lastKnownPlayerPosition = playerPosition;
            }

            float progress = ChargeWarningSeconds <= 0.0f ? 1.0f : Mathf.Clamp01(stateTimer / ChargeWarningSeconds);
            float eased = progress * progress * (3.0f - 2.0f * progress);
            float amplitude = Mathf.Lerp(ChargeBounceAmplitudeMin, ChargeBounceAmplitudeMax, eased);
            float frequency = Mathf.Lerp(0.85f, 7.5f, eased);
            float spinSpeed = Mathf.Lerp(ChargeSpinSpeedMin, ChargeSpinSpeedMax, eased);
            float bounce = Mathf.Abs(Mathf.Sin((Time.time + phase) * frequency)) * amplitude;

            ApplyVerticalBounce(bounce);
            ApplySpin(spinSpeed);
            ApplyChargeSquash(eased);
            SetAura(eased, Mathf.Lerp(0.35f, 1.0f, eased), true);
            FaceFlatTarget(lastKnownPlayerPosition);

            if (progress >= nextProgressLogThreshold)
            {
                WriteLog($"Super Ball charge warning progress={progress:0.00}, bounceAmp={amplitude:0.00}, spin={spinSpeed:0}, aura={eased:0.00}.");
                nextProgressLogThreshold += ChargeWarningProgressLogStep;
            }

            if (stateTimer >= ChargeWarningSeconds)
            {
                EnterState(SuperBallState.ChargeLaunch);
            }
        }

        private void UpdateChargeLaunch()
        {
            float bounce = Mathf.Abs(Mathf.Sin((Time.time + phase) * 8.5f)) * Mathf.Max(0.08f, ChargeBounceAmplitudeMax * 0.65f);
            ApplyVerticalBounce(bounce);
            ApplySpin(ChargeSpinSpeedMax);
            SetAura(0.75f, 0.75f, AuraEnabled);
            MoveWithRicochet(ChargeSpeed * Time.deltaTime);
            ApplySquashRecovery();

            if (stateTimer >= ChargeLaunchDuration || ricochetCount > MaxRicochetCount)
            {
                EnterState(SuperBallState.Recovery);
            }
        }

        private void UpdateRecovery()
        {
            float progress = RecoveryDuration <= 0.0f ? 1.0f : Mathf.Clamp01(stateTimer / RecoveryDuration);
            float bounce = Mathf.Abs(Mathf.Sin((Time.time + phase) * Mathf.Lerp(4.0f, IdleBounceFrequency, progress))) * Mathf.Lerp(0.10f, IdleBounceAmplitude, progress);
            ApplyVerticalBounce(bounce);
            ApplySpin(Mathf.Lerp(320.0f, 90.0f, progress));
            SetAura(1.0f - progress, 0.35f, AuraEnabled && progress < 0.85f);
            ApplySquashRecovery();

            if (stateTimer >= RecoveryDuration)
            {
                EnterState(SuperBallState.IdleRoam);
            }
        }

        private void PickRoamTarget(string reason)
        {
            Vector3 origin = RootTransform == null ? transform.position : RootTransform.position;
            Vector3 chosen = Vector3.zero;
            bool found = false;

            try
            {
                List<LevelPoint> points = SemiFunc.LevelPointsGetAll();
                if (points != null && points.Count > 0)
                {
                    for (int attempts = 0; attempts < 10; attempts++)
                    {
                        LevelPoint point = points[UnityEngine.Random.Range(0, points.Count)];
                        if (point == null)
                        {
                            continue;
                        }

                        Vector3 candidate = point.transform.position;
                        if (Vector3.Distance(GetFlatPosition(origin), GetFlatPosition(candidate)) < 2.0f)
                        {
                            continue;
                        }

                        if (TryProjectToCenterHeight(candidate, out chosen))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Super Ball roam target level point lookup failed: {ex.GetType().Name}: {ex.Message}");
            }

            if (!found)
            {
                for (int attempts = 0; attempts < 12; attempts++)
                {
                    Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2.0f, 7.0f);
                    Vector3 candidate = origin + new Vector3(circle.x, 0.0f, circle.y);
                    if (TryProjectToCenterHeight(candidate, out chosen))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                chosen = origin + RootTransform.forward * 3.0f;
                chosen.y = restCenterY;
            }

            roamTarget = chosen;
            roamTargetTimer = UnityEngine.Random.Range(RoamTargetMinSeconds, RoamTargetMaxSeconds);
            WriteLog($"Super Ball roam target chosen ({reason}): target={FormatVector3(roamTarget)}, nextPickIn={roamTargetTimer:0.00}s.");
        }

        private bool TryProjectToCenterHeight(Vector3 candidate, out Vector3 center)
        {
            center = candidate;
            float radius = GetRadius();
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                center = hit.position + Vector3.up * (radius + 0.03f);
                return true;
            }

            Ray ray = new Ray(candidate + Vector3.up * 3.0f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit groundHit, 8.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                center = groundHit.point + Vector3.up * (radius + 0.03f);
                return true;
            }

            return false;
        }

        private void MoveRootToward(Vector3 target, float speed)
        {
            Vector3 position = RootTransform.position;
            Vector3 flatPosition = GetFlatPosition(position);
            Vector3 flatTarget = GetFlatPosition(target);
            Vector3 delta = flatTarget - flatPosition;
            if (delta.sqrMagnitude < 0.0025f)
            {
                return;
            }

            Vector3 direction = delta.normalized;
            float step = speed * Time.deltaTime;
            Vector3 nextFlat = flatPosition + direction * Mathf.Min(step, delta.magnitude);
            restCenterY = Mathf.MoveTowards(restCenterY, target.y, step * 1.5f);
            RootTransform.position = new Vector3(nextFlat.x, RootTransform.position.y, nextFlat.z);
            FaceDirection(direction);
        }

        private void MoveWithRicochet(float distance)
        {
            if (distance <= 0.0f || RootTransform == null)
            {
                return;
            }

            Vector3 origin = RootTransform.position;
            Vector3 direction = launchDirection;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = RootTransform.forward;
                direction.y = 0.0f;
            }
            direction.Normalize();

            float radius = Mathf.Max(0.08f, GetRadius() * 0.92f);
            if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance + SelfHitGraceDistance, ~0, QueryTriggerInteraction.Ignore)
                && !IsSelfCollider(hit.collider)
                && hit.distance > SelfHitGraceDistance)
            {
                Vector3 hitCenter = hit.point + hit.normal * (radius + 0.025f);
                restCenterY = Mathf.Max(restCenterY, hitCenter.y);
                RootTransform.position = new Vector3(hitCenter.x, restCenterY, hitCenter.z);

                PlayerAvatar hitPlayer = hit.collider == null ? null : hit.collider.GetComponentInParent<PlayerAvatar>();
                if (hitPlayer != null)
                {
                    LogPotentialPlayerDamage(hit.collider);
                }

                launchDirection = Vector3.Reflect(direction, hit.normal);
                launchDirection.y = 0.0f;
                if (launchDirection.sqrMagnitude < 0.01f)
                {
                    launchDirection = -direction;
                }
                launchDirection.Normalize();

                ricochetCount++;
                Squash(new Vector3(1.14f, 0.88f, 1.14f), 0.14f);
                WriteLog($"Super Ball ricochet {ricochetCount}/{MaxRicochetCount}: hit='{GetColliderName(hit.collider)}', point={FormatVector3(hit.point)}, newDirection={FormatVector3(launchDirection)}.");
                WriteSoundHook("wall ricochet rubber smack");

                float remaining = Mathf.Max(0.0f, distance - hit.distance);
                if (remaining > 0.01f && ricochetCount <= MaxRicochetCount)
                {
                    RootTransform.position += launchDirection * remaining;
                }
            }
            else
            {
                RootTransform.position += direction * distance;
            }

            FaceDirection(launchDirection);
        }

        private void ApplyVerticalBounce(float bounce)
        {
            Vector3 position = RootTransform.position;
            RootTransform.position = new Vector3(position.x, restCenterY + bounce, position.z);
            VisualRoot.localPosition = baseLocalPosition;
        }

        private void ApplySpin(float spinDegreesPerSecond)
        {
            VisualRoot.Rotate(Vector3.right, spinDegreesPerSecond * Time.deltaTime, Space.Self);
            VisualRoot.Rotate(Vector3.up, spinDegreesPerSecond * 0.32f * Time.deltaTime, Space.World);
        }

        private void ApplyChargeSquash(float progress)
        {
            float pulse = Mathf.Abs(Mathf.Sin((Time.time + phase) * Mathf.Lerp(2.0f, 9.0f, progress)));
            float squash = Mathf.Lerp(0.02f, 0.14f, progress) * pulse;
            VisualRoot.localScale = new Vector3(
                baseLocalScale.x * (1.0f + squash * 0.65f),
                baseLocalScale.y * (1.0f - squash),
                baseLocalScale.z * (1.0f + squash * 0.65f));
        }

        private void Squash(Vector3 scale, float duration)
        {
            squashScale = scale;
            squashTimer = duration;
        }

        private void ApplySquashRecovery()
        {
            if (squashTimer > 0.0f)
            {
                VisualRoot.localScale = Vector3.Lerp(baseLocalScale, Vector3.Scale(baseLocalScale, squashScale), Mathf.Clamp01(squashTimer / 0.16f));
                return;
            }

            RestoreVisualScale();
        }

        private void SetAura(float intensity01, float pulseSpeedMultiplier, bool enabled)
        {
            bool auraActive = AuraEnabled && enabled && intensity01 > 0.01f && AuraRoot != null && AuraRenderer != null;
            if (AuraRenderer != null)
            {
                AuraRenderer.enabled = auraActive;
            }

            if (!auraActive)
            {
                if (AuraLight != null)
                {
                    AuraLight.intensity = baseLightIntensity;
                    AuraLight.range = baseLightRange;
                }
                return;
            }

            float pulse = (Mathf.Sin((Time.time + phase) * Mathf.Lerp(2.0f, 12.0f, pulseSpeedMultiplier)) + 1.0f) * 0.5f;
            float scale = AuraScaleMultiplier * Mathf.Lerp(0.88f, ChargeAuraScale, intensity01) * Mathf.Lerp(0.96f, 1.10f, pulse);
            AuraRoot.localScale = Vector3.one * scale;

            Material material = AuraRenderer.material;
            Color color = new Color(0.10f, 1.0f, 0.04f, AuraAlpha * Mathf.Lerp(0.15f, 1.0f, intensity01) * Mathf.Lerp(0.75f, 1.15f, pulse));
            Color emission = new Color(0.0f, 1.0f, 0.04f, 1.0f) * Mathf.Lerp(1.2f, 6.0f, intensity01);
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
            }

            if (AuraLight != null)
            {
                AuraLight.intensity = baseLightIntensity * Mathf.Lerp(1.0f, ChargeAuraScale, intensity01) * Mathf.Lerp(0.9f, 1.2f, pulse);
                AuraLight.range = baseLightRange * Mathf.Lerp(1.0f, ChargeAuraScale, intensity01);
            }
        }

        private bool TryGetPlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            PlayerAvatar player = null;

            try
            {
                player = SemiFunc.PlayerAvatarLocal();
            }
            catch
            {
                player = null;
            }

            if (player == null)
            {
                return false;
            }

            position = player.transform.position;
            return true;
        }

        private bool TryGetLaunchDirection(out Vector3 direction)
        {
            Vector3 target = lastKnownPlayerPosition;
            if (TryGetPlayerPosition(out Vector3 playerPosition))
            {
                target = playerPosition;
            }

            Vector3 delta = target - RootTransform.position;
            delta.y = 0.0f;
            if (delta.sqrMagnitude < 0.01f)
            {
                direction = Vector3.zero;
                return false;
            }

            direction = delta.normalized;
            return true;
        }

        private void FaceFlatTarget(Vector3 target)
        {
            Vector3 delta = target - RootTransform.position;
            delta.y = 0.0f;
            if (delta.sqrMagnitude > 0.01f)
            {
                FaceDirection(delta.normalized);
            }
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            RootTransform.rotation = Quaternion.Slerp(RootTransform.rotation, Quaternion.LookRotation(direction.normalized, Vector3.up), Time.deltaTime * 4.0f);
        }

        private bool IsSelfCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.transform == transform || collider.transform.IsChildOf(RootTransform))
            {
                return true;
            }

            return selfColliders.Contains(collider);
        }

        private void CacheSelfColliders()
        {
            selfColliders.Clear();
            if (RootTransform == null)
            {
                return;
            }

            Collider[] colliders = RootTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    selfColliders.Add(colliders[i]);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            if (state == SuperBallState.ChargeLaunch)
            {
                Squash(new Vector3(1.12f, 0.90f, 1.12f), 0.12f);
            }

            LogPotentialPlayerDamage(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            LogPotentialPlayerDamage(other);
        }

        private void LogPotentialPlayerDamage(Collider other)
        {
            if (other == null || contactLogCooldown > 0.0f)
            {
                return;
            }

            PlayerAvatar player = other.GetComponentInParent<PlayerAvatar>();
            bool looksLikePlayer = player != null
                || other.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
                || other.gameObject.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!looksLikePlayer)
            {
                return;
            }

            int damage = state == SuperBallState.ChargeLaunch ? ChargedDamage : ContactDamage;
            contactLogCooldown = 0.85f;
            WriteLog($"Super Ball contact hit '{other.name}' during {state}. Would damage player for {damage}; safe player damage API is still unresolved.");
        }

        private void CaptureBaseVisuals()
        {
            if (VisualRoot == null)
            {
                return;
            }

            baseLocalPosition = Vector3.zero;
            VisualRoot.localPosition = baseLocalPosition;
            baseLocalScale = VisualRoot.localScale;

            if (RootTransform != null)
            {
                restCenterY = RootTransform.position.y;
            }

            if (AuraLight != null)
            {
                baseLightIntensity = Mathf.Max(0.01f, AuraLight.intensity);
                baseLightRange = Mathf.Max(0.01f, AuraLight.range);
            }
        }

        private void RestoreVisualScale()
        {
            if (VisualRoot != null && baseLocalScale != Vector3.zero)
            {
                VisualRoot.localScale = baseLocalScale;
            }
        }

        private float GetRadius()
        {
            return Mathf.Max(0.05f, Diameter * 0.5f);
        }

        private static Vector3 GetFlatPosition(Vector3 value)
        {
            return new Vector3(value.x, 0.0f, value.z);
        }

        private static string GetColliderName(Collider collider)
        {
            return collider == null ? "<none>" : collider.name;
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
        }

        private static void WriteSoundHook(string soundMoment)
        {
            WriteLog($"Super Ball sound hook: {soundMoment}. No custom audio asset assigned yet.");
        }

        private static void WriteLog(string message)
        {
            if (Log != null)
            {
                Log.LogInfo(message);
            }
            else
            {
                Debug.Log("[RepoSuperBallEnemy] " + message);
            }
        }
    }
}
