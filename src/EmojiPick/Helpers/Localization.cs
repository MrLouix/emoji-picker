using System.Globalization;

namespace EmojiPick.Helpers;

/// <summary>
/// UI strings in English and French. The language comes from config.json ("en", "fr" or "auto"
/// for the Windows display language); anything else falls back to English.
/// </summary>
public static class Localization
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["Search"] = "Search emoji…",
        ["Refine"] = "Refine or search emoji…",
        ["NoEmoji"] = "No emoji available.",
        ["FromSelection"] = "From selection",
        ["FromCursor"] = "Near the cursor",
        ["SectionSuggestions"] = "SUGGESTIONS",
        ["SectionResults"] = "RESULTS",
        ["SectionRecent"] = "RECENT",
        ["SectionPopular"] = "POPULAR",
        ["NoResultsTitle"] = "No emoji for “{0}”",
        ["NoResultsHint"] = "Try a feeling, an object or an action",
        ["AiRefining"] = "AI refining…",
        ["Copied"] = "Copied to clipboard",
        ["Insert"] = "insert",
        ["Copy"] = "copy",
        ["Clear"] = "Clear (Esc)",
        ["SkinToneTip"] = "Skin tone (Ctrl+T)",
        ["SettingsTip"] = "Settings",

        ["OpenPicker"] = "Open picker",
        ["Settings"] = "Settings…",
        ["StartWithWindows"] = "Start with Windows",
        ["OpenSettingsFile"] = "Open settings file",
        ["OpenLogs"] = "Open logs folder",
        ["About"] = "About EmojiPick",
        ["Quit"] = "Quit",
        ["TrayTip"] = "EmojiPick — {0} to pick an emoji",
        ["Running"] = "Running in background. Press {0} to pick an emoji.",
        ["HotkeyFailed"] = "Could not register {0} — another application may use it. Change it in Settings, or open the picker from this icon.",
        ["AlreadyRunning"] = "EmojiPick is already running — look for its icon in the notification area.",
        ["AboutText"] = "EmojiPick v{0}\n\nPress {1} to open the emoji picker.\nType to search, arrow keys to navigate, Enter to insert,\nCtrl+C to copy (Shift+Enter or right-click: copy and close), Ctrl+T to change the skin tone, Esc to close.\n\nThe selected emoji will be inserted at the cursor position.",

        ["SettingsTitle"] = "EmojiPick settings",
        ["SectionShortcut"] = "Shortcut",
        ["SectionAppearance"] = "Appearance",
        ["SectionBehavior"] = "Behavior",
        ["SectionAi"] = "AI suggestions (Ollama)",
        ["Hotkey"] = "Keyboard shortcut",
        ["PressKey"] = "Press a key…",
        ["Theme"] = "Theme",
        ["ThemeSystem"] = "System",
        ["ThemeDark"] = "Dark",
        ["ThemeLight"] = "Light",
        ["Language"] = "Language",
        ["LanguageAuto"] = "Automatic (Windows)",
        ["ColorEmoji"] = "Color emoji",
        ["SkinTone"] = "Default skin tone",
        ["ToneDefault"] = "Default",
        ["EmojiSize"] = "Emoji size",
        ["Columns"] = "Columns",
        ["Rows"] = "Visible rows",
        ["Position"] = "Open the picker",
        ["PositionCaret"] = "At the text cursor",
        ["PositionCursor"] = "At the mouse pointer",
        ["PositionCenter"] = "In the center of the screen",
        ["InjectMode"] = "Selected emoji",
        ["InjectPaste"] = "Paste into the application",
        ["InjectType"] = "Type it (keeps the clipboard)",
        ["InjectClipboard"] = "Copy to clipboard only",
        ["RestoreClipboard"] = "Restore the clipboard after pasting",
        ["AiEnabled"] = "Re-rank suggestions with a local Ollama model",
        ["Endpoint"] = "Endpoint",
        ["Model"] = "Model",
        ["Save"] = "Save",
        ["Cancel"] = "Cancel",
        ["Saved"] = "Settings saved.",
        ["NeedModifier"] = "Choose at least one modifier (Ctrl, Alt, Shift or Win) for the shortcut.",
    };

    private static readonly Dictionary<string, string> French = new()
    {
        ["Search"] = "Rechercher un emoji…",
        ["Refine"] = "Affiner ou rechercher…",
        ["NoEmoji"] = "Aucun emoji disponible.",
        ["FromSelection"] = "Depuis la sélection",
        ["FromCursor"] = "Près du curseur",
        ["SectionSuggestions"] = "SUGGESTIONS",
        ["SectionResults"] = "RÉSULTATS",
        ["SectionRecent"] = "RÉCENTS",
        ["SectionPopular"] = "POPULAIRES",
        ["NoResultsTitle"] = "Aucun emoji pour « {0} »",
        ["NoResultsHint"] = "Essayez un sentiment, un objet ou une action",
        ["AiRefining"] = "Affinage par l’IA…",
        ["Copied"] = "Copié dans le presse-papiers",
        ["Insert"] = "insérer",
        ["Copy"] = "copier",
        ["Clear"] = "Effacer (Échap)",
        ["SkinToneTip"] = "Teinte de peau (Ctrl+T)",
        ["SettingsTip"] = "Paramètres",

        ["OpenPicker"] = "Ouvrir le sélecteur",
        ["Settings"] = "Paramètres…",
        ["StartWithWindows"] = "Lancer au démarrage de Windows",
        ["OpenSettingsFile"] = "Ouvrir le fichier de configuration",
        ["OpenLogs"] = "Ouvrir le dossier des journaux",
        ["About"] = "À propos d’EmojiPick",
        ["Quit"] = "Quitter",
        ["TrayTip"] = "EmojiPick — {0} pour choisir un emoji",
        ["Running"] = "EmojiPick tourne en arrière-plan. Appuyez sur {0} pour choisir un emoji.",
        ["HotkeyFailed"] = "Impossible d’enregistrer {0} — une autre application l’utilise peut-être. Changez-le dans les paramètres, ou ouvrez le sélecteur depuis cette icône.",
        ["AlreadyRunning"] = "EmojiPick est déjà lancé — son icône se trouve dans la zone de notification.",
        ["AboutText"] = "EmojiPick v{0}\n\nAppuyez sur {1} pour ouvrir le sélecteur d’emoji.\nTapez pour rechercher, flèches pour naviguer, Entrée pour insérer,\nCtrl+C pour copier (Maj+Entrée ou clic droit : copier et fermer), Ctrl+T pour la teinte de peau, Échap pour fermer.\n\nL’emoji choisi est inséré à la position du curseur.",

        ["SettingsTitle"] = "Paramètres d’EmojiPick",
        ["SectionShortcut"] = "Raccourci",
        ["SectionAppearance"] = "Apparence",
        ["SectionBehavior"] = "Comportement",
        ["SectionAi"] = "Suggestions IA (Ollama)",
        ["Hotkey"] = "Raccourci clavier",
        ["PressKey"] = "Appuyez sur une touche…",
        ["Theme"] = "Thème",
        ["ThemeSystem"] = "Système",
        ["ThemeDark"] = "Sombre",
        ["ThemeLight"] = "Clair",
        ["Language"] = "Langue",
        ["LanguageAuto"] = "Automatique (Windows)",
        ["ColorEmoji"] = "Emoji en couleur",
        ["SkinTone"] = "Teinte de peau par défaut",
        ["ToneDefault"] = "Par défaut",
        ["EmojiSize"] = "Taille des emoji",
        ["Columns"] = "Colonnes",
        ["Rows"] = "Lignes visibles",
        ["Position"] = "Ouvrir le sélecteur",
        ["PositionCaret"] = "Au curseur de texte",
        ["PositionCursor"] = "Au pointeur de la souris",
        ["PositionCenter"] = "Au centre de l’écran",
        ["InjectMode"] = "Emoji choisi",
        ["InjectPaste"] = "Coller dans l’application",
        ["InjectType"] = "Le taper (garde le presse-papiers)",
        ["InjectClipboard"] = "Copier dans le presse-papiers seulement",
        ["RestoreClipboard"] = "Restaurer le presse-papiers après le collage",
        ["AiEnabled"] = "Reclasser les suggestions avec un modèle Ollama local",
        ["Endpoint"] = "Adresse",
        ["Model"] = "Modèle",
        ["Save"] = "Enregistrer",
        ["Cancel"] = "Annuler",
        ["Saved"] = "Paramètres enregistrés.",
        ["NeedModifier"] = "Choisissez au moins une touche de modification (Ctrl, Alt, Maj ou Win) pour le raccourci.",
    };

    /// <summary>UI language actually used ("en" or "fr").</summary>
    public static string UiLanguage { get; private set; } = "en";

    /// <summary>Requested language for emoji names (any CLDR code, e.g. "fr").</summary>
    public static string ContentLanguage { get; private set; } = "en";

    public static void Apply(string? configured)
    {
        ContentLanguage = Resolve(configured);
        UiLanguage = ContentLanguage == "fr" ? "fr" : "en";
    }

    /// <summary>"auto"/empty → Windows display language, otherwise the lowercased code.</summary>
    public static string Resolve(string? configured) =>
        string.IsNullOrWhiteSpace(configured) || configured.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant()
            : configured.Trim().ToLowerInvariant();

    public static string T(string key)
    {
        var table = UiLanguage == "fr" ? French : English;
        return table.TryGetValue(key, out var value) ? value
             : English.TryGetValue(key, out var fallback) ? fallback
             : key;
    }

    public static string T(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    internal static IEnumerable<string> MissingFrenchKeys() => English.Keys.Except(French.Keys);
}
