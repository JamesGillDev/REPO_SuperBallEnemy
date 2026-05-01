# R.E.P.O. Vanilla Enemy Spawning Research

## Scope

Source inspected:

- `D:\SteamLibrary\steamapps\common\REPO\REPO_Data\Managed\Assembly-CSharp.dll`

Method:

- Read-only metadata inspection with Mono.Cecil.
- No game DLLs were modified.
- This report intentionally avoids verbatim game code. It only summarizes type names, member names, references, and high-level behavior inferred from metadata and method references.

## 1. Relevant Classes Found

- `EnemyDirector`
  - Public `MonoBehaviour` singleton.
  - Owns vanilla enemy difficulty lists, enemy amount curves, current selection buffers, spawned enemy tracking, spawn/idle timing, despawn timing, and debug controls.

- `EnemySetup`
  - Public `ScriptableObject`.
  - Stores spawn prefab references plus selection gates such as completed-level range, rarity preset, and runs-played requirement.

- `EnemyParent`
  - Public `Photon.Pun.MonoBehaviourPunCallbacks`.
  - Runtime parent/controller for an enemy instance.
  - Owns spawn/despawn timers, enabled object reference, current spawned state, linked `Enemy`, difficulty, first spawn point, and multiplayer serialization.

- `Enemy`
  - Public `Photon.Pun.MonoBehaviourPunCallbacks`.
  - Core runtime enemy component.
  - Caches references to optional behavior modules such as vision, player distance, rigidbody, navmesh agent wrapper, chase states, spawn/despawn states, health, and jump/stun helpers.

- `EnemyNavMeshAgent`
  - Public `MonoBehaviour`.
  - Wrapper around Unity's `UnityEngine.AI.NavMeshAgent`.
  - Provides helper methods for navmesh checks, warping, destinations, path calculation, stop/disable/enable, and movement.

- `LevelGenerator`
  - Public `Photon.Pun.MonoBehaviourPunCallbacks` singleton.
  - Owns level generation, navmesh setup, level path points, enemy spawn target counts, enemy ready sync, and enemy instantiation.

- `LevelPoint`
  - Public `MonoBehaviour`.
  - Represents path/navigation points in the generated level.
  - Registers itself with `LevelGenerator.LevelPathPoints`.
  - Tracks room, truck/start-room state, connected points, and navmesh validity checks.

- `Level`
  - Public `ScriptableObject`.
  - Owns level/module data and a `HasEnemies` flag used by generation flow.

- `RoomVolume`
  - Referenced by enemy placement metadata.
  - Has a `Truck` flag used to distinguish truck room points from level points.

- `NavMeshValidator`
  - Public helper object used by navmesh setup in debug/safety paths.

- `NavMeshBox`
  - Public `MonoBehaviour`, appears editor/debug related.

- `SpawnPoint`
  - Public `MonoBehaviour`, generic spawn point with only a small public debug field in metadata.

- `TruckSafetySpawnPoint`
  - Public `MonoBehaviour` singleton for truck safety spawning.

## 2. Relevant Methods Found

- `EnemyDirector.AmountSetup()`
  - Calculates desired enemy counts by difficulty.
  - Calls `EnemyDirector.PickEnemies(...)`.
  - Sets `EnemyDirector.totalAmount`.

- `EnemyDirector.PickEnemies(List<EnemySetup>)`
  - Chooses one enemy from a difficulty list.
  - References level completion gates, runs-played gates, rarity preset chance, current-run spawned history, and current selection list.

- `EnemyDirector.GetEnemy()`
  - Returns the next selected `EnemySetup` from the director's internal selected list.
  - Also updates `RunManager.enemiesSpawned`.

- `EnemyDirector.FirstSpawnPointAdd(EnemyParent)`
  - Assigns/records the enemy's first spawn point.
  - Uses level points and excludes the truck path point.

- `EnemyDirector.Update()`
  - Handles ongoing director timing, despawn pressure, spawn idle pause, close/player room logic, and investigation point behavior.

