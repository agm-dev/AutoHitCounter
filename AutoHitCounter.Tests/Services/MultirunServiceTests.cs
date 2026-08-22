//

using System;
using System.Collections.Generic;
using System.Linq;
using AutoHitCounter.Enums;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Services;
using NSubstitute;
using Xunit;

namespace AutoHitCounter.Tests.Services;

public class MultirunServiceTests
{
    private readonly FakeMultirunStore _store = new();
    private readonly IOverlayServerService _overlayServerService = Substitute.For<IOverlayServerService>();

    private MultirunService CreateSut(bool enabled = true, params string[] games)
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = enabled;
        _store.Config.Entries = games.Select(g => new MultirunEntry
        {
            GameName = g,
            Abbreviation = g
        }).ToList();
        _store.Config.CurrentIndex = games.Length > 0 ? 0 : -1;

        return new MultirunService(_store, _overlayServerService);
    }

    private static IEnumerable<string> Order(MultirunService sut) => sut.Entries.Select(e => e.GameName);

    private static MultirunStatus StatusOf(MultirunService sut, string game) =>
        sut.Entries.First(e => e.GameName == game).Status;

    #region Completing games

    [Fact]
    public void CompleteGame_WithoutHits_MarksCompletedAndMovesToNextGame()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");

        sut.CompleteGame("DS1", hasHits: false);

        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void CompleteGame_WithHits_StaysMarkedAsHit()
    {
        var sut = CreateSut(true, "DS1", "DS2");

        sut.CompleteGame("DS1", hasHits: true);

        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void CompleteGame_LastGame_LeavesNoCurrentGame()
    {
        var sut = CreateSut(true, "DS1", "DS2");
        sut.CompleteGame("DS1", hasHits: false);

        sut.CompleteGame("DS2", hasHits: false);

        Assert.Equal(-1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void CompleteGame_GameThatIsNotTheCurrentOne_IsIgnored()
    {
        var sut = CreateSut(true, "DS1", "DS2");

        sut.CompleteGame("DS2", hasHits: false);

        Assert.Equal(MultirunStatus.Pending, StatusOf(sut, "DS2"));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void CompleteGame_WhenDisabled_IsIgnored()
    {
        var sut = CreateSut(false, "DS1", "DS2");

        sut.CompleteGame("DS1", hasHits: false);

        Assert.Equal(MultirunStatus.Pending, StatusOf(sut, "DS1"));
    }

    #endregion

    #region Hits

    [Fact]
    public void SyncHits_CurrentGameWithHits_MarksItAsHit()
    {
        var sut = CreateSut(true, "DS1", "DS2");

        sut.SyncHits("DS1", hasHits: true);

        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS1"));
    }

    [Fact]
    public void SyncHits_HitsGoBackToZero_ClearsTheMark()
    {
        var sut = CreateSut(true, "DS1", "DS2");
        sut.SyncHits("DS1", hasHits: true);

        sut.SyncHits("DS1", hasHits: false);

        Assert.Equal(MultirunStatus.Pending, StatusOf(sut, "DS1"));
    }

    [Fact]
    public void SyncHits_GameThatIsNotTheCurrentOne_IsIgnored()
    {
        var sut = CreateSut(true, "DS1", "DS2");

        sut.SyncHits("DS2", hasHits: true);

        Assert.Equal(MultirunStatus.Pending, StatusOf(sut, "DS2"));
    }

    #endregion

    #region Randomize and reset

    [Fact]
    public void Randomize_KeepsTheSameGamesAndRestartsTheProgress()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3", "ER");
        sut.CompleteGame("DS1", hasHits: false);
        sut.SyncHits("DS2", hasHits: true);

        sut.Randomize();

        Assert.Equal(new[] { "DS1", "DS2", "DS3", "ER" }, Order(sut).OrderBy(n => n));
        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void Randomize_WithASeededRandom_ShufflesTheOrder()
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = true;
        _store.Config.Entries = new[] { "A", "B", "C", "D", "E" }
            .Select(g => new MultirunEntry { GameName = g, Abbreviation = g }).ToList();
        var sut = new MultirunService(_store, _overlayServerService, new Random(1));

        sut.Randomize();

        Assert.NotEqual(new[] { "A", "B", "C", "D", "E" }, Order(sut));
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, Order(sut).OrderBy(n => n));
    }

    [Fact]
    public void ResetProgress_ClearsEveryMarkAndMakesTheFirstGameCurrent()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);
        sut.SyncHits("DS2", hasHits: true);

        sut.ResetProgress();

        Assert.Equal(new[] { "DS1", "DS2", "DS3" }, Order(sut));
        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    #endregion

    #region Tracking a game

    [Fact]
    public void OnGameTracked_LaterGame_MovesItToTheCurrentSpot()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3", "ER");
        sut.CompleteGame("DS1", hasHits: false);

        sut.OnGameTracked("ER");

        Assert.Equal(new[] { "DS1", "ER", "DS2", "DS3" }, Order(sut));
        Assert.Equal(1, sut.Config.CurrentIndex);
        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
    }

    [Fact]
    public void OnGameTracked_EarlierGame_GoesBackToItWithoutReordering()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);

        sut.OnGameTracked("DS1");

        Assert.Equal(new[] { "DS1", "DS2", "DS3" }, Order(sut));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_AfterAHit_RestartsTheMultirunFromThatGame()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.SyncHits("DS1", hasHits: true);

        sut.OnGameTracked("DS2");

        Assert.Equal(new[] { "DS2", "DS1", "DS3" }, Order(sut));
        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_AfterCompletingAGameWithHits_RestartsTheMultirunFromThatGame()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: true);

        sut.OnGameTracked("DS3");

        Assert.Equal(new[] { "DS3", "DS1", "DS2" }, Order(sut));
        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_TheGameAlreadyBeingRun_KeepsItsHitMark()
    {
        var sut = CreateSut(true, "DS1", "DS2");
        sut.SyncHits("DS1", hasHits: true);

        sut.OnGameTracked("DS1");

        Assert.Equal(new[] { "DS1", "DS2" }, Order(sut));
        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS1"));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_GameOutsideTheMultirun_IsIgnored()
    {
        var sut = CreateSut(true, "DS1", "DS2");

        sut.OnGameTracked("Sekiro");

        Assert.Equal(new[] { "DS1", "DS2" }, Order(sut));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    #endregion

    #region Starting a new game

    [Fact]
    public void OnNewGameStarted_AfterAHit_RestartsTheMultirunFromThatGame()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);
        sut.SyncHits("DS2", hasHits: true);

        sut.OnNewGameStarted("DS2");

        Assert.Equal(new[] { "DS2", "DS1", "DS3" }, Order(sut));
        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnNewGameStarted_WithoutAnyHit_KeepsTheMultirunAsItIs()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);

        sut.OnNewGameStarted("DS2");

        Assert.Equal(new[] { "DS1", "DS2", "DS3" }, Order(sut));
        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    #endregion

    #region Setup changes

    [Fact]
    public void UpdateConfig_KeepsTheProgressOfTheGamesThatRemain()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);

        var config = sut.Config.Clone();
        config.Entries = new List<MultirunEntry>
        {
            new() { GameName = "DS1", Abbreviation = "DS1" },
            new() { GameName = "DS2", Abbreviation = "DS2" },
            new() { GameName = "ER", Abbreviation = "ER" }
        };
        sut.UpdateConfig(config);

        Assert.Equal(new[] { "DS1", "DS2", "ER" }, Order(sut));
        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void UpdateConfig_WhenTheCurrentGameIsRemoved_FallsBackToTheFirstGame()
    {
        var sut = CreateSut(true, "DS1", "DS2");
        sut.CompleteGame("DS1", hasHits: false);

        var config = sut.Config.Clone();
        config.Entries = new List<MultirunEntry> { new() { GameName = "DS1", Abbreviation = "DS1" } };
        sut.UpdateConfig(config);

        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void UpdateConfig_SavesTheSetup()
    {
        var sut = CreateSut(true, "DS1");

        var config = sut.Config.Clone();
        config.BaseColor = "#ffffff";
        sut.UpdateConfig(config);

        Assert.Equal("#ffffff", _store.Config.BaseColor);
    }

    #endregion

    #region Overlay payload

    [Fact]
    public void Broadcast_SendsTheAbbreviationsWithTheirStatus()
    {
        var sut = CreateSut(true, "DS1", "DS2");
        sut.CompleteGame("DS1", hasHits: false);

        MultirunState state = null;
        _overlayServerService.BroadcastMultirun(Arg.Do<MultirunState>(s => state = s));
        sut.Broadcast();

        Assert.NotNull(state);
        Assert.True(state.Enabled);
        Assert.Equal(new[] { "completed", "pending" }, state.Entries.Select(e => e.Status));
        Assert.Equal(new[] { false, true }, state.Entries.Select(e => e.IsCurrent));
    }

    #endregion

    #region Abbreviations

    [Theory]
    [InlineData("Dark Souls Remastered", "DS1")]
    [InlineData("Dark Souls 2", "DS2")]
    [InlineData("Elden Ring", "ER")]
    [InlineData("Bloodborne", "BB")]
    [InlineData("Demon's Souls", "DES")]
    public void GetDefaultAbbreviation_KnownGame_ReturnsItsAbbreviation(string gameName, string expected)
    {
        var sut = CreateSut();

        Assert.Equal(expected, sut.GetDefaultAbbreviation(gameName));
    }

    [Fact]
    public void GetDefaultAbbreviation_CustomGameWithSeveralWords_UsesItsInitials()
    {
        var sut = CreateSut();

        Assert.Equal("AC6", sut.GetDefaultAbbreviation("Armored Core 6"));
    }

    [Fact]
    public void GetDefaultAbbreviation_CustomGameWithASingleWord_UsesItsFirstLetters()
    {
        var sut = CreateSut();

        Assert.Equal("NIO", sut.GetDefaultAbbreviation("Nioh"));
    }

    #endregion

    private class FakeMultirunStore : IMultirunStore
    {
        public MultirunConfig Config { get; set; } = MultirunConfig.CreateDefault();

        public MultirunConfig Load() => Config;

        public void Save(MultirunConfig config) => Config = config;
    }
}
