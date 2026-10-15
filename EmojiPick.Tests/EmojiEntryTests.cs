using System.Text.Json;
using EmojiPick.Models;
using Xunit;

namespace EmojiPick.Tests;

public class EmojiEntryTests
{
    [Fact]
    public void Deserialize_ReadsCharFromSequenceProperty()
    {
        // Format of Data/emojis.json.gzip
        const string json = """[{"sequence": "😀", "name": "grinning face", "tags": ["grinning", "face"], "category": "fully-qualified", "unicode": "U+1F600"}]""";

        var entries = JsonSerializer.Deserialize<List<EmojiEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("😀", entries[0].Char);
        Assert.Equal("grinning face", entries[0].Name);
    }
}
