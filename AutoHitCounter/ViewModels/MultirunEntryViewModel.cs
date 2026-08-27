//

using System;

namespace AutoHitCounter.ViewModels;

public class MultirunEntryViewModel : BaseViewModel
{
    private readonly Action _onAbbreviationChanged;

    public MultirunEntryViewModel(string id, string gameName, string displayName, string abbreviation,
        Action onAbbreviationChanged)
    {
        Id = id;
        GameName = gameName;
        DisplayName = displayName;
        _abbreviation = abbreviation;
        _onAbbreviationChanged = onAbbreviationChanged;
    }

    /// <summary>Identity of the entry, kept so the progress follows it through edits even with repeated games.</summary>
    public string Id { get; }

    public string GameName { get; }

    /// <summary>What the row shows: the game name, or the cycle ("NG+3") in a same game multirun.</summary>
    public string DisplayName { get; }

    private string _abbreviation;

    public string Abbreviation
    {
        get => _abbreviation;
        set
        {
            if (!SetProperty(ref _abbreviation, value)) return;
            _onAbbreviationChanged?.Invoke();
        }
    }
}
