# EdgeShelf

EdgeShelf is a lightweight Windows desktop shelf for screenshots and files. It stays at the edge of the screen so items can be collected and dragged into other applications without searching through folders.

## Features

- Edge-attached, collapsible WPF shelf with a system-tray menu
- Capture screenshots through the native Windows `Win + Shift + S` workflow; clipboard images are added automatically
- Screenshot thumbnails and double-click preview
- Drag files and folders from File Explorer into the shelf; drag selected items back out
- Multi-item selection, pinning, and removal
- Configurable screenshot retention: 1 hour, 24 hours, 7 days, or forever
- Optional start with Windows
- Chinese and English interface languages
- Select the interface language from the tray menu: Language → 中文 / English
- Stores app settings and item metadata under `%LOCALAPPDATA%\EdgeShelf`; screenshots are stored in its `cache` subfolder

## Requirements

- Windows 10 or Windows 11, x64
- .NET 10 SDK to build from source

## Build and run

```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

To publish a self-contained single-file executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The application uses WPF UI (MIT License) for Fluent-style controls. See the [WPF UI repository](https://github.com/lepoco/wpfui).

## Data and privacy

EdgeShelf stores file paths and settings locally. It does not upload screenshots or file contents. Files added from Explorer remain at their original locations; captured screenshots are saved locally in the EdgeShelf cache.

Removing an imported file removes its shelf entry, not the original file. Removing a captured screenshot deletes its cached image; retention expiry also removes unpinned cached screenshots.

## License

No project license has been selected yet. Until a license is added, all rights remain with the copyright holder and reuse should not be assumed.
