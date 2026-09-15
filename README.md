# Memento Maker

Memento Maker is a Windows application designed to make creating, building and publishing mods for **Two Point Museum** easier.

It provides a guided interface around the official Two Point Museum modding workflow, including creating supported item types, preparing artwork, building mods, organising related items into families or Décor Packs, and publishing or updating items on the Steam Workshop.

> Memento Maker is an independent community tool. It is not an official Two Point Studios or SEGA product.

## Current beta

This repository is intended to provide source visibility and reproducible build information for the public beta of Memento Maker.

## What Memento Maker does

- Creates supported Two Point Museum item mods from user-supplied artwork.
- Provides artwork positioning controls such as zoom, pan and rotation.
- Supports item options and variants.
- Supports combined mod families and Décor/Wallpaper packs.
- Builds mod content using the supported Two Point Museum modding environment.
- Assists with Steam Workshop publishing and updating.
- Provides environment checks for required modding dependencies.

## Requirements

To build and use all mod-building features, the relevant official Two Point Museum modding components and Unity version are required. See [BUILDING.md](BUILDING.md).

## Source and third-party files

This repository should contain the **Memento Maker source code only**, plus documentation and build scripts/configuration that you have the right to redistribute.

It should **not** contain:
- Two Point Museum game files.
- Two Point Museum: Modding SDK files that are not redistributable.
- Unity installations or Unity-owned binaries.
- Steam credentials, API keys, passwords or tokens.
- Generated build output or installer executables.

## Security / Nexus Mods review

The source is published so that services such as Nexus Mods and users can inspect what the compiled application does.

For a reviewer-oriented overview, see [docs/NEXUS_REVIEW.md](docs/NEXUS_REVIEW.md).

## Building

See [BUILDING.md](BUILDING.md).

## License

No open-source licence is included in this starter pack. Until you deliberately choose and add a licence, normal copyright rules apply. This lets you make the source visible for inspection without automatically granting broad reuse rights.

## Credits

Developed by SeanB with assistance from ChatGPT by OpenAI.
