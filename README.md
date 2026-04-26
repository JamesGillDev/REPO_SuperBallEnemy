# REPO Super Ball Enemy

Experimental BepInEx/REPOLib mod for R.E.P.O. that adds a test-spawnable green glowing chrome sphere enemy.

> Status: Prototype / v0.1.0

## Features

- `F8` test spawn for host/single-player testing.
- Runtime-created green glowing chrome sphere.
- Default sphere diameter of `1.8m`.
- Visual bobbing/rolling for an early bounce feel.
- Clones `Animal` AI if available, with a fallback to the closest chase-style enemy if not.
- Spawn pool injection is disabled by default.
- Does not replace or delete vanilla enemies.

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

No asset bundle is required for `v0.1.0`; the enemy visual is generated at runtime.

## Testing Steps

1. Build the plugin.
2. Deploy `RepoSuperBallEnemy.dll` to `BepInEx\plugins\RepoSuperBallEnemy\`.
3. Launch R.E.P.O.
4. Start or host a single-player/hosted level.
5. Press `F8`.
6. Check `BepInEx\LogOutput.log`.

Expected log messages include:

```text
REPO Super Ball Enemy 0.1.0 loaded.
Selected base enemy ...
Registered Super Ball ...
Super Ball test spawn requested ...
```

## Config

After first launch, BepInEx should generate:

```text
BepInEx\config\James.RepoSuperBallEnemy.cfg
```

Useful entries:

```text
EnableSuperBall = true
SuperBallDiameter = 1.8
SpawnTestKey = F8
EnableSpawnPoolInjection = false
EmissionIntensity = 2.8
EnableBounceVisuals = true
SpawnDistance = 4
```

Keep `EnableSpawnPoolInjection = false` until the controlled `F8` test spawn is confirmed working.

## Known Limitations

- No authored Unity prefab yet.
- No true physics bounce locomotion yet.
- Uses a runtime primitive sphere for `v0.1.0`.
- Spawn pool integration is disabled by default.
- Requires local game DLL references to build.
- Hostile behavior is borrowed from the selected vanilla base enemy.

## Unity / Asset Bundle Notes

The local game install appears to use Unity:

```text
2022.3.67f2
```

That exact editor was not installed during the initial setup, so automated Unity asset-bundle authoring was skipped. See `UnityProject\README_UNITY.md` for the planned authored-prefab workflow.

## Safety Note

This repository does not include R.E.P.O. game files or third-party DLLs. It contains source code, project files, documentation, and helper scripts only.
