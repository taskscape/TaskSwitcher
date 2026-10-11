# TaskSwitcher Installer

This directory contains the Inno Setup installer script and build tools for creating the TaskSwitcher installation package.

## Prerequisites

- .NET 10 SDK
- PowerShell 5.1 or later
- Inno Setup compiler (included in `InnoSetup` directory)

## Building the Installer

### Option 1: Using PowerShell Script (Recommended)

Run the build script from PowerShell:

```powershell
.\Build-Installer.ps1
```

To build a specific version locally:

```powershell
.\Build-Installer.ps1 -Version 1.2.3
```

This script will:
1. Clean previous builds
2. Build and publish the TaskSwitcher application (Release, win-x64, self-contained)
3. Verify build output
4. Compile the Inno Setup installer
5. Display the location of the generated installer

### Option 2: Manual Build

1. Build and publish the application:
```powershell
dotnet publish ..\TaskSwitcher\TaskSwitcher.csproj --configuration Release --runtime win-x64 --self-contained true
```

2. Compile the installer:
```powershell
.\InnoSetup\ISCC.exe Installer.iss
```

## Installer Features

The installer includes the following features:

- **Self-contained deployment**: No .NET runtime installation required
- **64-bit application**: Optimized for modern Windows systems
- **Multi-language support**: English and Polish
- **Desktop shortcut**: Optional (unchecked by default)
- **Start with Windows**: Optional task to add TaskSwitcher to Windows startup
- **Auto-launch**: Option to launch the application after installation
- **Clean uninstall**: Removes all files and shortcuts

## Installer Configuration

The installer is configured in `Installer.iss` with the following key settings:

- **Application ID**: `{A5AF4C34-70A7-4D3B-BA18-E49C0AEEA5E6}`
- **Mutex**: `DBDE24E4-91F6-11DF-B495-C536DFD72085-TaskSwitcher`
- **Default installation directory**: `C:\Program Files\TaskSwitcher`
- **Requires admin privileges**: Yes
- **Architecture**: 64-bit only

## Output

The compiled installer will be created in the `Output` directory with the filename:
```
TaskSwitcher-Setup-{version}.exe
```

## GitHub Releases

The `Publish installer release` GitHub Actions workflow runs the unit tests, builds a
versioned, self-contained win-x64 installer and publishes it as
`TaskSwitcher-Setup-{version}.exe` on a GitHub release.

### Automatic releases

Every push to `master` publishes a release. Its version is the newest
`vMAJOR.MINOR.PATCH` tag with the patch number incremented (`v1.3.1` is followed by
`v1.3.2`), and the workflow creates the matching tag on the commit it built. A commit
that already carries a release tag is not released again.

The workflow can also be started manually from the Actions tab to release the current
`master` commit, for example after a run was cancelled.

### Choosing the version

To start a new major or minor version, push a `vMAJOR.MINOR.PATCH` tag whose commit is
reachable from `master`. For example:

```powershell
git tag v1.4.0
git push origin v1.4.0
```

The workflow publishes exactly that version, and later pushes to `master` continue
from it (`v1.4.1`, `v1.4.2`, ...). A tag that is not on `master`, or does not use the
required version format, is rejected.

## Customization

To customize the installer, edit the following files:

- `Installer.iss`: Main installer script
- `..\TaskSwitcher\icon.ico`: Application icon
- `..\LICENSE.txt`: License text shown during installation

## Troubleshooting

### Build fails with "TaskSwitcher.exe not found"
Ensure the project builds successfully before running the installer build:
```powershell
dotnet build ..\TaskSwitcher\TaskSwitcher.csproj --configuration Release
```

### Inno Setup compiler not found
Verify that the `InnoSetup` directory contains `ISCC.exe` and related files.

### Version mismatch
Update the version number in `Installer.iss`:
```
#define MyAppVersion "1.0.0"
```

## License

This installer script is part of the TaskSwitcher project.
