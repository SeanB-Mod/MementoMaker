# Memento Maker

Memento Maker is an unofficial Windows mod-creation tool for **Two Point Museum**. It provides guided creation, build/install, mod-family/Décor Pack, and Steam Workshop workflows for supported item types without requiring users to work directly in Unity for normal use.

**Current source release:** 0.9.91 Beta (`MM0991_CANON`)

Created by **SeanB**  
Developed with assistance from **ChatGPT by OpenAI**.

Memento Maker is not endorsed by or affiliated with Two Point Studios, SEGA, Valve, or OpenAI.

## Why this repository is public

The application is distributed as compiled Windows software. This repository makes the application and automation source available for inspection, including for security review by Nexus Mods.

## Source layout

- `src/` — Windows application source (C# / WinForms)
- `Properties/` — assembly metadata
- `Automation/` — Memento Maker Unity editor/worker and Steam Workshop automation source
- `Config/` — application configuration
- `Theme/` — Memento Maker UI artwork/resources
- `Installer/` — Inno Setup installer source
- `MementoMaker.csproj` — .NET Framework 4.8 project
- `Build_EXE.bat` — executable build script
- `Build_Installer.bat` — installer build script

## Building

See [BUILDING.md](BUILDING.md).

## Runtime template assets

The repository includes the Memento Maker Unity template assets used by the private automation environment. The developer has confirmed that these assets are theirs to distribute.

This allows reviewers to inspect the source and the runtime template content used by the 0.9.91 Beta build.

## Security review

See [docs/NEXUS_REVIEW.md](docs/NEXUS_REVIEW.md) for a reviewer-oriented description of expected system and network interaction.

## Licence

No open-source licence is granted by this repository unless a licence file is added later. Source visibility is provided for inspection and review; normal copyright rules otherwise apply.
