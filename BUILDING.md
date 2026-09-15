# Building Memento Maker

This file should describe how a reviewer can reproduce Memento Maker from source.

## Important

The exact application build commands depend on the current Memento Maker source project. Before publishing this repository, replace the marked **VERIFY FROM SOURCE** section below using the actual current source tree. Do not guess these commands.

## External prerequisites

Memento Maker's Two Point Museum mod-building workflow uses:

1. **Two Point Museum: Modding SDK** — installed through Steam.
2. **Unity Hub**.
3. **Unity 2020.3.47f1** — the Unity version specified by the Two Point Museum modding setup documentation.
4. **Two Point Museum** / Steam where required for testing and Workshop operations.

These external products should be installed separately. Do not copy their proprietary files into this repository.

## Application build

**VERIFY FROM SOURCE BEFORE PUBLISHING**

Add the exact steps required to compile the Memento Maker Windows application here, including:

- Required .NET / SDK / compiler version.
- Solution or project file to open.
- Build configuration (for example Release / x64).
- Exact command or Visual Studio steps.
- Expected output path.
- Any source-controlled resources required at build time.

Example layout only — do not publish this example as if it were the real command:

```text
1. Install <exact SDK/toolchain>.
2. Open <actual solution/project>.
3. Select <actual configuration>.
4. Build.
5. Output is written to <actual output path>.
```

## Installer build

If the installer source is included, document the exact Inno Setup script and version used here.

Known project convention: the Memento Maker installer is built with Inno Setup. Confirm the exact current script and settings from the source before publishing.

## Two Point Museum / Unity worker

If Memento Maker contains a private Unity worker or generated Unity project content, document:

- Which files are authored by Memento Maker and are safe to publish.
- Which files are generated locally from the user's installed SDK/environment.
- Which external files are intentionally excluded from GitHub.
- How the worker/project is reconstructed after a clean checkout.

This distinction is important for both reproducibility and redistribution rights.

## Clean-build verification

Before sending the repository to Nexus Mods:

1. Download/clone the repository into a new folder.
2. Follow this document without relying on files from your normal development folder.
3. Confirm the application compiles.
4. Confirm any excluded external dependencies are obtained from their official installers.
5. Confirm the resulting executable corresponds to the version uploaded to Nexus Mods.
