//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using AutoHitCounter.Core;
using AutoHitCounter.Enums;
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
        RandomizeCommand = new DelegateCommand(() => _multirunService.Randomize(), () => IsGamesMode);
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

    /// <summary>Games that can still be added to a multirun of several games.</summary>
    public ObservableCollection<Game> AvailableGames { get; } = new();

    /// <summary>Every game known to the app, to pick the one a same game multirun cycles through.</summary>
    public ObservableCollection<Game> AllGames { get; } = new();

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

    private bool _isCyclesMode;

    // Both modes are the two halves of one flag, so only a radio button being checked says anything:
    // acting on the unchecked one as well would have each of them undoing the other for ever.

    /// <summary>The multirun is several cycles (NG, NG+1...) of a single game instead of a list of games.</summary>
    public bool IsCyclesMode
    {
        get => _isCyclesMode;
        set
        {
            if (value) SetMode(cycles: true);
        }
    }

    public bool IsGamesMode
    {
        get => !_isCyclesMode;
        set
        {
            if (value) SetMode(cycles: false);
        }
    }

    private bool _keepProgressWithFailedGames;

    /// <summary>The multirun is being practised: a game that took hits no longer starts it over.</summary>
    public bool KeepProgressWithFailedGames
    {
        get => _keepProgressWithFailedGames;
        set
        {
            if (!SetProperty(ref _keepProgressWithFailedGames, value)) return;
            Apply();
        }
    }

    private Game _cycleGame;

    public Game CycleGame
    {
        get => _cycleGame;
        set
        {
            if (_isLoading) return;
            if (!SetProperty(ref _cycleGame, value)) return;
            if (!IsCyclesMode) return;

            RegenerateCycleEntries();
            Apply();
        }
    }

    private int _cycleCount;

    public int CycleCount
    {
        get => _cycleCount;
        set
        {
            if (_isLoading) return;

            var clamped = Math.Max(MultirunConfig.MinCycleCount, Math.Min(MultirunConfig.MaxCycleCount, value));
            if (!SetProperty(ref _cycleCount, clamped)) return;
            if (!IsCyclesMode) return;

            RegenerateCycleEntries();
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
        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            var config = _multirunService.Config;

            _isEnabled = config.Enabled;
            _isCyclesMode = config.Mode == MultirunMode.Cycles;
            _keepProgressWithFailedGames = config.KeepProgressWithFailedGames;
            _cycleCount = config.CycleCount;
            _fontFamily = config.FontFamily;
            _fontSize = config.FontSize;
            _fontBold = config.FontBold;
            _spacing = config.Spacing;
            _backgroundOpacity = config.BackgroundOpacity;
            _baseColor = config.BaseColor;
            _completedColor = config.CompletedColor;
            _hitColor = config.HitColor;
            _currentBorderColor = config.CurrentBorderColor;

            var selectedId = SelectedEntry?.Id;

            LoadEntries(config.Entries);

            SelectedEntry = Entries.FirstOrDefault(e => e.Id == selectedId);

            OnPropertyChanged(string.Empty);
        }
        finally
        {
            _isLoading = wasLoading;
        }

        RandomizeCommand.RaiseCanExecuteChanged();
    }

    private void LoadEntries(IEnumerable<MultirunEntry> entries)
    {
        foreach (var entry in Entries)
            entry.Dispose();
        Entries.Clear();

        var cycleIndex = 0;
        foreach (var entry in entries)
        {
            Entries.Add(new MultirunEntryViewModel(
                entry.Id,
                entry.GameName,
                IsCyclesMode ? _multirunService.GetDefaultCycleAbbreviation(cycleIndex) : entry.GameName,
                entry.Abbreviation,
                Apply));
            cycleIndex++;
        }
    }

    /// <summary>
    /// Rebuilds the game lists. Emptying a list drops the selection of the combo box bound to it, which a two way
    /// binding writes straight back, so the whole thing counts as loading: a refresh must never change the multirun.
    /// </summary>
    private void RefreshAvailableGames()
    {
        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            var selectedGameName = SelectedAvailableGame?.GameName;
            var cycleGameName = _cycleGame?.GameName ?? _multirunService.Config.CycleGameName;

            var games = _gameModuleFactory.GetRegisteredGames()
                .Concat(_customGameService.Load())
                .ToList();

            AllGames.Clear();
            foreach (var game in games)
                AllGames.Add(game);

            // A game can only be added once to a multirun of several games; the cycles of a single game
            // are picked from the full list instead.
            AvailableGames.Clear();
            foreach (var game in games.Where(game => Entries.All(e => !string.Equals(e.GameName, game.GameName,
                         StringComparison.OrdinalIgnoreCase))))
                AvailableGames.Add(game);

            SelectedAvailableGame = AvailableGames.FirstOrDefault(g => g.GameName == selectedGameName);

            // The game instances are rebuilt on every read, so the selection is re-resolved by name.
            _cycleGame = AllGames.FirstOrDefault(g =>
                string.Equals(g.GameName, cycleGameName, StringComparison.OrdinalIgnoreCase));
            OnPropertyChanged(nameof(CycleGame));
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void SetMode(bool cycles)
    {
        if (_isCyclesMode == cycles) return;

        _isCyclesMode = cycles;
        OnPropertyChanged(nameof(IsCyclesMode));
        OnPropertyChanged(nameof(IsGamesMode));

        if (_isLoading) return;

        RebuildEntriesForMode();
        Apply();
        RefreshAvailableGames();
        RandomizeCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Swaps the entry list for the setup of the mode being switched to.</summary>
    private void RebuildEntriesForMode()
    {
        if (IsCyclesMode)
        {
            // Dropped first so the cycles are built from scratch instead of inheriting the list of games.
            LoadEntries(Array.Empty<MultirunEntry>());
            RegenerateCycleEntries();
        }
        else
        {
            LoadEntries(_multirunService.Config.GameEntries);
        }
    }

    /// <summary>
    /// Rebuilds the list of cycles. Growing or shrinking it keeps the entries that stay (and so their progress),
    /// while picking another game starts a different multirun altogether.
    /// </summary>
    private void RegenerateCycleEntries()
    {
        var gameName = CycleGame?.GameName;
        if (string.IsNullOrWhiteSpace(gameName))
        {
            LoadEntries(Array.Empty<MultirunEntry>());
            return;
        }

        var reusable = Entries.All(e => string.Equals(e.GameName, gameName, StringComparison.OrdinalIgnoreCase))
            ? Entries.ToList()
            : new List<MultirunEntryViewModel>();

        var rebuilt = new List<MultirunEntry>();
        for (var i = 0; i < CycleCount; i++)
        {
            var existing = i < reusable.Count ? reusable[i] : null;
            rebuilt.Add(new MultirunEntry
            {
                Id = existing?.Id ?? Guid.NewGuid().ToString(),
                GameName = gameName,
                Abbreviation = existing?.Abbreviation ?? _multirunService.GetDefaultCycleAbbreviation(i)
            });
        }

        LoadEntries(rebuilt);
    }

    private void AddGame()
    {
        var game = SelectedAvailableGame;
        if (game == null) return;

        Entries.Add(new MultirunEntryViewModel(
            Guid.NewGuid().ToString(),
            game.GameName,
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
        config.Mode = IsCyclesMode ? MultirunMode.Cycles : MultirunMode.Games;
        config.KeepProgressWithFailedGames = KeepProgressWithFailedGames;
        config.CycleGameName = CycleGame?.GameName;
        config.CycleCount = CycleCount;
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
            Id = e.Id,
            GameName = e.GameName,
            Abbreviation = e.Abbreviation
        }).ToList();

        // The list of games is only rewritten while it is the one on screen, so that it is still there
        // after going to a same game multirun and back.
        if (IsGamesMode)
            config.GameEntries = config.Entries.Select(e => new MultirunEntry
            {
                Id = e.Id,
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
