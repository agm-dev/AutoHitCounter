//

using System.Collections.Generic;
using AutoHitCounter.Enums;

namespace AutoHitCounter.Models;

/// <summary>
/// Multirun setup (games, abbreviations and styling) plus the progress of the current multirun.
/// Saved as a whole so the overlay comes back exactly as it was left.
/// </summary>
public class MultirunConfig
{
    public const int DefaultCycleCount = 7;
    public const int MinCycleCount = 1;
    public const int MaxCycleCount = 20;

    public bool Enabled { get; set; }

    /// <summary>Whether the multirun is a list of different games or several cycles of a single one.</summary>
    public MultirunMode Mode { get; set; } = MultirunMode.Games;

    /// <summary>The games of the multirun in overlay order, with their progress. Built from the setup of the active mode.</summary>
    public List<MultirunEntry> Entries { get; set; } = new List<MultirunEntry>();

    /// <summary>Index of the game currently being run, or -1 when no game is marked as current.</summary>
    public int CurrentIndex { get; set; } = -1;

    // Setup of each mode, kept side by side so switching modes does not throw the other one away //

    /// <summary>The games picked for the Games mode, remembered while the Cycles mode is in use.</summary>
    public List<MultirunEntry> GameEntries { get; set; } = new List<MultirunEntry>();

    /// <summary>The game whose cycles make up the multirun in Cycles mode.</summary>
    public string CycleGameName { get; set; }

    /// <summary>How many cycles of <see cref="CycleGameName"/> the multirun is made of.</summary>
    public int CycleCount { get; set; } = DefaultCycleCount;

    // Styling //

    public string FontFamily { get; set; }
    public int FontSize { get; set; }
    public bool FontBold { get; set; }
    public int Spacing { get; set; }
    public double BackgroundOpacity { get; set; }
    public string BaseColor { get; set; }
    public string CompletedColor { get; set; }
    public string HitColor { get; set; }
    public string CurrentBorderColor { get; set; }

    public static MultirunConfig CreateDefault() => new MultirunConfig
    {
        Enabled = false,
        Mode = MultirunMode.Games,
        Entries = new List<MultirunEntry>(),
        CurrentIndex = -1,
        GameEntries = new List<MultirunEntry>(),
        CycleGameName = null,
        CycleCount = DefaultCycleCount,

        FontFamily = "Segoe UI",
        FontSize = 20,
        FontBold = true,
        Spacing = 12,
        BackgroundOpacity = 0,
        BaseColor = "#e0e0e0",
        CompletedColor = "#00cc66",
        HitColor = "#ff4c4c",
        CurrentBorderColor = "#e0e0e0",
    };

    public MultirunConfig Clone()
    {
        var clone = (MultirunConfig)MemberwiseClone();
        clone.Entries = CloneEntries(Entries);
        clone.GameEntries = CloneEntries(GameEntries);
        return clone;
    }

    private static List<MultirunEntry> CloneEntries(List<MultirunEntry> entries)
    {
        var clone = new List<MultirunEntry>();
        if (entries == null) return clone;

        foreach (var entry in entries)
            clone.Add(new MultirunEntry
            {
                Id = entry.Id,
                GameName = entry.GameName,
                Abbreviation = entry.Abbreviation,
                Status = entry.Status
            });
        return clone;
    }
}
