# EmojiPick — Suivi d'avancement

**Date :** 11 mai 2026
**Phase actuelle :** Phase 4 — Wiring MVP (Sprint "MVP Fonctionnel" en cours)
**Statut global :** 8 / 12 phases complètes — pipeline de base câblé, MVP fonctionnel en finalisation

---

## Vue d'ensemble des phases

| Phase | Nom | Statut |
|-------|-----|--------|
| 0 | Setup projet | Complet |
| 1 | Modèles & Configuration | Complet |
| 2 | Logging & Helpers | Complet |
| 3 | HotKey Manager | Complet |
| 4 | Clipboard & Selection | Complet |
| 5 | Fuzzy EmojiMatcher | Complet |
| 6 | Ollama LLM | Complet |
| 7 | LlamaSharp (LLM embarqué) | Non commencé |
| 8 | WPF Overlay | Complet (Sprint MVP Fonctionnel) |
| 9 | Output Handler & Injection | Non commencé |
| 10 | Intégration & Lifecycle | Complet (Sprint MVP Fonctionnel) |
| 11 | MSI Installer | Non commencé |

---

## Sprint en cours : "MVP Fonctionnel"

Objectif du sprint : rendre l'application exécutable de bout en bout — hotkey → overlay → sélection → injection — en utilisant uniquement le matching fuzzy (LLM différé à Phase 7).

Les 4 fichiers ci-dessous étaient des stubs. Ils ont été entièrement implémentés lors de ce sprint et apparaissent dans le `git status` comme modifiés (non encore commités).

| Fichier | État avant sprint | État après sprint | Lignes |
|---------|-------------------|-------------------|--------|
| `Services/TrayIcon.cs` | Stub vide (`Show() {}`) | Complet | 12 → 94 |
| `Windows/OverlayWindow.xaml` | Layout statique avec boutons inutiles | Complet — layout 3 lignes propre | 44 → 37 |
| `Windows/OverlayWindow.xaml.cs` | Stub vide | Complet | 13 → 237 |
| `App.xaml.cs` | Hotkey + wiring commentés | Complet — services câblés | 95 → 103 |

---

## Inventaire complet des fichiers

### Models

| Fichier | Statut | Description |
|---------|--------|-------------|
| `Models/Config.cs` | **Complet** | Racine de configuration — App, HotKey, UI, Behavior, LLM, Logging, avec defaults automatiques — 99 lignes |
| `Models/EmojiEntry.cs` | **Complet** | POCO emoji (Char, Name, Tags, Category, Unicode) + EmojiMatch avec scores fuzzy et LLM — 34 lignes |
| `Models/TextContext.cs` | **Complet** | Contexte textuel capturé (texte, source, position curseur, sélection) — 42 lignes |
| `Models/OllamaModels.cs` | **Complet** | Modèles Request / Options / Response pour l'API REST Ollama — 28 lignes |

### Services

| Fichier | Statut | Description |
|---------|--------|-------------|
| `Services/HotKeyManager.cs` | **Complet** | Registration globale configurable (défaut Ctrl+Alt+E) via `RegisterHotKey` P/Invoke, event `HotKeyPressed`, `IDisposable` — 106 lignes |
| `Services/EmojiMatcher.cs` | **Complet** | Chargement `emojis.json.gzip` embarqué (5 032 entrées), fuzzy matching avec cache TTL 5 min, fallback liste hardcodée — 127 lignes |
| `Services/SelectionHandler.cs` | **Complet** | Capture du texte sélectionné : clipboard (Ctrl+C simulé) puis UI Automation (TextPattern), fallback `TextSource.None` — 84 lignes |
| `Services/ClipboardService.cs` | **Complet** | GetText / SetText / HasText thread-safe, gestion silencieuse des contentions — 47 lignes |
| `Services/InputSimulator.cs` | **Complet** | `SendInput` P/Invoke, `SendCtrlC` / `SendCtrlV`, `AttachThreadInput` pour injection cross-process — 75 lignes |
| `Services/ConfigService.cs` | **Complet** | Load / Save `%APPDATA%\EmojiPick\config.json`, merge avec defaults, `EnsureDirectories` — 86 lignes |
| `Services/LoggerService.cs` | **Complet** | Serilog avec rotation quotidienne, taille max 10 MB, rétention 7 jours — 43 lignes |
| `Services/TrayIcon.cs` | **Complet** | NotifyIcon WinForms, menu About / Quit, chargement icône embarquée avec fallback `SystemIcons.Application` — 94 lignes — *Sprint MVP Fonctionnel* |
| `Services/ILlmMatcher.cs` | **Complet** | Interface unifiée `GetLlmRecommendations()` pour les providers LLM — 16 lignes |
| `Services/OllamaMatcher.cs` | **Complet** | HTTP POST vers Ollama `/api/generate`, cache 5 min, parsing graphèmes (`StringInfo`), timeout et fallback — 176 lignes |
| `Services/LlmProviderFactory.cs` | **Stub** | Chaîne de fallback ollama → llamacpp → fuzzy — TODO non implémenté — 11 lignes |
| `Services/ModelManager.cs` | **Stub** | Téléchargement et gestion de modèles GGUF depuis Hugging Face — TODO non implémenté — 10 lignes |

