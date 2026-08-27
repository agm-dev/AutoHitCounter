//

using AutoHitCounter.Enums;

namespace AutoHitCounter.Models;

/// <summary>
/// One game of a multirun, in the order it is shown on the multirun overlay.
/// </summary>
public class MultirunEntry
{
    /// <summary>Identity of the entry, so the progress survives edits even when a game is in the list more than once.</summary>
    public string Id { get; set; }

    public string GameName { get; set; }
    public string Abbreviation { get; set; }
    public MultirunStatus Status { get; set; }
}
