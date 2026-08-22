//

using System;

namespace AutoHitCounter.ViewModels;

/// <summary>One row of the Twitch category table: a game and the category it should switch to.</summary>
public class TwitchCategoryMappingViewModel : BaseViewModel
{
    private readonly Action<string, string> _onCategoryChanged;
    private string _categoryName;

    public string GameName { get; }

    public string CategoryName
    {
        get => _categoryName;
        set
        {
            if (!SetProperty(ref _categoryName, value)) return;
            _onCategoryChanged?.Invoke(GameName, value);
        }
    }

    public TwitchCategoryMappingViewModel(string gameName, string categoryName,
        Action<string, string> onCategoryChanged)
    {
        GameName = gameName;
        _categoryName = categoryName;
        _onCategoryChanged = onCategoryChanged;
    }
}
