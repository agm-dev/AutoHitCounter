//

using System;
using System.Collections.Generic;
using System.Linq;
using AutoHitCounter.Enums;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Services;
using AutoHitCounter.ViewModels;
using NSubstitute;
using Xunit;

namespace AutoHitCounter.Tests.ViewModels;

public class MultirunSettingsViewModelTests
{
    private readonly FakeMultirunStore _store = new();
    private readonly IOverlayServerService _overlayServerService = Substitute.For<IOverlayServerService>();
    private readonly IGameModuleFactory _gameModuleFactory = Substitute.For<IGameModuleFactory>();
    private readonly ICustomGameService _customGameService = Substitute.For<ICustomGameService>();

    public MultirunSettingsViewModelTests()
    {
        _gameModuleFactory.GetRegisteredGames().Returns(_ => new List<Game>
        {
            new() { GameName = "Elden Ring" },
            new() { GameName = "Dark Souls 3" }
        });
        _customGameService.Load().Returns(new List<Game>());
    }

    private MultirunService CreateCyclesService(string game, int count)
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = true;
        _store.Config.Mode = MultirunMode.Cycles;
        _store.Config.CycleGameName = game;
        _store.Config.CycleCount = count;
        _store.Config.Entries = Enumerable.Range(0, count).Select(i => new MultirunEntry
        {
            Id = Guid.NewGuid().ToString(),
            GameName = game,
            Abbreviation = i == 0 ? "NG" : $"NG+{i}"
        }).ToList();
        _store.Config.CurrentIndex = 0;

        return new MultirunService(_store, _overlayServerService);
    }

    [Fact]
    public void Refresh_OnACyclesMultirun_LeavesTheSetupAndItsProgressAlone()
    {
        var service = CreateCyclesService("Elden Ring", 7);
        var sut = new MultirunSettingsViewModel(service, _gameModuleFactory, _customGameService);
        service.CompleteGame("Elden Ring", hasHits: false);

        sut.Refresh();

        Assert.Equal(7, service.Entries.Count);
        Assert.Equal("Elden Ring", sut.CycleGame?.GameName);
        Assert.Equal(7, sut.CycleCount);
        Assert.True(sut.IsCyclesMode);
        Assert.Equal(MultirunStatus.Completed, service.Entries[0].Status);
        Assert.Equal(1, service.Config.CurrentIndex);
    }

    /// <summary>
    /// Emptying the game combo boxes to rebuild them drops their selection, which a two way binding writes
    /// straight back. That must not be taken for the user picking another game and wipe the cycles.
    /// </summary>
    [Fact]
    public void CycleGame_ClearedWhileRefreshing_DoesNotEmptyTheMultirun()
    {
        var service = CreateCyclesService("Elden Ring", 7);
        var sut = new MultirunSettingsViewModel(service, _gameModuleFactory, _customGameService);

        sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(sut.CycleGame)) sut.CycleGame = null;
        };
        sut.Refresh();

        Assert.Equal(7, service.Entries.Count);
        Assert.Equal("Elden Ring", sut.CycleGame?.GameName);
    }

    /// <summary>A radio button being unchecked by the one next to it must not be read as a mode change.</summary>
    [Fact]
    public void Mode_UncheckingTheOtherRadioButton_DoesNotSwitchMode()
    {
        var service = CreateCyclesService("Elden Ring", 7);
        var sut = new MultirunSettingsViewModel(service, _gameModuleFactory, _customGameService);

        sut.IsGamesMode = false;

        Assert.True(sut.IsCyclesMode);
        Assert.Equal(7, service.Entries.Count);
    }

    [Fact]
    public void Mode_SwitchingToSeveralGames_BringsBackTheSavedGameList()
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = true;
        _store.Config.Mode = MultirunMode.Cycles;
        _store.Config.CycleGameName = "Elden Ring";
        _store.Config.CycleCount = 2;
        _store.Config.Entries = new List<MultirunEntry>
        {
            new() { Id = Guid.NewGuid().ToString(), GameName = "Elden Ring", Abbreviation = "NG" },
            new() { Id = Guid.NewGuid().ToString(), GameName = "Elden Ring", Abbreviation = "NG+1" }
        };
        _store.Config.GameEntries = new List<MultirunEntry>
        {
            new() { Id = Guid.NewGuid().ToString(), GameName = "Dark Souls 3", Abbreviation = "DS3" },
            new() { Id = Guid.NewGuid().ToString(), GameName = "Elden Ring", Abbreviation = "ER" }
        };
        var service = new MultirunService(_store, _overlayServerService);
        var sut = new MultirunSettingsViewModel(service, _gameModuleFactory, _customGameService);

        sut.IsGamesMode = true;

        Assert.Equal(new[] { "Dark Souls 3", "Elden Ring" }, service.Entries.Select(e => e.GameName));
        Assert.Equal(new[] { "DS3", "ER" }, service.Entries.Select(e => e.Abbreviation));
    }

    [Fact]
    public void CycleCount_Raised_KeepsTheProgressOfTheCyclesThatStay()
    {
        var service = CreateCyclesService("Elden Ring", 2);
        var sut = new MultirunSettingsViewModel(service, _gameModuleFactory, _customGameService);
        service.CompleteGame("Elden Ring", hasHits: false);

        sut.CycleCount = 4;

        Assert.Equal(4, service.Entries.Count);
        Assert.Equal(MultirunStatus.Completed, service.Entries[0].Status);
        Assert.Equal(MultirunStatus.Pending, service.Entries[3].Status);
        Assert.Equal(new[] { "NG", "NG+1", "NG+2", "NG+3" }, service.Entries.Select(e => e.Abbreviation));
        Assert.Equal(1, service.Config.CurrentIndex);
    }

    private class FakeMultirunStore : IMultirunStore
    {
        public MultirunConfig Config { get; set; } = MultirunConfig.CreateDefault();

        public MultirunConfig Load() => Config;

        public void Save(MultirunConfig config) => Config = config;
    }
}
