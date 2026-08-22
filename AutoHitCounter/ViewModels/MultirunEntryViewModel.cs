//

using System;

namespace AutoHitCounter.ViewModels;

public class MultirunEntryViewModel : BaseViewModel
{
    private readonly Action _onAbbreviationChanged;

    public MultirunEntryViewModel(string gameName, string abbreviation, Action onAbbreviationChanged)
    {
        GameName = gameName;
        _abbreviation = abbreviation;
        _onAbbreviationChanged = onAbbreviationChanged;
    }

    public string GameName { get; }

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
