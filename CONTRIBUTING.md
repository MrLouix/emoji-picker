# Contributing to EmojiPick

Thank you for your interest in contributing! This document covers the essentials.

## Development Setup

### Prerequisites

- **Windows 10/11** (WPF is Windows-only)
- **.NET 8.0 SDK** ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))
- **Visual Studio 2022** or **VS Code + C# Dev Kit**
- No WiX install needed: the WiX v4 SDK is restored from NuGet when building the MSI

### Getting Started

```powershell
# 1. Clone the repository
git clone https://github.com/MrLouix/emoji-picker.git
cd emoji-picker

# 2. Build app + tests
dotnet build EmojiPick.sln -c Release

# 3. Run (quit any installed EmojiPick first: only one instance can run)
dotnet run --project src\EmojiPick\EmojiPick.csproj

# 4. Test
dotnet test EmojiPick.Tests\EmojiPick.Tests.csproj
```

Or use `.\Build.ps1` (`-Publish`, `-Installer`, `-Clean`) — see the README.

## Project Structure

```
emoji-picker/
├── EmojiPick.sln                # Solution (installer excluded from solution builds)
├── Build.ps1                    # Build / publish / MSI script
├── src/EmojiPick/
│   ├── EmojiPick.csproj         # Main project
│   ├── App.xaml / App.xaml.cs   # Application entry point
│   ├── Models/                  # Data models & POCOs
│   ├── Services/                # Business logic (thin wrappers)
│   ├── Windows/                 # WPF windows & UI
│   ├── Helpers/                 # Utilities, P/Invoke, algorithms, localization
│   └── Data/                    # Embedded resources
├── EmojiPick.Tests/             # xUnit tests
├── EmojiPick.Installer/         # WiX v4 MSI installer
└── tools/                       # Emoji database generator (Python)
```

## Coding Standards

### General

- **Language**: C# 10+ with nullable reference types enabled
- **Async/await**: Use everywhere I/O-bound. No `.Result` or `.Wait()` outside startup.
- **Naming**: PascalCase for types/public members, camelCase for locals/parameters, `_camelCase` for private fields
- **`var`**: Use when type is obvious. Be explicit when it isn't.
- **No magic strings/numbers**: Extract to named constants

### Architecture Rules

1. **Controllers/windows are thin** — no business logic in `OverlayWindow.xaml.cs`
2. **Services handle logic** — matchers, clipboard, hotkey management belong in `Services/`
3. **Helpers are stateless** — algorithms, P/Invoke, resource loading
4. **Models are POCOs** — no side effects, no dependencies

### Logging

Use `LoggerService` everywhere:

```csharp
LoggerService.Debug("Verbose detail");
LoggerService.Info("Normal operation");
LoggerService.Warn("Unexpected but handled");
LoggerService.Error("Something broke", ex);
```

### Error Handling

- Never swallow exceptions silently — log at minimum with `LoggerService.Warn/Error`
- Catch the most specific exception type possible
- Use `try/catch` around all external interactions (clipboard, file I/O, HTTP, native APIs)
- Return empty/fallback results rather than throwing in service methods

## Phased Development

This project follows a phased implementation plan (see `docs/plan.md`). Each phase:

1. Introduces specific new functionality
2. Has clear verification criteria
3. Should compile and not break previous phases

### Current Status

- ✅ **Phase 0**: Project scaffolding complete
- 🔲 **Phase 1**: Data models & configuration
- 🔲 **Phase 2**: Logging & helpers
- 🔲 **Phase 3-11**: Not started

## Git Workflow

### Branch Naming

| Prefix | Usage | Example |
|--------|-------|---------|
| `feat/` | New features | `feat/hotkey-manager` |
| `fix/` | Bug fixes | `fix/clipboard-threading` |
| `refactor/` | Code improvements | `refactor/config-service` |
| `docs/` | Documentation | `docs/installation-guide` |

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add HotKeyManager with RegisterHotKey
fix: restore clipboard after selection capture
docs: update README with LLM configuration
```

### Pull Requests

- One feature/fix per PR
- Update `CHANGELOG.md` under `[Unreleased]`
- Reference the phase from `docs/plan.md` if applicable
- Ensure `dotnet build` passes without warnings

## Testing Strategy

As the project evolves:

1. **Unit tests** for `FuzzyMatcher`, config parsing, scoring algorithms
2. **Integration tests** for clipboard save/restore, Ollama HTTP client
3. **Manual tests** for hotkey registration, overlay UI, cross-app injection

Tests directory: `EmojiPick.Tests/`

## Releasing

1. Update `major.minor` in `VERSION` — the app and the MSI take their version from it; `Build.ps1` adds the
   build number (+1 per build, local `.build-number`) and records each build in `build-history.log`
2. Update `"version"` in `default_config.json`
3. Move `[Unreleased]` section in `CHANGELOG.md` to new version
4. Tag release: `git tag -a v1.0.0 -m "Release v1.0.0"`
5. Build artifacts: `.\Build.ps1 -Installer` → `publish\EmojiPick.exe` and `EmojiPickerSetup.msi`
6. Publish both to GitHub Releases

---

Questions? Open an [issue](https://github.com/MrLouix/emoji-picker/issues).
