# 📊 Suivi d'Avancement — EmojiPick v1.0.0

**Dernière mise à jour :** 2025-06-18

---

## Vue d'ensemble

**4 / 12 phases réalisées** — Phases 0→3 complètes, Phase 4 finalisée (SelectionHandler + InputSimulator par Claude Code).

| Phase | Nom | Statut |
|-------|-----|--------|
| 0 | Setup projet | 🟢 Fait |
| 1 | Modèles & Config | 🟢 Fait |
| 2 | Logging & Helpers | 🟢 Fait |
| 3 | HotKey Manager | 🟢 Fait |
| 4 | Clipboard & Selection | 🟢 Fait |
| 5 | Fuzzy EmojiMatcher | 🔴 Non commencé |
| 6 | Ollama LLM | 🟡 Partiel |
| 7 | LlamaSharp | 🔴 Non commencé |
| 8 | WPF Overlay | 🟡 Partiel |
| 9 | Output Handler & Injection | 🔴 Non commencé |
| 10 | Intégration & Lifecycle | 🟡 Partiel |
| 11 | MSI Installer | 🔴 Non commencé |

---

## Détail par phase

### Phase 0 — Setup projet
**Statut :** 🟢 Fait
- `EmojiPick.sln` (3 projets : EmojiPick, EmojiPick.Installer, EmojiPick.Tests)
- `EmojiPick.csproj` (57 lignes, net7.0-windows + WPF)

**Structure du repo :**
```
emoji-overlay/
├── EmojiPick/
│   ├── EmojiPick.sln
│   ├── EmojiPick/                    ← projet C# principal
│   │   ├── EmojiPick.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── Helpers/
│   │   │   ├── FuzzyMatcher.cs
│   │   │   ├── NativeMethods.cs
│   │   │   └── ResourceLoader.cs
│   │   ├── Models/
│   │   │   ├── Config.cs
│   │   │   ├── EmojiEntry.cs
│   │   │   ├── OllamaModels.cs
│   │   │   └── TextContext.cs
│   │   ├── Services/
│   │   │   ├── ClipboardService.cs
│   │   │   ├── ConfigService.cs
│   │   │   ├── HotKeyManager.cs
│   │   │   ├── ILlmMatcher.cs
│   │   │   ├── InputSimulator.cs
│   │   │   ├── LoggerService.cs
│   │   │   ├── LlmProviderFactory.cs  (placeholder)
│   │   │   ├── ModelManager.cs        (placeholder)
│   │   │   ├── OllamaMatcher.cs       (placeholder)
│   │   │   ├── SelectionHandler.cs
│   │   │   └── TrayIcon.cs            (placeholder)
│   │   ├── Windows/
│   │   │   ├── OverlayWindow.xaml
│   │   │   └── OverlayWindow.xaml.cs  (placeholder)
│   │   ├── Data/
│   │   │   └── emojis.json.gzip
│   │   └── app.ico
│   ├── EmojiPick.Tests/              ← projet de tests xUnit
│   │   ├── EmojiPick.Tests.csproj
│   │   └── FuzzyMatcherTests.cs
│   └── EmojiPick.Installer/          ← WiX (placeholder)
│       ├── icon.ico
│       └── license.rtf
└── docs/
    ├── plan.md
    └── AVANCEMENT.md
```

### Phase 1 — Modèles & Configuration
**Statut :** 🟢 Fait
- ✅ `Models/EmojiEntry.cs` (33 lignes) — Char, Name, Tags, Category, Unicode + EmojiMatch
- ✅ `Models/Config.cs` (98 lignes) — Config, HotKeyConfig, LlmConfig, ProviderConfig, UiConfig, BehaviorConfig, LoggingConfig
- ✅ `Services/ConfigService.cs` (74 lignes) — Load/Save avec defaults
- ✅ `Models/OllamaModels.cs` (27 lignes) — OllamaRequest, OllamaOptions, OllamaResponse
- ✅ `Models/TextContext.cs` (41 lignes) — TextContext avec Source, CursorPosition, etc.

**Audit (vérifié par Claude Code) :**
- ⚠️ ConfigService : pas de validation post-désérialisation
- ⚠️ ConfigService : `EnsureDirectories()` non appelé avant `Save()` — crash première installation
- ⚠️ ConfigService : pas de propriété `Current` pour accès sans recharger

### Phase 2 — Logging & Helpers
**Statut :** 🟢 Fait
- ✅ `Services/LoggerService.cs` (42 lignes, Serilog file rotation 7 jours / 10MB)
- ✅ `Helpers/NativeMethods.cs` (88 lignes, P/Invoke complet + GetCurrentThreadId)
- ✅ `Helpers/ResourceLoader.cs` (42 lignes, ressources embedded)
- ✅ `Helpers/FuzzyMatcher.cs` (~80 lignes, implémenté par Claude Code)
  - `DistanceLevenshtein()` — two-row rolling array O(min(m,n)) mémoire
  - `ComputeScore()` — cascade : exact(100) → prefix(90) → substring(75) → Levenshtein
  - `GetMatches()` — matching sur tous tags, tri score décroissant, seuil configurable