### Helpers

| Fichier | Statut | Description |
|---------|--------|-------------|
| `Helpers/NativeMethods.cs` | **Complet** | Toutes les déclarations P/Invoke : hotkey, SendInput, GetForegroundWindow, AttachThreadInput — 92 lignes |
| `Helpers/FuzzyMatcher.cs` | **Complet** | Levenshtein two-row O(min(m,n)) + scoring cascade exact (100) → préfixe (90) → sous-chaîne (75) → Levenshtein — 87 lignes |
| `Helpers/ResourceLoader.cs` | **Complet** | Chargement ressources embarquées depuis l'assembly (byte array ou string UTF-8), helper de diagnostic — 43 lignes |

### Windows

| Fichier | Statut | Description |
|---------|--------|-------------|
| `Windows/OverlayWindow.xaml` | **Complet** | Overlay sombre (#222233), sans bordure, topmost — CtxLabel / UniformGrid 4×4 / SearchBox — 37 lignes — *Sprint MVP Fonctionnel* |
| `Windows/OverlayWindow.xaml.cs` | **Complet** | `InitializeAsync()`, `RenderGrid()`, navigation clavier (flèches, Enter, Escape), recherche temps réel, auto-close sur perte de focus — 237 lignes — *Sprint MVP Fonctionnel* |

### Point d'entrée

| Fichier | Statut | Description |
|---------|--------|-------------|
| `App.xaml` | **Complet** | WPF root, `ShutdownMode="OnExplicitShutdown"` (opération en background) — 8 lignes |
| `App.xaml.cs` | **Complet** | Startup séquentiel : Logger → Config → TrayIcon → HotKeyManager → attente hotkey → Overlay ; shutdown propre — 103 lignes — *Sprint MVP Fonctionnel* |

### Projet et solution

| Fichier | Statut | Description |
|---------|--------|-------------|
| `EmojiPick.csproj` | **Complet** | .NET 7.0-windows, WPF + WinForms, Serilog, System.Management — ressources embarquées déclarées |
| `EmojiPick.sln` | **Complet** | 3 projets : EmojiPick (principal), EmojiPick.Tests (xUnit), EmojiPick.Installer (WiX placeholder) |

### Tests — `EmojiPick.Tests/`

| Fichier | Statut | Couverture |
|---------|--------|------------|
| `EmojiMatcherTests.cs` | **Complet** | 11 tests — chargement ressource, entrée vide, qualité du tri, seuil, `GetPopularEmoji`, cache |
| `FuzzyMatcherTests.cs` | **Complet** | 10 tests — distance Levenshtein (cas limites inclus), scoring cascade, `GetMatches`, liste vide |
| `OllamaMatcherTests.cs` | **Complet** | 9 tests — `IsEnabled`, `GetLlmRecommendations`, parsing, cache, timeout, `CancellationToken`, `Dispose` |

---

## Fonctionnalités implantées

### Pipeline principal (MVP)
- Hotkey global configurable, non bloquant, avec cleanup au shutdown
- Capture du texte sélectionné (clipboard) ou du contexte curseur (UI Automation)
- Overlay WPF transparent, topmost, sans bordure, positionné sous le curseur
- Grille emoji 4×4 avec rendu dynamique des résultats
- Navigation clavier complète : flèches, Enter pour sélectionner, Escape pour fermer
- Recherche temps réel dans la SearchBox (refiltrage fuzzy à chaque frappe)
- Auto-close de l'overlay sur perte de focus
- Sélection de l'emoji et injection dans l'application cible via clipboard + Ctrl+V
- Icône tray persistante avec menu About / Quit

### Matching
- Base de 5 032 emojis embarquée (gzip) avec nom, tags, catégorie, codepoint Unicode
- Algorithme Levenshtein optimisé avec scoring cascade : exact → préfixe → sous-chaîne → similarité
- Cache in-memory TTL 5 minutes pour requêtes identiques
- Fallback liste d'emojis populaires hardcodés si la ressource est indisponible

### LLM optionnel (Ollama)
- Interface `ILlmMatcher` extensible pour plusieurs providers
- Client HTTP async vers Ollama avec timeout configurable
- Parsing des réponses par graphème Unicode (`StringInfo`)
- Cache résultats 5 minutes ; dégradation silencieuse si Ollama est absent

### Infrastructure
- Configuration JSON persistée dans `%APPDATA%\EmojiPick\config.json` avec merge des defaults
- Logging Serilog structuré, rotation quotidienne, 10 MB max, 7 jours de rétention
- 30 tests unitaires xUnit couvrant les algorithmes critiques (FuzzyMatcher, EmojiMatcher, OllamaMatcher)

---

## Fonctionnalités restantes

### Phase 7 — LlamaSharp / LLM embarqué (Non commencé)
- `LlmProviderFactory` : chaîne de fallback ollama → llamacpp → fuzzy (stub existant)
- `ModelManager` : téléchargement de modèles GGUF depuis Hugging Face, reporting de progression (stub existant)
- `LlamaSharpMatcher` : inférence locale via LLamaSharp (fichier à créer)
- Détection GPU via WMI `Win32_VideoController` (CUDA / ROCm)

### Phase 9 — Output Handler dédié (Non commencé)
- `OutputHandler` : service isolé gérant les modes d'injection (paste / replace / append)
- Restauration du focus sur la fenêtre précédente après injection
- Restauration du clipboard original après injection

### Phase 11 — MSI Installer (Non commencé)
- `EmojiPick.wixproj` + `Product.wxs` (dossier `EmojiPick.Installer` existe mais est vide)
- Installation per-user dans `%LOCALAPPDATA%`
- Raccourcis Menu Démarrer et Bureau
- Démarrage automatique via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Pipeline de release : `dotnet publish` → `wix build` → artefacts `EmojiPick-1.0.0.exe` + `.msi`

### Améliorations identifiées mais non planifiées
- `HelpWindow.xaml` : fenêtre de diagnostic (statut LLM, provider actif, cache hits, logs en direct)
- Navigation souris dans l'overlay (hover highlight, clic direct)
- IAccessible comme fallback de UI Automation pour applications non compatibles TextPattern
- Configuration de la hotkey via l'interface (actuellement uniquement via `config.json`)

---

## Prochaines étapes

1. **Valider le build** — lancer `dotnet restore && dotnet build -c Release` sur Windows après le sprint
2. **Exécuter les tests** — `dotnet test` pour confirmer la non-régression (30 tests attendus passants)
3. **Test end-to-end manuel** — scénario complet sur Windows :
   - Démarrage → icône tray visible
   - Ctrl+Alt+E dans Bloc-Notes avec texte sélectionné → overlay affiché
   - Navigation flèches → Enter → emoji injecté
   - Frappe dans la SearchBox → résultats filtrés
   - Clic droit tray → Quit → application fermée proprement
4. **Commit du sprint** — commiter les 4 fichiers modifiés une fois les tests validés
5. **Phase 9 (recommandé avant release)** — extraire la logique d'injection dans un `OutputHandler` dédié
6. **Phase 7 (optionnel)** — implémenter `LlmProviderFactory` et `ModelManager` pour le LLM local
7. **Phase 11 (release)** — MSI installer pour la distribution v1.0.0

---

## Récapitulatif par statut

| Catégorie | Total | Complet | Stub | Non commencé |
|-----------|-------|---------|------|--------------|
| Models | 4 | 4 | 0 | 0 |
| Services | 12 | 10 | 2 | 0 |
| Helpers | 3 | 3 | 0 | 0 |
| Windows | 2 | 2 | 0 | 0 |
| Point d'entrée | 2 | 2 | 0 | 0 |
| Tests | 3 | 3 | 0 | 0 |
| **Total** | **26** | **24** | **2** | **0** |

Les 2 stubs restants (`LlmProviderFactory`, `ModelManager`) concernent exclusivement le LLM local embarqué (Phase 7), qui n'est pas requis pour le MVP. Le pipeline complet hotkey → capture → fuzzy matching → overlay → sélection → injection est câblé et prêt pour les tests sur Windows.
