# Sprint Plan — Rendre EmojiPick Fonctionnel

> **Objectif minimal:** L'application démarre, l'icône tray est visible, `Ctrl+Alt+E` ouvre l'overlay avec des emoji fuzz-matchés, la navigation clavier/souris marche, et un clic ou Enter injecte l'emoji dans l'app cible via clipboard.
>
> **Architecture:** WPF app sans fenêtre principale — vit cachée + icône tray + overlay au hotkey. Le matching LLM est différé à une phase ultérieure; la V1 utilise uniquement le fuzzy matching (qui existe déjà et fonctionne).
>
> **Prérequis build:** `dotnet restore` → `dotnet build` (Windows uniquement — pas compilable sur Linux/WSL).

---

## État des lieux

### Services existants (OK — pas de modifs)
| Service | Rôle | Statut |
|---------|------|--------|
| HotKeyManager | Registration `RegisterHotKey` P/Invoke | ✅ Complet |
| EmojiMatcher | Chargement gzip + fuzzy matching + cache | ✅ Complet |
| FuzzyMatcher | Levenshtein + scoring cascade | ✅ Complet |
| SelectionHandler | Clipboard + UI Automation pour contexte texte | ✅ Complet |
| ClipboardService | Get/Set clipboard STA-safe | ✅ Complet |
| InputSimulator | SendInput + Ctrl+C/V simulés | ✅ Complet |
| ConfigService | Load/Save %APPDATA%\config.json | ✅ Complet |
| LoggerService | Serilog rolling files | ✅ Complet |
| ResourceLoader | Chargement ressources embarquées | ✅ Complet |
| NativeMethods | Tous les P/Invoke nécessaires | ✅ Complet |
| Models (Config, EmojiEntry, TextContext, OllamaModels) | Data models | ✅ Complet |

### Services à écrire (ce sprint)
| Fichier | Problème actuel | Action requise |
|---------|----------------|----------------|
| `TrayIcon.cs` | Stub vide (`Show() {}`) | Implémenter NotifyIcon + menu contextuel |
| `OverlayWindow.xaml` | XAML statique avec boutons inutiles pour le V1 | Retirer les boutons, nettoyer le layout |
| `OverlayWindow.xaml.cs` | Stub vide | Implémenter grille, navigation, matching, sélection |
| `App.xaml.cs` | Hotkey + matching + overlay tout commentés | Décommenter, wire les services existants |

### Services ignorés (phases futures)
| Fichier | Raison |
|---------|--------|
| LlmProviderFactory | Stub — V1 fuzzy-only |
| OllamaMatcher | Existant mais non appelé |
| ILlmMatcher | Existant mais non appelé |
| ModelManager | Stub — téléchargement GGUF |

---

## Étapes d'implémentation

### Étape 1 : Implémenter `TrayIcon.cs`

**Fichier cible:** `EmojiPick/EmojiPick/Services/TrayIcon.cs` (réécriture complète, ~13 → ~80 lignes)

**Actions:**
1. Créer une classe `TrayIcon : IDisposable`
2. Instancier un `System.Windows.Forms.NotifyIcon` dans `Show()`
   - Charger l'icône embarquée depuis les ressources (`ResourceLoader.LoadEmbeddedResource("EmojiPick.app.ico")`)
   - Fallback sur `SystemIcons.Application` si l'icône ne charge pas
   - Tooltip: `"EmojiPick — Press Ctrl+Alt+E to pick an emoji"`
   - Afficher un ballon tip au démarrage
3. Créer un `ContextMenuStrip` avec deux entrées:
   - **About** → `MessageBox.Show` avec version et instructions
   - **Quit** → lève un événement `QuitRequested` (que `App.xaml.cs` connectera à `Shutdown()`)
4. `Dispose()`: cache le NotifyIcon, libère les ressources, log