### Phase 3 — HotKey Manager
**Statut :** 🟢 Fait (implémenté par Claude Code)
- ✅ `Services/HotKeyManager.cs` (106 lignes, complet)
  - Constructeur prend `HotKeyConfig`, parse Modifiers/Key → flags MOD_*
  - `Register()` → `HwndSource` invisible + message hook + `RegisterHotKey`
  - `Unregister()` → cleanup + implémente `IDisposable`
  - `HotKeyPressed` event déclenché depuis `WndProc` (WM_HOTKEY = 0x0312)
  - Logging Serilog sur succès/échec (conflit hotkey détecté)

### Phase 4 — Clipboard & Selection
**Statut :** 🟢 Fait (implémenté par Claude Code)
- ✅ `Services/ClipboardService.cs` (46 lignes, GetText/SetText/HasText)
- ✅ `Services/SelectionHandler.cs` (~60 lignes, implémenté par Claude Code)
  - Try 1 : lecture clipboard → `TextSource.Selection` si texte trouvé
  - Try 2 : UI Automation (`AutomationElement.FromHandle` + `TextPattern`) → `TextSource.CursorContext`
  - Fallback : `TextSource.None`
  - Logging de chaque étape
- ✅ `Services/InputSimulator.cs` (~60 lignes, implémenté par Claude Code)
  - `SendKeyStroke/Down/Up/Press` via `SendInput` Windows API
  - `SendCtrlC()` / `SendCtrlV()` avec `AttachThreadInput` pour cross-process
  - `SendTextViaPaste()` → clipboard + Ctrl+V
  - Délais 10-200ms pour stabilité

### Phase 5 — Fuzzy EmojiMatcher
**Statut :** 🔴 Non commencé
- ❌ `Services/EmojiMatcher.cs` — **PLACEHOLDER** (10 lignes, TODO: implement)

### Phase 6 — Ollama LLM
**Statut :** 🟡 Partiel
- ✅ `Services/ILlmMatcher.cs` (15 lignes, interface)
- ✅ `Models/OllamaModels.cs` (27 lignes, modèles request/response)
- ❌ `Services/OllamaMatcher.cs` — **PLACEHOLDER** (21 lignes, retourne liste vide)
  - ⚠️ Signature de méthode présente mais pas d'implémentation HTTP client

### Phase 7 — LlamaSharp
**Statut :** 🔴 Non commencé
- ❌ `Services/ModelManager.cs` — **PLACEHOLDER** (10 lignes, TODO: implement)
- ❌ `Services/LlmProviderFactory.cs` — **PLACEHOLDER** (11 lignes, TODO: implement)

### Phase 8 — WPF Overlay
**Statut :** 🟡 Partiel
- ✅ `Windows/OverlayWindow.xaml` (44 lignes, UI Grid 4×3 définie)
- ❌ `Windows/OverlayWindow.xaml.cs` — **PLACEHOLDER** (13 lignes, TODO: implement)
- ❌ `Services/TrayIcon.cs` — **PLACEHOLDER** (12 lignes, TODO: implement)

### Phase 9 — Output Handler & Injection
**Statut :** 🔴 Non commencé
- ❌ `Services/OutputHandler.cs` — **FICHIER MANQUANT**

### Phase 10 — Intégration & Lifecycle
**Statut :** 🟡 Partiel
- ✅ `App.xaml.cs` (95 lignes, startup séquentiel avec lifecycle)
- ❌ `Program.cs` — **FICHIER MANQUANT**

### Phase 11 — MSI Installer
**Statut :** 🔴 Non commencé
- 📁 Dossier `EmojiPick.Installer` existe (icône + license uniquement)
- ❌ `EmojiPick.wixproj` — manquant
- ❌ `Product.wxs` — manquant

---

## Tests unitaires

| Phase | Tests | Fichier | Statut |
|-------|-------|---------|--------|
| 2 | FuzzyMatcher | `EmojiPick.Tests/FuzzyMatcherTests.cs` | ✅ Créés (11 tests xUnit) |
| 3 | HotKeyManager | — | ❌ Non créés (nécessite Windows APIs, difficilement testable sur Linux) |

**Détail des tests FuzzyMatcher (11 tests) :**
1. ✅ Levenshtein("kitten", "sitting") == 3
2. ✅ Levenshtein("", "abc") == 3
3. ✅ Levenshtein("abc", "") == 3
4. ✅ Levenshtein("", "") == 0
5. ✅ Levenshtein("abc", "abc") == 0
6. ✅ ComputeScore("love", "love") == 100 (exact)
7. ✅ ComputeScore("lov", "love") == 90 (prefix)
8. ✅ ComputeScore("happy", "happy face") >= 75 (substring)
9. ✅ GetMatches : meilleur match ("😍" pour "love") en premier
10. ✅ GetMatches : score sous seuil → pas inclus
11. ✅ GetMatches : texte vide → liste vide
12. ✅ GetMatches : emojis null → liste vide

---

## Fonctionnalités complémentaires réalisées hors plan

| Date | Feature | Description |
|------|---------|-------------|
| — | Aucune | |

---

## Résumé

- **Complètement implémenté :** Phases 0, 1, 2, 3, 4
- **Partiellement implémenté :** Phases 6, 8, 10 (modèles/interfaces OK, logique manquante)
- **À faire de zéro :** Phases 5, 7, 9, 11
