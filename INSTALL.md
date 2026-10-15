# 🚀 Installation Guide — EmojiPick

## Requirements

| Requirement | Details |
|-------------|---------|
| **OS** | Windows 10 or Windows 11 (x64) |
| **Runtime** | .NET 8.0 **Desktop** Runtime, x64 ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)) |
| **Disk Space** | ~15 MB (application only) |
| **RAM** | ~100 MB |

Check the runtime with `dotnet --list-runtimes`: a `Microsoft.WindowsDesktop.App 8.0.x` line must be listed.

## Install

### Option 1: MSI Installer (recommended)

Download `EmojiPickerSetup.msi` from the [Releases page](https://github.com/MrLouix/emoji-picker/releases),
or build it yourself (see [Build the installer](#build-the-installer)).

1. Double-click `EmojiPickerSetup.msi`.
2. Accept the license and click **Install**, then confirm the administrator prompt.
   EmojiPick is installed for all users in `C:\Program Files\EmojiPick\`, with Start Menu and desktop shortcuts.
3. Start **EmojiPick** from the Start Menu — the installer does not launch it.
4. Optional: right-click the tray icon → **Start with Windows** (or **Settings…** → *Start with Windows*).

**Updating**: run the new `EmojiPickerSetup.msi` over the installed one, no need to uninstall first —
it replaces the previous version (the version is shown in the tray menu → **About EmojiPick**).

After the first start:
- ✅ The EmojiPick icon appears in the notification area (click it to open the picker)
- ✅ `Ctrl+Alt+E` opens the picker anywhere
- ✅ Your settings are created in `%APPDATA%\EmojiPick\config.json`

### Option 2: Standalone Executable

1. Get `EmojiPick.exe` from a release, or build it with `.\Build.ps1 -Publish` (→ `publish\EmojiPick.exe`).
2. Put it anywhere (e.g. `C:\Tools\EmojiPick\`) and double-click it.

> ⚠️ No Start Menu shortcut is created. Use **Start with Windows** in the tray menu for auto-start
> (it points to the exe's current location — enable it again if you move the file).

## Build the installer

Requires Windows 10/11, the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and Git.
WiX does not need to be installed: the WiX v4 SDK is restored from NuGet.

```powershell
git clone https://github.com/MrLouix/emoji-picker.git
cd emoji-picker
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass   # allow the unsigned script for this session
.\Build.ps1 -Installer
```

`-Installer` builds the app, publishes the single-file `publish\EmojiPick.exe`, then builds the MSI
and prints its path (under `EmojiPick.Installer\bin\`). Details are in `build.log`.
Each build is numbered `major.minor.N` (`N` + 1 per build) and recorded in `build-history.log`.

---

## Optional: AI Suggestions (Ollama)

EmojiPick works fully offline with its built-in keyword search. To let a local model re-rank suggestions:

1. Install [Ollama](https://ollama.com) and pull a model: `ollama pull mistral`
2. Keep Ollama running (it listens on `http://localhost:11434`)
3. In **Settings…** → *AI suggestions*, keep the option enabled and set the endpoint/model if needed

If Ollama is not running, EmojiPick simply skips it (and retries after 2 minutes).

> Embedded llama.cpp models are planned but not implemented yet.

---

## Configuration

Use **Settings…** in the tray menu (or ⚙ in the picker): changes apply immediately.
Advanced users can edit the files directly (restart EmojiPick afterwards):

```
%APPDATA%\EmojiPick\
├── config.json          ← Settings (unreadable files are backed up to config.json.bak)
├── recent.json          ← Recently used emoji
├── logs\                ← Log files (rotated daily, 7 days kept)
│   └── EmojiPick-20260501.log
├── cache\
└── models\              ← Reserved for future embedded models
```

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `hotkey.modifiers` / `hotkey.key` | `["Ctrl", "Alt"]` / `"E"` | Global shortcut |
| `language` | `"auto"` | `"en"`, `"fr"` or `"auto"` (Windows language) |
| `ui.theme` | `"system"` | `"dark"`, `"light"` or `"system"` |
| `ui.colorEmoji` | `true` | Color emoji (false = monochrome) |
| `ui.skinTone` | `0` | 0 = default, 1..5 = light → dark |
| `ui.gridColumns` / `ui.gridRows` | `6` / `3` | Grid size (more results scroll) |
| `ui.positionMode` | `"caret"` | `"caret"`, `"cursor"` or `"center"` |
| `behavior.injectMode` | `"paste"` | `"paste"`, `"type"` or `"clipboard"` |
| `behavior.restoreClipboard` | `true` | Restore the clipboard after pasting |
| `llm.enabled` | `true` | Ollama re-ranking |
| `logging.level` | `"info"` | `"debug"` for troubleshooting |

---

## Uninstall

### Via Windows Settings

1. Turn off **Start with Windows** in the tray menu first (the MSI does not remove this entry).
2. Quit EmojiPick from the tray icon.
3. **Settings** → **Apps** → **Installed apps** → **EmojiPick** → **Uninstall**.

This removes the application files and shortcuts. It keeps your settings, recent emoji and logs
in `%APPDATA%\EmojiPick\`.

### Complete Removal (optional)

```powershell
Remove-Item -Recurse -Force "$env:APPDATA\EmojiPick"
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "EmojiPick" -ErrorAction SilentlyContinue
```

---

## Troubleshooting

### EmojiPick doesn't start

- Check that the .NET 8 **Desktop** Runtime (x64) is installed: `dotnet --list-runtimes`
- "EmojiPick is already running": quit the other instance from its tray icon
- Check the logs: tray menu → **Open logs folder**

### Hotkey doesn't work

- Another application may already use `Ctrl+Alt+E` — EmojiPick shows a notification in that case
- Pick another shortcut in **Settings…** (it is re-registered immediately)

### AI suggestions never appear

- Verify that `http://localhost:11434/api/tags` answers in a browser
- Check the model name in **Settings…** (`ollama list`)

### Emoji not inserted in some apps

- Apps running as administrator block simulated input from normal apps (Windows UIPI)
- Try **Settings…** → *Selected emoji* → *Type it*, or use `Shift+Enter` to copy and paste manually
