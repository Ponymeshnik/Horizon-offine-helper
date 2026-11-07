# Horizon-offine-helper

English | [Русский](README.ru.md)

An unofficial Windows helper that prepares and launches [Horizon](https://playhorizon.dev/) using your existing **Minecraft 26.3** files.

Select a launcher folder, an instance folder, or the version folder itself. The helper identifies the original client by its file size and SHA-1 hash, so it does not depend on a specific launcher name or JAR filename.

## Features

- Finds the original client in nested folders and shared launcher libraries.
- Prepares libraries and resources through the official Horizon Launcher.
- Downloads missing files from Mojang servers and official Horizon releases.
- Verifies downloaded file hashes.
- Detects an installed JDK 25 and creates a desktop shortcut for future launches.
- Provides cancellation and preparation details in the application window.

## Getting started

The application interface is currently in Russian. The steps below include translations of the button labels.

1. Download the Windows ZIP from this repository's **Releases** section and extract it.
2. Open `Horizon-offine-helper.exe`.
3. Click **«Выбрать…» (Browse)** and select a folder containing Minecraft 26.3.
4. Click **«Подготовить и играть» (Prepare and play)**. Initial preparation may take several minutes.

For subsequent launches, use **«Играть» (Play)** or the **Horizon 26.3** desktop shortcut.

Requires **Windows 10/11 x64** and **.NET Framework 4.8**. An internet connection is needed during initial preparation. The helper uses an existing Horizon installation when available; otherwise, it downloads the official launcher. If it cannot find a suitable JDK 25, Horizon Launcher handles Java setup.

## Compatibility

This version supports **Minecraft 26.3** and a compatible Horizon build. Client discovery and preparation have been tested with Prism Launcher and with an original client renamed and placed in an arbitrary nested folder. Horizon startup was then verified at the main menu.

Other launchers use the same hash-based discovery. The original Minecraft 26.3 JAR is required: modified clients and other game versions will not pass verification. The search is limited to nine levels of nesting and 10,000 folders. Select a launcher or instance folder rather than an entire drive.

Forge/Fabric mods from the selected instance are not automatically ported to Horizon. Supported game features depend on Horizon itself.

## Data and troubleshooting

| Purpose | Location |
| --- | --- |
| Helper settings, prepared input files, and logs | `%LOCALAPPDATA%\HorizonFolderHelper` |
| Game worlds and settings | `%APPDATA%\horizon` |
| Horizon builds and resources | `%APPDATA%\horizon-launcher` |

The helper reads input files from the selected folder. It uses hard links for immutable files where possible and copies them when the destination is on another drive. It does not edit the source launcher's files.

- **Client not found:** download and launch 26.3 in your original launcher, then select its instance folder or shared launcher folder.
- **Preparation failed:** open **«Подробности» (Details)**. The log is saved as `helper.log` in the helper's data folder.
- **A newer Horizon build is incompatible:** use a build whose `research-lock.json` specifies Minecraft 26.3.
- **Launching a prepared game fails:** run preparation again if you have moved or deleted Horizon files.

To uninstall, remove the EXE and desktop shortcut. You can also remove the helper's data folder if desired. Game worlds are stored separately in the game folder.

Remove personal paths and other private information before attaching logs to a public issue.

## Building from source

You need Windows, PowerShell, and .NET Framework 4.8. Visual Studio and third-party packages are not required.

```powershell
.\build.ps1 -Test
```

The executable is written to `bin\Horizon-offine-helper.exe`. The `-Test` option runs the built-in checks for file discovery, hashes, path handling, ZIP extraction, Windows arguments, shortcuts, and child-process cancellation. These checks use temporary test data; they do not download or launch the game.

To create a release package:

```powershell
.\package.ps1
```

The archive and its SHA-256 checksum are written to `dist`. The included GitHub Actions workflow builds the application, runs the built-in checks, and uploads a Windows package as an artifact. Actual game startup is verified separately on a machine with Minecraft and Horizon installed.

See the [publishing guide (Russian)](docs/PUBLISHING.md) and [changelog (Russian)](CHANGELOG.md).

## License and credits

The helper's code is distributed under the [MIT License](LICENSE).

Horizon, Minecraft, Java, and their resources are distributed separately under their respective terms. Their executables, game data, and decompiled code are not included in this repository or release package.

Thanks to the [Horizon team](https://github.com/horizon-rust/horizon) for the client, launcher, and preparation tools. Minecraft belongs to Mojang/Microsoft. This project is not an official product of either team.

The helper and documentation were developed with assistance from OpenAI Codex. Preparation and startup were verified on Windows; support for other launchers depends on the availability of original Minecraft 26.3 files.
