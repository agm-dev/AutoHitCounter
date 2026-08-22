//

using System.Collections.Generic;

namespace AutoHitCounter.Models;

/// <summary>
/// Multirun setup (games, abbreviations and styling) plus the progress of the current multirun.
/// Saved as a whole so the overlay comes back exactly as it was left.
/// </summary>
public class MultirunConfig
{
    public bool Enabled { get; set; }

    public List<MultirunEntry> Entries { get; set; } = new List<MultirunEntry>();

    /// <summary>Index of the game currently being run, or -1 when no game is marked as current.</summary>
    public int CurrentIndex { get; set; } = -1;

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
        Entries = new List<MultirunEntry>(),
        CurrentIndex = -1,

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
        clone.Entries = new List<MultirunEntry>();
        foreach (var entry in Entries)
            clone.Entries.Add(new MultirunEntry
            {
                GameName = entry.GameName,
                Abbreviation = entry.Abbreviation,
                Status = entry.Status
            });
        return clone;
    }
}
