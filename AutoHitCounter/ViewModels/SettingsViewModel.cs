// 

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AutoHitCounter.Core;
using AutoHitCounter.Enums;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Services;
using AutoHitCounter.Services.Twitch;
using AutoHitCounter.Utilities;
using AutoHitCounter.Views.Windows;

namespace AutoHitCounter.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly OverlaySettingsViewModel _overlaySettingsViewModel;
    private readonly ITwitchAuthService _twitchAuth;

    public event Action OnGameSettingChanged;

    public IReadOnlyList<GameTitle> GameTitles { get; } = EnumExtensions.GetValues<GameTitle>()
        .Where(title => title != GameTitle.DarkSoulsRemastered && title != GameTitle.Manual).ToList();

    private GameTitle _selectedSettingsGame;
    private OverlaySettingsWindow _overlaySettingsWindow;

    public GameTitle SelectedSettingsGame
    {
        get => _selectedSettingsGame;
        set => SetProperty(ref _selectedSettingsGame, value);
    }

    public SettingsViewModel(IStateService stateService, OverlaySettingsViewModel overlaySettingsViewModel,
        ITwitchAuthService twitchAuth = null, ITwitchCategoryService twitchCategory = null)
    {
        _overlaySettingsViewModel = overlaySettingsViewModel;
        SelectedSettingsGame = GameTitle.DarkSouls2;
        stateService.Subscribe(State.AppStart, OnAppStart);
        OpenOverlaySettingsCommand = new DelegateCommand(OpenOverlaySettings);
        IsExternalIntegrationEnabled = SettingsManager.Default.ExternalIntegrationEnabled;
        ExternalIntegrationEndpoint = SettingsManager.Default.ExternalIntegrationEndpointUrl;
        ExternalIntegrationUserId = SettingsManager.Default.ExternalIntegrationUserIdentifier;

        _twitchAuth = twitchAuth;
        ConnectTwitchCommand = new DelegateCommand(ConnectTwitch, () => !IsTwitchConnected);
        DisconnectTwitchCommand = new DelegateCommand(DisconnectTwitch, () => IsTwitchConnected);

        _isTwitchIntegrationEnabled = SettingsManager.Default.TwitchIntegrationEnabled;
        _twitchOnlyWhenLive = SettingsManager.Default.TwitchOnlyWhenLive;
        _twitchClientId = SettingsManager.Default.TwitchClientId;

        if (_twitchAuth != null)
            _twitchAuth.ConnectionChanged += () => OnUiThread(RefreshTwitchConnection);

        if (twitchCategory != null)
            twitchCategory.StatusChanged += status => OnUiThread(() => TwitchStatus = status);
    }


    #region Commands

    public DelegateCommand OpenOverlaySettingsCommand { get; }

    #endregion

    #region Properties

    private bool _isAlwaysOnTopEnabled;

    public bool IsAlwaysOnTopEnabled
    {
        get => _isAlwaysOnTopEnabled;
        set
        {
            if (!SetProperty(ref _isAlwaysOnTopEnabled, value)) return;
            SettingsManager.Default.AlwaysOnTop = value;
            SettingsManager.Default.Save();
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null) mainWindow.Topmost = _isAlwaysOnTopEnabled;
        }
    }

    public IReadOnlyList<NotesDisplayMode> NotesDisplayModes { get; } =
        EnumExtensions.GetValues<NotesDisplayMode>().ToList();

    public IReadOnlyList<ThemeMode> ThemeModes { get; } =
        EnumExtensions.GetValues<ThemeMode>().ToList();

    private ThemeMode _themeMode;

    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (!SetProperty(ref _themeMode, value)) return;
            SettingsManager.Default.ThemeMode = (int)value;
            SettingsManager.Default.Save();

            if (value == ThemeMode.System)
                ThemeService.StartWatchingSystem();
            else
                ThemeService.StopWatchingSystem();

            ThemeService.Apply(value);
        }
    }

    private NotesDisplayMode _notesDisplayMode;

    public NotesDisplayMode NotesDisplayMode
    {
        get => _notesDisplayMode;
        set
        {
            if (!SetProperty(ref _notesDisplayMode, value)) return;
            SettingsManager.Default.NotesDisplayMode = (int)value;
            SettingsManager.Default.Save();
        }
    }
    
    private bool _autoResetOnNewGameStart;

    public bool AutoResetOnNewGameStart
    {
        get => _autoResetOnNewGameStart;
        set
        {
            if (!SetProperty(ref _autoResetOnNewGameStart, value)) return;
            SettingsManager.Default.AutoResetOnNewGameStart = value;
            SettingsManager.Default.Save();
        }
    }


    #region Elden Ring

    private bool _erNoLogo;

    public bool ErNoLogo
    {
        get => _erNoLogo;
        set
        {
            if (!SetProperty(ref _erNoLogo, value)) return;
            SettingsManager.Default.ERNoLogo = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _erStutterFix;

    public bool ERStutterFix
    {
        get => _erStutterFix;
        set
        {
            if (!SetProperty(ref _erStutterFix, value)) return;
            SettingsManager.Default.ERStutterFix = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _erDisableAchievements;

    public bool ERDisableAchievements
    {
        get => _erDisableAchievements;
        set
        {
            if (!SetProperty(ref _erDisableAchievements, value)) return;
            SettingsManager.Default.ERDisableAchievements = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    #endregion

    #region Dark Souls 3

    private bool _ds3NoLogo;

    public bool DS3NoLogo
    {
        get => _ds3NoLogo;
        set
        {
            if (!SetProperty(ref _ds3NoLogo, value)) return;
            SettingsManager.Default.DS3NoLogo = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _ds3StutterFix;

    public bool DS3StutterFix
    {
        get => _ds3StutterFix;
        set
        {
            if (!SetProperty(ref _ds3StutterFix, value)) return;
            SettingsManager.Default.DS3StutterFix = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }
    
    private bool _ds3NoOnlineInvasions;

    public bool DS3NoOnlineInvasions
    {
        get => _ds3NoOnlineInvasions;
        set
        {
            if (!SetProperty(ref _ds3NoOnlineInvasions, value)) return;
            SettingsManager.Default.DS3NoOnlineInvasions = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    #endregion

    #region Sekiro

    private bool _skNoLogo;

    public bool SKNoLogo
    {
        get => _skNoLogo;
        set
        {
            if (!SetProperty(ref _skNoLogo, value)) return;
            SettingsManager.Default.SKNoLogo = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _skNoTutorials;

    public bool SKNoTutorials
    {
        get => _skNoTutorials;
        set
        {
            if (!SetProperty(ref _skNoTutorials, value)) return;
            SettingsManager.Default.SKNoTutorials = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    #endregion

    #region Dark Souls 2

    private bool _ds2NoBabyJump;

    public bool DS2NoBabyJump
    {
        get => _ds2NoBabyJump;
        set
        {
            if (!SetProperty(ref _ds2NoBabyJump, value)) return;
            SettingsManager.Default.DS2NoBabyJump = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _ds2SkipCredits;

    public bool DS2SkipCredits
    {
        get => _ds2SkipCredits;
        set
        {
            if (!SetProperty(ref _ds2SkipCredits, value)) return;
            SettingsManager.Default.DS2SkipCredits = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private bool _ds2DisableDoubleClick;

    public bool DS2DisableDoubleClick
    {
        get => _ds2DisableDoubleClick;
        set
        {
            if (!SetProperty(ref _ds2DisableDoubleClick, value)) return;
            SettingsManager.Default.DS2DisableDoubleClick = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    #endregion

    #region External Integration

    private bool _isExternalIntegrationEnabled;

    public bool IsExternalIntegrationEnabled
    {
        get => _isExternalIntegrationEnabled;
        set
        {
            if (!SetProperty(ref _isExternalIntegrationEnabled, value)) return;
            SettingsManager.Default.ExternalIntegrationEnabled = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private string _externalIntegrationEndpoint;
    public string ExternalIntegrationEndpoint
        {
        get => _externalIntegrationEndpoint;
        set
        {
            if (!SetProperty(ref _externalIntegrationEndpoint, value)) return;
            SettingsManager.Default.ExternalIntegrationEndpointUrl = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    private string _externalIntegrationUserId;
    public string ExternalIntegrationUserId
    {
        get => _externalIntegrationUserId;
        set
        {
            if (!SetProperty(ref _externalIntegrationUserId, value)) return;
            SettingsManager.Default.ExternalIntegrationUserIdentifier = value;
            SettingsManager.Default.Save();
            OnGameSettingChanged?.Invoke();
        }
    }

    #endregion

    #endregion

    #region Twitch Integration

    public DelegateCommand ConnectTwitchCommand { get; }
    public DelegateCommand DisconnectTwitchCommand { get; }

    public ObservableCollection<TwitchCategoryMappingViewModel> TwitchCategories { get; } = new();

    private bool _isTwitchIntegrationEnabled;

    public bool IsTwitchIntegrationEnabled
    {
        get => _isTwitchIntegrationEnabled;
        set
        {
            if (!SetProperty(ref _isTwitchIntegrationEnabled, value)) return;
            SettingsManager.Default.TwitchIntegrationEnabled = value;
            SettingsManager.Default.Save();
        }
    }

    private bool _twitchOnlyWhenLive;

    public bool TwitchOnlyWhenLive
    {
        get => _twitchOnlyWhenLive;
        set
        {
            if (!SetProperty(ref _twitchOnlyWhenLive, value)) return;
            SettingsManager.Default.TwitchOnlyWhenLive = value;
            SettingsManager.Default.Save();
        }
    }

    private string _twitchClientId;

    public string TwitchClientId
    {
        get => _twitchClientId;
        set
        {
            if (!SetProperty(ref _twitchClientId, value)) return;
            SettingsManager.Default.TwitchClientId = value;
            SettingsManager.Default.Save();
        }
    }

    private string _twitchStatus;

    public string TwitchStatus
    {
        get => _twitchStatus;
        private set => SetProperty(ref _twitchStatus, value);
    }

    public bool IsTwitchConnected => _twitchAuth != null && _twitchAuth.IsConnected;

    public string TwitchConnectionText => IsTwitchConnected
        ? $"Connected as {_twitchAuth.BroadcasterLogin}"
        : "Not connected";

    /// <summary>
    /// Rebuilds the category table. Driven by MainViewModel because the game list, custom games
    /// included, lives there.
    /// </summary>
    public void LoadTwitchCategories(IEnumerable<Game> games)
    {
        TwitchCategories.Clear();
        if (games == null) return;

        var overrides = TwitchCategoryStore.Load();

        foreach (var game in games)
        {
            var name = overrides.TryGetValue(game.GameName, out var configured) && configured != null
                ? configured.Name
                : TwitchCategoryMap.ForGameName(game.GameName)?.Name;

            TwitchCategories.Add(new TwitchCategoryMappingViewModel(
                game.GameName, name ?? string.Empty, OnTwitchCategoryChanged));
        }
    }

    private void OnTwitchCategoryChanged(string gameName, string categoryName)
    {
        var overrides = TwitchCategoryStore.Load();
        var trimmed = categoryName?.Trim();
        var fallback = TwitchCategoryMap.ForGameName(gameName);

        // Blank, or back to the built-in value: drop the override so later default changes apply.
        if (string.IsNullOrEmpty(trimmed) ||
            (fallback != null && string.Equals(fallback.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            if (!overrides.Remove(gameName)) return;
            TwitchCategoryStore.Save(overrides);
            return;
        }

        // The id is resolved against Helix the first time this category is actually used.
        overrides[gameName] = new TwitchCategory { Name = trimmed, Id = null };
        TwitchCategoryStore.Save(overrides);
    }

    private async void ConnectTwitch()
    {
        if (_twitchAuth == null) return;

        try
        {
            TwitchStatus = "Asking Twitch for a code...";

            var device = await _twitchAuth.StartDeviceAuthorizationAsync();
            if (device == null)
            {
                TwitchStatus = "Twitch did not return a device code.";
                return;
            }

            var window = new TwitchAuthWindow(device, token => _twitchAuth.AwaitAuthorizationAsync(device, token))
            {
                Owner = Application.Current?.MainWindow
            };

            var authorized = window.ShowDialog() == true;

            TwitchStatus = authorized
                ? $"Connected as {_twitchAuth.BroadcasterLogin}."
                : "Twitch authorization was not completed.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Connecting to Twitch failed");
            TwitchStatus = "Could not reach Twitch - see the log for details.";
        }
        finally
        {
            RefreshTwitchConnection();
        }
    }

    private void DisconnectTwitch()
    {
        _twitchAuth?.Disconnect();
        TwitchStatus = "Disconnected from Twitch.";
        RefreshTwitchConnection();
    }

    private void RefreshTwitchConnection()
    {
        OnPropertyChanged(nameof(IsTwitchConnected));
        OnPropertyChanged(nameof(TwitchConnectionText));
        ConnectTwitchCommand.RaiseCanExecuteChanged();
        DisconnectTwitchCommand.RaiseCanExecuteChanged();
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    #endregion

    #region Private Methods

    private void OnAppStart()
    {
        ApplyERSettings();
        ApplyDS3Settings();
        ApplySKSettings();
        ApplyDS2Settings();

        IsAlwaysOnTopEnabled = SettingsManager.Default.AlwaysOnTop;

        _themeMode = (ThemeMode)SettingsManager.Default.ThemeMode;
        OnPropertyChanged(nameof(ThemeMode));
        ThemeService.Apply(_themeMode);

        _notesDisplayMode = (NotesDisplayMode)SettingsManager.Default.NotesDisplayMode;
        OnPropertyChanged(nameof(NotesDisplayMode));
        
        _autoResetOnNewGameStart = SettingsManager.Default.AutoResetOnNewGameStart;
        OnPropertyChanged(nameof(AutoResetOnNewGameStart));
        
    }


    private void OpenOverlaySettings()
    {
        if (_overlaySettingsWindow != null)
        {
            _overlaySettingsWindow.Activate();
            return;
        }

        _overlaySettingsWindow = new OverlaySettingsWindow { DataContext = _overlaySettingsViewModel };
        _overlaySettingsWindow.Closed += (s, e) => _overlaySettingsWindow = null;
        _overlaySettingsWindow.Show();
    }

    private void ApplyERSettings()
    {
        _erNoLogo = SettingsManager.Default.ERNoLogo;
        OnPropertyChanged(nameof(ErNoLogo));

        _erStutterFix = SettingsManager.Default.ERStutterFix;
        OnPropertyChanged(nameof(ERStutterFix));

        _erDisableAchievements = SettingsManager.Default.ERDisableAchievements;
        OnPropertyChanged(nameof(ERDisableAchievements));
    }

    private void ApplyDS3Settings()
    {
        _ds3NoLogo = SettingsManager.Default.DS3NoLogo;
        OnPropertyChanged(nameof(DS3NoLogo));

        _ds3StutterFix = SettingsManager.Default.DS3StutterFix;
        OnPropertyChanged(nameof(DS3StutterFix)); 
        
        _ds3NoOnlineInvasions = SettingsManager.Default.DS3NoOnlineInvasions;
        OnPropertyChanged(nameof(DS3NoOnlineInvasions));
    }

    private void ApplySKSettings()
    {
        _skNoLogo = SettingsManager.Default.SKNoLogo;
        OnPropertyChanged(nameof(SKNoLogo));

        _skNoTutorials = SettingsManager.Default.SKNoTutorials;
        OnPropertyChanged(nameof(SKNoTutorials));
    }

    private void ApplyDS2Settings()
    {
        _ds2NoBabyJump = SettingsManager.Default.DS2NoBabyJump;
        OnPropertyChanged(nameof(DS2NoBabyJump));

        _ds2SkipCredits = SettingsManager.Default.DS2SkipCredits;
        OnPropertyChanged(nameof(DS2SkipCredits));

        _ds2DisableDoubleClick = SettingsManager.Default.DS2DisableDoubleClick;
        OnPropertyChanged(nameof(DS2DisableDoubleClick));
    }

    #endregion
}