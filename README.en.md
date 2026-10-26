<div align="center">

# EdgeShelf

**A screenshot and file shelf at the edge of your screen**

Keep screenshots and files within reach, then drag them where you need them.

Windows desktop · C# / WPF · .NET 10 · 中文 / English

[简体中文](README.md) · **English**

**Source available · Noncommercial use only** — [PolyForm Noncommercial 1.0.0](LICENSE.md)

</div>

## Preview

<p align="center">
  <img src="docs/images/edgeshelf-preview.png" alt="EdgeShelf in English: a dark edge-attached shelf with a drop area and selection controls" width="300" />
</p>
<p align="center"><sub>Actual application screenshot · English interface · Empty shelf</sub></p>

## What is EdgeShelf?

EdgeShelf is a Windows desktop utility that keeps screenshots, temporary files, and folders in a collapsible shelf at the right edge of the screen. Use it to gather images or collect files from different locations before dragging them into an application that accepts file drops.

It stays collapsed when not in use. Reveal the handle to open it, or drag files to the edge to expand it automatically. It is a temporary transfer shelf, not a cloud drive or backup tool.

## Features

| Feature | Current behavior |
| --- | --- |
| Native screenshot workflow | Capture with `Win + Shift + S`; clipboard images are added to the shelf automatically |
| Files and folders | Drop multiple items from File Explorer; only the original paths are stored |
| Box selection and multi-file drag | Select from list whitespace or the top drop-hint area, then left-drag a selected card to export the group; no Alt key required |
| Collapsible edge panel | Hover reveals the handle; clicking opens the shelf; incoming file drags expand it automatically; it retracts about 3 seconds after the pointer leaves |
| Preview and pinning | Screenshot thumbnails, double-click preview, and pinning to prevent automatic expiry |
| Screenshot retention | Keep captures for 1 hour, 24 hours, 7 days, or forever; the default is 24 hours |
| Tray settings | Show the shelf, toggle startup, choose retention, switch Chinese / English, or exit |
| Local persistence | Restore items and settings after restart, with an unavailable-file indicator |

> EdgeShelf listens for clipboard images, not just screenshots. Images copied in other applications may also be collected. Outgoing drags use Windows file-drop data; acceptance depends on the receiving application.

## Getting started

1. **Open the shelf:** launch the app, move the pointer to the right-edge trigger area, and click the revealed handle. Alternatively, use **Show shelf** in the tray menu.
2. **Capture an image:** use `Win + Shift + S`. Once the capture reaches the clipboard, it is added automatically.
3. **Collect files:** drag files or folders from File Explorer to the right edge, wait for the shelf to expand, and drop them.
4. **Select and export:** box-select from list whitespace or the **Drop files here** area, then left-drag any selected card to another app. Dragging directly on a file card starts a file drag, not a box selection.
5. **Manage items:** right-click to pin or remove an item, use **Delete selected** for a selection, or double-click a screenshot to preview it.

### Settings

Right-click the **EdgeShelf icon in the Windows notification area**, including the hidden-icons panel if necessary. Settings currently live in this tray menu; there is no separate settings window.

- **语言 / Language → 中文 / English:** choose the interface language; the choice is saved.
- **Screenshot retention:** choose 1 hour, 24 hours, 7 days, or forever. Changing this setting recalculates expiry for existing unpinned screenshots from the time of the change.
- **Start with Windows:** enable or disable launch at sign-in.
- **Show shelf / Exit:** reveal the window or quit the application. The top-right `×` only hides the shelf.

The 3-second auto-collapse delay is currently built in, not user-configurable.

## Build and run

**The repository currently provides source code only; no downloadable EXE Release has been published.** GitHub's **Download ZIP** downloads the source, not an installer.

### Requirements

- Intended for Windows 10 / 11 x64.
- **.NET 10 SDK** for building; a runtime-only installation is not enough.
- NuGet access for the initial dependency restore.

Run in Windows PowerShell:

```powershell
git clone https://github.com/dhmingzi358746-create/EdgeShelf.git
cd EdgeShelf
dotnet restore
dotnet run -c Release
```

### Publish a self-contained executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Output:

```text
bin/Release/net10.0-windows/win-x64/publish/EdgeShelf.exe
```

This package includes the .NET runtime, so the target computer does not need a separate .NET installation. Keep `IncludeNativeLibrariesForSelfExtract=true` to bundle WPF's native libraries.

## Local data and privacy

```text
%LOCALAPPDATA%/EdgeShelf/
├── cache/          Collected image cache
├── items.json      Paths, pin states, and expiry times
└── settings.json   Retention, startup, and language settings
```

- EdgeShelf itself does not upload screenshots or file contents and requires no account or cloud service. Files handed to another application are handled by that application.
- Imported files and folders stay at their original paths. Moving, renaming, or deleting the source can make a shelf entry unavailable.
- `items.json` contains local file paths. Check diagnostic files for personal information before sharing them.

> **Deletion behavior:** removing an imported file removes its shelf entry, not the original file. Removing a collected screenshot deletes its cached image. Expired, unpinned screenshots are also cleaned up automatically. Pinning prevents automatic expiry, not manual deletion. Save important images elsewhere.

## Built with

- **C# + WPF / .NET 10** for the desktop window and interaction layer.
- **[WPF UI](https://github.com/lepoco/wpfui) 4.3.0** for the dark theme and Fluent-style controls.
- **Windows clipboard notifications** to collect images as PNG files.
- **Windows file-drop data** to export selected items.
- **Local JSON storage** for entries and settings.

## Feedback and current scope

EdgeShelf is under active iteration and currently focuses on a right-edge Windows workflow. Multi-monitor setups, different DPI settings, and receiving applications need further compatibility testing. There is no macOS / Linux version, cloud sync, or separate settings window at present.

Report problems or suggest improvements through [Issues](https://github.com/dhmingzi358746-create/EdgeShelf/issues). Include your Windows version, display scaling, receiving app, reproduction steps, and a redacted screenshot where useful. Do not attach a complete personal cache or configuration containing sensitive paths.

## License and acknowledgements

**EdgeShelf is source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE.md). Only permitted noncommercial purposes are licensed; commercial use is not granted.**

- Noncommercial study, research, personal use, modification, and redistribution are permitted subject to the license terms.
- Commercial uses, including selling the project or derivatives, integrating them into commercial products, or providing paid services based on them, require separate permission from the rights holder.
- Redistribution must include the license terms or their URL, plus the required notice in [NOTICE](NOTICE).
- This is a summary: the definitions, permitted purposes, exceptions, and obligations in [LICENSE.md](LICENSE.md) govern. The software is provided as is.

Because it restricts commercial use, this is **source-available software**, not open source under the [OSI Open Source Definition](https://opensource.org/osd).

Thanks to [WPF UI](https://github.com/lepoco/wpfui) for controls and themes. Third-party components retain their own licenses, including WPF UI's MIT license. EdgeShelf's noncommercial terms do not change the licenses of those components themselves.
