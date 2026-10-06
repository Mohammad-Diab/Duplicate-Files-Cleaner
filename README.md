# Duplicate Files Cleaner

A small Windows desktop app (WPF, .NET Framework) that finds duplicate files in one or more folders and lets you remove the extra copies. It compares file contents, not just names, so renamed copies are found too. It also has a tool that checks whether two files are identical.

I wrote it in 2017. It was touched up in 2019 and 2024, and cleaned up for GitHub in 2026.

## Features

- **Find duplicates** in a folder, or in several folders at once (**Multi Folders**). Files are grouped by size first, then compared by content.
- **Three scan modes** (Settings):
  - **Full scan**: compares every file in full. It's slow but 100% sure.
  - **Smart scan** (default): reads files in parts and stops as soon as they differ. It's faster and about 99.9% sure.
  - **Fast method**: compares sizes only. It takes a few moments but it's only a rough guess.
- **Results list** with groups, file sizes and total size. You can sort by duplicate count, single file size, total size or type.
- **Select helpers**: Select all, Select none, and Select first file. **Copy to keep** rules pick the copy you keep by the longest or shortest name or path, or by the most recent or oldest created or modified date.
- **Delete selected** (v2017.1.1 and later): asks for confirmation, then moves the files to the **Recycle Bin**.
- **IS SIMILAR TO**: pick or drag and drop two files to check if they are identical.
- **Exclusions**: skip hidden files, system files, the `page_files` folders that browsers save next to `page.html`, and a list of folders (Windows, Program Files and so on by default).
- **Operation log**: every scan writes a detailed log that you can save as a `.log` file.
- Adjustable read buffer size, plus **About**, **What's new** and **Credits** windows.

## Requirements

- Windows 7 or later.
- .NET Framework **4.0 or later**. Windows 10 and 11 already include .NET Framework 4.8, so there is nothing to install there.
  - v2017.0.1 needs 4.6 or later, and v2017.0.3 needs 4.8.

## Install and use

1. Download the latest `Duplicate-Files-Cleaner-<version>.7z` from [Releases](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases).
2. Extract it anywhere (with [7-Zip](https://www.7-zip.org/) or Windows 11 Explorer) and run `Duplicate Files Cleaner.exe`. It's portable, so there's no installer.
3. Choose a folder (or **Multi Folders**) and start the search.
4. Check the copies you want to remove (or use **Select first file** and the **Copy to keep** rules in Settings), then press **Delete**. The files go to the Recycle Bin.

Keep `Ookii.Dialogs.Wpf.dll` next to the exe, because the folder picker needs it.

## Versions

The version scheme is `v2017.x.y`: 2017 is the year the app was written.

| Version | Date | .NET | What changed |
|---|---|---|---|
| `main` (not released yet) | 2026-10 | 4.0 | Name without the apostrophe everywhere in the UI, typo fixes in the UI and log, version number and copyright in the exe and About, and `Ookii.Dialogs.Wpf` kept in `lib\` so a clean clone builds. |
| [v2017.1.1](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases/tag/v2017.1.1) | 2024-09 | 4.0 | **Delete works**: it asks for confirmation, moves files to the Recycle Bin with progress, then updates the list. Back to .NET 4.0. |
| [v2017.0.3](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases/tag/v2017.0.3) | 2024-09 | 4.8 | Targets .NET Framework 4.8. |
| [v2017.0.2](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases/tag/v2017.0.2) | 2019-12 | 4.0 | Black bar across the top of the main window, and targets .NET 4.0. |
| [v2017.0.1](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases/tag/v2017.0.1) | 2019-12 | 4.6 | First version (code from November 2017). It finds duplicates, but the Delete button is disabled. |

## Build from source

The project is a classic (non-SDK) WPF `.csproj`. Open `Duplicate Files Cleaner.sln` in Visual Studio and build. Or use MSBuild from a PowerShell prompt:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  "Duplicate Files Cleaner.sln" /t:Build /p:Configuration=Release
```

The output goes to `Duplicate Files Cleaner\bin\Release\`.

**.NET Framework 4.0 reference assemblies:** the project targets 4.0, and current Visual Studio versions don't install the 4.0 targeting pack. If the build fails with `MSB3644`, use the reference assemblies from NuGet without installing anything:

```powershell
# download and unzip the package once
Invoke-WebRequest https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net40/1.0.3 -OutFile net40.zip
Expand-Archive net40.zip net40

& "...\MSBuild.exe" "Duplicate Files Cleaner.sln" /t:Build /p:Configuration=Release `
  "/p:TargetFrameworkRootPath=$PWD\net40\build"
```

## Credits

- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf) 1.0 by Sven Groot provides the folder browser dialog. It's under the BSD 3-Clause license, see [`lib/Ookii.Dialogs.Wpf.LICENSE.txt`](lib/Ookii.Dialogs.Wpf.LICENSE.txt).
- The icons come from [Iconfinder](https://www.iconfinder.com/).

## License

Copyright 2017-2026 Mohammad Diab. Licensed under the [Apache License 2.0](LICENSE).
