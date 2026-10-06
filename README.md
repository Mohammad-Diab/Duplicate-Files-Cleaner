# Duplicate Files Cleaner

A Windows app that finds duplicate files and removes the extra copies. Written in 2017 with WPF and .NET Framework.

Files are compared by their content, so copies with different names are found too.

## Features

- Search one folder or several folders at once
- Three scan modes: full (compares whole files), smart (the default, faster) and fast (size only)
- Pick the copies to delete yourself, or let the app keep one copy based on name, path or date
- Deleted files go to the Recycle Bin
- Compare two files to see if they are identical
- Skip hidden files, system files and folders you choose
- Save a log of each scan

## Requirements

Windows with .NET Framework 4.0 or later. Windows 10 and 11 already have it.

## Usage

Download the `.7z` file from [Releases](https://github.com/Mohammad-Diab/Duplicate-Files-Cleaner/releases), extract it, and run `Duplicate Files Cleaner.exe`. There is no installer.

## Versions

| Version | Changes |
|---|---|
| v2017.1.1 | The Delete button works: it asks first, then moves the files to the Recycle Bin. |
| v2017.0.2 | Black bar at the top of the main window. Targets .NET 4.0. |
| v2017.0.1 | First version. It finds duplicates, but Delete is disabled. |

## Building

Open `Duplicate Files Cleaner.sln` in Visual Studio and build.

The project targets .NET Framework 4.0, and recent Visual Studio versions don't include the 4.0 reference assemblies. If the build fails with `MSB3644`, download [Microsoft.NETFramework.ReferenceAssemblies.net40](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net40), unzip it, and build with:

```
MSBuild "Duplicate Files Cleaner.sln" /p:Configuration=Release /p:TargetFrameworkRootPath=<unzipped folder>\build
```

## Credits

- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf) for the folder picker (BSD license, see `lib`)
- Icons from [Iconfinder](https://www.iconfinder.com/)

## License

[Apache 2.0](LICENSE)
