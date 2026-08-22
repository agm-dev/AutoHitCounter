//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using AutoHitCounter.Core;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.ViewModels;

public class MultirunSettingsViewModel : BaseViewModel
{
    private readonly IMultirunService _multirunService;
    private readonly IGameModuleFactory _gameModuleFactory;
    private readonly ICustomGameService _customGameService;

    private bool _isLoading;
    private bool _isApplying;

    public MultirunSettingsViewModel(IMultirunService multirunService, IGameModuleFactory gameModuleFactory,
        ICustomGameService customGameService)
    {
        _multirunService = multirunService;
        _gameModuleFactory = gameModuleFactory;
        _customGameService = customGameService;

        AddGameCommand = new DelegateCommand(AddGame, () => SelectedAvailableGame != null);
        RemoveEntryCommand = new DelegateCommand(RemoveEntry, () => SelectedEntry != null);
        MoveEntryUpCommand = new DelegateCommand(MoveEntryUp, CanMoveEntryUp);
        MoveEntryDownCommand = new DelegateCommand(MoveEntryDown, CanMoveEntryDown);
        RandomizeCommand = new DelegateCommand(() => _multirunService.Randomize());
        ResetProgressCommand = new DelegateCommand(() => _multirunService.ResetProgress());
        ResetStyleCommand = new DelegateCommand(ResetStyle);

        _multirunService.Changed += OnMultirunChanged;

        Refresh();
    }

    #region Commands

    public DelegateCommand AddGameCommand { get; }
    public DelegateCommand RemoveEntryCommand { get; }
    public DelegateCommand MoveEntryUpCommand { get; }
    public DelegateCommand MoveEntryDownCommand { get; }
    public DelegateCommand RandomizeCommand { get; }
    public DelegateCommand ResetProgressCommand { get; }
    public DelegateCommand ResetStyleCommand { get; }

    #endregion

    #region Properties

    public IReadOnlyList<string> AvailableFonts { get; } = Fonts.SystemFontFamilies
        .Select(f => f.Source)
        .OrderBy(f => f)
        .ToList();

    public ObservableCollection<Game> AvailableGames { get; } = new();

    public ObservableCollection<MultirunEntryViewModel> Entries { get; } = new();

    private Game _selectedAvailableGame;

    public Game SelectedAvailableGame
    {
        get => _selectedAvailableGame;
        set
        {
            if (!SetProperty(ref _selectedAvailableGame, value)) return;
            AddGameCommand.RaiseCanExecuteChanged();
        }
    }

    private MultirunEntryViewModel _selectedEntry;