- `LevelGenerator.Generate()`
  - Main level generation coroutine.
  - References module generation, navmesh setup, item setup, player spawn, and enemy setup.

- `LevelGenerator.NavMeshSetup()`
  - Starts navmesh setup locally or through Photon RPC.

- `LevelGenerator.NavMeshSetupRPC(...)`
  - Removes/rebuilds the `NavMeshSurface` data.
  - Calls `NavMeshValidator.SafetyCheck()` in debug builds.

- `LevelGenerator.EnemySetup()`
  - Coroutine that selects and instantiates natural level enemies.
  - Calls `EnemyDirector.AmountSetup()`, `EnemyDirector.GetEnemy()`, and `LevelGenerator.EnemySpawn(...)`.

- `LevelGenerator.EnemySpawn(EnemySetup, Vector3)`
  - Instantiates each prefab reference in an `EnemySetup`.
  - Uses Photon room-object instantiation.
  - Calls `Enemy.EnemyTeleported(...)` and `EnemyDirector.FirstSpawnPointAdd(...)` when setup state allows.

- `EnemyParent.Setup()`
  - Coroutine that initializes the runtime enemy parent.
  - Adds the enemy to `EnemyDirector.enemiesSpawned`.
  - Starts runtime logic coroutines and participates in multiplayer ready tracking.

- `EnemyParent.Logic()`
  - Coroutine that drives spawn/despawn timer decisions.
  - Calls `EnemyParent.Spawn()` and `EnemyParent.Despawn()`.

- `EnemyParent.Spawn()`
  - Sends or performs spawn RPC behavior.
  - Sets spawned timer from min/max values.

- `EnemyParent.SpawnRPC(...)`
  - Enables the enemy's active object.
  - Marks `EnemyParent.Spawned`.
  - Calls `Enemy.Spawn()`, optional `EnemyHealth.OnSpawn()`, optional jump/stun reset helpers, and spawn-state events.

- `Enemy.Spawn()`
  - Resets core frozen/stunned state.
  - Enemy-specific components provide the actual behavior states and attacks.

- `Enemy.TeleportToPoint(float, float)`
  - Chooses a level point by player-distance criteria and teleports/records the enemy position.

- `SemiFunc.EnemySpawn(Enemy)`
  - Runtime helper that uses `Enemy.TeleportToPoint(...)`, collision checks, despawn timer setup, and first-spawn-point tracking.

- `SemiFunc.LevelPointsGetAll()`
  - Returns `LevelGenerator.LevelPathPoints`.

- `SemiFunc.LevelPointGetPlayerDistance(...)`
  - Picks a random level point from a filtered set.

- `SemiFunc.LevelPointsGetPlayerDistance(...)`
  - Filters level points by player distance, start-room flag, truck room flag, player disabled state, and point room data.

- `EnemyNavMeshAgent.OnNavmesh(...)`
  - Uses `UnityEngine.AI.NavMesh.SamplePosition(...)` and a physics raycast check.

- `EnemyNavMeshAgent.Warp(...)`
  - Verifies navmesh placement and calls the wrapped Unity `NavMeshAgent.Warp(...)`.

- `EnemyNavMeshAgent.SetDestination(...)`
  - Checks wrapped agent enabled/path/on-navmesh state before setting a destination.

No exact vanilla `SpawnEnemy` method was found in `Assembly-CSharp.dll`. The closest vanilla path is `LevelGenerator.EnemySpawn(...)`. `SpawnEnemy` appears to be a REPOLib-facing API, not a vanilla method in this assembly.

`AmountSetup` was found as a method on `EnemyDirector`, not as a standalone type.

## 3. Relevant Fields And Properties Found

### `EnemyDirector`

