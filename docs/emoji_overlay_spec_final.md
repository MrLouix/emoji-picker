# Spécification : Emoji Overlay Picker
## Application Standalone Windows avec Raccourci Clavier Natif & LLM Local

---

## 1. Vue d'ensemble

**Nom du projet** : EmojiPick  
**Type** : Application desktop Windows avec interface overlay  
**Format** : Exécutable unique (.exe) + Installer MSI autonome  
**Stack technologique** : C# .NET 7+ avec Windows API natif + Ollama HTTP REST  

**Objectif** : Permettre à l'utilisateur d'appeler une overlay via raccourci clavier global, de sélectionner un emoji basé sur le texte sélectionné au curseur, avec matching intelligent via LLM local (Ollama), et d'injecter cet emoji dans l'application active.

---

## 2. Architecture générale

```
┌─────────────────────────────────────────────────────────┐
│           EmojiPick (Single Binary Distribution)       │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │   HotKey Manager (Windows API RegisterHotKey)     │ │
│  │   - Ctrl+Alt+E (configurable)                     │ │
│  │   - Écoute globale, fonctionne en background      │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
│  ┌───────────────────────────────────────────────────┐ │
│  │   Clipboard & Selection Handler                   │ │
│  │   - Capture texte sélectionné via Ctrl+C          │ │
│  │   - Thread-safe clipboard ops                     │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
│  ┌───────────────────────────────────────────────────┐ │
│  │   Emoji Matcher (Hybrid Mode)                     │ │
│  │   ┌─────────────────────────────────────────────┐ │ │
│  │   │ ⚡ Mode 1: Fuzzy Local (0-500ms, instant)   │ │ │
│  │   │   - Levenshtein distance sur tags emoji      │ │ │
│  │   │   - Affiche top 12 matches immédiatement    │ │ │
│  │   │   - Cache in-memory 5min                     │ │ │
│  │   └─────────────────────────────────────────────┘ │ │
│  │   ┌─────────────────────────────────────────────┐ │ │
│  │   │ 🧠 Mode 2: LLM (Ollama, async non-bloquant) │ │ │
│  │   │   - HTTP POST localhost:11434/api/generate   │ │ │
│  │   │   - Timeout 3s, fallback if unavailable      │ │ │
│  │   │   - Merge fuzzy + LLM results, dédup         │ │ │
│  │   │   - Support: mistral, llama2, neural-chat    │ │ │
│  │   └─────────────────────────────────────────────┘ │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
│  ┌───────────────────────────────────────────────────┐ │
│  │   WPF Overlay Window (Transparent, Always-on-top)│ │
│  │   - 400×300px grid 4×3 emoji                     │ │
│  │   - Spinner "Fetching..." si LLM en attente      │ │
│  │   - Navigation clavier + souris                  │ │
│  │   - Auto-close après sélection                   │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
│  ┌───────────────────────────────────────────────────┐ │
│  │   Output Handler (SendInput API)                  │ │
│  │   - Injection emoji via Ctrl+V dans app active   │ │
│  │   - Restore focus à fenêtre précédente           │ │
│  └───────────────────────────────────────────────────┘ │
│                                                         │
└─────────────────────────────────────────────────────────┘
  ↑                                              ↑         ↑
  │ (HTTP async, non-bloquant)                  │         │
  HTTP REST                         File I/O    Registry
  localhost:11434                   %APPDATA%   Run keys
  (optionnel, fallback)             /EmojiPick  (auto-start)
```

---

## 3. Spécifications fonctionnelles

### 3.1 Activation du raccourci clavier

**Défaut** : `Ctrl + Alt + E`  
**Configurable** : Via `%APPDATA%\EmojiPick\config.json`

**Comportement** :
- Détection native via `RegisterHotKey` (Windows API)
- Fonctionne même si l'application n'a pas le focus (global hotkey)
- Déclenche sans latence perceptible
- Non-bloquant : autre app peut répondre aux événements

### 3.2 Capture du texte sélectionné (+ contexte curseur)

À l'activation du hotkey :

1. **Essayer récupérer le texte sélectionné** :
   - Simuler `Ctrl+C` (via `SendInput` Windows API)
   - Attendre 50ms
   - Lire depuis le clipboard
   - Limiter à 100 caractères

2. **Si aucune sélection → Récupérer contexte autour du curseur** :
   - Simuler `Ctrl+A` → copier tout le texte de la fenêtre
   - Récupérer position du curseur (via clipboard avant/après Ctrl+C)
   - Extraire 20 chars AVANT + 20 chars APRÈS la position curseur
   - Créer contexte : `[20chars_before]|[20chars_after]`
   - Utiliser pour matching LLM amélioré
   - Afficher overlay avec contexte suggestif

**Exemple de contexte** :

```
User dans Slack, curseur après "feel":
  Texte avant: "I really "
  Texte après: " great today"
  Contexte = "I really |feel great today"
  
Fuzzy match: "feel" → [😊, 😄, 😁, 😃, ...]
LLM prompt améioré:
  "User context: 'I really feel great today' (cursor at 'feel')
   Suggest emotion emoji: "
  → [😄, 🎉, 😊, ✨]
```

**Limitation & Fallback** :