    public MultirunEntryViewModel SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetProperty(ref _selectedEntry, value)) return;
            RemoveEntryCommand.RaiseCanExecuteChanged();
            MoveEntryUpCommand.RaiseCanExecuteChanged();
            MoveEntryDownCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
            Apply();
        }
    }

    private string _fontFamily;

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (!SetProperty(ref _fontFamily, value)) return;
            Apply();
        }
    }

    private int _fontSize;

    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (!SetProperty(ref _fontSize, value)) return;
            Apply();
        }
    }

    private bool _fontBold;

    public bool FontBold
    {
        get => _fontBold;
        set
        {
            if (!SetProperty(ref _fontBold, value)) return;
            Apply();
        }
    }

    private int _spacing;

    public int Spacing
    {
        get => _spacing;
        set
        {
            if (!SetProperty(ref _spacing, value)) return;
            Apply();
        }
    }

    private double _backgroundOpacity;

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            if (!SetProperty(ref _backgroundOpacity, value)) return;
            Apply();
        }
    }

    private string _baseColor;

    public string BaseColor
    {
        get => _baseColor;
        set
        {
            if (!SetProperty(ref _baseColor, value)) return;
            Apply();
        }
    }

    private string _completedColor;

    public string CompletedColor
    {
        get => _completedColor;
        set
        {
            if (!SetProperty(ref _completedColor, value)) return;
            Apply();
        }
    }

    private string _hitColor;

    public string HitColor
    {
        get => _hitColor;
        set
        {
            if (!SetProperty(ref _hitColor, value)) return;
            Apply();
        }
    }

    private string _currentBorderColor;

    public string CurrentBorderColor
    {
        get => _currentBorderColor;
        set
        {
            if (!SetProperty(ref _currentBorderColor, value)) return;
            Apply();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>Reloads the games known to the app and the multirun as it currently stands.</summary>
    public void Refresh()
    {
        LoadFromService();
        RefreshAvailableGames();
    }

    public override void Dispose()
    {
        _multirunService.Changed -= OnMultirunChanged;
        base.Dispose();
    }

    #endregion

    #region Private Methods

    private void OnMultirunChanged()
    {
        if (_isApplying) return;
        LoadFromService();
        RefreshAvailableGames();
    }

    private void LoadFromService()
    {
        _isLoading = true;
        try
        {
            var config = _multirunService.Config;

            _isEnabled = config.Enabled;
            _fontFamily = config.FontFamily;
            _fontSize = config.FontSize;
            _fontBold = config.FontBold;
            _spacing = config.Spacing;
            _backgroundOpacity = config.BackgroundOpacity;
            _baseColor = config.BaseColor;
            _completedColor = config.CompletedColor;
            _hitColor = config.HitColor;
            _currentBorderColor = config.CurrentBorderColor;

            var selectedGameName = SelectedEntry?.GameName;

            foreach (var entry in Entries)
                entry.Dispose();
            Entries.Clear();

            foreach (var entry in config.Entries)
                Entries.Add(new MultirunEntryViewModel(entry.GameName, entry.Abbreviation, Apply));

            SelectedEntry = Entries.FirstOrDefault(e => e.GameName == selectedGameName);

            OnPropertyChanged(string.Empty);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RefreshAvailableGames()
    {
        var selectedGameName = SelectedAvailableGame?.GameName;

        var games = _gameModuleFactory.GetRegisteredGames()
            .Concat(_customGameService.Load())
            .Where(game => Entries.All(e => !string.Equals(e.GameName, game.GameName,
                StringComparison.OrdinalIgnoreCase)))
            .ToList();

        AvailableGames.Clear();
        foreach (var game in games)
            AvailableGames.Add(game);

        SelectedAvailableGame = AvailableGames.FirstOrDefault(g => g.GameName == selectedGameName);
    }

    private void AddGame()
    {
        var game = SelectedAvailableGame;
        if (game == null) return;

        Entries.Add(new MultirunEntryViewModel(
            game.GameName,
            _multirunService.GetDefaultAbbreviation(game.GameName),
            Apply));

        Apply();
        RefreshAvailableGames();
    }

    private void RemoveEntry()
    {
        var entry = SelectedEntry;
        if (entry == null) return;

        Entries.Remove(entry);
        entry.Dispose();
        SelectedEntry = null;

        Apply();
        RefreshAvailableGames();
    }

    private void MoveEntryUp() => MoveEntry(-1);

    private void MoveEntryDown() => MoveEntry(1);

    private void MoveEntry(int offset)
    {
        var entry = SelectedEntry;
        if (entry == null) return;

        var index = Entries.IndexOf(entry);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Entries.Count) return;

        Entries.Move(index, target);
        SelectedEntry = entry;
        Apply();
    }

    private bool CanMoveEntryUp() => SelectedEntry != null && Entries.IndexOf(SelectedEntry) > 0;

    private bool CanMoveEntryDown() =>
        SelectedEntry != null && Entries.IndexOf(SelectedEntry) < Entries.Count - 1;

    private void ResetStyle()
    {
        var confirmed = MsgBox.ShowOkCancel(
            "This will restore the default multirun colours and font. Are you sure?",
            "Multirun Reset");
        if (!confirmed) return;

        var defaults = MultirunConfig.CreateDefault();

        _isLoading = true;
        try
        {
            _fontFamily = defaults.FontFamily;
            _fontSize = defaults.FontSize;
            _fontBold = defaults.FontBold;
            _spacing = defaults.Spacing;
            _backgroundOpacity = defaults.BackgroundOpacity;
            _baseColor = defaults.BaseColor;
            _completedColor = defaults.CompletedColor;
            _hitColor = defaults.HitColor;
            _currentBorderColor = defaults.CurrentBorderColor;
            OnPropertyChanged(string.Empty);
        }
        finally
        {
            _isLoading = false;
        }

        Apply();
    }

    /// <summary>Pushes the panel contents to the multirun service, which saves them and updates the overlay.</summary>
    private void Apply()
    {
        if (_isLoading) return;

        var config = _multirunService.Config.Clone();
        config.Enabled = IsEnabled;
        config.FontFamily = FontFamily;
        config.FontSize = FontSize;
        config.FontBold = FontBold;
        config.Spacing = Spacing;
        config.BackgroundOpacity = BackgroundOpacity;
        config.BaseColor = BaseColor;
        config.CompletedColor = CompletedColor;
        config.HitColor = HitColor;
        config.CurrentBorderColor = CurrentBorderColor;
        config.Entries = Entries.Select(e => new MultirunEntry
        {
            GameName = e.GameName,
            Abbreviation = e.Abbreviation
        }).ToList();

        _isApplying = true;
        try
        {
            _multirunService.UpdateConfig(config);
        }
        finally
        {
            _isApplying = false;
        }

        MoveEntryUpCommand.RaiseCanExecuteChanged();
        MoveEntryDownCommand.RaiseCanExecuteChanged();
    }

    #endregion
}