- `public static EnemyDirector instance`
- `public List<EnemySetup> enemiesDifficulty1`
- `public List<EnemySetup> enemiesDifficulty2`
- `public List<EnemySetup> enemiesDifficulty3`
- `public AnimationCurve amountCurve1_1`
- `public AnimationCurve amountCurve1_2`
- `public AnimationCurve amountCurve2_1`
- `public AnimationCurve amountCurve2_2`
- `public AnimationCurve amountCurve3_1`
- `public AnimationCurve amountCurve3_2`
- `private int amountCurve1Value`
- `private int amountCurve2Value`
- `private int amountCurve3Value`
- `internal int totalAmount`
- `private List<EnemySetup> enemyList`
- `private List<EnemySetup> enemyListCurrent`
- `private int enemyListIndex`
- `public List<EnemyParent> enemiesSpawned`
- `internal List<LevelPoint> enemyFirstSpawnPoints`
- `public AnimationCurve spawnIdlePauseCurve`
- `internal float spawnIdlePauseTimer`
- `public AnimationCurve despawnTimeCurve_1`
- `public AnimationCurve despawnTimeCurve_2`
- Debug fields such as `debugEnemy`, `debugSpawnClose`, `debugDespawnClose`, and `debugNoSpawnIdlePause`.

### `EnemySetup`

- `public List<PrefabRef> spawnObjects`
- `public bool levelsCompletedCondition`
- `public int levelsCompletedMin`
- `public int levelsCompletedMax`
- `public RarityPreset rarityPreset`
- `public int runsPlayed`

### `LevelGenerator`

- `public static LevelGenerator Instance`
- `public PhotonView PhotonView`
- `public Level Level`
- `public GameObject EnemyParent`
- `internal int EnemiesSpawnTarget`
- `internal int EnemiesSpawned`
- `internal List<Photon.Realtime.Player> EnemyReadyPlayerList`
- `public List<LevelPoint> LevelPathPoints`
- `public LevelPoint LevelPathTruck`
- `private Unity.AI.Navigation.NavMeshSurface NavMeshSurface`
- `internal bool DebugNoEnemy`

### `LevelPoint`

- `internal bool inStartRoom`
- `public bool ModuleConnect`
- `public bool Truck`
- `public RoomVolume Room`
- `public List<LevelPoint> ConnectedPoints`
- `public List<LevelPoint> AllLevelPoints`

### `EnemyParent`

- `public string enemyName`
- `internal bool SetupDone`
- `internal bool Spawned`
- `internal Enemy Enemy`
- `public EnemyParent.Difficulty difficulty`
- `public GameObject EnableObject`
- `public float SpawnedTimeMin`
- `public float SpawnedTimeMax`
- `public float DespawnedTimeMin`
- `public float DespawnedTimeMax`
- `public float SpawnedTimer`
- `public float DespawnedTimer`
- `internal LevelPoint firstSpawnPoint`
- `internal bool firstSpawnPointUsed`
- `internal List<RoomVolume> currentRooms`

### `Enemy`

- `internal PhotonView PhotonView`
- `internal EnemyParent EnemyParent`
- `public EnemyType Type`
- `public EnemyState CurrentState`
- `internal EnemyVision Vision`
- `internal EnemyPlayerDistance PlayerDistance`
- `internal EnemyRigidbody Rigidbody`
- `internal EnemyNavMeshAgent NavMeshAgent`
- `internal EnemyStateChase StateChase`
- `internal EnemyStateSpawn StateSpawn`
- `internal EnemyStateDespawn StateDespawn`
- `internal EnemyHealth Health`
- Boolean `Has...` fields for each optional module.

### `EnemyNavMeshAgent`

- `internal UnityEngine.AI.NavMeshAgent Agent`
- `internal Vector3 AgentVelocity`
- `public bool updateRotation`
- `internal float DefaultSpeed`
- `internal float DefaultAcceleration`

### `RunManager`

- `public/int-like levelsCompleted` was referenced by selection gates.
- `List<EnemySetup> enemiesSpawned` was referenced as recent-spawn history.
- `EnemiesSpawnedRemoveStart()` and `EnemiesSpawnedRemoveEnd()` manage pending removals around enemy setup.

## 4. Enemy Selection Flow Hypothesis

The likely vanilla selection flow is:

