//

using System;
using System.Collections.Generic;
using System.Text.Json;
using AutoHitCounter.Models.Twitch;

namespace AutoHitCounter.Utilities;

/// <summary>
/// Per-game Twitch category overrides, kept as one line of JSON inside settings.txt.
///
/// JSON rather than the comma separated format CustomGames uses: category names are free text and
/// the settings file splits each line on the first '=' only, so any character except a newline is
/// safe, and System.Text.Json never emits one.
/// </summary>
public static class TwitchCategoryStore
{
    public static Dictionary<string, TwitchCategory> Load()
    {
        var raw = SettingsManager.Default.TwitchCategoryMappings;
        if (string.IsNullOrWhiteSpace(raw)) return Empty();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, TwitchCategory>>(raw);
            return parsed == null
                ? Empty()
                : new Dictionary<string, TwitchCategory>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not read the Twitch category mappings");
            return Empty();
        }
    }

    public static void Save(Dictionary<string, TwitchCategory> mappings)
    {
        try
        {
            SettingsManager.Default.TwitchCategoryMappings = JsonSerializer.Serialize(mappings);
            SettingsManager.Default.Save();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not store the Twitch category mappings");
        }
    }

    /// <summary>Keeps a custom game's category when the game is renamed.</summary>
    public static void Rename(string oldName, string newName)
    {
        var mappings = Load();
        if (!mappings.TryGetValue(oldName, out var category)) return;

        mappings.Remove(oldName);
        mappings[newName] = category;
        Save(mappings);
    }

    private static Dictionary<string, TwitchCategory> Empty() =>
        new Dictionary<string, TwitchCategory>(StringComparer.OrdinalIgnoreCase);
}
