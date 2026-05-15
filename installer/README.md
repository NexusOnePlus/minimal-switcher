# Minimal Switcher Installer

Minimal Switcher uses Inno Setup for the Windows installer.

## Requirements

- .NET SDK matching the project target
- Inno Setup 6

Local builds require Inno Setup 6 because `ISCC.exe` compiles the installer script. CI installs it automatically on the Windows runner.

## Build

```powershell
.\installer\build-installer.ps1
```

Override the package version without editing the project file:

```powershell
.\installer\build-installer.ps1 -Version 0.2.0
```

The script publishes the app to `artifacts/publish/win-x64` and writes the installer to `artifacts/installer`.

## Release Flow

1. Update `<Version>` in `minimal-switcher.csproj`, or tag with `v<version>`.
2. Push a tag such as `v0.2.0`.
3. GitHub Actions builds `MinimalSwitcherSetup-0.2.0.exe`.
4. The workflow uploads the installer as an artifact and attaches it to the GitHub Release for tag builds.

## Installer Features

- Installs per-user by default unless the user chooses elevation.
- Uses the app icon from `Assets/AppIcon.ico`.
- Optional desktop shortcut.
- Optional startup entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Launches Minimal Switcher after install unless running silently.
