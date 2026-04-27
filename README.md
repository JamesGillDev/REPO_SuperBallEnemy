# REPO Super Ball Enemy

Experimental BepInEx/REPOLib mod for R.E.P.O. that adds a test-spawnable neon green "Super Ball from Hell" enemy.

> Status: Prototype / v0.2.1

![Super Ball from Hell concept sheet](Docs/Images/SuperBallFromHell.png)

## Features

- `F8` test spawn for host/single-player testing.
- Runtime-created eerie neon green translucent/glassy sphere.
- Concept-style evil face decals, internal crack veins, dark inner core, and chrome-like highlight decals.
- Default sphere diameter of `0.55m`.
- Custom `IdleRoam -> ChargeWarning -> ChargeLaunch -> Recovery` behavior scaffold.
- Roams between level points when available, then physically launches and ricochets.
- Charge warning ramps bounce, spin, glow, and aura from slow to fast.
- Pulsing translucent aura sphere provides a fake displaced-air/pressure effect, with face/crack glow pulsing harder during charge.
- Sound hook log points are present for idle, charge, launch, ricochet, and recovery moments.
- Inherited colliders are disabled so the active collider set is minimal and intentional.
- Inherited base enemy renderers are disabled so Animal parts should not show.
- Suspicious inherited Animal/zap/ranged attack scripts are disabled by default.
- Physical blocking sphere collider is enabled by default for v0.2 testing.
- Clones `Animal` AI if available, with a fallback to the closest chase-style enemy if not.
- Spawn pool injection is disabled by default.
- Does not replace or delete vanilla enemies.

`v0.2.x` focuses on visibility cleanup, collider setup, inherited attack suppression, size/material tuning, readable charge-up, physical launch/ricochet behavior, and safe diagnostics. Natural spawning will come later after the behavior is stable.

## Requirements

- R.E.P.O.
- BepInEx
- REPOLib
- .NET SDK capable of building `net48`

The project references local game and modding assemblies from your own R.E.P.O. install. Those DLLs are required to build locally, but they are not included in this repository.

## Build / Deploy Summary

Default local game path used by the project:

```text
D:\SteamLibrary\steamapps\common\REPO
```

Build and deploy from PowerShell:

```powershell
cd 'C:\MSSA Code-github\REPO_SuperBallEnemy'
.\BuildAndDeploy.ps1
```

Manual build:

```powershell
dotnet build 'C:\MSSA Code-github\REPO_SuperBallEnemy\Plugin\RepoSuperBallEnemy\RepoSuperBallEnemy.csproj'
```

Manual deploy target:

```text
D:\SteamLibrary\steamapps\common\REPO\BepInEx\plugins\RepoSuperBallEnemy\RepoSuperBallEnemy.dll
```

No asset bundle is required for `v0.2.1`; the enemy visual is generated at runtime.

## Testing Steps

1. Build the plugin.
2. Deploy `RepoSuperBallEnemy.dll` to `BepInEx\plugins\RepoSuperBallEnemy\`.
3. Launch R.E.P.O.
4. Start or host a single-player/hosted level.
5. Press `F8`.
6. Check `BepInEx\LogOutput.log`.

Expected log messages include:

```text
REPO Super Ball Enemy 0.2.1 loaded.
Selected base enemy ...
Registered Super Ball ...
Concept visual setup: faceEnabled=True ...
Super Ball test spawn requested ...
F8 spawn diagnostics ...
Super Ball roam target chosen ...
Super Ball charge warning started.
Super Ball charge launch started.
Super Ball ricochet ...
```

## Config

After first launch, BepInEx should generate:

```text
BepInEx\config\James.RepoSuperBallEnemy.cfg
```

Useful entries:

```text
EnableSuperBall = true
SuperBallDiameter = 0.55
SpawnTestKey = F8
EnableSpawnPoolInjection = false
MainEmission = 3.15
MainAlpha = 0.38
EnableConceptFace = true
EnableInternalCracks = true
EnableChromeHighlights = true
FaceGlowIntensity = 5.5
CrackGlowIntensity = 4.75
CrackLayerAlpha = 0.72
InnerCoreAlpha = 0.26
AuraEnabled = true
AuraAlpha = 0.24
AuraScaleMultiplier = 1.7
EnableBounceVisuals = true
SpawnDistance = 4
EnableFallbackDebugSphere = true
EnablePhysicalBlockingCollider = true
DisableInheritedBaseAttacks = true
EnableCustomSuperBallBehavior = true
ContactDamage = 5
ChargedDamage = 10
RoamSpeed = 1.25
IdleBounceAmplitude = 0.06
IdleBounceFrequency = 1.55
ChargeWarningDuration = 3
ChargeCooldownSeconds = 5
ChargeBounceAmplitudeMin = 0.04
ChargeBounceAmplitudeMax = 0.22
ChargeSpinSpeedMin = 120
ChargeSpinSpeedMax = 1320
ChargeSpeed = 9.5
MaxRicochetCount = 3
RecoveryDuration = 1.25
ChargeAuraScale = 2.25
```

Keep `EnableSpawnPoolInjection = false` until the controlled `F8` test spawn is confirmed working.

## Known Limitations

- No authored Unity prefab yet; the concept look is assembled from procedural runtime layers.
- Room-to-room roaming is heuristic and uses level points/navmesh sampling where available.
- Physical launch damage is not applied yet.
- Uses runtime primitive spheres/quads/textures for `v0.2.1`.
- Spawn pool integration is disabled by default.
- Requires local game DLL references to build.
- Player damage is not wired yet. Contact currently logs the intended damage amount.

## Unity / Asset Bundle Notes

The local game install appears to use Unity:

```text
2022.3.67f2
```

That exact editor was not installed during the initial setup, so automated Unity asset-bundle authoring was skipped. See `UnityProject\README_UNITY.md` for the planned authored-prefab workflow.

## Safety Note

This repository does not include R.E.P.O. game files or third-party DLLs. It contains source code, project files, documentation, and helper scripts only.
