# Nexus Mods security review notes

## Application

Memento Maker 0.9.911 Beta is an unofficial Windows utility for creating, building, installing and publishing Two Point Museum mods.

The main application is C# / Windows Forms targeting .NET Framework 4.8. The repository also contains Memento Maker's Unity editor/worker C# automation and Steam Workshop automation source.

## Expected local system access

The source shows legitimate local operations including:

- Reading/writing Memento Maker settings, project records, jobs, generated images, build output and support/log data.
- Reading Windows Registry uninstall/Steam locations to discover Unity Hub and Steam installations.
- Starting Windows Explorer for user-requested output-folder actions.
- Starting Unity processes as part of the mod build/worker workflow.
- Opening prerequisite/help URLs or Steam protocol links when the user chooses those actions.

Memento Maker stores its user-specific working data beneath:

```text
%LOCALAPPDATA%\MementoMaker\
```

## Network / external-service behaviour

The reviewed 0.9.91 source contains these intentional network-enabled interactions:

1. **Steam / Steam Workshop** — the Unity-side Workshop source uses Steamworks `SteamUGC` operations to query the user's Workshop items and, when requested, create/update Workshop items, set title/description/tags/visibility/content/previews/metadata, add/remove dependencies, and submit updates.
2. **Steam Community links** — the application can open a Workshop item page and the Steam Workshop legal agreement in the user's default handler/browser.
3. **Prerequisite links** — the application can open the Unity Hub download page and the Unity 2020.3.47f1 release page, and can invoke a `steam://` URI for the Two Point Museum: Modding SDK.
4. **No separate telemetry/analytics implementation was identified by the source scan used to prepare these notes.**

The application does not need Nexus Mods credentials and no Nexus authentication implementation was identified in the reviewed source.

## Installer

Installer source is in:

```text
Installer/MementoMaker.iss
```

The installer requests elevation for installation. The application itself is a normal Windows desktop application. Installer diagnostic logs are designed to be retained under Memento Maker's local application data area.

## Build

See `BUILDING.md`.

## Runtime template assets

The public source repository includes `Automation/Assets/TPMSimpleModMaker/`. The developer has confirmed that the included Memento Maker Unity template assets are theirs to distribute. This gives reviewers access to both the application/automation source and the runtime template content used by the build workflow.
