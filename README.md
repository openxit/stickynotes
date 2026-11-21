# StickyNotes

Lightweight, native Windows desktop sticky notes that bind directly to your apps.

Focus any application (like **Visual Studio Code**, **Chrome**, or **Terminal**), press `Ctrl + Alt + N`, and a lined paper sticky note appears for that app. It stays wherever you drag it, minimizes when the app minimizes, hides when the app closes, and reappears automatically when you open the app again.

## Features

- **App-Bound Lifecycle**: Each sticky note is tied to an application (e.g. `Code.exe`).
  - Appears when the bound app is open.
  - Automatically hides when the app is minimized or closed.
  - Restores in its saved position with all your notes when the app is reopened.
  - Automatically yields z-order when you switch to another app so it never blocks unrelated windows.
- **Realistic Ruled Paper**: Warm paper tones (*Butter*, *Ivory*, *Sage*, *Sky*, *Blush*) with crisp 28px ruled lines, a vertical margin line, an adhesive drag strip, and soft paper shadow.
- **Todos With or Without Checkboxes**:
  - Click checkboxes to check items off with strikethrough.
  - Mix plain note lines and checkbox todos freely.
  - Type `- [ ] ` or `[] ` at the start of a line to turn it into a checkbox todo, or press `Ctrl + T` / `Ctrl + Space` to toggle any line.
  - Press `Enter` to add a new line, `Backspace` on an empty line to remove it, and `Up`/`Down` to navigate.
- **Global Hotkey & System Tray**: Default `Ctrl + Alt + N` (customizable in Settings), optional launch on Windows startup, and automatic saving to `%APPDATA%\StickyNotes\notes.json`.
- **Zero Dependencies**: Native ~54 KB `.exe` built on Windows .NET Framework 4.x WPF.

## Download & Install

Download `StickyNotes.exe` from the [Releases](https://github.com/openxit/stickynotes/releases) page and run it.

- On first launch, `StickyNotes.exe` automatically copies itself to `%LOCALAPPDATA%\StickyNotes\StickyNotes.exe` and registers itself for Windows startup, so you can safely delete the downloaded file from your `Downloads` folder.
- All notes and settings are stored in `%APPDATA%\StickyNotes\notes.json`.

## Uninstall

Download and run `UninstallStickyNotes.bat` from the [Releases](https://github.com/openxit/stickynotes/releases) page to stop the app, remove Windows autostart, and delete both `StickyNotes.exe` and all saved notes.

## Build from Source

Requires Windows 10/11 (uses the built-in .NET Framework `csc.exe` compiler):

```powershell
.\build.ps1
```

The compiled executable is output to `dist\StickyNotes.exe`.

## License

MIT