1. During level generation, `LevelGenerator.Generate()` reaches its enemy setup stage if the selected `Level` allows enemies and debug flags do not suppress them.
2. `LevelGenerator.EnemySetup()` asks `EnemyDirector` to prepare the enemy selection.
3. `EnemyDirector.AmountSetup()` evaluates difficulty amount curves using run difficulty multipliers from `SemiFunc`.
4. `AmountSetup()` calls `PickEnemies(...)` repeatedly for difficulty 3, difficulty 2, and difficulty 1 according to the evaluated counts.
5. `PickEnemies(...)` shuffles a difficulty list, filters enemy setups by completed-level gates and runs-played gates, then scores candidates using rarity and repeat penalties.
6. The chosen setup is added to the director's selected enemy list and current-level list.
7. `AmountSetup()` stores the total selected count in `EnemyDirector.totalAmount`.
8. `LevelGenerator.EnemySetup()` loops over `totalAmount`, calls `EnemyDirector.GetEnemy()`, and passes each selected `EnemySetup` into `LevelGenerator.EnemySpawn(...)`.
9. `EnemyDirector.GetEnemy()` advances through the internal selected list and updates `RunManager.enemiesSpawned`, likely to reduce immediate repeats in future selection.

Debug path:

- If `EnemyDirector.debugEnemy` is populated, `LevelGenerator.EnemySetup()` uses those debug enemy setups instead of the normal `AmountSetup()` path.

## 5. Enemy Spawn Placement Flow Hypothesis

Initial natural placement appears to be level-point based:

1. `LevelPoint.Start()` registers each point into `LevelGenerator.LevelPathPoints`.
2. The point marked as truck-related is stored on `LevelGenerator.LevelPathTruck`.
3. `LevelPoint.NavMeshCheck()` validates points against Unity navmesh sampling after generation.
4. `LevelGenerator.NavMeshSetupRPC(...)` rebuilds the level `NavMeshSurface`.
5. `LevelGenerator.EnemySetup()` finds the truck `RoomVolume`, then finds the furthest `LevelPoint` from that truck room.
6. Each selected natural enemy is initially sent to `LevelGenerator.EnemySpawn(...)` using that furthest point position.
7. `LevelGenerator.EnemySpawn(...)` instantiates the enemy prefab references through Photon room object instantiation.
8. `EnemyParent.Setup()` completes runtime registration and starts enemy logic.
9. `EnemyParent.Logic()` controls later spawn/despawn cycling.
10. Runtime respawn/teleport behavior uses `SemiFunc.EnemySpawn(...)` and `Enemy.TeleportToPoint(...)`, which choose `LevelPoint` candidates by player-distance and room/truck/start-room filters.

Important implication for Super Ball:

- A naturally spawned enemy is not just a visible prefab at a position. It needs the `EnemyParent`, `Enemy`, optional module references, Photon identity, setup completion, and navmesh-compatible placement path to all line up.
- Vanilla movement does not appear to use raw `UnityEngine.AI.NavMeshAgent` directly from enemy-specific scripts. It uses the custom `EnemyNavMeshAgent` wrapper, which holds the Unity agent.

## 6. Where Vanilla Enemy Groups/Tables Appear To Live

The vanilla spawn tables appear to live on the scene `EnemyDirector` singleton:

- `EnemyDirector.enemiesDifficulty1`
- `EnemyDirector.enemiesDifficulty2`
- `EnemyDirector.enemiesDifficulty3`

Each entry is an `EnemySetup`.

The per-enemy prefab data appears to live inside each `EnemySetup`:

- `EnemySetup.spawnObjects`

The selection constraints live on each `EnemySetup`:

- `levelsCompletedCondition`
- `levelsCompletedMin`
- `levelsCompletedMax`
- `runsPlayed`
- `rarityPreset`

The level placement graph appears to live on `LevelGenerator` and `LevelPoint`:

- `LevelGenerator.LevelPathPoints`
- `LevelGenerator.LevelPathTruck`
- `LevelPoint.Room`
- `LevelPoint.Truck`
- `RoomVolume.Truck`

## 7. Where Super Ball Should Be Injected Later

The safest later injection point is the `EnemyDirector` difficulty lists, after the Super Ball `EnemySetup` has been registered and before `LevelGenerator.EnemySetup()` calls `EnemyDirector.AmountSetup()` for a level.

