# Unity Project Notes

The automated first pass does not need a Unity-authored prefab or bundle. The BepInEx plugin builds the Super Ball enemy at runtime.

To move this to a full REPOLib-Sdk asset-bundle workflow later:

1. Install Unity `2022.3.67f2`, matching the game.
2. Create/open a Unity project in this `UnityProject` folder.
3. Import REPOLib-Sdk if you have it locally or from its official distribution.
4. Add the game's managed assemblies as references only; do not copy modified game assets back into the game folder.
5. Create a prefab named `SuperBallEnemy`.
6. Add a sphere mesh scaled to `1.8` meters diameter.
7. Create a material with metallic `1.0`, smoothness near `0.96`, green base color, and green emission.
8. Add a matching `SphereCollider`.
9. Create a REPOLib EnemyContent asset named `SuperBallEnemy`.
10. Assign an `EnemySetup` named `SuperBallEnemySetup`.
11. Assign the `SuperBallEnemy` prefab as the spawn object.
12. Build the asset bundle into `..\AssetBundles`.
13. Update the BepInEx plugin to load that bundle instead of creating the runtime sphere.

Do not use the installed Unity 2023/Unity 6 editors for production bundles unless you have confirmed they are compatible with R.E.P.O.'s Unity `2022.3.67f2` runtime.
