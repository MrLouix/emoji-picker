# 📋 Plan de Codage — EmojiPick v1.0.0

## Architecture cible
Application WPF .NET 7+ (C#), exécutable unique Windows, avec matching emoji Fuzzy + LLM (Ollama + llama.cpp), overlay transparent, injection via SendInput, installer MSI WiX.

---

## Phase 0 — Fondations du Projet (Jour 1)

### Étape 0.1 : Structure solution
```
EmojiPick/
├── EmojiPick.sln
├── EmojiPick/                    # Projet principal (WPF, .NET 7)
│   ├── EmojiPick.csproj
│   ├── Program.cs / App.xaml.cs
│   ├── Models/
│   ├── Services/
│   ├── Windows/
│   ├── Helpers/
│   └── Data/
└── EmojiPick.Installer/          # Projet WiX
    ├── EmojiPick.wixproj
    └── Product.wxs
```

### Étape 0.2 : NuGet dependencies
- `Serilog` + `Serilog.Sinks.File` (logging)
- `System.Text.Json` (déjà inclus .NET 7)
- `LLamaSharp` + `LLamaSharp.cuda` (optionnel, v2+)
- `System.Management` (WMI GPU detection)

**Vérification** : `dotnet build` compile sans erreur.

---

## Phase 1 — Modèle de Données & Configuration (Jour 1-2)

### 1.1 `Models/EmojiEntry.cs`
- `Char` (string), `Name`, `Tags` (List<string>), `Category`, `Unicode`
- Méthode `FromJson()` pour parsing DB embedded

### 1.2 `Models/Config.cs` et dérivés
- `Config` (app, hotkey, ui, behavior, llm, logging, language)
- `HotKeyConfig` (Modifiers[], Key)
- `LlmConfig` (Enabled, Provider, Providers dict, FallbackChain, CacheResults, CacheTtlMinutes)
- `ProviderConfig` (Endpoint, Model, ModelPath, UseGpu, GpuLayers, TimeoutMs…)
- `UiConfig`, `BehaviorConfig`, `LoggingConfig`

### 1.3 `Services/ConfigService.cs`
- `Load()` / `Save()` avec `%APPDATA%\EmojiPick\config.json`
- Validation schema + apply defaults si clés manquantes
- `ResolvePath()` pour `%APPDATA%` expansion

### 1.4 `Data/emojis.json.resx`
- Base de 1500+ emoji avec tags Unicode
- Ressource embedded + décompression gzip

**Vérification** : Config se charge, se sauve, les defaults s'appliquent. Emoji DB se parse correctement.

---

## Phase 2 — Logging & Helpers (Jour 2)

### 2.1 `Services/LoggerService.cs`
- Serilog configuré file rotation (`%APPDATA%\EmojiPick\logs\EmojiPick-{date}.log`)
- Retention 7 jours, max 10MB/file
- Niveaux : Debug, Info, Warn, Error

### 2.2 `Helpers/NativeMethods.cs`
- Tous les P/Invoke Windows API :
  - `RegisterHotKey`, `UnregisterHotKey`
  - `GetForegroundWindow`, `GetAsyncKeyState`
  - `SendInput` (structs INPUT, KEYBDINPUT, MOUSEINPUT)
  - `SetForegroundWindow`, `GetWindowThreadProcessId`
  - `AttachThreadInput`

### 2.3 `Helpers/FuzzyMatcher.cs`
- Distance Levenshtein optimisée
- Scoring 0-100
- Matching fuzzy sur les tags emoji

### 2.4 `Helpers/ResourceLoader.cs`
- Chargement ressources embedded (emojis.json.gzip, icon, default_config)

**Vérification** : FuzzyMatcher retourne scores corrects sur "amazing" → [🤩, ✨, ...]. Logs écrits dans fichier.

---

## Phase 3 — Hotkey Manager (Jour 3)

### 3.1 `Services/HotKeyManager.cs`
- `RegisterHotKey(Ctrl+Alt+E)` via Windows API
- Écoute globale (background)
- Événement `HotKeyPressed`
- `UnregisterHotKey()` pour cleanup
- Support hotkey configurable depuis config.json
- Non-bloquant

**Vérification** : App en background, Ctrl+Alt+E déclenche un log. Unregister au shutdown.

---

## Phase 4 — Clipboard & Selection Handler (Jour 3-4)

### 4.1 `Services/ClipboardService.cs`
- Sauvegarde clipboard actuel
- Lecture/écriture thread-safe (`System.Windows.Forms.Clipboard`)
- Restoration clipboard original

### 4.2 `Services/SelectionHandler.cs` (section 3.2.1 de la spec)
- **Étape 1** : `TryGetSelectedText()` — Ctrl+C simulé → clipboard → limiter 100 chars
- **Étape 2** : `TryGetCursorContext()` — UI Automation (`AutomationElement.FromHandle`) → TextPattern → caret position → 20 chars avant/après
- **Étape 3** : Fallback — aucun texte → retourne `TextSource.None`
- Retourne `TextContext` (Text, Source, CursorPosition, BeforeCursor, AfterCursor, HasSelection, IsFromClipboard)

### 4.3 `Services/InputSimulator.cs`
- Wrapper SendInput pour keystrokes (Ctrl+C, Ctrl+V, etc.)
- Gestion thread input (AttachThreadInput pour apps différentes)

**Vérification** : Ctrl+Alt+E dans Notepad avec texte sélectionné → texte capturé. Sans sélection → contexte récupéré.

---

## Phase 5 — EmojiMatcher (Fuzzy) (Jour 4-5)

### 5.1 `Services/EmojiMatcher.cs`
- Charge emojis.json au démarrage
- Indexe par tags pour recherche rapide
- `GetMatches(string text)` → retourne top 12 par score décroissant
- `GetPopularEmoji()` → fallback quand aucun texte
- Cache in-memory 5min TTL

### 5.2 Algorithme de scoring
- Fuzzy : distance Levenshtein entre texte et tags → score 0-100
- Tri par score décroissant

**Vérification** : "amazing" → [🤩, ✨, 👏, 🌟, ...], "love" → [❤️, 😍, 💕, ...]. Cache hit confirmé.

---

## Phase 6 — Ollama LLM Integration (Jour 5-6)

### 6.1 `Services/OllamaMatcher.cs`
- `ILlmMatcher` interface
- `HttpClient` POST `http://localhost:11434/api/generate`
- Prompt template (section 4.6)
- Timeout 3s configurable
- `IsEnabled()` via health check `/api/tags`
- Cache in-memory 5min

### 6.2 `Models/OllamaModels.cs`
- `OllamaRequest` (model, prompt, stream, options)
- `OllamaOptions` (temperature, top_p, top_k, num_predict)
- `OllamaResponse` (model, response, done)

### 6.3 Parsing réponse (section 4.3)
- Split virgule/espace → extrait emoji characters
- Match avec candidates → retourne `List<EmojiEntry>`

**Vérification** : Ollama running → réponse emoji en <3s. Ollama absent → fallback fuzzy. Timeout → fallback.

---

## Phase 7 — LlamaSharp Integration (Jour 6-7)

### 7.1 `Services/LlamaSharpMatcher.cs`
- Implémente `ILlmMatcher`
- `Initialize()` → load GGUF model, create context, create executor
- `GetLlmRecommendations()` → InferAsync avec timeout 3s
- Parsing réponse (similaire Ollama)

### 7.2 `Services/ModelManager.cs`
- `EnsureModelExists()` → download depuis Hugging Face
- Progress reporting
- Cache dans `%APPDATA%\EmojiPick\models\`
- Recommended models list (Mistral-7B-Q4_K_M, phi-2, neural-chat)

### 7.3 `Helpers/GpuDetection.cs` (section 6.1)
- WMI `Win32_VideoController` → détecte NVIDIA CUDA / AMD ROCm
- Retourne meilleur provider GPU

### 7.4 `Services/LlmProviderFactory.cs`
- Fallback chain : ollama → llamacpp → fuzzy
- `CreateProvider()` → itère fallback chain, initialise premier dispo
- `CreateOllamaProvider()`, `CreateLlamaCppProvider()`, auto-download modèle

**Vérification** : Fallback chain fonctionne. Modèle se télécharge. LlamaSharp charge GGUF et répond.

---

## Phase 8 — WPF Overlay Window (Jour 7-9)

### 8.1 `Windows/OverlayWindow.xaml`
- `WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`
- Fond noir 20% opacité
- Dimensions 400×300px
- Grid 4×3 emoji buttons
- Label contexte en haut
- Search box en bas
- Action buttons : [Copier] [Paste] [Fermer]

### 8.2 `Windows/OverlayWindow.xaml.cs`
- `OnHotKeyTriggered()` :
  - Récupère TextContext via SelectionHandler
  - Lance Fuzzy instant (affiche résultats immédiatement)
  - Lance LLM async (spinner "Fetching...")
  - Merge résultats quand LLM arrive
  - Auto-refresh UI
- Navigation clavier : ↑↓←→, Entrée (sélectionner), Echap (fermer)
- Navigation souris : clic emoji, hover highlight
- Search bar : filtre en temps réel
- `AutoClose` après sélection
- Positionnement centre écran ou curseur souris

### 8.3 `Windows/TrayIcon.cs`
- SystemTray icon (NotifyIcon)
- Menu contextuel : Config, Help → LLM Status, Exit
- Double-clic → toggle config

### 8.4 `Windows/HelpWindow.xaml` (section 6.4)
- Status LLM, provider actif, modèles, cache hits, logs
- Buttons : Open config.json, Download Models, Cache Clear

**Vérification** : Overlay s'affiche en <50ms après hotkey. Navigation clavier/souris fonctionne. Auto-close après sélection.

---

## Phase 9 — Output Handler & Injection (Jour 9-10)

### 9.1 `Services/OutputHandler.cs`
- Copier emoji dans clipboard
- `SendInput(Ctrl+V)` pour injection
- Attendre 100ms
- Fermer overlay
- Restaurer focus fenêtre précédente

### 9.2 Modes d'injection (configurable)
- `paste` : Ctrl+V (défaut)
- `replace` : supprimer sélection, injecter emoji
- `append` : ajouter emoji après texte

**Vérification** : Emoji injecté dans Notepad, Slack, Chrome. Focus restauré.

---

## Phase 10 — Intégration & Lifecycle (Jour 10-12)

### 10.1 `Program.cs` / `App.xaml.cs`
Startup séquentiel (section 13.1) :
1. `EnsureInstallation()` — créer dossiers %APPDATA%, config par défaut
2. `LoadConfiguration()` — charger + valider config.json
3. `EnsureEmojiDatabase()` — charger emojis.json embedded → indexer
4. `LlmProviderFactory.CreateProvider()` — fallback chain init
5. `InitializeHotKeyManager()` — RegisterHotKey
6. `InitTrayIcon()` — systray icon, hide main window
7. Logger "EmojiPick v1.0.0 ready"

### 10.2 Shutdown (section 13.3)
1. `SaveConfiguration()` — écrire config.json
2. `UnregisterHotKey()` — cleanup Windows API
3. `Dispose()` — HttpClient, LLM resources
4. `Application.Current.Shutdown()`

### 10.3 Runtime workflow (section 13.2)
```
HotKey → Capture texte → Fuzzy (instant) → LLM (async) → Overlay → Sélection → Injection → Close
```

**Vérification** : Cycle complet hotkey → overlay → injection → shutdown. Pas de fuite mémoire.

---

## Phase 11 — Installer MSI WiX (Jour 12-13)

### 11.1 `EmojiPick.wixproj` + `Product.wxs`
- Per-user install dans `%LOCALAPPDATA%`
- Start Menu shortcut
- Desktop shortcut
- Auto-start registry (HKCU Run)
- License MIT

### 11.2 Build pipeline
```bash
dotnet publish -c Release -p:PublishSingleFile=true
wix build EmojiPick.wixproj -o ./dist/
```

### 11.3 Release artifacts
```
v1.0.0/
├── EmojiPick-1.0.0.exe   (portable standalone)
├── EmojiPick-1.0.0.msi   (installer)
├── CHANGELOG.md
├── README.md
└── INSTALL.md
```

**Vérification** : MSI installe, shortcuts créés, auto-start fonctionne, uninstall propre.

---

## Ordre d'exécution recommandé

| Phase | Jours | Dépendances | Critique |
|-------|-------|-------------|----------|
| 0 — Setup projet | 0.5 | Aucune | ✅ |
| 1 — Modèles & Config | 1 | Phase 0 | ✅ |
| 2 — Logging & Helpers | 1 | Phase 1 | ✅ |
| 3 — HotKey Manager | 0.5 | Phase 2 | ✅ |
| 4 — Clipboard & Selection | 1 | Phase 2-3 | ✅ |
| 5 — Fuzzy EmojiMatcher | 1 | Phase 1-2 | ✅ |
| 6 — Ollama LLM | 1 | Phase 5 | ⚠️ LLM |
| 7 — LlamaSharp | 1 | Phase 5-6 | ⚠️ LLM |
| 8 — WPF Overlay | 2 | Phase 3-7 | ✅ |
| 9 — Output Handler | 0.5 | Phase 4-8 | ✅ |
| 10 — Intégration | 1 | Toutes | ✅ |
| 11 — MSI Installer | 1 | Toutes | ⚠️ Release |

**Total : ~11-13 jours de dev**

---

## Points critiques & risques identifiés

1. **SendInput reliability** : Le cross-thread input (AttachThreadInput) peut échouer sur certaines apps (UWP, elevated processes). Fallback clipboard-only requis.
2. **UI Automation** : Les sections 3.2.1 mentionnent IAccessible comme fallback non implémenté — c'est un risque de compatibilité.
3. **Clipboard thread-safety** : `System.Windows.Forms.Clipboard` nécessite STA thread. L'overlay WPF doit être en STA.
4. **LLamaSharp poids** : Les modèles GGUF font 2-6GB — pas embeddable dans l'exe. Download obligatoire au first-run.
5. **Ollama non installé** : Le fallback chain est bien specifié (ollama → llamacpp → fuzzy), mais llama.cpp nécessite aussi un modèle. Si aucun modèle n'est dispo, fuzzy seul doit suffire.
6. **Raccourcis conflit** : Ctrl+Alt+E peut être utilisé par d'autres apps. Rendre configurable est essentiel.
