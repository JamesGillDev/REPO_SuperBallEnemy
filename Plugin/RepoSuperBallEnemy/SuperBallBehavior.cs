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
        private const float SkinWidth = 0.045f;
        private const float MinMoveDistance = 0.0025f;
        private const float BlockedStuckSeconds = 0.75f;
        private const float NoProgressStuckSeconds = 1.25f;

        private static ManualLogSource Log;

        public Transform RootTransform;
        public Transform VisualRoot;
        public Transform AuraRoot;
        public Renderer AuraRenderer;
        public SuperBallAuraField AuraField;
        public SuperBallLightningArcs Lightning;
        public Light AuraLight;
        public Collider ContactCollider;
        public Rigidbody Body;
        public bool BehaviorEnabled = true;
        public int ContactDamage = 5;
        public int ChargedDamage = 10;
        public float ChargeWarningSeconds = 3.0f;
        public float ChargeCooldownSeconds = 5.0f;
        public float RoamSpeed = 2.0f;
        public float RoamBounceHeight = 0.22f;
        public float RoamBounceFrequency = 2.1f;
        public float ChargeBounceHeight = 0.28f;
        public float ChargeBounceFrequency = 4.0f;
        public float ElasticSquashAmount = 0.18f;
        public float ChargeSpinSpeedMin = 120.0f;
        public float ChargeSpinSpeedMax = 1320.0f;
        public float ChargeSpeed = 9.5f;
        public int MaxRicochetCount = 3;
        public float RecoveryDuration = 1.25f;
        public float ChargeAuraScale = 2.25f;
        public bool AuraEnabled = true;
        public float AuraIdleAlpha = 0.0f;
        public float AuraChargeAlpha = 0.12f;
        public float AuraPulseSpeedMin = 1.0f;
        public float AuraPulseSpeedMax = 7.0f;
        public float AuraMaxScale = 1.45f;
        public float ChargeScaleMultiplier = 1.75f;
        public float ChargeScaleCurvePower = 1.6f;
        public float FaceAppearAtChargeProgress = 0.25f;
        public bool LightningEnabled = true;
        public float LightningDuringChargeProgress = 0.70f;
        public float Diameter = 0.55f;

        private SuperBallState state = SuperBallState.IdleRoam;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale = Vector3.one;
        private Vector3 roamTarget;
        private Vector3 launchDirection = Vector3.forward;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 lastMoveDirection = Vector3.forward;
        private Vector3 lastProgressPosition;
        private float restCenterY;
        private float baseLightIntensity = 1.0f;
        private float baseLightRange = 2.0f;
        private float stateTimer;
        private float roamTargetTimer;
        private float phase;
        private float nextChargeAllowedTime;
        private float contactLogCooldown;
        private float nextProgressLogThreshold;
        private float blockedMovementTimer;
        private float noProgressTimer;
        private float stuckCheckTimer;
        private float nextBlockedLogTime;
        private float nextChargeCastLogTime;
        private float nextInsideRecoveryLogTime;
        private float nextIdleBounceSoundTime;
        private float nextChargeHumSoundTime;
        private float nextLightningSoundTime;
        private float previousBounceCycle;
        private float currentScaleMultiplier = 1.0f;
        private float squashTimer;
        private Vector3 squashScale = Vector3.one;
        private int ricochetCount;
        private bool faceActiveLogged;
        private bool lightningActiveLogged;
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

        public float VisualFaceIntensity
        {
            get
            {
                if (state == SuperBallState.IdleRoam)
                {
                    return 0.0f;
                }

                float charge = VisualChargeIntensity;
                return Mathf.InverseLerp(FaceAppearAtChargeProgress, 1.0f, charge);
            }
        }

        public float VisualScaleMultiplier
        {
            get { return currentScaleMultiplier; }
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
            SuperBallAuraField auraField,
            SuperBallLightningArcs lightning,
            Light auraLight,
            Collider contactCollider,
            Rigidbody body,
            bool behaviorEnabled,
            int contactDamage,
            int chargedDamage,
            float chargeWarningSeconds,
            float chargeCooldownSeconds,
            float roamSpeed,
            float roamBounceHeight,
            float roamBounceFrequency,
            float chargeBounceHeight,
            float chargeBounceFrequency,
            float elasticSquashAmount,
            float chargeSpinSpeedMin,
            float chargeSpinSpeedMax,
            float chargeSpeed,
            int maxRicochetCount,
            float recoveryDuration,
            float chargeAuraScale,
            bool auraEnabled,
            float auraIdleAlpha,
            float auraChargeAlpha,
            float auraPulseSpeedMin,
            float auraPulseSpeedMax,
            float auraMaxScale,
            float chargeScaleMultiplier,
            float chargeScaleCurvePower,
            float faceAppearAtChargeProgress,
            bool lightningEnabled,
            float lightningDuringChargeProgress,
            float diameter)
        {
            RootTransform = rootTransform;
            VisualRoot = visualRoot == null ? transform : visualRoot;
            AuraRoot = auraRoot;
            AuraRenderer = auraRenderer;
            AuraField = auraField;
            Lightning = lightning;
            AuraLight = auraLight;
            ContactCollider = contactCollider;
            Body = body;
            BehaviorEnabled = behaviorEnabled;
            ContactDamage = contactDamage;
            ChargedDamage = chargedDamage;
            ChargeWarningSeconds = chargeWarningSeconds;
            ChargeCooldownSeconds = chargeCooldownSeconds;
            RoamSpeed = roamSpeed;
            RoamBounceHeight = roamBounceHeight;
            RoamBounceFrequency = roamBounceFrequency;
            ChargeBounceHeight = chargeBounceHeight;
            ChargeBounceFrequency = chargeBounceFrequency;
            ElasticSquashAmount = elasticSquashAmount;
            ChargeSpinSpeedMin = chargeSpinSpeedMin;
            ChargeSpinSpeedMax = Mathf.Max(chargeSpinSpeedMin, chargeSpinSpeedMax);
            ChargeSpeed = chargeSpeed;
            MaxRicochetCount = maxRicochetCount;
            RecoveryDuration = recoveryDuration;
            ChargeAuraScale = chargeAuraScale;
            AuraEnabled = auraEnabled;
            AuraIdleAlpha = auraIdleAlpha;
            AuraChargeAlpha = auraChargeAlpha;
            AuraPulseSpeedMin = auraPulseSpeedMin;
            AuraPulseSpeedMax = Mathf.Max(auraPulseSpeedMin, auraPulseSpeedMax);
            AuraMaxScale = auraMaxScale;
            ChargeScaleMultiplier = chargeScaleMultiplier;
            ChargeScaleCurvePower = chargeScaleCurvePower;
            FaceAppearAtChargeProgress = faceAppearAtChargeProgress;
            LightningEnabled = lightningEnabled;
            LightningDuringChargeProgress = lightningDuringChargeProgress;
            Diameter = diameter;

            CaptureBaseVisuals();
            CacheSelfColliders();
            SetAura(0.0f, false, false);
            SetLightning(0.0f, false);
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
                Body = RootTransform == null ? GetComponentInParent<Rigidbody>() : RootTransform.GetComponent<Rigidbody>();
            }

            phase = UnityEngine.Random.value * 10.0f;
            CaptureBaseVisuals();
            CacheSelfColliders();
        }

        private void OnEnable()
        {
            CaptureBaseVisuals();
            CacheSelfColliders();
            lastProgressPosition = RootTransform == null ? transform.position : RootTransform.position;
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

            RecoverIfInsideGeometry();

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

            TrackNoProgress();
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
                blockedMovementTimer = 0.0f;
                noProgressTimer = 0.0f;
                faceActiveLogged = false;
                lightningActiveLogged = false;
                SetAura(0.0f, false, false);
                SetLightning(0.0f, false);
                ApplyVisualScale(1.0f);
                if (roamTarget == Vector3.zero || Vector3.Distance(GetFlatPosition(RootTransform.position), GetFlatPosition(roamTarget)) < 0.45f)
                {
                    PickRoamTarget("enter roam");
                }
            }
            else if (nextState == SuperBallState.ChargeWarning)
            {
                nextProgressLogThreshold = ChargeWarningProgressLogStep;
                faceActiveLogged = false;
                lightningActiveLogged = false;
                blockedMovementTimer = 0.0f;
                lastKnownPlayerPosition = TryGetPlayerPosition(out Vector3 playerPosition) ? playerPosition : RootTransform.position + RootTransform.forward * 4.0f;
                WriteLog($"Super Ball charge warning started. target={FormatVector3(lastKnownPlayerPosition)}, duration={ChargeWarningSeconds:0.00}s.");
                PlayChargeHumSound();
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
                Squash(new Vector3(1.0f + ElasticSquashAmount, 1.0f - ElasticSquashAmount, 1.0f + ElasticSquashAmount), 0.16f);
                SetAura(1.0f, true, true);
                SetLightning(1.0f, true);
                WriteLog($"Super Ball charge launch started. direction={FormatVector3(launchDirection)}, speed={ChargeSpeed:0.00}, maxRicochets={MaxRicochetCount}, damageOnContact={ChargedDamage}.");
                PlayLaunchSound();
            }
            else if (nextState == SuperBallState.Recovery)
            {
                nextChargeAllowedTime = Time.time + ChargeCooldownSeconds;
                SetLightning(0.0f, false);
                SetAura(0.0f, false, false);
                ApplyVisualScale(1.0f);
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

            Vector3 moveDirection;
            float movedSpeed01 = MoveRootToward(roamTarget, RoamSpeed, out moveDirection);
            float frequency = RoamBounceFrequency * Mathf.Lerp(0.75f, 1.35f, movedSpeed01);
            float bounce = ComputeBounce(RoamBounceHeight * Mathf.Lerp(0.55f, 1.0f, movedSpeed01), frequency, true, "roam");

            ApplyCenterHeight(bounce);
            ApplySpin(moveDirection, Mathf.Lerp(115.0f, 260.0f, movedSpeed01));
            ApplyVisualScale(1.0f);
            SetAura(0.0f, false, true);
            SetLightning(0.0f, false);

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
            float curvedProgress = Mathf.Pow(progress, Mathf.Max(0.1f, ChargeScaleCurvePower));
            float eased = progress * progress * (3.0f - 2.0f * progress);
            float bounceHeight = Mathf.Lerp(RoamBounceHeight * 0.45f, ChargeBounceHeight, eased);
            float frequency = Mathf.Lerp(RoamBounceFrequency * 0.75f, ChargeBounceFrequency, eased);
            float spinSpeed = Mathf.Lerp(ChargeSpinSpeedMin, ChargeSpinSpeedMax, eased);
            float scaleMultiplier = Mathf.Lerp(1.0f, ChargeScaleMultiplier, curvedProgress);
            float bounce = ComputeBounce(bounceHeight, frequency, true, "charge");

            ApplyCenterHeight(bounce);
            ApplySpin(lastMoveDirection, spinSpeed);
            ApplyVisualScale(scaleMultiplier);
            SetAura(eased, false, true);
            SetLightning(eased, false);
            FaceFlatTarget(lastKnownPlayerPosition);

            if (!faceActiveLogged && VisualFaceIntensity > 0.01f)
            {
                faceActiveLogged = true;
                WriteLog($"Super Ball face activated at chargeProgress={progress:0.00}.");
                WriteSoundHook("face activation sting");
            }

            if (!lightningActiveLogged && LightningEnabled && progress >= LightningDuringChargeProgress)
            {
                lightningActiveLogged = true;
                WriteLog($"Super Ball lightning enabled at chargeProgress={progress:0.00}.");
                PlayLightningCrackleSound();
            }

            if (Time.time >= nextChargeHumSoundTime)
            {
                PlayChargeHumSound();
            }

            if (progress >= nextProgressLogThreshold)
            {
                WriteLog($"Super Ball chargeProgress {nextProgressLogThreshold:0.00}: bounceHeight={bounceHeight:0.00}, bounceFrequency={frequency:0.00}, spin={spinSpeed:0}, scale={scaleMultiplier:0.00}, aura={eased:0.00}, face={VisualFaceIntensity:0.00}, lightning={(LightningEnabled && progress >= LightningDuringChargeProgress)}.");
                nextProgressLogThreshold += ChargeWarningProgressLogStep;
            }

            if (stateTimer >= ChargeWarningSeconds)
            {
                EnterState(SuperBallState.ChargeLaunch);
            }
        }

        private void UpdateChargeLaunch()
        {
            float bounce = ComputeBounce(ChargeBounceHeight * 0.78f, ChargeBounceFrequency * 1.35f, true, "launch");
            ApplyCenterHeight(bounce);
            ApplySpin(launchDirection, ChargeSpinSpeedMax * 1.15f);
            ApplyVisualScale(Mathf.Lerp(ChargeScaleMultiplier, Mathf.Max(1.0f, ChargeScaleMultiplier * 0.88f), Mathf.Clamp01(stateTimer / 0.22f)));
            SetAura(1.0f, true, true);
            SetLightning(1.0f, true);
            MoveWithRicochet(ChargeSpeed * Time.deltaTime);

            if (stateTimer >= ChargeLaunchDuration || ricochetCount > MaxRicochetCount)
            {
                EnterState(SuperBallState.Recovery);
            }
        }

        private void UpdateRecovery()
        {
            float progress = RecoveryDuration <= 0.0f ? 1.0f : Mathf.Clamp01(stateTimer / RecoveryDuration);
            float bounce = ComputeBounce(Mathf.Lerp(ChargeBounceHeight * 0.55f, RoamBounceHeight * 0.45f, progress), Mathf.Lerp(ChargeBounceFrequency, RoamBounceFrequency, progress), true, "recovery");
            ApplyCenterHeight(bounce);
            ApplySpin(lastMoveDirection, Mathf.Lerp(420.0f, 90.0f, progress));
            ApplyVisualScale(Mathf.Lerp(Mathf.Max(1.0f, ChargeScaleMultiplier * 0.92f), 1.0f, progress));
            SetAura(1.0f - progress, false, progress < 0.85f);
            SetLightning(0.0f, false);

            if (faceActiveLogged && progress > 0.85f)
            {
                faceActiveLogged = false;
                WriteLog("Super Ball face deactivated after recovery.");
            }

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
                    for (int attempts = 0; attempts < 14; attempts++)
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

                        if (TryProjectToCenterHeight(candidate, out chosen) && HasReasonablePath(origin, chosen))
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
                for (int attempts = 0; attempts < 16; attempts++)
                {
                    Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2.0f, 7.0f);
                    Vector3 candidate = origin + new Vector3(circle.x, 0.0f, circle.y);
                    if (TryProjectToCenterHeight(candidate, out chosen) && HasReasonablePath(origin, chosen))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                chosen = origin + RootTransform.forward * 2.5f;
                chosen.y = restCenterY;
            }

            roamTarget = chosen;
            roamTargetTimer = UnityEngine.Random.Range(RoamTargetMinSeconds, RoamTargetMaxSeconds);
            blockedMovementTimer = 0.0f;
            noProgressTimer = 0.0f;
            lastProgressPosition = RootTransform.position;
            WriteLog($"Super Ball roam target chosen ({reason}): target={FormatVector3(roamTarget)}, nextPickIn={roamTargetTimer:0.00}s.");
        }

        private bool TryProjectToCenterHeight(Vector3 candidate, out Vector3 center)
        {
            center = candidate;
            float radius = GetPhysicsRadius();
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

        private bool HasReasonablePath(Vector3 origin, Vector3 target)
        {
            Vector3 flatOrigin = GetFlatPosition(origin);
            Vector3 flatTarget = GetFlatPosition(target);
            Vector3 delta = flatTarget - flatOrigin;
            float distance = delta.magnitude;
            if (distance < 0.1f)
            {
                return true;
            }

            Vector3 direction = delta / distance;
            return !FindBlockingHit(origin, direction, Mathf.Min(distance, 4.0f), GetPhysicsRadius() * 0.86f, out _);
        }

        private float MoveRootToward(Vector3 target, float speed, out Vector3 moveDirection)
        {
            Vector3 position = RootTransform.position;
            Vector3 flatPosition = GetFlatPosition(position);
            Vector3 flatTarget = GetFlatPosition(target);
            Vector3 delta = flatTarget - flatPosition;
            moveDirection = lastMoveDirection;
            if (delta.sqrMagnitude < 0.0025f)
            {
                return 0.0f;
            }

            moveDirection = delta.normalized;
            lastMoveDirection = moveDirection;
            float step = Mathf.Min(speed * Time.deltaTime, delta.magnitude);
            restCenterY = Mathf.MoveTowards(restCenterY, target.y, step * 1.7f);
            bool moved = TryMoveHorizontal(moveDirection * step, false, out RaycastHit hit);
            if (!moved)
            {
                blockedMovementTimer += Time.deltaTime;
                if (Time.time >= nextBlockedLogTime)
                {
                    nextBlockedLogTime = Time.time + 0.75f;
                    WriteLog($"Super Ball blocked movement detected while roaming. hit='{GetColliderName(hit.collider)}', normal={FormatVector3(hit.normal)}, blockedFor={blockedMovementTimer:0.00}s.");
                }

                if (blockedMovementTimer >= BlockedStuckSeconds)
                {
                    WriteLog("Super Ball stuck recovery triggered from repeated blocked roam movement; choosing a new roam target.");
                    PickRoamTarget("blocked movement recovery");
                }

                return 0.0f;
            }

            blockedMovementTimer = 0.0f;
            FaceDirection(moveDirection);
            return Mathf.Clamp01(step / Mathf.Max(0.001f, speed * Time.deltaTime));
        }

        private bool TryMoveHorizontal(Vector3 delta, bool reflectOnHit, out RaycastHit blockingHit)
        {
            blockingHit = default;
            delta.y = 0.0f;
            float distance = delta.magnitude;
            if (distance <= MinMoveDistance)
            {
                return true;
            }

            Vector3 direction = delta / distance;
            float remaining = distance;
            int stepCount = 0;
            bool movedAny = false;

            while (remaining > MinMoveDistance && stepCount < 5)
            {
                stepCount++;
                float stepDistance = Mathf.Min(remaining, Mathf.Max(0.08f, GetPhysicsRadius() * 0.65f));
                if (TryMoveStep(direction, stepDistance, reflectOnHit, out blockingHit))
                {
                    remaining -= stepDistance;
                    movedAny = true;
                    continue;
                }

                if (!reflectOnHit)
                {
                    Vector3 slide = Vector3.ProjectOnPlane(direction, blockingHit.normal);
                    slide.y = 0.0f;
                    if (slide.sqrMagnitude > 0.05f && TryMoveStep(slide.normalized, stepDistance * 0.65f, false, out _))
                    {
                        lastMoveDirection = slide.normalized;
                        movedAny = true;
                        remaining -= stepDistance;
                        continue;
                    }
                }

                return movedAny;
            }

            return true;
        }

        private bool TryMoveStep(Vector3 direction, float distance, bool reflectOnHit, out RaycastHit blockingHit)
        {
            blockingHit = default;
            if (direction.sqrMagnitude < 0.01f || distance <= MinMoveDistance)
            {
                return true;
            }

            direction.Normalize();
            float radius = GetPhysicsRadius() * 0.92f;
            if (FindBlockingHit(RootTransform.position, direction, distance + SkinWidth, radius, out blockingHit))
            {
                float safeDistance = Mathf.Max(0.0f, blockingHit.distance - SkinWidth);
                if (safeDistance > MinMoveDistance)
                {
                    MoveRootTo(RootTransform.position + direction * safeDistance);
                }

                return false;
            }

            MoveRootTo(RootTransform.position + direction * distance);
            return true;
        }

        private void MoveWithRicochet(float distance)
        {
            if (distance <= 0.0f || RootTransform == null)
            {
                return;
            }

            Vector3 direction = launchDirection;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = RootTransform.forward;
                direction.y = 0.0f;
            }
            direction.Normalize();

            float radius = GetPhysicsRadius() * 0.92f;
            if (FindBlockingHit(RootTransform.position, direction, distance + SkinWidth, radius, out RaycastHit hit))
            {
                float safeDistance = Mathf.Max(0.0f, hit.distance - SkinWidth);
                if (safeDistance > MinMoveDistance)
                {
                    MoveRootTo(RootTransform.position + direction * safeDistance);
                }

                PlayerAvatar hitPlayer = hit.collider == null ? null : hit.collider.GetComponentInParent<PlayerAvatar>();
                if (hitPlayer != null)
                {
                    LogPotentialPlayerDamage(hit.collider);
                }

                bool acceptedWallRicochet = IsAcceptedWallRicochet(hit);
                LogChargeCastHit(hit, acceptedWallRicochet);
                if (!acceptedWallRicochet)
                {
                    Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                    slide.y = 0.0f;
                    if (slide.sqrMagnitude > 0.05f)
                    {
                        lastMoveDirection = slide.normalized;
                        float remainingNoRicochet = Mathf.Max(0.0f, distance - hit.distance);
                        if (remainingNoRicochet > MinMoveDistance)
                        {
                            TryMoveHorizontal(lastMoveDirection * remainingNoRicochet, false, out _);
                        }
                    }
                    else if (safeDistance <= MinMoveDistance)
                    {
                        blockedMovementTimer += Time.deltaTime;
                    }

                    FaceDirection(lastMoveDirection);
                    return;
                }

                launchDirection = Vector3.Reflect(direction, hit.normal);
                launchDirection.y = 0.0f;
                if (launchDirection.sqrMagnitude < 0.01f)
                {
                    launchDirection = -direction;
                }
                launchDirection.Normalize();
                lastMoveDirection = launchDirection;

                ricochetCount++;
                Squash(new Vector3(1.0f + ElasticSquashAmount, 1.0f - ElasticSquashAmount, 1.0f + ElasticSquashAmount), 0.14f);
                WriteLog($"Super Ball ricochet {ricochetCount}/{MaxRicochetCount}: wallHit='{GetColliderName(hit.collider)}', hitNormal={FormatVector3(hit.normal)}, normalY={hit.normal.y:0.00}, ricochetDirection={FormatVector3(launchDirection)}, point={FormatVector3(hit.point)}.");
                PlayRicochetSound();
                if (Lightning != null)
                {
                    Lightning.Burst();
                    WriteLog("Super Ball ricochet lightning burst.");
                    PlayLightningCrackleSound();
                }

                float remaining = Mathf.Max(0.0f, distance - hit.distance);
                if (remaining > MinMoveDistance && ricochetCount <= MaxRicochetCount)
                {
                    TryMoveHorizontal(launchDirection * remaining, true, out _);
                }
            }
            else
            {
                MoveRootTo(RootTransform.position + direction * distance);
            }

            FaceDirection(launchDirection);
        }

        private bool FindBlockingHit(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit blockingHit)
        {
            blockingHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || IsSelfCollider(hit.collider))
                {
                    continue;
                }
                if (hit.normal.y > 0.55f)
                {
                    continue;
                }
                if (hit.normal.y < -0.55f)
                {
                    continue;
                }
                if (hit.distance < 0.001f && Vector3.Dot(direction, hit.normal) >= -0.05f)
                {
                    continue;
                }
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    blockingHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool IsAcceptedWallRicochet(RaycastHit hit)
        {
            bool suspiciousPoint = hit.point.sqrMagnitude < 0.0001f && hit.distance > 0.01f;
            bool wallLikeNormal = Mathf.Abs(hit.normal.y) < 0.35f;
            bool likelyFloorOrCeiling = IsLikelyFloorOrCeilingCollider(hit.collider);
            return !suspiciousPoint && wallLikeNormal && !likelyFloorOrCeiling;
        }

        private void LogChargeCastHit(RaycastHit hit, bool acceptedAsWallRicochet)
        {
            if (acceptedAsWallRicochet || Time.time >= nextChargeCastLogTime)
            {
                nextChargeCastLogTime = Time.time + 0.50f;
                bool suspiciousPoint = hit.point.sqrMagnitude < 0.0001f && hit.distance > 0.01f;
                WriteLog($"Super Ball charge cast hit object='{GetColliderName(hit.collider)}', normal={FormatVector3(hit.normal)}, normalY={hit.normal.y:0.00}, point={FormatVector3(hit.point)}, suspiciousPoint={suspiciousPoint}, acceptedAsWallRicochet={acceptedAsWallRicochet}.");
            }
        }

        private static bool IsLikelyFloorOrCeilingCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            string name = (collider.name ?? string.Empty) + " " + (collider.gameObject == null ? string.Empty : collider.gameObject.name);
            return name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ceiling", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MoveRootTo(Vector3 position)
        {
            if (Body != null && Body.transform == RootTransform)
            {
                Body.MovePosition(position);
                RootTransform.position = position;
            }
            else
            {
                RootTransform.position = position;
            }
        }

        private void ApplyCenterHeight(float bounce)
        {
            Vector3 position = RootTransform.position;
            MoveRootTo(new Vector3(position.x, restCenterY + bounce, position.z));
            VisualRoot.localPosition = baseLocalPosition;
        }

        private float ComputeBounce(float height, float frequency, bool squashOnImpact, string soundContext)
        {
            float cycle = Mathf.Repeat((Time.time + phase) * Mathf.Max(0.1f, frequency), 1.0f);
            float bounce = Mathf.Sin(cycle * Mathf.PI) * Mathf.Max(0.0f, height);
            if (cycle < previousBounceCycle && squashOnImpact)
            {
                float amount = Mathf.Clamp(ElasticSquashAmount, 0.04f, 0.24f);
                Squash(new Vector3(1.0f + amount, 1.0f - amount, 1.0f + amount), 0.10f);
                if (soundContext == "roam")
                {
                    PlayIdleBounceSound();
                }
            }

            previousBounceCycle = cycle;
            return bounce;
        }

        private void ApplySpin(Vector3 moveDirection, float spinDegreesPerSecond)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, moveDirection.sqrMagnitude < 0.01f ? lastMoveDirection : moveDirection.normalized);
            if (axis.sqrMagnitude < 0.01f)
            {
                axis = Vector3.right;
            }

            VisualRoot.Rotate(axis.normalized, spinDegreesPerSecond * Time.deltaTime, Space.World);
            VisualRoot.Rotate(Vector3.up, spinDegreesPerSecond * 0.18f * Time.deltaTime, Space.World);
        }

        private void ApplyVisualScale(float scaleMultiplier)
        {
            currentScaleMultiplier = Mathf.Max(0.1f, scaleMultiplier);
            Vector3 scale = baseLocalScale * currentScaleMultiplier;
            if (squashTimer > 0.0f)
            {
                float t = Mathf.Clamp01(squashTimer / 0.16f);
                scale = Vector3.Scale(scale, Vector3.Lerp(Vector3.one, squashScale, t));
            }

            VisualRoot.localScale = scale;
        }

        private void Squash(Vector3 scale, float duration)
        {
            squashScale = scale;
            squashTimer = Mathf.Max(squashTimer, duration);
        }

        private void SetAura(float intensity01, bool launch, bool enabled)
        {
            float intensity = Mathf.Clamp01(intensity01);
            bool active = AuraEnabled && enabled;
            if (AuraField != null)
            {
                AuraField.SetAura(intensity, active, launch);
            }
            else if (AuraRenderer != null)
            {
                bool rendererActive = active && (intensity > 0.01f || state == SuperBallState.IdleRoam);
                AuraRenderer.enabled = rendererActive;
                if (rendererActive)
                {
                    float pulse = (Mathf.Sin((Time.time + phase) * Mathf.Lerp(AuraPulseSpeedMin, AuraPulseSpeedMax, intensity)) + 1.0f) * 0.5f;
                    AuraRoot.localScale = Vector3.one * Mathf.Lerp(1.12f, AuraMaxScale, intensity) * Mathf.Lerp(0.97f, 1.08f, pulse);
                    Material material = AuraRenderer.material;
                    Color color = new Color(0.08f, 1.0f, 0.03f, Mathf.Lerp(AuraIdleAlpha, AuraChargeAlpha, intensity) * Mathf.Lerp(0.75f, 1.15f, pulse));
                    Color emission = new Color(0.0f, 1.0f, 0.04f, 1.0f) * Mathf.Lerp(0.8f, 5.0f, intensity);
                    SetMaterialColor(material, color, emission);
                }
            }

            if (AuraLight != null)
            {
                AuraLight.intensity = active ? baseLightIntensity * Mathf.Lerp(0.7f, ChargeAuraScale, intensity) : baseLightIntensity;
                AuraLight.range = active ? baseLightRange * Mathf.Lerp(0.9f, AuraMaxScale, intensity) : baseLightRange;
            }
        }

        private void SetLightning(float chargeProgress, bool launch)
        {
            if (Lightning == null)
            {
                return;
            }

            bool active = LightningEnabled && (launch || chargeProgress >= LightningDuringChargeProgress);
            Lightning.SetIntensity(active ? Mathf.Clamp01(chargeProgress) : 0.0f, launch);
        }

        private void RecoverIfInsideGeometry()
        {
            stuckCheckTimer -= Time.deltaTime;
            if (stuckCheckTimer > 0.0f || ContactCollider == null)
            {
                return;
            }

            stuckCheckTimer = 0.35f;
            Collider[] overlaps = Physics.OverlapSphere(RootTransform.position, GetPhysicsRadius() * 0.92f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 nudge = Vector3.zero;
            int penetrations = 0;

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider other = overlaps[i];
                if (other == null || IsSelfCollider(other))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    ContactCollider,
                    ContactCollider.transform.position,
                    ContactCollider.transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
                {
                    if (distance < 0.015f || direction.y > 0.65f)
                    {
                        continue;
                    }

                    nudge += direction.normalized * Mathf.Min(distance + SkinWidth, GetPhysicsRadius() * 0.55f);
                    penetrations++;
                }
            }

            if (penetrations <= 0 || nudge.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            MoveRootTo(RootTransform.position + nudge);
            if (Time.time >= nextInsideRecoveryLogTime)
            {
                nextInsideRecoveryLogTime = Time.time + 1.0f;
                WriteLog($"Super Ball stuck recovery triggered: inside geometry penetrationCount={penetrations}, nudge={FormatVector3(nudge)}.");
            }
        }

        private void TrackNoProgress()
        {
            if (state != SuperBallState.IdleRoam)
            {
                lastProgressPosition = RootTransform.position;
                noProgressTimer = 0.0f;
                return;
            }

            float moved = Vector3.Distance(GetFlatPosition(lastProgressPosition), GetFlatPosition(RootTransform.position));
            if (moved < 0.04f)
            {
                noProgressTimer += Time.deltaTime;
            }
            else
            {
                noProgressTimer = 0.0f;
                lastProgressPosition = RootTransform.position;
            }

            if (noProgressTimer >= NoProgressStuckSeconds)
            {
                WriteLog("Super Ball stuck recovery triggered from no horizontal progress; choosing a new roam target.");
                PickRoamTarget("no progress recovery");
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

            RootTransform.rotation = Quaternion.Slerp(RootTransform.rotation, Quaternion.LookRotation(direction.normalized, Vector3.up), Time.deltaTime * 5.0f);
        }

        private bool IsSelfCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (RootTransform != null && collider.transform.IsChildOf(RootTransform))
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
                Squash(new Vector3(1.0f + ElasticSquashAmount, 1.0f - ElasticSquashAmount, 1.0f + ElasticSquashAmount), 0.12f);
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
            baseLocalScale = VisualRoot.localScale == Vector3.zero ? Vector3.one : VisualRoot.localScale;

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

        private float GetPhysicsRadius()
        {
            return Mathf.Max(0.05f, Diameter * 0.5f * Mathf.Max(1.0f, currentScaleMultiplier));
        }

        private static Vector3 GetFlatPosition(Vector3 value)
        {
            return new Vector3(value.x, 0.0f, value.z);
        }

        private static void SetMaterialColor(Material material, Color color, Color emission)
        {
            if (material == null)
            {
                return;
            }

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
        }

        private static string GetColliderName(Collider collider)
        {
            return collider == null ? "<none>" : collider.name;
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
        }

        private void PlayIdleBounceSound()
        {
            if (Time.time < nextIdleBounceSoundTime)
            {
                return;
            }

            nextIdleBounceSoundTime = Time.time + 0.55f;
            WriteSoundHook("idle/roam soft rubber bounce thump");
        }

        private void PlayChargeHumSound()
        {
            if (Time.time < nextChargeHumSoundTime)
            {
                return;
            }

            nextChargeHumSoundTime = Time.time + 0.90f;
            WriteSoundHook("charge rising hum / pressure wobble");
        }

        private void PlayLaunchSound()
        {
            WriteSoundHook("launch snap / whip burst");
        }

        private void PlayRicochetSound()
        {
            WriteSoundHook("ricochet rubber impact");
        }

        private void PlayLightningCrackleSound()
        {
            if (Time.time < nextLightningSoundTime)
            {
                return;
            }

            nextLightningSoundTime = Time.time + 0.75f;
            WriteSoundHook("lightning crackle");
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
