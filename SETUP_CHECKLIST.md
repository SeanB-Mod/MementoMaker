# GitHub + Nexus setup checklist

## 1. Create the GitHub repository

- Sign in to GitHub.
- Create a new repository named `MementoMaker`.
- Description: `A mod creation and publishing tool for Two Point Museum.`
- Set visibility to **Public** so Nexus reviewers can access it.
- You can leave GitHub's README, .gitignore and licence options off because this starter pack supplies the first two and intentionally does not choose a licence for you.

## 2. Add this starter pack

Upload the contents of this folder to the root of the repository.

At this point the repository is **documentation only**. It is not yet sufficient as the source-code repository Nexus requested.

## 3. Add the actual current Memento Maker source

Copy/upload the current source project into the repository.

Before uploading, remove or exclude:
- Compiled EXEs and installer output.
- `bin`, `obj`, build, temp and log folders.
- Passwords, tokens, API keys and signing certificates.
- Proprietary Two Point Museum / Modding SDK / Unity / Steam files that you do not have permission to redistribute.
- Nested release archives.

## 4. Finish BUILDING.md

Open `BUILDING.md` and replace the **VERIFY FROM SOURCE** section with the exact build steps for the source you uploaded.

A Nexus reviewer should be able to start with a clean copy of the repository and understand how the executable was produced.

## 5. Finish the Nexus review notes

Open `docs/NEXUS_REVIEW.md`.

Verify the current source and document all network-related behaviour, especially Steam Workshop access, update checks, download helpers, and external links.

## 6. Verify the public repository

Open the repository in a private/incognito browser window while signed out.

Confirm that:
- The repository is visible.
- Source code is present.
- README renders correctly.
- BUILDING.md is complete.
- No secrets or proprietary third-party files are present.

## 7. Contact Nexus Mods

Use `docs/NEXUS_SUPPORT_EMAIL.txt`.

Replace the placeholders with:
- Your GitHub repository URL.
- Your Nexus Mods page URL.
- Your preferred name/sign-off.

Send it to Nexus Mods support using the contact method specified in their quarantine notice.

## 8. Keep the quarantined upload in place

Unless Nexus tells you otherwise, do not delete the quarantined file while it is awaiting review.

## 9. For future Memento Maker versions

For each public release:
- Commit the matching source changes.
- Tag the source with the same application version.
- Keep BUILDING.md accurate.
- Build the release from that tagged source where practical.
- Avoid committing generated binaries to the source repository unless there is a deliberate reason to publish them as a GitHub Release.