Recommended target:

- Add the Super Ball `EnemySetup` to exactly one of:
  - `EnemyDirector.instance.enemiesDifficulty1`
  - `EnemyDirector.instance.enemiesDifficulty2`
  - `EnemyDirector.instance.enemiesDifficulty3`

Recommended difficulty:

- Start by using the difficulty of the cloned base `EnemyParent`.
- Keep this idempotent: only add if the list does not already contain the setup.

Recommended timing:

- Wait until `EnemyDirector.instance` exists.
- Wait until the Super Ball prefab/setup registration has succeeded.
- Inject before natural enemy setup runs for the next generated level.
- If injection happens after `EnemyDirector.AmountSetup()` for the current level, expect it to affect a future level rather than the current selection pass.

Recommended validation:

- Log difficulty list counts before and after injection.
- Log the setup name, prefab name, cloned base enemy name, and chosen difficulty.
- Confirm the spawned runtime object has `EnemyParent`, `Enemy`, Photon identity, and `EnemyNavMeshAgent`/Unity `NavMeshAgent` where the base enemy expects them.

## 8. Risks Of Patching `EnemyDirector` Directly

- `EnemyDirector.AmountSetup()`, `PickEnemies(...)`, and `GetEnemy()` depend on private state such as `enemyList`, `enemyListCurrent`, and `enemyListIndex`.
- Patching the selection methods risks breaking repeat-prevention behavior stored in `RunManager.enemiesSpawned`.
- `LevelGenerator.EnemySetup()` is a compiler-generated coroutine state machine, so transpiler or direct method patches would be brittle across game updates.
- Multiplayer spawning uses Photon room object instantiation and ready-count synchronization. A host/client mismatch in selection or prefab registration can desync enemy counts or object identity.
- Direct patches can conflict with REPOLib or other enemy mods that also register or inject content.
- Adding duplicate entries to difficulty lists could unintentionally bias spawn chance.
- Patching vanilla methods makes it harder to keep F8 diagnostics and natural spawn behavior separate while Super Ball AI setup is still being validated.

## 9. Recommended Safe Approach For v0.2.0

Keep the current F8 test spawn path:

- It remains the fastest controlled way to verify prefab setup, Photon identity, `EnemyParent`, `Enemy`, navmesh state, rigidbody state, and AI component state.

Add optional config-driven spawn pool injection:

- Keep `EnableSpawnPoolInjection` available.
- Default it to `false`.
- When enabled, inject Super Ball into one selected `EnemyDirector` difficulty list.
- Make the injection idempotent.
- Log before/after list counts.
- Prefer list injection over patching `EnemyDirector` methods.

Do not rewrite behavior yet:

- First use the F8 diagnostics to prove why the cloned Animal-based runtime object is idle.
- Natural spawn should wait until the runtime clone has valid AI setup, navmesh wrapper state, and network identity.

Suggested v0.2.0 guardrails:

- `EnableSpawnPoolInjection = false` by default.
- Optional config for target difficulty only after the base difficulty path is confirmed.
- Only inject after registration succeeds.
- Avoid direct modification of `Assembly-CSharp.dll`.
- Avoid Harmony patches to `EnemyDirector` unless list injection proves insufficient.

## 10. Practical Takeaways For Super Ball

- The vanilla natural spawn route is `LevelGenerator.EnemySetup()` -> `EnemyDirector.AmountSetup()` -> `EnemyDirector.GetEnemy()` -> `LevelGenerator.EnemySpawn(...)`.
- The vanilla spawn table to target is `EnemyDirector.enemiesDifficulty1/2/3`.
- Placement is based on `LevelPoint` data, initially using a point far from the truck room, then later using player-distance level point helpers.
- Navmesh behavior is mediated by `EnemyNavMeshAgent`, not just a raw Unity `NavMeshAgent`.
- For natural spawning, Super Ball should behave like a normal `EnemySetup` entry rather than bypassing the vanilla selection flow.
- Direct method patches are higher risk than idempotent list injection.
