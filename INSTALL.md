# 🚀 Installation Guide — EmojiPick

## Quick Install (End User)

### Option 1: MSI Installer (Recommended)

1. Download `EmojiPick-1.0.0.msi` from the [Releases page](https://github.com/<user>/emojipick/releases)
2. Double-click the `.msi` file
3. Follow the installation wizard:
   - Accept the MIT License
   - Choose installation location (default: `%LOCALAPPDATA%\EmojiPick\`)
   - Check desired shortcuts (Start Menu, Desktop)
   - Enable auto-start on Windows login (optional)
4. Click **Install** → wait for completion → **Launch**

After installation:
- ✅ EmojiPick icon appears in the system tray
- ✅ Press `Ctrl+Alt+E` anywhere to open the emoji overlay
- ✅ User config created at `%APPDATA%\EmojiPick\config.json`

### Option 2: Standalone Executable

1. Download `EmojiPick-1.0.0.exe` from [Releases](https://github.com/<user>/emojipick/releases)
2. Place the `.exe` anywhere (Desktop, Documents, etc.)
3. Double-click to run
4. First run creates `%APPDATA%\EmojiPick\` with default config

> ⚠️ This method does not create Start Menu shortcuts or auto-start entries.

---

## Requirements

| Requirement | Details |
|-------------|---------|
| **OS** | Windows 10 or Windows 11 |
| **Runtime** | .NET 7.0 Desktop Runtime ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)) |
| **Disk Space** | ~10 MB (application only) |
| **RAM** | ~100 MB base |

### Optional: LLM Support

For AI-powered emoji suggestions (not required — app works without it):

| Provider | Setup |
|----------|-------|
| **Ollama** | Install from [ollama.com](https://ollama.com), then `ollama pull mistral` |
| **llama.cpp** | Built-in — auto-downloads model on first LLM use (~5 GB) |

> 💡 Even without any LLM, EmojiPick works perfectly with fuzzy matching alone.

---

## Configuration

### Location

```
%APPDATA%\EmojiPick\
├── config.json          ← Main configuration file
├── logs\                ← Log files (auto-rotated, 7 days)
│   └── EmojiPick-2025-05-08.log
└── cache\               ← Internal cache (LLM results, 5 min TTL)
```

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `hotkey.modifiers` | `["Ctrl", "Alt"]` | Hotkey modifier keys |
| `hotkey.key` | `"E"` | Hotkey letter |
| `llm.enabled` | `true` | Enable LLM integration |
| `llm.provider` | `"auto"` | Preferred provider |
| `llm.fallback_chain` | `["ollama", "llamacpp", "fuzzy"]` | Fallback order |
| `behavior.injectMode` | `"paste"` | How emoji is injected |
| `ui.theme` | `"dark"` | UI theme |
| `ui.gridColumns` | `4` | Emoji grid columns |

### Changing the Hotkey

Edit `%APPDATA%\EmojiPick\config.json`:

```json
{
  "hotkey": {
    "modifiers": ["Ctrl", "Shift"],
    "key": "X"
  }
}
```

Restart EmojiPick after changes.

---

## Uninstall

### Via Windows Settings

1. **Settings** → **Apps** → **Apps & features**
2. Find **EmojiPick** → **Uninstall**

This removes:
- ✅ Application files
- ✅ Shortcuts (Start Menu, Desktop)
- ✅ Auto-start registry entry

This preserves:
- 🔒 User config (`%APPDATA%\EmojiPick\config.json`)
- 🔒 Downloaded LLM models (`%APPDATA%\EmojiPick\models\`)
- 🔒 Log files

### Complete Removal (Optional)

To delete everything including user data:

```powershell
Remove-Item -Recurse "$env:APPDATA\EmojiPick" -Force
Remove-Item -Recurse "$env:LOCALAPPDATA\EmojiPick" -Force
```

---

## Troubleshooting

### EmojiPick doesn't start

- Verify .NET 7.0 Runtime is installed: `dotnet --list-runtimes`
- Check logs: `%APPDATA%\EmojiPick\logs\EmojiPick-{date}.log`

### Hotkey doesn't work

- Check if another app uses `Ctrl+Alt+E`
- Change the hotkey in `config.json`

### LLM not responding

- Ollama: verify `http://localhost:11434/api/tags` returns 200
- llama.cpp: check model file exists at configured path
- App automatically falls back to fuzzy matching if LLM unavailable

### Emoji not pasting in some apps

- Some elevated apps (admin mode) block input simulation
- Try running EmojiPick as Administrator (not recommended for security)
