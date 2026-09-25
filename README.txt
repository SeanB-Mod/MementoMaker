Memento Maker 0.9.911 Beta — Canonical Beta Baseline
====================================================

Canonical source: MM0991_CANON
Promoted from MM098_CANON2 after acceptance of the 0.9.8 canonical feature and reliability baseline.

Memento Maker is an unofficial Windows mod-creation tool for Two Point Museum.
It provides guided Create Mod, My Mods, build/install and Steam Workshop workflows
without requiring users to work directly in Unity for supported item types.

Created by SeanB
Developed with assistance from ChatGPT by OpenAI

SUPPORTED ITEM TYPES
--------------------
- Décor — Wallpaper; Wallpapers can be assigned to a Décor Pack before build and packaged/published together (BL-022 Wallpaper / Décor Pack workflow)
- Poster — Small / Standard / Tall; Standard is the default
- Mural
- Small Rug — Square / Rectangle / Circle / Octagon; Square is the default
- Large Rug — Square / Rectangle / Circle / Octagon; Square is the default
- Banner — 11 themes (internal compatibility identifier remains Single Banner)
- Double Banner — 10 SDK themes; no Digiverse Double Banner exists in the SDK
- Hanging Sign — Small / Large; independent Front / Back artwork
- Wall Sign — Small / Large; independent Front / Back artwork

BL-022 WALLPAPER / DÉCOR PACK WORKFLOW
---------------------------
- Wallpaper Icon Source now defaults to Generated, using the composed Wallpaper artwork.
- Create Mod now includes Décor Pack Options: Standalone Wallpaper, New Décor Pack, or an existing pack.
- Existing pack choices show their current Wallpaper member count.
- Build/Rebuild action text changes to Décor Pack when a pack is selected.
- Adding a Wallpaper from Create Mod rebuilds the complete selected pack atomically.
- A new pack can begin with one Wallpaper and remains selected for the follow-on project, making it easy to add the next Wallpaper immediately.
- Opening an existing pack member automatically selects its current Décor Pack.
- Moving a Wallpaper out of or between packs is explicitly confirmed before the old shared install is changed.

BL-022 WALLPAPER PREVIEW 10
---------------------------
- Wallpaper artwork is now always composed against a fixed 1024 x 1024 square guide.
- Landscape, portrait and square source images no longer change the editor/output aspect ratio.
- Fill, Fit and Stretch operate against the 1024 x 1024 wallpaper texture area.
- Pan, zoom, rotation and Reset use the fixed square texture coordinate system.
- The selected Guide Colour now outlines the Wallpaper square in the Artwork / Image editor.
- The square guide is visible even before artwork is selected.
- Built Wallpaper Albedo textures are always exported as 1024 x 1024 PNGs.

BL-022 WALLPAPER PREVIEW 1
--------------------------
- Added Decor as the left-most Create Mod item type using the supplied Decor icon.
- Added Wallpaper as the first Decor Item Option using the supplied Wallpaper icon.
- Wallpaper uses RoomVisualModConfig rather than ItemModConfig.
- Cost and Kudosh are fixed at 0; variants/families are disabled for Wallpaper.
- Wallpaper texture is assigned to a Two Point/Wall material with Metallic set to 0.
- The RoomVisualModConfig is Addressable and the icon atlas remains Addressable.
- My Mods displays Item Type = Decor and Item Options = Wallpaper.
- Multi-Wallpaper pack creation is deliberately not exposed in this first preview.

0.9.911 BETA HIGHLIGHTS
---------------------
- Added Small/Large Hanging Sign workflows with independent Front/Back artwork.
- Added Small/Large Wall Sign workflows with independent Front/Back artwork.
- Added projective Wall Sign generated icons and the accepted projective Mural generated-icon mapping.
- Standardised initial artwork scale across Poster, Hanging Sign and Rug size families.
- Retained refreshed rug meshes/guides and corrected Rectangle Rug mapping.
- Preserved Create Mod composition after a successful build for faster follow-on creation.
- Added BL-020 combined variant-family build/install and shared Steam Workshop publishing.
- Added grouped My Mods family presentation and bulk family management.
- Added family-wide Installed (old) / Published (old) tracking.
- Added family Workshop legacy-item deprecation and Hidden visibility handling.
- Added transparent family composite Workshop previews with selectable grid styles.
- Added automatic replacement of additional family Workshop gallery images on update.
- Standardised Workshop action labels to Publish to Workshop / Update Workshop.
- Made Workshop unlink consistently available for single selected mods, including family associations.
- Strengthened persistent installer technical diagnostics.

REQUIREMENTS
------------
- Windows
- Microsoft .NET Framework 4.8
- Unity 2020.3.47f1
- Official Two Point Museum ModdingProject.zip SDK
- Steam running/logged in for Workshop features

APP DATA
--------
Memento Maker stores settings, projects, jobs, the private Unity environment and support
data under:

  %LOCALAPPDATA%\MementoMaker\

BUILDING FROM SOURCE
--------------------
Build and run:
  Build_And_Run.bat

Build executable only:
  Build_EXE.bat

Build executable + installer:
  Build_Installer.bat

Recommended installer compiler: Inno Setup 7.1.0 x64.

Outputs:
  dist\MementoMaker.exe
  Installer\Output\MementoMakerSetup_0.9.91_Beta.exe

INSTALLER BEHAVIOUR
-------------------
The installer requests administrator rights for installation. The optional Finish-page
"Launch Memento Maker" action uses the original unelevated user token so normal Windows
Explorer drag-and-drop works immediately after setup.

The installer does not display release notes/changelog pages. Persistent install/uninstall
logs are stored under:

  %LOCALAPPDATA%\MementoMaker\Logs\Installer\

VERSION / FORMATS
-----------------
Application:                Memento Maker 0.9.911 Beta
Beta testing baseline:      MM0991_CANON
Private Unity automation:   9J-29
Settings format:            2
Project format:             7
Environment marker format: 2

See BASELINE.md for the baseline contract, BETA_TESTING.md / QA_MATRIX.md for validation,
and CHANGELOG_0.9.8_BETA.md for the current beta changes.
