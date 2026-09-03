// 

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoHitCounter.Models;

public class Profile : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string GameName { get; set; }
    public List<SplitEntry> Splits { get; set; }
    public Dictionary<string, bool> GameSettings { get; set; } = new();

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    
    public int AttemptCount { get; set; }
    public int DistancePb { get; set; } = -1;
    public RunState SavedRun { get; set; }

    /// <summary>
    /// Raw in-game time the run is counted from, for runs picked up part way through a save rather than
    /// started from scratch — the entrance of the Elden Ring DLC, say. It belongs to the profile and not
    /// to the run: resetting keeps it, because the player reloads that same save and starts over from the
    /// very same spot. Only starting a new game clears it.
    /// </summary>
    public long IgtOffsetMilliseconds { get; set; }
}