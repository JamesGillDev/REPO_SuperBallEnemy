# Asset Bundles

The current plugin release does not require an asset bundle.

The runtime plugin still creates the Super Ball body by cloning a vanilla enemy prefab and replacing the visible renderers with generated runtime visuals. The Unity project is now included as source for authored visual iteration, including internal cracks and the `SuperBallInternalVeins` energy-vein system, but those authored scene assets are not bundled into the shipped plugin yet.

Game Unity version found from `UnityPlayer.dll`:

```text
2022.3.67f2
```

Installed local Unity editors found:

```text
2022.3.62f3
2023.2.8f1
6000.2.13f1
6000.3.11f1
6000.4.0a5
```

Use this folder later for REPOLib-Sdk asset bundles after the authored Unity visuals are ready to be exported and runtime-loaded. Keep generated bundles, built DLLs, Unity `Library`, and local game files out of GitHub.
