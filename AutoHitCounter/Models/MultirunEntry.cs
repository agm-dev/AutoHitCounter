//

using AutoHitCounter.Enums;

namespace AutoHitCounter.Models;

/// <summary>
/// One game of a multirun, in the order it is shown on the multirun overlay.
/// </summary>
public class MultirunEntry
{
    public string GameName { get; set; }
    public string Abbreviation { get; set; }
    public MultirunStatus Status { get; set; }
}
