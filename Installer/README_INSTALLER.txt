Memento Maker 0.9.9 Beta — Installer Source
===========================================

Recommended compiler: Inno Setup 7.1.0 x64.

BUILD
-----
Run `Build_Installer.bat` from the project root. The script:
1. builds the application;
2. detects the Inno Setup compiler;
3. refreshes the version badge in the large wizard image;
4. compiles `Installer\MementoMaker.iss`.

Output:
  Installer\Output\MementoMakerSetup_0.9.9_Beta.exe

ACTIVE INSTALLER BEHAVIOUR
--------------------------
- Requests administrator rights for installation.
- Checks for Microsoft .NET Framework 4.8.
- Installs Memento Maker and Start Menu shortcut; Desktop shortcut is optional.
- Preserves `%LOCALAPPDATA%\MementoMaker` on uninstall.
- Writes persistent installer/uninstaller logs under
  `%LOCALAPPDATA%\MementoMaker\Logs\Installer`.
- Does not display a changelog/release-notes page.
- Finish-page `Launch Memento Maker` uses `runasoriginaluser` so the app starts
  unelevated and normal Windows Explorer drag/drop works immediately.

FILES TO KEEP
-------------
- `MementoMaker.iss` — active installer script.
- `BETA_NOTICE.txt` — pre-install beta / requirements notice.
- `RefreshSplashVersion.ps1` — updates the large wizard image version badge.
- `Assets\WizardLarge.png` / `WizardSmall.png` — installer artwork.

The beta installer is not code-signed by this build process; SmartScreen reputation
warnings may occur until signing is introduced.
