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

        var previousCurrentGame = CurrentEntry?.GameName;
        var previousStatuses = _config.Entries
            .GroupBy(e => e.GameName ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Status, StringComparer.OrdinalIgnoreCase);

        var updated = Normalize(config.Clone());

        // Keep the progress of the games that are still part of the multirun.
        foreach (var entry in updated.Entries)
        {
            if (previousStatuses.TryGetValue(entry.GameName ?? "", out var status))
                entry.Status = status;
        }

        if (previousCurrentGame != null)
        {
            updated.CurrentIndex = IndexOf(updated.Entries, previousCurrentGame);
            if (updated.CurrentIndex < 0 && updated.Entries.Count > 0)
                updated.CurrentIndex = 0;
        }
        else
        {
            // No game was current: the multirun was either finished or empty, and only the latter starts one.
            updated.CurrentIndex = _config.Entries.Count == 0 && updated.Entries.Count > 0 ? 0 : -1;
        }

        _config = updated;
        SaveAndPublish();
    }

    public void Randomize()
    {
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

        var index = IndexOf(_config.Entries, gameName);
        if (index < 0) return;

        var currentIndex = _config.CurrentIndex;
        if (index == currentIndex) return;

        if (currentIndex < 0 || index < currentIndex)
        {
            // Nothing to reorder: the multirun simply goes back to (or starts at) that game.
            _config.CurrentIndex = index;
        }
        else
        {
            var entry = _config.Entries[index];
            _config.Entries.RemoveAt(index);
            _config.Entries.Insert(currentIndex, entry);
        }

        SaveAndPublish();
    }

    public void OnNewGameStarted(string gameName)
    {
        if (!IsEnabled) return;

        var index = IndexOf(_config.Entries, gameName);
        if (index < 0) return;
        if (_config.Entries.All(e => e.Status != MultirunStatus.Hit)) return;

        var entry = _config.Entries[index];
        _config.Entries.RemoveAt(index);
        _config.Entries.Insert(0, entry);

        ClearProgress();
        SaveAndPublish();
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

    #region Private Methods

    private MultirunEntry CurrentEntry =>
        _config.CurrentIndex >= 0 && _config.CurrentIndex < _config.Entries.Count
            ? _config.Entries[_config.CurrentIndex]
            : null;

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

    private static int IndexOf(List<MultirunEntry> entries, string gameName) =>
        entries.FindIndex(e => Matches(e.GameName, gameName));

    private static bool Matches(string left, string right) =>
        !string.IsNullOrEmpty(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    #endregion
}
