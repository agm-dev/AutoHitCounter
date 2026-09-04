//

using System;
using System.Collections.Generic;
using System.Linq;
using AutoHitCounter.Enums;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;

namespace AutoHitCounter.Services;

/// <summary>
/// Keeps the list of games of a multirun and the progress through it, and pushes it to the multirun overlay.
/// </summary>
public class MultirunService : IMultirunService
{
    private static readonly Dictionary<string, string> DefaultAbbreviations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dark Souls Remastered"] = "DS1",
            ["Dark Souls"] = "DS1",
            ["Dark Souls 2"] = "DS2",
            ["Dark Souls 3"] = "DS3",
            ["Sekiro"] = "SK",
            ["Elden Ring"] = "ER",
            ["Demon's Souls"] = "DES",
            ["Demons Souls"] = "DES",
            ["Bloodborne"] = "BB",
        };

    private readonly IMultirunStore _store;
    private readonly IOverlayServerService _overlayServerService;
    private readonly Random _random;
    private MultirunConfig _config;

    public MultirunService(IMultirunStore store, IOverlayServerService overlayServerService, Random random = null)
    {
        _store = store;
        _overlayServerService = overlayServerService;
        _random = random ?? new Random();
        _config = Normalize(_store.Load());
    }

    public event Action Changed;

    public bool IsEnabled => _config.Enabled;

    public MultirunConfig Config => _config;

    public IReadOnlyList<MultirunEntry> Entries => _config.Entries;

    public void UpdateConfig(MultirunConfig config)
    {
        if (config == null) return;

        var previousCurrent = CurrentEntry;
        var statusesById = _config.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Id))
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Status, StringComparer.Ordinal);
        var statusesByGame = _config.Entries
            .GroupBy(e => e.GameName ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Status, StringComparer.OrdinalIgnoreCase);

        // The progress is carried over before the ids missing from a hand written setup are filled in,
        // so that those entries can still fall back to being matched by game name.
        var updated = config.Clone();
        updated.Entries ??= new List<MultirunEntry>();
        updated.Entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.GameName));

        // Keep the progress of the games that are still part of the multirun. Entries are matched by id
        // rather than by name, because the same game is in the list several times once the multirun is
        // made of the cycles of a single game.
        foreach (var entry in updated.Entries)
        {
            var hasId = !string.IsNullOrWhiteSpace(entry.Id);
            if (hasId && statusesById.TryGetValue(entry.Id, out var byId))
                entry.Status = byId;
            else if (!hasId && statusesByGame.TryGetValue(entry.GameName, out var byGame))
                entry.Status = byGame;
        }

        if (previousCurrent != null)
        {
            updated.CurrentIndex = updated.Entries.FindIndex(e =>
                !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(previousCurrent.Id)
                    ? e.Id == previousCurrent.Id
                    : Matches(e.GameName, previousCurrent.GameName));
            if (updated.CurrentIndex < 0 && updated.Entries.Count > 0)
                updated.CurrentIndex = 0;
        }
        else
        {
            // No game was current: the multirun was either finished or empty, and only the latter starts one.
            updated.CurrentIndex = _config.Entries.Count == 0 && updated.Entries.Count > 0 ? 0 : -1;
        }

        _config = Normalize(updated);
        SaveAndPublish();
    }

    public void Randomize()
    {
        // The order of the cycles of a game is the run itself, so there is nothing to shuffle.
        if (IsCyclesMode) return;
        if (_config.Entries.Count == 0) return;

        for (var i = _config.Entries.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_config.Entries[i], _config.Entries[j]) = (_config.Entries[j], _config.Entries[i]);
        }

        ClearProgress();
        SaveAndPublish();
    }

    public void ResetProgress()
    {
        if (!IsEnabled) return;

        ClearProgress();
        SaveAndPublish();
    }

    public void SyncHits(string gameName, bool hasHits)
    {
        if (!IsEnabled) return;

        var current = CurrentEntry;
        if (current == null || !Matches(current.GameName, gameName)) return;

        var status = hasHits ? MultirunStatus.Hit : MultirunStatus.Pending;
        if (current.Status == status) return;

        current.Status = status;
        SaveAndPublish();
    }

    public void CompleteGame(string gameName, bool hasHits)
    {
        if (!IsEnabled) return;

        var current = CurrentEntry;
        if (current == null || !Matches(current.GameName, gameName)) return;

        current.Status = hasHits ? MultirunStatus.Hit : MultirunStatus.Completed;
        _config.CurrentIndex = _config.CurrentIndex + 1 < _config.Entries.Count
            ? _config.CurrentIndex + 1
            : -1;

        SaveAndPublish();
    }

    public void OnGameTracked(string gameName)
    {
        if (!IsEnabled) return;

        var index = IndexOfRelevant(gameName);
        if (index < 0) return;

        var currentIndex = _config.CurrentIndex;
        if (index == currentIndex) return;

        if (IsCyclesMode)
        {
            // Cycles are never reordered: tracking the game again only picks the multirun back up,
            // and a finished one starts over.
            if (currentIndex < 0)
            {
                ClearProgress();
                SaveAndPublish();
            }

            return;
        }

        // Moving to another game after taking a hit is a restart of the multirun from that game,
        // unless the games already lost are being kept.
        if (RestartsOnHit)
        {
            RestartFrom(index);
            return;
        }

        if (currentIndex < 0 || index < currentIndex)
        {
            // Nothing to reorder: the multirun simply goes back to (or starts at) that game.
            _config.CurrentIndex = index;
        }
        else
        {
            // A game abandoned with hits stays where it is, in red: a multirun being practised is
            // simply picked back up on the spot behind it.
            var target = _config.KeepProgressWithFailedGames && CurrentEntry?.Status == MultirunStatus.Hit
                ? currentIndex + 1
                : currentIndex;

            var entry = _config.Entries[index];
            _config.Entries.RemoveAt(index);
            _config.Entries.Insert(target, entry);
            _config.CurrentIndex = target;
        }

        SaveAndPublish();
    }

    public void OnNewGameStarted(string gameName)
    {
        if (!IsEnabled) return;

        if (IsCyclesMode)
        {
            HandleCycleRestartSignal(gameName);
            return;
        }

        var index = IndexOf(_config.Entries, gameName);
        if (index < 0) return;
        if (!RestartsOnHit) return;

        RestartFrom(index);
    }

    public void OnRunReset(string gameName)
    {
        if (!IsEnabled) return;

        if (IsCyclesMode)
        {
            HandleCycleRestartSignal(gameName);
            return;
        }

        ResetProgress();
    }

    public void Broadcast() => _overlayServerService?.BroadcastMultirun(BuildState());

    public string GetDefaultAbbreviation(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return "";

        var trimmed = gameName.Trim();
        if (DefaultAbbreviations.TryGetValue(trimmed, out var known)) return known;

        var words = trimmed.Split([' ', '-', '_', ':'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
            return new string(words.Take(4).Select(w => char.ToUpperInvariant(w[0])).ToArray());

        return trimmed.Substring(0, Math.Min(3, trimmed.Length)).ToUpperInvariant();
    }

    public string GetDefaultCycleAbbreviation(int cycleIndex) => cycleIndex <= 0 ? "NG" : $"NG+{cycleIndex}";

    #region Private Methods

    private bool IsCyclesMode => _config.Mode == MultirunMode.Cycles;

    private MultirunEntry CurrentEntry =>
        _config.CurrentIndex >= 0 && _config.CurrentIndex < _config.Entries.Count
            ? _config.Entries[_config.CurrentIndex]
            : null;

    /// <summary>
    /// A game marked with a hit that starts the multirun over. A multirun being practised keeps its
    /// progress instead, so there the mark is only there to be seen on the overlay.
    /// </summary>
    private bool RestartsOnHit =>
        !_config.KeepProgressWithFailedGames && _config.Entries.Any(e => e.Status == MultirunStatus.Hit);

    /// <summary>
    /// A reset or a new game on the game of a cycles multirun. Without a hit it is simply how the next
    /// cycle is started, so the progress is left alone; a hit (or a finished multirun) starts it over.
    /// While the games already lost are being kept, only a finished multirun starts over.
    /// </summary>
    private void HandleCycleRestartSignal(string gameName)
    {
        if (!Matches(_config.CycleGameName, gameName)) return;
        if (!RestartsOnHit && _config.CurrentIndex >= 0) return;

        ClearProgress();
        SaveAndPublish();
    }

    /// <summary>Starts the multirun over with the given game first and current.</summary>
    private void RestartFrom(int index)
    {
        var entry = _config.Entries[index];
        _config.Entries.RemoveAt(index);
        _config.Entries.Insert(0, entry);

        ClearProgress();
        SaveAndPublish();
    }

    private void ClearProgress()
    {
        foreach (var entry in _config.Entries)
            entry.Status = MultirunStatus.Pending;

        _config.CurrentIndex = _config.Entries.Count > 0 ? 0 : -1;
    }

    private void SaveAndPublish()
    {
        _store.Save(_config);
        Broadcast();
        Changed?.Invoke();
    }

    private MultirunState BuildState()
    {
        var currentIndex = _config.CurrentIndex;
        return new MultirunState
        {
            Enabled = _config.Enabled,
            FontFamily = _config.FontFamily,
            FontSize = _config.FontSize,
            FontBold = _config.FontBold,
            Spacing = _config.Spacing,
            BackgroundOpacity = _config.BackgroundOpacity,
            BaseColor = _config.BaseColor,
            CompletedColor = _config.CompletedColor,
            HitColor = _config.HitColor,
            CurrentBorderColor = _config.CurrentBorderColor,
            Entries = _config.Entries.Select((entry, index) => new MultirunStateEntry
            {
                Abbreviation = string.IsNullOrWhiteSpace(entry.Abbreviation)
                    ? GetDefaultAbbreviation(entry.GameName)
                    : entry.Abbreviation,
                Status = entry.Status.ToString().ToLowerInvariant(),
                IsCurrent = index == currentIndex
            }).ToList()
        };
    }

    /// <summary>Fills in anything a hand edited or older settings file may be missing.</summary>
    private static MultirunConfig Normalize(MultirunConfig config)
    {
        var defaults = MultirunConfig.CreateDefault();

        config.Entries ??= new List<MultirunEntry>();
        config.Entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.GameName));

        config.GameEntries ??= new List<MultirunEntry>();
        config.GameEntries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.GameName));

        // Settings files written before entries had an identity, and any hand edited one, get ids here.
        foreach (var entry in config.Entries.Concat(config.GameEntries))
            if (string.IsNullOrWhiteSpace(entry.Id))
                entry.Id = Guid.NewGuid().ToString();

        if (config.CycleCount < MultirunConfig.MinCycleCount) config.CycleCount = MultirunConfig.DefaultCycleCount;
        if (config.CycleCount > MultirunConfig.MaxCycleCount) config.CycleCount = MultirunConfig.MaxCycleCount;

        if (string.IsNullOrWhiteSpace(config.FontFamily)) config.FontFamily = defaults.FontFamily;
        if (config.FontSize <= 0) config.FontSize = defaults.FontSize;
        if (config.Spacing < 0) config.Spacing = defaults.Spacing;
        if (config.BackgroundOpacity < 0 || config.BackgroundOpacity > 1)
            config.BackgroundOpacity = defaults.BackgroundOpacity;
        if (string.IsNullOrWhiteSpace(config.BaseColor)) config.BaseColor = defaults.BaseColor;
        if (string.IsNullOrWhiteSpace(config.CompletedColor)) config.CompletedColor = defaults.CompletedColor;
        if (string.IsNullOrWhiteSpace(config.HitColor)) config.HitColor = defaults.HitColor;
        if (string.IsNullOrWhiteSpace(config.CurrentBorderColor)) config.CurrentBorderColor = config.BaseColor;

        if (config.CurrentIndex >= config.Entries.Count)
            config.CurrentIndex = config.Entries.Count > 0 ? 0 : -1;
        if (config.CurrentIndex < 0 && config.Entries.Count > 0 &&
            config.Entries.All(e => e.Status == MultirunStatus.Pending))
            config.CurrentIndex = 0;

        return config;
    }

    /// <summary>
    /// The occurrence of the game the multirun is already on, or its first one. Keeps a multirun made of
    /// several cycles of one game from snapping back to the first cycle every time the game comes up.
    /// </summary>
    private int IndexOfRelevant(string gameName) =>
        CurrentEntry != null && Matches(CurrentEntry.GameName, gameName)
            ? _config.CurrentIndex
            : IndexOf(_config.Entries, gameName);

    private static int IndexOf(List<MultirunEntry> entries, string gameName) =>
        entries.FindIndex(e => Matches(e.GameName, gameName));

    private static bool Matches(string left, string right) =>
        !string.IsNullOrEmpty(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    #endregion
}
