# What to upload to GitHub

This folder is the prepared public-source tree for Memento Maker 0.9.9 Beta.

## Recommended method

Because you already created the GitHub repository, use GitHub Desktop:

1. Install GitHub Desktop and sign in.
2. On your GitHub MementoMaker page, click **Code** → **Open with GitHub Desktop**.
3. Choose a local folder and click **Clone**.
4. Open the cloned `MementoMaker` folder in Windows Explorer.
5. Copy **all contents of this GitHub-ready folder** into the cloned folder.
6. If Windows asks about replacing README.md, BUILDING.md or .gitignore, choose **Replace**. These are the final source-aware versions.
7. Return to GitHub Desktop. It should show the added/changed files.
8. Review the changed-file list. You should NOT see `dist`, `bin`, `obj`, installer EXEs or ZIPs. You SHOULD see files under `Automation/Assets/TPMSimpleModMaker`.
9. Commit message: `Add Memento Maker 0.9.9 Beta source`
10. Click **Commit to main**.
11. Click **Push origin**.
12. Open the repository on GitHub and verify `src`, `Automation`, `Installer`, `README.md`, and `BUILDING.md` are visible.

Do not upload the canonical development ZIP or the Nexus installer ZIP to the source tree.
