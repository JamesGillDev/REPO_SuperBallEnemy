# Asset Bundles

Version 0.1.0 does not require an asset bundle.

The current plugin creates the Super Ball body at runtime by cloning a vanilla enemy prefab and replacing the visible renderers with a generated green chrome sphere. This is safer for the first working pass because the local machine does not currently have the game's matching Unity editor version installed.

Game Unity version found from `UnityPlayer.dll`:

```text
2022.3.67f2
```

Installed local Unity editors found:

```text
2023.2.8f1
6000.2.13f1
6000.3.11f1
6000.4.0a5
```

Use this folder later for REPOLib-Sdk asset bundles after installing Unity `2022.3.67f2`.
