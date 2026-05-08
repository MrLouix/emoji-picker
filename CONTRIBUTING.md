# Contributing to EmojiPick

Thank you for your interest in contributing! This document covers the essentials.

## Development Setup

### Prerequisites

- **Windows 10/11** (WPF is Windows-only)
- **.NET 7.0 SDK** ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/7.0))
- **Visual Studio 2022** or **VS Code + C# Dev Kit**
- **WiX Toolset v3.x** (optional, for MSI builds)

### Getting Started

```bash
# 1. Clone the repository
git clone https://github.com/<user>/emojipick.git
cd emojipick

# 2. Restore NuGet packages
dotnet restore

# 3. Build
dotnet build -c Release

# 4. Run (Windows only — must have WPF runtime)
dotnet run --project EmojiPick/EmojiPick/EmojiPick.csproj
```

## Project Structure

```
EmojiPick/
├── EmojiPick.sln                # Visual Studio / Rider solution
├── EmojiPick/
│   ├── EmojiPick.csproj         # Main project
│   ├── App.xaml / App.xaml.cs   # Application entry point
│   ├── Models/                  # Data models & POCOs
│   ├── Services/                # Business logic (thin wrappers)
│   ├── Windows/                 # WPF windows & UI
│   ├── Helpers/                 # Utilities, P/Invoke, algorithms
│   └── Data/                    # Embedded resources
├── EmojiPick.Installer/         # WiX MSI installer
└── docs/                        # Spec, plans, notes
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

Tests directory: `EmojiPick.Tests/` (to be created)

## Releasing

1. Update `Version` in `EmojiPick.csproj`
2. Update `Version` in `Product.wxs` (WiX)
3. Update `"version"` in `default_config.json`
4. Move `[Unreleased]` section in `CHANGELOG.md` to new version
5. Tag release: `git tag -a v1.0.0 -m "Release v1.0.0"`
6. Build artifacts: `dotnet publish` + `wix build`
7. Publish to GitHub Releases

---

Questions? Open an [issue](https://github.com/<user>/emojipick/issues).