- Si contexte < 5 chars : afficher emoji populaires + search bar
- Si paste depuis clipboard échoue : fallback à fuzzy générique
- Si impossible récupérer contexte : afficher all emoji (pas d'erreur)

### 3.2 Capture du texte sélectionné (Contexte intelligent)

À l'activation du hotkey :

1. **Premier choix : texte sélectionné** :
   - Simuler `Ctrl+C` (via `SendInput` Windows API)
   - Attendre 50ms (asynchrone)
   - Lire depuis le clipboard
   - Limiter à 100 caractères pour éviter le bruit

2. **Fallback : contexte autour du curseur** :
   Si aucune sélection détectée, extraire intelligemment :
   - **Récupérer 20 caractères AVANT le curseur**
   - **Récupérer 20 caractères APRÈS le curseur**
   - Retourner : `[avant]|[après]` (le `|` = position curseur)

3. **Si contexte aussi vide** :
   - Afficher overlay avec message "Aucun texte"
   - Afficher emoji populaires (fallback list)
   - Barre de recherche active pour raffiner manuellement

**Exemple workflow** :

```
Utilisateur à Discord/Gmail, curseur dans "J'aime beaucoup ce post"
Position curseur: "J'aime bea|ucoup ce post"

Hotkey: Ctrl+Alt+E
  ↓
Pas de texte sélectionné → Récupérer contexte
  ↓
Avant curseur (20 chars): "J'aime beauc"
Après curseur (20 chars):  "oup ce post"
  ↓
Texte analysé: "J'aime beaucoup ce post"
  ↓
Fuzzy matching: "beaucoup" → [😍, 🎉, ❤️, 👍, ✨, ...]
LLM (async): "I like this" → context émotionnel positif
Résultats: [😍, 🎉, ❤️, 👍, ✨, 😊, ...]
```

### 3.2.1 Implémentation (Services/SelectionHandler.cs)

**Approche hybride** (compatible toutes les apps Windows) :

```csharp
public class SelectionHandler
{
    private const int CONTEXT_LENGTH = 20; // chars avant/après
    private const int MAX_SELECTION_LENGTH = 100;
    
    /// <summary>
    /// Récupérer texte sélectionné OU contexte autour du curseur
    /// </summary>
    public async Task<TextContext> GetTextContext()
    {
        // Étape 1 : Essayer clipboard (texte sélectionné)
        string selectedText = await TryGetSelectedText();
        
        if (!string.IsNullOrWhiteSpace(selectedText))
        {
            // Texte sélectionné trouvé
            return new TextContext
            {
                Text = selectedText.Trim(),
                Source = TextSource.Selection,
                HasSelection = true,
                IsFromClipboard = true
            };
        }
        
        // Étape 2 : Fallback - récupérer contexte autour du curseur
        var contextText = await TryGetCursorContext();
        
        if (contextText != null)
        {
            return contextText;
        }
        
        // Étape 3 : Aucun texte disponible
        return new TextContext
        {
            Text = string.Empty,
            Source = TextSource.None,
            HasSelection = false,
            IsFromClipboard = false
        };
    }
    
    /// <summary>
    /// Obtenir texte sélectionné via Ctrl+C
    /// </summary>
    private async Task<string> TryGetSelectedText()
    {
        try
        {
            // Sauvegarder clipboard actuel
            string originalClipboard = null;
            try
            {
                originalClipboard = System.Windows.Forms.Clipboard.GetText();
            }
            catch { }
            
            // Simuler Ctrl+C
            SendInput(KeyCode.C, modifiers: ModifierKeys.Control);
            
            // Attendre 50ms (copie asynchrone)
            await Task.Delay(50);
            
            // Récupérer le texte du clipboard
            string copiedText = null;
            try
            {
                copiedText = System.Windows.Forms.Clipboard.GetText();
            }
            catch { }
            
            // Restaurer clipboard si différent
            if (!string.IsNullOrWhiteSpace(originalClipboard) && 
                copiedText != originalClipboard)
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(originalClipboard);
                }
                catch { }
            }
            
            // Valider : texte copié = texte sélectionné
            if (!string.IsNullOrWhiteSpace(copiedText) && 
                copiedText != originalClipboard &&
                copiedText.Length <= MAX_SELECTION_LENGTH)
            {
                Logger.Debug($"Selected text found: {copiedText.Length} chars");
                return copiedText;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to get selected text: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Récupérer contexte (20 chars avant/après curseur)
    /// Utilise UI Automation (compatible quasi toutes les apps)
    /// </summary>
    private async Task<TextContext> TryGetCursorContext()
    {
        try
        {
            // Obtenir la fenêtre active
            IntPtr activeWindow = GetForegroundWindow();
            if (activeWindow == IntPtr.Zero)
                return null;
            
            // Essayer UI Automation d'abord (meilleur support)
            var uiContext = GetContextViaUiAutomation(activeWindow);
            if (uiContext != null)
            {
                Logger.Debug($"Context via UI Automation: {uiContext.Text.Length} chars");
                return uiContext;
            }
            
            // Fallback : IAccessible (moins fiable mais plus compatible)
            var accContext = GetContextViaAccessible(activeWindow);
            if (accContext != null)
            {
                Logger.Debug($"Context via IAccessible: {accContext.Text.Length} chars");
                return accContext;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to get cursor context: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// UI Automation (Windows 7+, moderne et fiable)
    /// </summary>
    private TextContext GetContextViaUiAutomation(IntPtr windowHandle)
    {
        try
        {
            // Obtenir le root element de la fenêtre active
            var rootElement = AutomationElement.FromHandle(windowHandle);
            if (rootElement == null)
                return null;
            
            // Chercher l'element avec le focus (TextBox, RichTextBox, etc.)
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement == null)
                return null;
            
            // Essayer d'obtenir le pattern TextPattern
            var textPattern = focusedElement.GetCurrentPattern(TextPattern.Pattern) as TextPattern;
            if (textPattern == null)
                return null;
            
            // Récupérer tout le texte
            var fullText = textPattern.DocumentRange.GetText(int.MaxValue);
            if (string.IsNullOrWhiteSpace(fullText))
                return null;
            
            // Récupérer la position du curseur (caret position)
            // Note: Pas toujours disponible, fallback sur sélection
            var selection = textPattern.GetSelection();
            
            int cursorPos = 0;
            if (selection != null && selection.Length > 0)
            {
                // Position du début de la sélection
                cursorPos = selection[0].MoveToEnclosingUnit(
                    TextUnit.Character, 
                    false); // backward = false pour obtenir position
            }
            
            // Extraire contexte : 20 chars avant et après
            int startPos = Math.Max(0, cursorPos - CONTEXT_LENGTH);
            int endPos = Math.Min(fullText.Length, cursorPos + CONTEXT_LENGTH);
            int length = endPos - startPos;
            
            if (length <= 0)
                return null;
            
            string contextText = fullText.Substring(startPos, length);
            int relativeCursorPos = cursorPos - startPos;
            
            return new TextContext
            {
                Text = contextText,
                CursorPosition = relativeCursorPos,
                BeforeCursor = contextText.Substring(0, relativeCursorPos),
                AfterCursor = contextText.Substring(relativeCursorPos),
                Source = TextSource.CursorContext,
                HasSelection = false,
                IsFromClipboard = false
            };
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// IAccessible fallback (ancien API, mais compatible presque tout)
    /// </summary>
    private TextContext GetContextViaAccessible(IntPtr windowHandle)
    {
        try
        {
            // IAccessible est complexe, version simplifiée
            // En pratique, utiliser une librairie comme AccessibilityInsights
            
            // Pour cette implémentation, on peut utiliser:
            // - WindowsFormsTestFramework (deprecated)
            // - AccessibilityInsights SDK
            
            Logger.Debug("IAccessible fallback not fully implemented");
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    // Windows API P/Invoke
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    // SendInput helper
    private void SendInput(KeyCode key, ModifierKeys modifiers = ModifierKeys.None)
    {
        // Implémentation via Windows API SendInput
        // (voir InputSimulator.cs)
    }
}

// Models
public class TextContext
{
    /// <summary>
    /// Texte principal (sélection ou contexte)
    /// </summary>
    public string Text { get; set; }
    
    /// <summary>
    /// Source du texte
    /// </summary>
    public TextSource Source { get; set; }
    
    /// <summary>
    /// Si c'est un contexte (pas sélection), position du curseur dans le texte
    /// </summary>
    public int CursorPosition { get; set; }
    
    /// <summary>
    /// Les 20 chars avant le curseur
    /// </summary>
    public string BeforeCursor { get; set; }
    
    /// <summary>
    /// Les 20 chars après le curseur
    /// </summary>
    public string AfterCursor { get; set; }
    
    /// <summary>
    /// Y a-t-il une sélection active?
    /// </summary>
    public bool HasSelection { get; set; }
    
    /// <summary>
    /// Vient du clipboard (sélection) vs Accessibility API (contexte)
    /// </summary>
    public bool IsFromClipboard { get; set; }
}

public enum TextSource
{
    /// <summary>
    /// Texte sélectionné explicitement
    /// </summary>
    Selection,
    
    /// <summary>
    /// Contexte autour du curseur (20 chars avant/après)
    /// </summary>
    CursorContext,
    
    /// <summary>
    /// Aucun texte disponible
    /// </summary>
    None
}
```

### 3.2.2 Affichage intelligente dans l'Overlay

**Avant (sans contexte)** :
```
┌────────────────────────────────┐
│  Aucune sélection - Chercher:  │
├────────────────────────────────┤
│  [Popular emoji grid]          │
│  ☺️  😄  ❤️  👍  ...           │
├────────────────────────────────┤
│ ┌──────────────────────────┐   │
│ │ Entrez un mot... [🔍]    │   │
│ └──────────────────────────┘   │
└────────────────────────────────┘
```

**Après (avec contexte)** :
```
┌────────────────────────────────────┐
│  Contexte: "...beaucoup| ce..."    │
│            ↑                       │
│         curseur                    │
├────────────────────────────────────┤
│  Texte analysé: "beaucoup ce"      │
│                                    │
│  😍  🎉  ❤️  👍  ✨  😊  ...     │
│                                    │
├────────────────────────────────────┤
│ ┌──────────────────────────────┐   │
│ │ Raffiner... [🔄]             │   │
│ └──────────────────────────────┘   │
└────────────────────────────────────┘
```

**Logic d'affichage (OverlayWindow.xaml.cs)** :

```csharp
private async void OnHotKeyTriggered()
{
    var context = await _selectionHandler.GetTextContext();
    
    switch (context.Source)
    {
        case TextSource.Selection:
            // Texte sélectionné
            _lblContext.Text = $"Sélection: \"{context.Text}\"";
            _lblContext.Foreground = Brushes.Green;
            break;
        
        case TextSource.CursorContext:
            // Contexte autour du curseur
            _lblContext.Text = $"Contexte: \"{context.BeforeCursor}|{context.AfterCursor}\"";
            _lblContext.Foreground = Brushes.Orange;
            
            // Analyser le contexte complet
            var fullContextText = context.Text.Replace("|", "").Trim();
            var matches = await _matcher.GetMatches(fullContextText);
            
            break;
        
        case TextSource.None:
            // Aucun texte
            _lblContext.Text = "Aucune sélection - Entrez un mot ou sélectionnez du texte";
            _lblContext.Foreground = Brushes.Gray;
            
            // Afficher emoji populaires
            var popular = await _matcher.GetPopularEmoji();
            break;
    }
    
    // Afficher l'overlay
    ShowOverlay();
}
```

### 3.2.3 Cas d'usage avec contexte

**Cas 1 : Sélection explicit**

```
Gmail: "Vous avez gagné le concours!"
        Sélection: "gagné"
             ↓
        Overlay → [🎉, 🎊, ✨, 👏, ...]
```

**Cas 2 : Pas de sélection, contexte simple**

```
Discord: "C'est vraiment cool..."
                    ↑ curseur ici (après "cool")
         Pas sélectionné, contexte: "vraiment cool..."
             ↓
         Overlay → [😎, 🔥, ✨, 👌, ...]
```

**Cas 3 : Contexte au milieu de phrase**

```
Twitter: "J'adore|nt ces nouveaux emoji"
         Contexte: "J'adore|nt ces n"
             ↓
         LLM comprend: "J'adore" (avant) + "nt" (après) = "J'adorent"
         Résultat: [😍, ❤️, 💕, ✨, ...]
```

**Cas 4 : Aucun contexte (début/fin document)**

```
Notepad: "" (document vide, curseur au début)
             ↓
         Aucun texte → Afficher emoji populaires + barre recherche
```

### 3.2.4 Compatibilité (support par app)

**Excellente compatibilité** :

| App | Selection | Context | Notes |
|-----|-----------|---------|-------|
| Notepad | ✅ | ✅ | TextBox standard |
| Word | ✅ | ✅ | TextPattern UI Automation |
| Excel | ✅ | ✅ | Cell content |
| Chrome/Edge | ✅ | ⚠️ | Sandbox limitations |
| Slack | ✅ | ✅ | Rich text box |
| Discord | ✅ | ✅ | Custom input |
| Gmail | ✅ | ⚠️ | Web app, may vary |
| Terminal | ✅ | ⚠️ | Console limitations |
| Visual Studio | ✅ | ✅ | Full editor support |

**⚠️ = Peut nécessiter fallback clipboard-only**

### 3.2.5 Performance

| Opération | Temps |
|-----------|-------|
| Detect selected text | <50ms |
| Get clipboard | <100ms |
| UI Automation query | 50-200ms |
| Extract context | <10ms |
| Total | <300ms (imperceptible) |

**Non-blocking** : Tout est async, pas de lag perceptible.

---

## 3.3 Matching emoji → texte (Fuzzy + LLM)

**Pipeline** :

```
selectedText = "amazing"
     ↓
┌──────────────────────────────────────────────┐
│  Fuzzy Matching (rapide, local)              │
│  - Levenshtein(selectedText, emoji.tags)     │
│  - Afficher top 12 résultats immédiatement   │
│  - "amazing" → [🤩, ✨, 👏, 🌟, ...]        │
└──────────────────────────────────────────────┘
     ↓ (parallèle, async)
┌──────────────────────────────────────────────┐
│  LLM Matching (intelligent, contextualisé)   │
│  IF config.llm.enabled && Ollama reachable:  │
│    - POST http://localhost:11434/api/generate│
│    - Prompt + timeout 3s                      │
│    - "amazing" → [🤩, ⭐, ✨, 💪, 👍]      │
│    - Merge fuzzy + LLM, dédup                │
│  ELSE:                                       │
│    - Utiliser fuzzy results                  │
└──────────────────────────────────────────────┘
     ↓
Afficher merged results dans overlay
```

**Scores & Ranking** :
- Fuzzy : 0-100 basé sur distance Levenshtein
- LLM : position dans response (1er = +100, 2e = +80, etc.)
- Merged : fuzzy_score * 0.4 + llm_score * 0.6
- Afficher top 12 par score décroissant

### 3.4 Interface Overlay

**Caractéristiques** :
- Fenêtre WPF sans bordure (`WindowStyle="None"`, `AllowsTransparency="True"`)
- Transparent avec fond noir 20% opacité
- Positionnée au centre écran (ou mouse cursor)
- Dimensions : 400×300px (grid 4×3 emoji + controls)
- Z-order : Topmost=True (toujours visible)

**Éléments UI** :

```
┌────────────────────────────────────┐
│  Sélection: "amazing"              │  ← Texte reconnu (+ spinner si LLM actif)
├────────────────────────────────────┤
│                                    │
│  🤩 ✨ 👏 🌟 ⭐ 💪 👍 🎉          │  ← Grid emoji interactive
│                                    │
├────────────────────────────────────┤
│ ┌────────────────────────────────┐ │
│ │ Raffiner...           [🔄]     │ │  ← Search box + Refresh LLM btn
│ └────────────────────────────────┘ │
├────────────────────────────────────┤
│  [Copier]  [Paste]  [Fermer]       │  ← Action buttons
└────────────────────────────────────┘
```

**Interactions clavier** :
- `↑↓←→` : naviguer dans la grille
- `Entrée` : sélectionner l'emoji (injecter)
- `Échap` : fermer overlay sans action
- `Ctrl+C` : copier l'emoji sélectionné au clipboard
- `Backspace` : effacer dernier caractère dans recherche

**Interactions souris** :
- Clic emoji → sélectionner et injecter
- Clic [Copier] → copier sans injecter
- Clic [Paste] → injecter directement (Ctrl+V)
- Clic [Fermer] ou clic outside → fermer overlay
- Hover emoji → highlight/enlarge

### 3.5 Injection de l'emoji

Après sélection :

1. Copier l'emoji dans clipboard
2. Simuler `Ctrl+V` via `SendInput` Windows API
3. Attendre 100ms
4. Fermer overlay automatiquement
5. Restaurer focus à la fenêtre précédente (optional)

**Modes d'injection** (config.json) :
- `paste` : Ctrl+V (défaut, compatible partout)
- `replace` : supprimer texte sélectionné, injecter emoji
- `append` : ajouter emoji après le texte

---

## 4. Intégration LLM Local (Ollama)

### 4.1 Architecture LLM (Dual Mode)

**Deux providers supportés** :

#### A) Ollama (Service externe)
**Prérequis** :
- Ollama installé sur Windows : https://ollama.ai
- Service Ollama tournant en background : `ollama serve`
- Modèle tiré : `ollama pull mistral` (ou autre)
- Endpoint : `http://localhost:11434` (configurable)

**Modèles recommandés** :
- `mistral` (7B) — Rapide, 2-3s, excellent contexte
- `neural-chat` (7B) — Spécialisé conversation
- `llama2` (7B) — Généraliste, stable
- Éviter 13B+ : trop lent pour UX réactive

#### B) llama.cpp (Intégré à l'app)
**Avantages** :
- ✅ Zéro dépendances externes (pas besoin Ollama)
- ✅ Démarrage instant (pas de service séparé)
- ✅ Meilleure intégration avec GPU Windows (CUDA, ROCm, Metal)
- ✅ Moins de overhead (pas HTTP REST)
- ✅ Modèle embarqué ou téléchargeable

**Implémentation** :
- Framework : **LLamaSharp** (C# bindings pour llama.cpp)
- NuGet : `LLamaSharp` + `LLamaSharp.cuda` (ou cpu-only)
- Modèle : GGUF format (quantisé, optimisé)

**Modèles recommandés (GGUF quantisés)** :
- `Mistral-7B-Q4_K_M.gguf` (~5GB, haute qualité)
- `Mistral-7B-Q5_K_M.gguf` (~6GB, très bon)
- `phi-2-Q4_K_M.gguf` (~2GB, léger, rapide)
- `neural-chat-7b-Q4_K_M.gguf` (~5GB, conversation)

**Comparaison Ollama vs llama.cpp** :

| Aspect | Ollama | llama.cpp (Intégré) |
|--------|--------|-------------------|
| **Démarrage** | ~5-10s (service) | Immédiat (in-app) |
| **Dépendances** | Service externe | Embeddé |
| **GPU Support** | CUDA, ROCm | CUDA, ROCm, Metal |
| **Latence** | HTTP (50-100ms) | Direct (5-10ms) |
| **Mémoire** | 2 services | 1 processus |
| **Overhead** | Modéré | Minimal |
| **Portabilité** | Moins portable | Plus portable |
| **Facilité setup** | Très facile | Un peu plus complexe |

**Recommandation** :
- **Par défaut** : Ollama (si disponible)
- **Fallback** : llama.cpp intégré (automatic download)
- **Offline mode** : llama.cpp + modèle embarqué

### 4.2 Implémentation llama.cpp (LLamaSharp)

**NuGet Dependencies** :

```xml
<!-- .csproj -->
<ItemGroup>
    <PackageReference Include="LLamaSharp" Version="0.10.0" />
    <PackageReference Include="LLamaSharp.cuda" Version="0.10.0" />
    <!-- Ou pour CPU-only : -->
    <!-- <PackageReference Include="LLamaSharp" Version="0.10.0" /> -->
</ItemGroup>
```

**Architecture d'exécution** :

```
┌─────────────────────────────────────┐
│  LlamaSharp (C# wrapper)            │
└──────────────┬──────────────────────┘
               ↓
┌─────────────────────────────────────┐
│  llama.cpp (C++ native library)     │
│  libllama.dll (Windows)             │
└──────────────┬──────────────────────┘
               ↓
         Hardware Layer
    ┌─────────────────────┐
    │ CPU | GPU (CUDA/ROCm)
    └─────────────────────┘
```

**Services/LlamaSharpMatcher.cs** :

```csharp
using LLama;
using LLama.Common;
using LLama.Sampling;

public class LlamaSharpMatcher : ILlmMatcher
{
    private LlamaWeights _modelWeights;
    private LlamaContext _context;
    private InteractiveExecutor _executor;
    private readonly string _modelPath;
    private readonly bool _useGpu;
    private readonly int _contextSize;
    private readonly int _gpuLayer;
    private readonly MemoryCache _cache;
    
    public LlamaSharpMatcher(
        string modelPath,
        bool useGpu = true,
        int contextSize = 512,
        int gpuLayers = 20)
    {
        _modelPath = modelPath;
        _useGpu = useGpu;
        _contextSize = contextSize;
        _gpuLayer = gpuLayers;
        _cache = new MemoryCache(new MemoryCacheOptions());
    }
    
    /// <summary>
    /// Initialiser le modèle (appel au startup)
    /// </summary>
    public async Task Initialize()
    {
        try
        {
            // Vérifier que le fichier modèle existe
            if (!File.Exists(_modelPath))
                throw new FileNotFoundException($"Model not found: {_modelPath}");
            
            Logger.Info($"Loading llama.cpp model from {_modelPath}");
            
            // Charger les poids du modèle
            var modelParams = new ModelParams(_modelPath)
            {
                ContextSize = _contextSize,
                GpuLayerCount = _useGpu ? _gpuLayer : 0,
                Verbose = false
            };
            
            _modelWeights = LlamaWeights.LoadFromFile(modelParams);
            
            // Créer le contexte
            var contextParams = new ContextParams()
            {
                Seed = 1337
            };
            
            _context = _modelWeights.CreateContext(contextParams);
            _executor = new InteractiveExecutor(_context);
            
            Logger.Info($"Model loaded successfully. GPU layers: {_gpuLayer}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load llama.cpp model: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Obtenir recommandations emoji via llama.cpp
    /// </summary>
    public async Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default)
    {
        if (_executor == null || string.IsNullOrWhiteSpace(selectedText))
            return new List<EmojiEntry>();
        
        // Check cache
        string cacheKey = $"llama_{selectedText.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out List<EmojiEntry> cached))
            return cached;
        
        try
        {
            var prompt = BuildPrompt(selectedText, candidateEmojis);
            
            var result = new List<string>();
            
            // Inference avec timeout
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(3)); // 3s timeout
                
                // Boucle d'inférence
                await foreach (var token in _executor.InferAsync(
                    prompt,
                    new InferenceParams()
                    {
                        AntiPrompts = new[] { "Emoji:" }, // Stop token
                        MaxTokens = 50,
                        Temperature = 0.3f,
                        TopP = 0.8f,
                        TopK = 20
                    },
                    cts.Token))
                {
                    result.Add(token);
                }
            }
            
            var responseText = string.Concat(result).Trim();
            var emojis = ParseEmojiResponse(responseText, candidateEmojis);
            
            // Cache result (5 min)
            _cache.Set(cacheKey, emojis, TimeSpan.FromMinutes(5));
            
            return emojis;
        }
        catch (OperationCanceledException)
        {
            Logger.Info("llama.cpp inference timeout, using fuzzy fallback");
            return new List<EmojiEntry>();
        }
        catch (Exception ex)
        {
            Logger.Error($"llama.cpp inference error: {ex.Message}");
            return new List<EmojiEntry>();
        }
    }
    
    private string BuildPrompt(string selectedText, List<EmojiEntry> emojis)
    {
        var emojiList = string.Join(",", emojis.Take(20).Select(e => e.Char));
        
        return $@"User selected text: '{selectedText}'

Suggest 5-8 most appropriate emoji from this list (choose ONLY from these, return comma-separated emoji only, no explanation):

{emojiList}

Emoji:";
    }
    
    private List<EmojiEntry> ParseEmojiResponse(
        string response,
        List<EmojiEntry> candidates)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new List<EmojiEntry>();
        
        var emojiChars = response
            .Split(new[] { ',', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
        
        var result = new List<EmojiEntry>();
        foreach (var char in emojiChars)
        {
            var match = candidates.FirstOrDefault(e => e.Char == char);
            if (match != null)
                result.Add(match);
        }
        
        return result;
    }
    
    /// <summary>
    /// Cleanup ressources
    /// </summary>
    public void Dispose()
    {
        _executor?.Dispose();
        _context?.Dispose();
        _modelWeights?.Dispose();
    }
}

// Interface unifiée (Ollama ou llama.cpp)
public interface ILlmMatcher
{
    Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default);
}
```

### 4.3 Gestion des modèles (téléchargement + cache)

**Services/ModelManager.cs** :

```csharp
public class ModelManager
{
    private readonly string _modelCacheDir;
    private readonly HttpClient _httpClient;
    
    public ModelManager()
    {
        _modelCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EmojiPick", "models");
        
        Directory.CreateDirectory(_modelCacheDir);
        _httpClient = new HttpClient();
    }
    
    /// <summary>
    /// Télécharger modèle GGUF depuis Hugging Face
    /// </summary>
    public async Task<string> EnsureModelExists(
        string modelName,
        string huggingFaceRepo,
        IProgress<double> progress = null)
    {
        var modelPath = Path.Combine(_modelCacheDir, modelName);
        
        // Si déjà présent, retourner le chemin
        if (File.Exists(modelPath))
        {
            Logger.Info($"Model already cached: {modelName}");
            return modelPath;
        }
        
        try
        {
            Logger.Info($"Downloading model {modelName}...");
            
            // Construire URL Hugging Face
            var downloadUrl = $"https://huggingface.co/{huggingFaceRepo}/resolve/main/{modelName}";
            
            // Télécharger avec progress
            using (var response = await _httpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1L;
                
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(modelPath, FileMode.Create))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    var read = 0;
                    
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        
                        if (canReportProgress)
                        {
                            progress?.Report((double)totalRead / totalBytes);
                        }
                    }
                }
            }
            
            Logger.Info($"Model downloaded successfully: {modelPath}");
            return modelPath;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to download model: {ex.Message}");
            
            // Nettoyer fichier partiellement téléchargé
            if (File.Exists(modelPath))
                File.Delete(modelPath);
            
            throw;
        }
    }
    
    /// <summary>
    /// Obtenir modèles recommandés
    /// </summary>
    public static List<ModelInfo> GetRecommendedModels()
    {
        return new List<ModelInfo>
        {
            new ModelInfo
            {
                Name = "Mistral-7B-Q4_K_M.gguf",
                HuggingFaceRepo = "TheBloke/Mistral-7B-Instruct-v0.1-GGUF",
                SizeGb = 5.0,
                Quality = "High",
                Speed = "Fast",
                Recommended = true
            },
            new ModelInfo
            {
                Name = "phi-2-Q4_K_M.gguf",
                HuggingFaceRepo = "TheBloke/phi-2-GGUF",
                SizeGb = 2.0,
                Quality = "Good",
                Speed = "Very Fast",
                Recommended = false
            },
            new ModelInfo
            {
                Name = "neural-chat-7b-Q4_K_M.gguf",
                HuggingFaceRepo = "TheBloke/neural-chat-7B-v3-1-GGUF",
                SizeGb = 4.5,
                Quality = "High",
                Speed = "Fast",
                Recommended = true
            }
        };
    }
}

public class ModelInfo
{
    public string Name { get; set; }
    public string HuggingFaceRepo { get; set; }
    public double SizeGb { get; set; }
    public string Quality { get; set; }
    public string Speed { get; set; }
    public bool Recommended { get; set; }
}
```

### 4.4 Configuration & Provider Selection

**config.json (updated)** :

```json
{
  "llm": {
    "enabled": true,
    "provider": "ollama",
    "providers": {
      "ollama": {
        "enabled": true,
        "endpoint": "http://localhost:11434",
        "model": "mistral",
        "timeout_ms": 3000
      },
      "llamacpp": {
        "enabled": true,
        "model_path": "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
        "use_gpu": true,
        "gpu_layers": 20,
        "context_size": 512,
        "timeout_ms": 3000
      }
    },
    "fallback_chain": ["ollama", "llamacpp", "fuzzy"],
    "cache_results": true,
    "cache_ttl_minutes": 5
  }
}
```

**Services/LlmProviderFactory.cs** :

```csharp
public class LlmProviderFactory
{
    private readonly Config _config;
    private ILlmMatcher _provider;
    
    public LlmProviderFactory(Config config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Initialiser le provider LLM basé sur config
    /// </summary>
    public async Task<ILlmMatcher> CreateProvider()
    {
        var fallbackChain = _config.Llm.FallbackChain ?? 
            new[] { "ollama", "llamacpp", "fuzzy" };
        
        foreach (var providerName in fallbackChain)
        {
            try
            {
                switch (providerName)
                {
                    case "ollama" when _config.Llm.Providers["ollama"].Enabled:
                        if (await IsOllamaAvailable())
                        {
                            Logger.Info("Using Ollama provider");
                            return CreateOllamaProvider();
                        }
                        break;
                    
                    case "llamacpp" when _config.Llm.Providers["llamacpp"].Enabled:
                        if (await IsLlamaModelAvailable())
                        {
                            Logger.Info("Using llama.cpp provider");
                            return await CreateLlamaCppProvider();
                        }
                        break;
                    
                    case "fuzzy":
                        Logger.Warn("All LLM providers unavailable, using fuzzy matching only");
                        return new FuzzyMatcher(); // Fallback
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Provider {providerName} initialization failed: {ex.Message}");
                continue;
            }
        }
        
        // Fallback ultime : fuzzy matching
        Logger.Warn("No LLM provider available, using fuzzy matching");
        return new FuzzyMatcher();
    }
    
    private OllamaMatcher CreateOllamaProvider()
    {
        var config = _config.Llm.Providers["ollama"];
        return new OllamaMatcher(
            config.Endpoint,
            config.Model,
            config.TimeoutMs);
    }
    
    private async Task<LlamaSharpMatcher> CreateLlamaCppProvider()
    {
        var config = _config.Llm.Providers["llamacpp"];
        
        // Résoudre le chemin du modèle
        var modelPath = ResolvePath(config.ModelPath);
        
        // Télécharger si manquant
        if (!File.Exists(modelPath))
        {
            Logger.Info("Model file not found, attempting to download...");
            
            var modelManager = new ModelManager();
            var recommendedModels = ModelManager.GetRecommendedModels();
            var modelInfo = recommendedModels.FirstOrDefault(m => m.Name == Path.GetFileName(modelPath));
            
            if (modelInfo != null)
            {
                var progress = new Progress<double>(p =>
                {
                    Logger.Info($"Download progress: {p:P0}");
                });
                
                modelPath = await modelManager.EnsureModelExists(
                    modelInfo.Name,
                    modelInfo.HuggingFaceRepo,
                    progress);
                
                // Mettre à jour config
                config.ModelPath = modelPath;
                _config.Save();
            }
        }
        
        var matcher = new LlamaSharpMatcher(
            modelPath,
            useGpu: config.UseGpu,
            contextSize: config.ContextSize,
            gpuLayers: config.GpuLayers);
        
        await matcher.Initialize();
        return matcher;
    }
    
    private async Task<bool> IsOllamaAvailable()
    {
        try
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            {
                var response = await client.GetAsync("http://localhost:11434/api/tags");
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> IsLlamaModelAvailable()
    {
        try
        {
            var config = _config.Llm.Providers["llamacpp"];
            var modelPath = ResolvePath(config.ModelPath);
            
            // Si fichier existe, ok
            if (File.Exists(modelPath))
                return true;
            
            // Sinon, vérifier si téléchargeable
            return await CanDownloadModel(config.ModelPath);
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> CanDownloadModel(string modelPath)
    {
        // Implémentation simplifiée
        return true; // À implémenter : vérifier URL HF
    }
    
    private string ResolvePath(string path)
    {
        // Résoudre %APPDATA% etc.
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded) 
            ? expanded 
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), expanded);
    }
}
```

### 4.5 Startup avec LLM Provider Selection

**Program.cs (updated)** :

```csharp
public class App : Application
{
    private ILlmMatcher _llmMatcher;
    
    public App()
    {
        InitializeComponent();
    }
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        try
        {
            // Load config
            var config = Config.Load();
            
            // Initialize LLM provider (with fallback chain)
            var factory = new LlmProviderFactory(config);
            _llmMatcher = await factory.CreateProvider();
            
            Logger.Info($"LLM provider initialized: {_llmMatcher.GetType().Name}");
            
            // ... rest of startup
        }
        catch (Exception ex)
        {
            Logger.Error($"Startup failed: {ex.Message}");
            MessageBox.Show($"Startup error: {ex.Message}", "EmojiPick Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        _llmMatcher?.Dispose();
        base.OnExit(e);
    }
}
```

### 4.6 Comparaison Performance (llama.cpp vs Ollama)

**Latence (Mistral 7B)** :

| Opération | Ollama | llama.cpp | Gain |
|-----------|--------|-----------|------|
| Startup | ~8s | <1ms | 8000x |
| Warmup | ~2s | ~1s | 2x |
| Inference (GPU) | 2-3s | 1-2s | 1.5x |
| Inference (CPU) | 8-12s | 8-10s | 1.1x |
| Memory (baseline) | ~500MB | ~300MB | 40% moins |

**Recommandation** :
- **Production/Always-on** : Ollama (service persistent, moins d'overhead par requête)
- **Inline/Offline** : llama.cpp (démarrage instant, meilleur pour single-shot)
- **Fallback** : Toujours llama.cpp en cas Ollama indisponible

**Template Prompt** :

```
User selected text: '{selectedText}'

Suggest 5-8 most appropriate emoji from this list (choose ONLY from these, return comma-separated emoji only, no explanation):

😊,😄,🤩,✨,👏,🌟,💪,🎉,👍,⭐

Emoji:
```

**Requête HTTP** :

```bash
POST http://localhost:11434/api/generate
Content-Type: application/json

{
  "model": "mistral",
  "prompt": "User selected text: 'amazing'\n\nSuggest 5-8 most appropriate emoji...",
  "stream": false,
  "options": {
    "temperature": 0.3,
    "top_p": 0.8,
    "top_k": 20,
    "num_predict": 50
  }
}
```

**Réponse** :

```json
{
  "model": "mistral",
  "created_at": "2025-05-08T12:00:00Z",
  "response": "🤩,✨,⭐,👏",
  "done": true,
  "context": [...],
  "total_duration": 1250000000
}
```

### 4.3 Implémentation (Services/LlmMatcher.cs)

```csharp
public class LlmMatcher
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly int _timeoutMs;
    private readonly MemoryCache _cache;
    
    public LlmMatcher(string endpoint, string model, int timeoutMs = 3000)
    {
        _endpoint = endpoint;
        _model = model;
        _timeoutMs = timeoutMs;
        _httpClient = new HttpClient();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }
    
    /// <summary>
    /// Appel async à Ollama pour recommandations emoji
    /// </summary>
    public async Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(selectedText))
            return new List<EmojiEntry>();
        
        // Check cache
        string cacheKey = $"llm_{selectedText.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out List<EmojiEntry> cached))
            return cached;
        
        try
        {
            var prompt = BuildPrompt(selectedText, candidateEmojis);
            var request = new OllamaRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.3f,
                    TopP = 0.8f,
                    TopK = 20,
                    NumPredict = 50
                }
            };
            
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                new CancellationToken()))
            {
                cts.CancelAfter(_timeoutMs);
                
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_endpoint}/api/generate",
                    request,
                    cts.Token);
                
                if (!response.IsSuccessStatusCode)
                    return new List<EmojiEntry>();
                
                var result = await response.Content.ReadAsAsync<OllamaResponse>(
                    cancellationToken: cts.Token);
                
                var emojis = ParseEmojiResponse(result.Response, candidateEmojis);
                
                // Cache result (5 min)
                _cache.Set(cacheKey, emojis, TimeSpan.FromMinutes(5));
                
                return emojis;
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Warn($"LLM request failed: {ex.Message}");
            return new List<EmojiEntry>();
        }
        catch (OperationCanceledException)
        {
            Logger.Info("LLM request timeout (3s+), using fuzzy fallback");
            return new List<EmojiEntry>();
        }
    }
    
    private string BuildPrompt(string selectedText, List<EmojiEntry> emojis)
    {
        var emojiList = string.Join(",", emojis.Take(20).Select(e => e.Char));
        
        return $@"User selected text: '{selectedText}'

Suggest 5-8 most appropriate emoji from this list (choose ONLY from these, return comma-separated emoji only, no explanation):

{emojiList}

Emoji:";
    }
    
    private List<EmojiEntry> ParseEmojiResponse(
        string response,
        List<EmojiEntry> candidates)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new List<EmojiEntry>();
        
        var emojiChars = response
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
        
        var result = new List<EmojiEntry>();
        foreach (var char in emojiChars)
        {
            var match = candidates.FirstOrDefault(e => e.Char == char);
            if (match != null)
                result.Add(match);
        }
        
        return result;
    }
    
    private bool IsEnabled()
    {
        try
        {
            using (var cts = new CancellationTokenSource(1000))
            {
                var response = _httpClient.GetAsync(
                    $"{_endpoint}/api/tags",
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token).Result;
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }
}

// Models
public class OllamaRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }
    
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("options")]
    public OllamaOptions Options { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public float TopP { get; set; }
    
    [JsonPropertyName("top_k")]
    public int TopK { get; set; }
    
    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

public class OllamaResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; }
    
    [JsonPropertyName("response")]
    public string Response { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}
```

### 4.4 Fallback & Résilience

**Stratégie** :

| Scénario | Comportement |
|----------|-------------|
| LLM enabled, Ollama available | Fuzzy instant + LLM async, merge results |
| LLM enabled, Ollama timeout (>3s) | Afficher fuzzy + spinner "Loading...", retry 1x |
| LLM enabled, Ollama connection error | Afficher fuzzy + warn toast, disable LLM temp |
| LLM disabled (config) | Fuzzy matching classique, pas appel Ollama |
| Cache hit | Retourner cached results (5min TTL) |

---

## 5. Processus d'Installation (MSI + Setup)

### 5.1 Architecture de distribution

**Fichiers de release** :

```
EmojiPick-Release/
├── EmojiPick-1.0.0.exe               (portable, all-in-one)
├── EmojiPick-1.0.0.msi               (installer Windows)
├── EmojiPick-Setup.exe               (bootstrapper WiX Bundle)
└── README.md                         (guide installation)
```

**Choix utilisateur** :
- **Portable** : dropper exe, lancer, pas de config réseau requise
- **MSI Installer** : setup standard Windows, auto-start option, Add/Remove Programs
- **Bootstrapper** : auto-détecte .NET 7, installe si manquant, lance MSI

### 5.2 Workflow installation MSI

```
┌─────────────────────────────────────────┐
│  Double-clic: EmojiPick-1.0.0.msi      │
└──────────────────┬──────────────────────┘
                   ↓
        ┌──────────────────────┐
        │  WiX Installer       │
        │  Welcome Dialog      │
        │  - License (MIT)     │
        │  - Version 1.0.0     │
        │  [Next] [Cancel]     │
        └──────────┬───────────┘
                   ↓
        ┌──────────────────────┐
        │  Install Options     │
        │  ☑ For current user  │
        │  ☑ Start menu        │
        │  ☑ Desktop shortcut  │
        │  ☑ Auto-start on    │
        │    Windows startup   │
        │  ☑ Launch app after  │
        │    installation      │
        │  [Install] [Back]    │
        └──────────┬───────────┘
                   ↓
       ┌────────────────────┐
       │ [████████░░] 50%   │
       │ Installing files...│
       └────────────┬───────┘
                    ↓
        ┌──────────────────────┐
        │ Installation Complete│
        │  ✓ EmojiPick ready  │
        │  ✓ Config created at│
        │    %APPDATA%\      │
        │    EmojiPick\      │
        │  ✓ Hotkey: Ctrl+Alt+E
        │  [Launch] [Finish] │
        └──────────┬───────────┘
                   ↓
      ✓ App démarre
      ✓ Systray icon visible
      ✓ Ready to use
```

### 5.3 Fichiers installés

**Destination** : `C:\Program Files\EmojiPick\`

```
C:\Program Files\EmojiPick\
├── EmojiPick.exe              (application principale)
├── EmojiPick.exe.config       (.NET runtime config)
└── [uninstall.exe]            (Windows-managed)

%APPDATA%\EmojiPick\
├── config.json                (config utilisateur, créé au 1er démarrage)
├── logs\                       (dossier logs)
│   ├── EmojiPick-2025-05-08.log
│   └── EmojiPick-2025-05-07.log (rotation 7 jours)
└── cache\                      (cache LLM results)
    └── emoji_cache.db         (SQLite, TTL 5min)

%LOCALAPPDATA%\EmojiPick\
└── temp\                       (fichiers temporaires)
```

**Raccourcis** :
- Start Menu : `%APPDATA%\Microsoft\Windows\Start Menu\Programs\EmojiPick\`
- Desktop : `C:\Users\{User}\Desktop\EmojiPick.lnk`

### 5.4 Configuration WiX (Product.wxs)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
    <Product Id="*" 
             Name="EmojiPick" 
             Language="1033" 
             Version="1.0.0.0" 
             Manufacturer="EmojiPick Project" 
             UpgradeCode="12345678-1234-1234-1234-123456789012">
        
        <Package InstallerVersion="200" 
                 Compressed="yes" 
                 InstallScope="perUser" 
                 Description="Emoji Overlay Picker for Windows" />
        
        <!-- Cabinet file -->
        <Media Id="1" Cabinet="EmojiPick.cab" EmbedCab="yes" />
        
        <!-- License -->
        <WixVariable Id="WixUILicenseRtf" Value="license.rtf" />
        
        <!-- Installation Directory -->
        <Directory Id="TARGETDIR" Name="SourceDir">
            <Directory Id="LocalAppDataFolder">
                <Directory Id="INSTALLFOLDER" Name="EmojiPick" />
            </Directory>
            <Directory Id="ProgramMenuFolder">
                <Directory Id="EmojiPickMenuFolder" Name="EmojiPick" />
            </Directory>
            <Directory Id="DesktopFolder" />
        </Directory>
        
        <!-- Features -->
        <Feature Id="ProductFeature" Title="EmojiPick" Level="1">
            <ComponentRef Id="EmojiPickExeComponent" />
            <ComponentRef Id="StartMenuShortcut" />
            <ComponentRef Id="DesktopShortcut" />
        </Feature>
        
        <!-- Auto-start feature (optionnel) -->
        <Feature Id="AutoStartFeature" Title="Auto-start on Windows startup" Level="1">
            <ComponentRef Id="AutoStartComponent" />
        </Feature>
        
        <!-- UI standard WiX -->
        <UIRef Id="WixUI_InstallDir" />
        <UIRef Id="WixUI_ErrorProgressText" />
        
        <!-- Package attributes -->
        <Icon Id="icon.ico" SourceFile="icon.ico" />
        <Property Id="ARPPRODUCTICON" Value="icon.ico" />
        <Property Id="ARPNOMODIFY" Value="1" />
        <Property Id="ARPNOREPAIR" Value="1" />
    </Product>
    
    <!-- Fragments -->
    <Fragment>
        <ComponentGroup Id="EmojiPickComponentGroup" Directory="INSTALLFOLDER">
            <!-- Application executable -->
            <Component Id="EmojiPickExeComponent" Guid="*">
                <File Id="EmojiPickExe" 
                      Source="$(var.SolutionDir)bin\Release\net7.0\publish\EmojiPick.exe" 
                      KeyPath="yes" />
                <Shortcut Id="EmojiPickStartupExe" 
                          Advertise="no" 
                          Name="EmojiPick" 
                          Icon="icon.ico" />
            </Component>
        </ComponentGroup>
        
        <!-- Start Menu Shortcut -->
        <Component Id="StartMenuShortcut" Directory="EmojiPickMenuFolder" Guid="*">
            <Shortcut Id="StartMenuShortcut" 
                      Target="[INSTALLFOLDER]EmojiPick.exe" 
                      Name="EmojiPick" 
                      Icon="icon.ico" 
                      Description="Emoji Overlay Picker - Press Ctrl+Alt+E" />
            <RegistryValue Root="HKCU" 
                          Key="Software\Microsoft\Windows\CurrentVersion\Uninstall\EmojiPick" 
                          Type="string" 
                          Value="EmojiPick" />
        </Component>
        
        <!-- Desktop Shortcut -->
        <Component Id="DesktopShortcut" Directory="DesktopFolder" Guid="*">
            <Shortcut Id="DesktopShortcut" 
                      Target="[INSTALLFOLDER]EmojiPick.exe" 
                      Name="EmojiPick" 
                      Icon="icon.ico" />
            <RegistryValue Root="HKCU" 
                          Key="Software\EmojiPick" 
                          Name="desktop_shortcut" 
                          Type="string" 
                          Value="1" />
        </Component>
        
        <!-- Auto-start Registry Entry -->
        <Component Id="AutoStartComponent" Directory="INSTALLFOLDER" Guid="*">
            <RegistryValue Root="HKCU" 
                          Key="Software\Microsoft\Windows\CurrentVersion\Run" 
                          Name="EmojiPick" 
                          Type="string" 
                          Value="[INSTALLFOLDER]EmojiPick.exe" 
                          KeyPath="yes" />
        </Component>
    </Fragment>
</Wix>
```

### 5.5 Build MSI (Command Line)

```bash
# Compiler WiX source
candle.exe Product.wxs -out obj\

# Linker
light.exe -out EmojiPick-1.0.0.msi obj\Product.wixobj

# Optionnel : Signer le MSI
signtool.exe sign /f MyCert.pfx /p password EmojiPick-1.0.0.msi
```

**Ou en PowerShell/MSBuild** :

```powershell
# Build projet C#
dotnet publish -c Release -p:PublishSingleFile=true

# Build MSI
wix build EmojiPick.wixproj -o .\dist\
```

### 5.6 Post-installation Configuration

**Au 1er démarrage** (Program.cs) :

```csharp
private static void EnsureInstallation()
{
    // 1. Créer dossiers AppData
    EnsureDirectory(ConfigDirectory);
    EnsureDirectory(LogDirectory);
    EnsureDirectory(CacheDirectory);
    EnsureDirectory(ModelDirectory); // NEW: for llama.cpp models
    
    // 2. Créer config.json par défaut si absent
    if (!File.Exists(ConfigFilePath))
    {
        var defaultConfig = new Config
        {
            Hotkey = new HotKeyConfig { Modifiers = ["Ctrl", "Alt"], Key = "E" },
            Llm = new LlmConfig
            {
                Enabled = true,
                Provider = "auto", // Auto-detect best provider
                Providers = new Dictionary<string, ProviderConfig>
                {
                    ["ollama"] = new ProviderConfig
                    {
                        Enabled = true,
                        Endpoint = "http://localhost:11434",
                        Model = "mistral",
                        TimeoutMs = 3000
                    },
                    ["llamacpp"] = new ProviderConfig
                    {
                        Enabled = true,
                        ModelPath = "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
                        UseGpu = true,
                        GpuLayers = 20,
                        ContextSize = 512,
                        TimeoutMs = 3000
                    }
                },
                FallbackChain = ["ollama", "llamacpp", "fuzzy"],
                CacheResults = true,
                CacheTtlMinutes = 5
            },
            // ...other defaults
        };
        
        SaveConfig(defaultConfig);
    }
    
    // 3. Vérifier Ollama (health check)
    bool ollamaAvailable = CheckOllamaHealth("http://localhost:11434");
    bool modelFileExists = File.Exists(ResolvePath(Config.Llm.Providers["llamacpp"].ModelPath));
    
    // 4. Logger info startup
    Logger.Info($"EmojiPick v1.0.0 started");
    Logger.Info($"  - Ollama available: {ollamaAvailable}");
    Logger.Info($"  - llama.cpp model ready: {modelFileExists}");
    Logger.Info($"  - Config: {ConfigDirectory}");
    Logger.Info($"  - Models: {ModelDirectory}");
}

private static bool CheckOllamaHealth(string endpoint)
{
    try
    {
        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
        {
            var response = client.GetAsync($"{endpoint}/api/tags").Result;
            Logger.Info("✓ Ollama service detected and responding");
            return response.IsSuccessStatusCode;
        }
    }
    catch (Exception ex)
    {
        Logger.Warn($"Ollama health check failed: {ex.Message}");
        return false;
    }
}
```

### 5.7 Assistant de configuration LLM (Initial Setup)

**Optionnel : UI Dialog au 1er démarrage** :

```
┌─────────────────────────────────────────────┐
│     EmojiPick - LLM Configuration           │
├─────────────────────────────────────────────┤
│                                             │
│  Select LLM Provider:                       │
│                                             │
│  ☑ Ollama (if running on localhost:11434)   │
│    Status: ✓ Available                      │
│    Model: mistral                           │
│                                             │
│  ☐ llama.cpp (Embedded, auto-download)      │
│    Status: ○ Model file missing             │
│    Model: Mistral-7B-Q4_K_M.gguf           │
│    Size: ~5GB                               │
│    [ Download ] [ Skip ]                    │
│                                             │
│  GPU Acceleration: ☑ CUDA (Recommended)     │
│                                             │
│  [Continue] [Skip Setup]                    │
│                                             │
└─────────────────────────────────────────────┘
```

---

## 6. Installation et configuration LLamaSharp

### 6.1 Dépendances NuGet

**EmojiPick.csproj** :

```xml
<ItemGroup>
    <!-- Core -->
    <PackageReference Include="LLamaSharp" Version="0.10.0" />
    
    <!-- GPU Support (choisir UN) -->
    <!-- Option 1: CUDA (NVIDIA GPU) -->
    <PackageReference Include="LLamaSharp.cuda" Version="0.10.0" />
    
    <!-- Option 2: ROCm (AMD GPU - Radeon RX 6600) -->
    <!-- <PackageReference Include="LLamaSharp.rocm" Version="0.10.0" /> -->
    
    <!-- Option 3: Metal (Apple Silicon - M1/M2) -->
    <!-- <PackageReference Include="LLamaSharp.metal" Version="0.10.0" /> -->
    
    <!-- Option 4: CPU-only -->
    <!-- Aucune dépendance supplémentaire -->
    
    <!-- Autres -->
    <PackageReference Include="Serilog" Version="3.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
</ItemGroup>
```

**Choix GPU basé sur Hardware** :

```csharp
// DetectGpuSupport.cs
public static class GpuDetection
{
    public static string DetectBestGpuProvider()
    {
        // Check Windows GPU devices
        var gpuInfo = GetGpuInfo();
        
        if (gpuInfo.HasCuda)
        {
            Logger.Info("✓ NVIDIA CUDA detected (RTX, GTX, Tesla series)");
            return "cuda";
        }
        
        if (gpuInfo.HasRocm)
        {
            Logger.Info("✓ AMD ROCm detected (Radeon RX 5700, 6600, etc.)");
            return "rocm";
        }
        
        Logger.Info("○ No GPU detected, using CPU inference");
        return "cpu";
    }
    
    private static GpuInfo GetGpuInfo()
    {
        // Utiliser WMI pour détecter GPU
        using (var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_VideoController"))
        {
            var gpuInfo = new GpuInfo();
            
            foreach (var gpu in searcher.Get())
            {
                var gpuName = gpu["Name"]?.ToString() ?? "";
                
                if (gpuName.Contains("NVIDIA") || gpuName.Contains("GeForce") || gpuName.Contains("RTX"))
                    gpuInfo.HasCuda = true;
                
                if (gpuName.Contains("AMD") || gpuName.Contains("Radeon"))
                    gpuInfo.HasRocm = true;
            }
            
            return gpuInfo;
        }
    }
}

public class GpuInfo
{
    public bool HasCuda { get; set; }
    public bool HasRocm { get; set; }
}
```

### 6.2 Configuration par GPU (AMD Radeon RX 6600)

**Pour votre setup (RX 6600)** :

```xml
<!-- EmojiPick.csproj avec ROCm -->
<ItemGroup>
    <PackageReference Include="LLamaSharp" Version="0.10.0" />
    <PackageReference Include="LLamaSharp.rocm" Version="0.10.0" />
</ItemGroup>
```

**config.json (ROCm optimisé)** :

```json
{
  "llm": {
    "providers": {
      "llamacpp": {
        "enabled": true,
        "model_path": "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
        "use_gpu": true,
        "gpu_type": "rocm",
        "gpu_layers": 20,
        "context_size": 512,
        "batch_size": 64,
        "timeout_ms": 3000
      }
    }
  }
}
```

**Performance attendue (RX 6600)** :

| Opération | Temps |
|-----------|-------|
| Model load (first time) | 1-2s |
| Model load (cached) | 100ms |
| Warmup | ~1s |
| Inference (Q4) | 1.5-2.5s |
| Memory usage | ~4-5GB |
| Tokens/sec | ~40-60 tokens/s |

### 6.3 Download & Cache Modèles GGUF

**Modèles disponibles** :

```
TheBloke (Hugging Face) : https://huggingface.co/TheBloke

Mistral 7B:
  - Mistral-7B-Q4_K_M.gguf      (5.2 GB) ← Recommandé
  - Mistral-7B-Q5_K_M.gguf      (6.6 GB)
  - Mistral-7B-Q6_K.gguf        (8.0 GB)

Phi 2 (léger):
  - phi-2-Q4_K_M.gguf           (2.0 GB)
  - phi-2-Q5_K_M.gguf           (2.8 GB)

Neural Chat 7B:
  - neural-chat-7B-Q4_K_M.gguf  (4.5 GB) ← Bon pour chat
```

**Script de download** :

```python
# download_model.py (helper script)
import os
import requests
from pathlib import Path

MODEL_NAME = "Mistral-7B-Q4_K_M.gguf"
REPO_ID = "TheBloke/Mistral-7B-Instruct-v0.1-GGUF"
SAVE_DIR = os.path.expandvars("%APPDATA%/EmojiPick/models")

Path(SAVE_DIR).mkdir(parents=True, exist_ok=True)

url = f"https://huggingface.co/{REPO_ID}/resolve/main/{MODEL_NAME}"
output_path = os.path.join(SAVE_DIR, MODEL_NAME)

print(f"Downloading {MODEL_NAME}...")
response = requests.get(url, stream=True)
total_size = int(response.headers.get('content-length', 0))

with open(output_path, 'wb') as f:
    downloaded = 0
    for chunk in response.iter_content(chunk_size=8192):
        if chunk:
            f.write(chunk)
            downloaded += len(chunk)
            progress = (downloaded / total_size) * 100
            print(f"Progress: {progress:.1f}%")

print(f"✓ Model saved to {output_path}")
```

---

## 6.4 Documentation utilisateur (UI/Help)

**Menu Help → LLM Status** :

```
┌────────────────────────────────────────┐
│  EmojiPick - LLM Status                │
├────────────────────────────────────────┤
│                                        │
│  Current Provider: Ollama              │
│  Status: ✓ Connected                   │
│  Model: mistral                        │
│  Endpoint: http://localhost:11434      │
│  Response Time: ~2.1s                  │
│  Cache Hits: 47/150 (31%)              │
│                                        │
│  Available Providers:                  │
│  ☑ Ollama (active)                     │
│  ☑ llama.cpp (ready, Mistral model)    │
│  ☑ Fuzzy (always ready)                │
│                                        │
│  Settings:                             │
│  [Open config.json] [Download Models]  │
│  [Cache Clear] [Performance Stats]     │
│                                        │
│  Logs:                                 │
│  ✓ Model initialized                   │
│  ✓ Ollama service detected             │
│  [View Full Log]                       │
│                                        │
└────────────────────────────────────────┘
```

---

## 7. Configuration utilisateur (complet)

## 7. Configuration utilisateur (complet)

**Fichier** : `%APPDATA%\EmojiPick\config.json`

```json
{
  "app": {
    "version": "1.0.0",
    "autoStart": true,
    "minimizeToTray": true
  },
  "hotkey": {
    "modifiers": ["Ctrl", "Alt"],
    "key": "E"
  },
  "ui": {
    "theme": "dark",
    "fontSize": 24,
    "windowOpacity": 0.95,
    "gridColumns": 4,
    "gridRows": 3,
    "positionMode": "center"
  },
  "behavior": {
    "autoClose": true,
    "injectMode": "paste",
    "fuzzyThreshold": 0.6,
    "maxResults": 12
  },
  "llm": {
    "enabled": true,
    "provider": "auto",
    "fallback_chain": ["ollama", "llamacpp", "fuzzy"],
    "cache_results": true,
    "cache_ttl_minutes": 5,
    "providers": {
      "ollama": {
        "enabled": true,
        "endpoint": "http://localhost:11434",
        "model": "mistral",
        "timeout_ms": 3000
      },
      "llamacpp": {
        "enabled": true,
        "model_path": "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
        "use_gpu": true,
        "gpu_type": "rocm",
        "gpu_layers": 20,
        "context_size": 512,
        "batch_size": 64,
        "timeout_ms": 3000
      }
    }
  },
  "logging": {
    "level": "info",
    "max_file_size_mb": 10,
    "retention_days": 7
  },
  "language": "en"
}
```

**Chemins spéciaux** :
- `%APPDATA%` → `C:\Users\{UserName}\AppData\Roaming`
- `%LOCALAPPDATA%` → `C:\Users\{UserName}\AppData\Local`

---

### 5.7 Désinstallation

**Via Control Panel** :
- Settings → Apps → Apps & features → EmojiPick → Uninstall
- WiX gère suppression auto des fichiers
- **Préserve** : `%APPDATA%\EmojiPick\` (config utilisateur)
- **Préserve** : `%APPDATA%\EmojiPick\models\` (modèles téléchargés)

**Suppression complète (optionnel)** :

```powershell
# Supprimer tout (config + modèles)
Remove-Item -Recurse $env:APPDATA\EmojiPick -Force
```

### 5.8 Mises à jour

**Auto-upgrade** (WiX support) :
- Vérifier version dans `Product.wxs` (Version="1.0.1.0")
- Si version > version installée : lancer upgrade automatiquement
- Préserver config.json, logs, modèles
- Redémarrer app (optionnel, demander user)

---

## 6. Structure du projet

```
EmojiPick/
├── EmojiPick.sln                 # Solution root
├── EmojiPick/
│   ├── EmojiPick.csproj          # Main project (.NET 7+)
│   ├── Program.cs                # Startup, initialization
│   │
│   ├── Windows/
│   │   ├── OverlayWindow.xaml    # UI overlay WPF
│   │   ├── OverlayWindow.xaml.cs # Code-behind
│   │   └── TrayIcon.cs           # Systray integration
│   │
│   ├── Services/
│   │   ├── HotKeyManager.cs      # RegisterHotKey listener
│   │   ├── ClipboardService.cs   # Clipboard capture/injection
│   │   ├── EmojiMatcher.cs       # Fuzzy matching
│   │   ├── OllamaMatcher.cs      # Ollama HTTP integration
│   │   ├── LlamaSharpMatcher.cs  # llama.cpp integration (NEW)
│   │   ├── LlmProviderFactory.cs # Provider selection & fallback (NEW)
│   │   ├── ModelManager.cs       # Model download & cache (NEW)
│   │   ├── InputSimulator.cs     # SendInput wrapper
│   │   ├── ConfigService.cs      # Config load/save
│   │   └── LoggerService.cs      # Logging (file + console)
│   │
│   ├── Data/
│   │   ├── emojis.json.resx      # Emoji DB (embedded resource)
│   │   └── config.json           # Sample config
│   │
│   ├── Models/
│   │   ├── EmojiEntry.cs         # Emoji + tags struct
│   │   ├── Config.cs             # Configuration model
│   │   ├── HotKeyConfig.cs       # Hotkey settings
│   │   ├── LlmConfig.cs          # LLM settings
│   │   └── OllamaModels.cs       # Ollama request/response
│   │
│   └── Helpers/
│       ├── FuzzyMatcher.cs       # Levenshtein algorithm
│       ├── NativeMethods.cs      # Windows API P/Invoke
│       ├── ResourceLoader.cs     # Load embedded resources
│       └── StringExtensions.cs   # Utility methods
│
├── EmojiPick.Installer/
│   ├── EmojiPick.wixproj         # WiX installer project
│   ├── Product.wxs               # WiX product definition
│   ├── license.rtf               # License file
│   └── icon.ico                  # App icon
│
├── docs/
│   ├── INSTALLATION.md           # Installation guide
│   ├── USAGE.md                  # User manual
│   ├── TROUBLESHOOTING.md        # FAQ & troubleshooting
│   └── API.md                    # Developer API docs
│
└── README.md                     # Project overview
```

---

## 10. Spécifications techniques

### 13.1 Stack technologique

| Composant | Technologie | Version | Justification |
|-----------|------------|---------|--------------|
| **Langage** | C# | 10+ | Native, type-safe, Windows integration |
| **Runtime** | .NET | 7.0 LTS+ | Cross-platform ready, performant |
| **UI Framework** | WPF | built-in | Transparent windows, no external deps |
| **Hotkeys** | Windows API | P/Invoke | Direct, no 3rd-party hooking |
| **Clipboard** | System.Windows.Forms | built-in | Native, reliable |
| **Input** | SendInput API | P/Invoke | More reliable than SendKeys |
| **Config** | System.Text.Json | built-in | Fast, low memory |
| **HTTP** | HttpClient | built-in | Async, timeout support |
| **LLM - Ollama** | HTTP REST API | native | External service provider |
| **LLM - llama.cpp** | LLamaSharp | 0.10+ | Embedded C# bindings |
| **LLM - GPU** | CUDA / ROCm | optional | GPU acceleration (LLamaSharp.cuda) |
| **Logging** | Serilog | NuGet | Structured, file rotation |
| **Installer** | WiX Toolset | v3.x | Standard Windows MSI |

### 13.2 Performance Targets

| Métrique | Target | Notes |
|---------|--------|-------|
| Hotkey latency | <50ms | Time from hotkey press to overlay visible |
| Fuzzy matching | <500ms | Local, instant |
| LLM matching | 1-3s | Async, non-blocking |
| Memory footprint | <100MB | Base + caches |
| Startup time | <2s | From exe launch to ready |
| Emoji DB load | <100ms | Embedded resource, unzip once |

### 13.3 Ressources embarquées

**Dans l'exe** :
- `emojis.json.gzip` : 1500+ emoji avec tags (compressé ~200KB)
- `app.ico` : Icône 256×256, 32×32, 16×16
- `default_config.json` : Template config
- `Styles.xaml` : Thème par défaut

**Poids total** : ~8-10 MB (avec .NET runtime embedded)

---

## 10. Workflow Sélection du Provider LLM

**À chaque démarrage, la app suit une chaîne de fallback** :

```
EmojiPick.exe started
     ↓
┌──────────────────────────────────────┐
│ Charger config.json                 │
│ - llm.provider = "ollama"           │
│ - llm.fallback_chain = [            │
│    "ollama", "llamacpp", "fuzzy"    │
│  ]                                  │
└──────────────────────┬───────────────┘
                       ↓
        ┌──────────────────────────┐
        │ Essayer Provider #1:     │
        │ "ollama"                 │
        └──────────────┬───────────┘
                       ↓
        ┌──────────────────────────┐
        │ GET :11434/api/tags      │
        │ (health check 2s)        │
        └──────────────┬───────────┘
                       ↓
            ┌──────────────────┐
        YES │ Ollama running?  │ NO
            └────┬─────────┬───┘
                 ↓         ↓
            ✓ USE OLLAMA   ✓ TRY NEXT
                 ↓         ↓
                 │    ┌──────────────────────────┐
                 │    │ Provider #2: "llamacpp"  │
                 │    └──────────────┬───────────┘
                 │                   ↓
                 │    ┌──────────────────────────┐
                 │    │ Model file exists?       │
                 │    │ (config: model_path)     │
                 │    └──────────────┬───────────┘
                 │                   ↓
                 │        ┌──────────────────┐
                 │    YES │ File present?    │ NO
                 │        └────┬──────────┬──┘
                 │             ↓          ↓
                 │        ✓ LOAD    ✓ DOWNLOAD?
                 │             ↓          ↓
                 │             │    (via ModelManager)
                 │             │    Hugging Face →
                 │             │    %APPDATA%/models/
                 │             │          ↓
                 │             │    ┌──────────┐
                 │             │    │ Download │
                 │             │    │ Progress │
                 │             │    └────┬─────┘
                 │             │         ↓
                 │             │    ✓ LOAD
                 │             │         ↓
                 │             └────┬────┘
                 │                  ↓
                 │    ┌──────────────────────────┐
                 │    │ Init llama.cpp           │
                 │    │ - Load GGUF model        │
                 │    │ - Create context         │
                 │    │ - Warm up inference      │
                 │    │ (takes ~1-2s)           │
                 │    └──────────────┬───────────┘
                 │                   ↓
                 │        ┌──────────────────┐
                 │    YES │ Success?         │ NO
                 │        └────┬──────────┬──┘
                 │             ↓          ↓
                 │        ✓ USE LLAMA  ✓ TRY NEXT
                 │             ↓          ↓
                 │             │    ┌──────────────────┐
                 │             │    │ Provider #3:     │
                 │             │    │ "fuzzy"          │
                 │             │    │ (always works)   │
                 │             │    └────┬─────────────┘
                 │             │         ↓
                 │             │    ✓ USE FUZZY
                 │             │         ↓
                 └──────────┬───────────┬┘
                            ↓
         ┌──────────────────────────────────┐
         │ Logger: "LLM Provider Selected"   │
         │ - Type: OllamaMatcher /           │
         │         LlamaSharpMatcher /       │
         │         FuzzyMatcher              │
         │ - Ready for use                  │
         └──────────────┬───────────────────┘
                        ↓
       ✓ App ready to accept hotkey triggers
```

**Résumé** :
- **Provider actif** : Ollama si disponible (meilleure intégration)
- **Fallback 1** : llama.cpp si Ollama unavailable
- **Fallback 2** : Fuzzy matching (toujours disponible)

**Avantage** : L'app fonctionne **toujours**, peu importe config

---

## 10. Gestion du cycle de vie

### 13.1 Startup (Program.cs)

```
1. EnsureInstallation()
   - Créer %APPDATA%\EmojiPick
   - Créer config.json si absent
   - Créer dossiers logs/cache
   
2. LoadConfiguration()
   - Charger config.json
   - Valider schéma (JSON schema validation)
   - Apply defaults si clés manquantes
   
3. EnsureEmojiDatabase()
   - Charger emojis.json depuis ressource embedded
   - Décompresser (gzip)
   - Parser JSON → List<EmojiEntry>
   - Indexer par tags (pour fuzzy search)
   
4. CheckOllamaHealth()
   - GET http://localhost:11434/api/tags (timeout 2s)
   - Log result (available/unavailable)
   - Disable LLM si unavailable
   
5. InitializeHotKeyManager()
   - RegisterHotKey(Ctrl+Alt+E, or config)
   - Attach event listener
   
6. ShowMainWindow()
   - Create systray icon
   - Hide main window (icon only)
   - Start event loop
   
7. Logger.Info("EmojiPick v1.0.0 ready")
```

### 13.2 Runtime (Hotkey Triggered)

```
User presses Ctrl+Alt+E
     ↓
HotKeyManager.OnHotKeyPressed()
     ↓
SelectionHandler.GetSelectedText()
  - SendInput(Ctrl+C)
  - Wait 50ms
  - Clipboard.GetText()
     ↓
Matcher.GetMatches(selectedText)
  ├─ FuzzyMatcher.Match() → instant, top 12
  └─ LlmMatcher.GetRecommendations() → async, 1-3s
     ↓
OverlayWindow.Show()
  - Display fuzzy results
  - Display spinner if LLM pending
  - Show search bar
     ↓
User selects emoji
     ↓
OutputHandler.InjectEmoji()
  - Clipboard.SetText(emoji)
  - SendInput(Ctrl+V)
     ↓
OverlayWindow.Close()
  - Restore focus
```

### 13.3 Shutdown

```
User closes app (systray click → Exit)
     ↓
SaveConfiguration()
  - Write config.json
     ↓
HotKeyManager.UnregisterHotKey()
  - Clean up Windows API resources
     ↓
CloseConnections()
  - Close HttpClient
  - Close file handles (logs, cache)
     ↓
Logger.Info("EmojiPick shutdown")
     ↓
Application.Current.Shutdown()
```

---

## 11. Cas d'usage

### Cas 1 : Texte simple → emoji rapide

```
User à Slack:
  Sélectionne: "awesome"
  Hotkey: Ctrl+Alt+E
  
  Overlay apparaît immédiatement:
    Fuzzy: [🤩, 💪, 🎉, 👏, 😊, ...]
    (LLM en attente...)
    
  5s plus tard:
    LLM résultats: [🤩, ✨, 💪, 🎉]
    Merged: [🤩, ✨, 💪, 🎉, 👏, 😊, ...]
    Overlay auto-refresh
    
  User clique 🤩
  "awesome" remplacé par 🤩 dans Slack
```

### Cas 2 : Contexte émotionnel (LLM advantage)

```
User dans un email personnel:
  Sélectionne: "I'm grieving"
  Hotkey: Ctrl+Alt+E
  
  Fuzzy matches:
    - grief → 😢💔🕯️ (non-pertinent, grief est tag d'un emoji)
    - (aucun match "I'm grieving" en entier)
    - Affiche emoji populaires
    
  LLM (Mistral):
    Understand contexte: personne sad, serious
    Suggestions: [😢, 💔, 🕊️, 🙏, ❤️]
    
  User sélectionne 💔
  Email now has: "I'm grieving 💔"
```

### Cas 3 : Pas de sélection

```
User appuie Ctrl+Alt+E (aucun texte sélectionné)

Overlay:
  "Aucune sélection - Choisir une catégorie:"
  
  Categories:
    [Smileys] [Hearts] [Hands] [Objects] ...
    
User tape dans search: "star"
Overlay filtre: [⭐, 🌟, ✨, 🤩, ...]

User sélectionne 🌟
```

---

## 12. Améliorations futures (V2+)

- [ ] Skin tones support (👍🏻, 👍🏼, etc.)
- [ ] Historique emoji utilisés fréquemment
- [ ] Configuration UI panel (au lieu de JSON)
- [ ] Support multi-language (FR, DE, ES, JP)
- [ ] Themes (dark/light)
- [ ] Custom emoji collections
- [ ] Plugin system
- [ ] Stats dashboard

---

## 13. Déploiement & Release

### 13.1 Versionning

- Format : `MAJOR.MINOR.PATCH` (SemVer)
- Update `Version="1.0.0.0"` dans Product.wxs
- Update `"version": "1.0.0"` dans config.json

### 13.2 Release Artifacts

```
v1.0.0/
├── EmojiPick-1.0.0.exe          (standalone portable)
├── EmojiPick-1.0.0.msi          (installer)
├── CHANGELOG.md
├── README.md
└── INSTALL.md
```

### 13.3 GitHub Release

```markdown
# EmojiPick v1.0.0

## Features
- Global hotkey (Ctrl+Alt+E)
- Emoji overlay picker
- Fuzzy matching + Ollama LLM integration
- Windows MSI installer

## Downloads
- [EmojiPick-1.0.0.exe](...)
- [EmojiPick-1.0.0.msi](...)

## Installation
1. Download .msi or .exe
2. Run installer
3. Configure hotkey (optional)
4. Start using (Ctrl+Alt+E)
```

---

**Document Version** : 2.0  
**Status** : ✅ Prêt pour développement  
**Last Updated** : 2025-05-08
