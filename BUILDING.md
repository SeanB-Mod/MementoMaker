# Building Memento Maker 0.9.9 Beta

These instructions are based on the `MM099_CANON` source used for Memento Maker 0.9.9 Beta.

## Application prerequisites

- Windows
- Microsoft .NET Framework 4.8
- The Windows .NET Framework C# compiler (`csc.exe`)

The project targets `.NET Framework 4.8` and `AnyCPU`.

## Build the Windows application

From the repository root, run:

```text
Build_EXE.bat
```

The script locates the Microsoft .NET Framework C# compiler under:

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

and falls back to the 32-bit Framework location if required.

It compiles:

```text
Properties\AssemblyInfo.cs
src\*.cs
src\Services\*.cs
```

with references to the standard .NET Framework assemblies used by the application.

Expected executable output:

```text
dist\MementoMaker.exe
```

The application can also be built from `MementoMaker.csproj`, which targets .NET Framework 4.8.

## Build and run

```text
Build_And_Run.bat
```

## Installer

The production installer is built with Inno Setup. The canonical build recommends **Inno Setup 7.1.0 x64**.

Installer source:

```text
Installer\MementoMaker.iss
```

Production command:

```text
Build_Installer.bat
```

Expected production output:

```text
Installer\Output\MementoMakerSetup_0.9.9_Beta.exe
```

`Build_Installer.bat` first runs `Build_EXE.bat`, locates Inno Setup's `ISCC.exe`, refreshes the installer splash version, then compiles the `.iss` script.

### Runtime automation assets

The public repository includes the Memento Maker-authored Unity template assets under:

```text
Automation\Assets\TPMSimpleModMaker\
```

The developer has confirmed these assets may be redistributed. As a result, the repository contains the runtime template content expected by the production build scripts.

## External runtime prerequisites used by Memento Maker

For actual mod building/publishing, Memento Maker expects the user's separately installed official environment, including:

- Two Point Museum: Modding SDK (Steam)
- Unity Hub
- Unity 2020.3.47f1
- Steam running/logged in for Steam Workshop features

Those third-party products are not included in this repository.
