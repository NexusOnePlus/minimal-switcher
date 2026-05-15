# Minimal Switcher Installer

Minimal Switcher uses Inno Setup for the Windows installer.

## Requirements

- .NET SDK matching the project target
- Inno Setup 6

## Build

```powershell
.\installer\build-installer.ps1
```

The script publishes the app to `artifacts/publish/win-x64` and writes the installer to `artifacts/installer`.

## Release Flow

1. Update `<Version>` in `minimal-switcher.csproj`.
2. Add release notes in GitHub Releases.
3. Run `.\installer\build-installer.ps1`.
4. Upload `artifacts/installer/MinimalSwitcherSetup-<version>.exe` to the GitHub Release.

## Installer Features

- Installs per-user by default unless the user chooses elevation.
- Uses the app icon from `Assets/AppIcon.ico`.
- Optional desktop shortcut.
- Optional startup entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Launches Minimal Switcher after install unless running silently.
