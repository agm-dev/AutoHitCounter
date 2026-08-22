//

using System.Collections.Generic;

namespace AutoHitCounter.Models;

/// <summary>
/// Payload broadcast to the multirun overlay (Multirun.html).
/// </summary>
public class MultirunState
{
    public bool Enabled { get; set; }
    public List<MultirunStateEntry> Entries { get; set; } = new List<MultirunStateEntry>();

    public string FontFamily { get; set; }
    public int FontSize { get; set; }
    public bool FontBold { get; set; }
    public int Spacing { get; set; }
    public double BackgroundOpacity { get; set; }
    public string BaseColor { get; set; }
    public string CompletedColor { get; set; }
    public string HitColor { get; set; }
    public string CurrentBorderColor { get; set; }
}

public class MultirunStateEntry
{
    public string Abbreviation { get; set; }

    /// <summary>"pending", "completed" or "hit".</summary>
    public string Status { get; set; }

    public bool IsCurrent { get; set; }
}
