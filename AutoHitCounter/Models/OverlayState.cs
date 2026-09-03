// 

using System.Collections.Generic;

namespace AutoHitCounter.Models;

public class OverlayState
{
    public List<OverlaySplit> Splits { get; set; } = new List<OverlaySplit>();
    public int TotalHits { get; set; }
    
    public int TotalDiff { get; set; }
    public int TotalPb { get; set; }
    public string InGameTime { get; set; }

    /// <summary>
    /// The in-game time is counted from a point the player pinned rather than from the start of the save,
    /// so it will not match the clock in the game. Overlays that show the IGT should say so.
    /// </summary>
    public bool IsIgtRelative { get; set; }
    public bool IsRunComplete { get; set; }
    public int AttemptCount { get; set; }
    public int DistancePb { get; set; } = -1;
    public string GameName { get; set; }
    public string ProfileName { get; set; }
}

public class OverlaySplit
{
    public string Name { get; set; }
    public int Hits { get; set; }
    public int Pb { get; set; }
    public int Diff { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsParent { get; set; }
}