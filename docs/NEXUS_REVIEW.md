# Nexus Mods review notes

## Purpose

Memento Maker is a Windows utility for creating and publishing Two Point Museum mods. It wraps parts of the official modding workflow in a simpler graphical interface.

## Why this repository exists

The downloadable Memento Maker package contains compiled Windows code. This public source repository is provided so Nexus Mods can inspect the application's behaviour and reproduce the build.

## Expected system interaction

Depending on the installed Memento Maker version and feature being used, the application may legitimately:

- Read and write Memento Maker project, settings, artwork, build and log files.
- Check for required local modding dependencies.
- Launch or interact with the locally installed Two Point Museum modding/Unity environment as part of building mods.
- Use Steam/Steam Workshop functionality when the user explicitly chooses publishing or update actions.
- Create generated mod packages and preview images.

The exact implementation of these behaviours must be verifiable in the source committed to this repository.

## Network behaviour

Before publishing this file, inspect the current source and list every feature that makes a network request or invokes a network-enabled external service.

In particular, document any:
- Steam / Steam Workshop interaction.
- Update checking.
- Web links opened by the application.
- Download/install helpers.
- Telemetry or analytics, if any.

If a category is not used, state that explicitly after verifying the source.

## Files intentionally not included

Proprietary Two Point Museum, Two Point Museum: Modding SDK, Unity, Steam, or other third-party binaries/assets should not be committed merely to make the repository self-contained. They should be obtained through their official distribution channels.

## Reproducibility

See [../BUILDING.md](../BUILDING.md) for clean-build instructions.