**Dépendances externes:** `System.Windows.Forms` (déjà dans `.csproj` via `UseWindowsForms`), `System.Drawing` (pour l'icône).

**Vérification:** `dotnet build` compile sans erreur sur `TrayIcon`.

---

### Étape 2 : Réviser `OverlayWindow.xaml`

**Fichier cible:** `EmojiPick/EmojiPick/Windows/OverlayWindow.xaml` (modification du layout)

**Actions:**
1. Supprimer la `<StackPanel>` des boutons (Copier, Paste, Fermer) — Row 3 entière à retirer
2. Le layout devient: Row 0 = CtxLabel, Row 1 = EmojiGrid, Row 2 = SearchBox seul
3. Améliorer le style de la SearchBox (fond sombre, texte blanc)
4. Ajuster la hauteur de la fenêtre à ~320px
5. Mettre `WindowStartupLocation="Manual"` (positionnement au curseur géré dans le code)

**Vérification:** `dotnet build` — le XAML compile.

---

### Étape 3 : Implémenter `OverlayWindow.xaml.cs`

**Fichier cible:** `EmojiPick/EmojiPick/Windows/OverlayWindow.xaml.cs` (~13 → ~200 lignes)

**Actions:**
1. **Constructeur** —接受 un `SelectionHandler` en paramètre, appeler `InitializeComponent()`, configurer les événements:
   - `SearchBox.TextChanged` → re-filter les résultats
   - `KeyDown` → navigation (flèches, Enter, Escape, Backspace)
   - `Deactivated` → auto-close quand la fenêtre perd le focus
   - Charger le config (`ConfigService.Current`) pour les dimensions de grille et l'opacité du fond

2. **`InitializeAsync()`** — méthode async appelée par `App.xaml.cs` après construction:
   - Sauvegarder le clipboard actuel
   - `InputSimulator.SendCtrlC()` pour capturer le texte sélectionné
   - Attendre 150ms
   - `_selectionHandler.GetTextContext()` pour obtenir le contexte
   - Afficher le contexte dans `CtxLabel` (tronqué à 40 chars)
   - `EmojiMatcher.GetMatches(query)` pour obtenir les résultats fuzzy
   - `RenderGrid()` pour peupler la grille
   - Focus sur la SearchBox

3. **`RenderGrid()`** — remplit l'`UniformGrid` avec des boutons:
   - Un bouton par `EmojiMatch`, max `Columns × Rows` items
   - Chaque bouton affiche l'emoji (grande taille) + une ligne de tags (petite taille, gris)
   - Le bouton a un `Tag = index` pour l'identification
   - Les boutons vides (au-delà du nombre de résultats) sont `Visibility.Collapsed`
   - `UpdateSelection()` applique le style visuel au `_selectedIndex`

4. **Navigation clavier** (`KeyDown`):
   - `Up/Down/Left/Right` → déplacer `_selectedIndex` dans la grille
   - `Enter` → `SelectAndClose(selectedIndex)`
   - `Escape` → `Close()`
   - `Backspace` → effacer la SearchBox

5. **`SearchBox_TextChanged()`** → ré-exécuter `EmojiMatcher.GetMatches()` avec le texte de la search box, re-render la grille

6. **`SelectAndClose(index)`** → stocker `SelectedEmoji` puis `Close()`

7. **`PositionAtCursor()`** → `System.Windows.Forms.Cursor.Position` pour placer l'overlay sous le curseur

**Propriétés exposées:** `InitialContext` (TextContext), `SelectedEmoji` (string)

**Vérification:** `dotnet build` — compile sans erreur.

---

### Étape 4 : Finaliser `App.xaml.cs`

**Fichier cible:** `EmojiPick/EmojiPick/App.xaml.cs` (réécriture — décommenter + wiring)

**Actions:**
1. **`OnStartup()`**:
   - LoggerService.Initialize() (déjà présent)
   - ConfigService.EnsureDirectories() + Load() (déjà présent)
   - **Créer et afficher le TrayIcon**
     - Connecter `QuitRequested` à `Shutdown()`
   - **Créer et enregistrer le HotKeyManager** (décommenter les 3 lignes)
     - Connecter `HotKeyPressed` à `OnHotKeyPressed`
   - **Pré-chauffer le cache emoji** → `EmojiMatcher.GetMatches("")` pour charger la DB au startup

2. **`OnHotKeyPressed()`** — nouvelle logique:
   - Fermer l'overlay précédent s'il est encore ouvert (double-trigger protection)
   - `Dispatcher.Invoke(async () => ...)`:
     - Créer `SelectionHandler`
     - Créer `OverlayWindow(selectionHandler)`
     - S'abonner à `Closed` → si `SelectedEmoji` existe, `ClipboardService.SetText()` + `InputSimulator.SendCtrlV()`
     - `await overlay.InitializeAsync()`
     - `overlay.Show()` + `overlay.Activate()`

3. **`OnExit()`** — garder tel quel, ajouter `_overlay?.Close()` avant le shutdown

**Vérification:** `dotnet build` — compile sans erreur.

---

### Étape 5 : Build + Tests

**Commandes:**
```bash
cd ~/projects/emoji-overlay/EmojiPick
dotnet restore
dotnet build -c Release
dotnet test
```

**Attendu:**
- Zero erreurs de compilation
- `FuzzyMatcherTests`, `EmojiMatcherTests`, `OllamaMatcherTests` passent tous
- Binaire dans `bin/Release/net7.0-windows/EmojiPick.exe`

---

### Étape 6 : Tests manuels end-to-end (sur Windows)

**Scénario:**
1. Lancer `EmojiPick.exe` → icône tray visible
2. Double-cliquer tray → messagebox About
3. Ouvrir Bloc-Notes, taper un texte, le sélectionner
4. `Ctrl+Alt+E` → overlay s'affiche avec emoji matchés
5. Navigation flèches → sélection visuelle change
6. `Enter` → emoji injecté dans Bloc-Notes
7. `Escape` → overlay fermé sans action
8. Taper dans la search box → résultats filtrés
9. Clic droit tray → Quit → app fermée

---

## Résumé des modifications

| Fichier | Action | Lignes estimées |
|---------|--------|-----------------|
| `Services/TrayIcon.cs` | Réécriture complète | ~13 → ~80 |
| `Windows/OverlayWindow.xaml` | Nettoyage layout | 44 → ~36 |
| `Windows/OverlayWindow.xaml.cs` | Réécriture complète | ~13 → ~200 |
| `App.xaml.cs` | Décommenter + wiring + nouvelle méthode | ~95 → ~110 |

**Zero modifications sur les 10+ fichiers de services existants** — ils sont utilisés tels quels.
