using System.IO;
using System.Reflection;

namespace EmojiPick.Helpers;

/// <summary>
/// Loads embedded resources from the assembly (emojis.json.gzip, icons, default config).
/// </summary>
public static class ResourceLoader
{
    /// <summary>
    /// Read an embedded resource as a byte array.
    /// Resource names are fully qualified: AssemblyNamespace.Folder.filename.ext
    /// </summary>
    public static byte[]? LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Read an embedded resource as a UTF-8 string.
    /// </summary>
    public static string? LoadEmbeddedResourceAsString(string resourceName)
    {
        var bytes = LoadEmbeddedResource(resourceName);
        if (bytes == null) return null;
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// List all embedded resource names (useful for debugging).
    /// </summary>
    public static string[] ListResources()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceNames();
    }
}
