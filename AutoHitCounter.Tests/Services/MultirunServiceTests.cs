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

    private MultirunService CreateSut(bool enabled = true, params string[] games) =>
        CreateSut(enabled, keepProgressWithFailedGames: false, games);

    /// <summary>A multirun of several games being practised, where a game that took hits does not start it over.</summary>
    private MultirunService CreatePracticeSut(params string[] games) =>
        CreateSut(enabled: true, keepProgressWithFailedGames: true, games);

    private MultirunService CreateSut(bool enabled, bool keepProgressWithFailedGames, params string[] games)
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = enabled;
        _store.Config.KeepProgressWithFailedGames = keepProgressWithFailedGames;
        _store.Config.Entries = games.Select(g => new MultirunEntry
        {
            Id = Guid.NewGuid().ToString(),
            GameName = g,
            Abbreviation = g
        }).ToList();
        _store.Config.CurrentIndex = games.Length > 0 ? 0 : -1;

        return new MultirunService(_store, _overlayServerService);
    }

    /// <summary>A multirun made of several cycles (NG, NG+1...) of a single game.</summary>
    private MultirunService CreateCyclesSut(string game, int count, bool enabled = true,
        bool keepProgressWithFailedGames = false)
    {
        _store.Config = MultirunConfig.CreateDefault();
        _store.Config.Enabled = enabled;
        _store.Config.KeepProgressWithFailedGames = keepProgressWithFailedGames;
        _store.Config.Mode = MultirunMode.Cycles;
        _store.Config.CycleGameName = game;
        _store.Config.CycleCount = count;
        _store.Config.Entries = Enumerable.Range(0, count).Select(i => new MultirunEntry
        {
            Id = Guid.NewGuid().ToString(),
            GameName = game,
            Abbreviation = i == 0 ? "NG" : $"NG+{i}"
        }).ToList();
        _store.Config.CurrentIndex = count > 0 ? 0 : -1;

        return new MultirunService(_store, _overlayServerService);
    }

    private static IEnumerable<string> Order(MultirunService sut) => sut.Entries.Select(e => e.GameName);

    private static MultirunStatus StatusOf(MultirunService sut, string game) =>
        sut.Entries.First(e => e.GameName == game).Status;

    private static MultirunStatus StatusAt(MultirunService sut, int index) => sut.Entries[index].Status;

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

    #region Cycles of a single game

    [Fact]
    public void CompleteGame_OnACyclesMultirun_WalksThroughTheRepeatedEntries()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);

        sut.CompleteGame("Elden Ring", hasHits: false);
        sut.CompleteGame("Elden Ring", hasHits: false);

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 1));
        Assert.Equal(MultirunStatus.Pending, StatusAt(sut, 2));
        Assert.Equal(2, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnACyclesMultirunWithoutHits_MovesOnToTheNextCycle()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.OnRunReset("Elden Ring");

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnACyclesMultirunAfterAHit_StartsItOver()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);
        sut.SyncHits("Elden Ring", hasHits: true);

        sut.OnRunReset("Elden Ring");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_AfterCompletingTheLastCycle_StartsTheMultirunOver()
    {
        var sut = CreateCyclesSut("Elden Ring", 2);
        sut.CompleteGame("Elden Ring", hasHits: false);
        sut.CompleteGame("Elden Ring", hasHits: false);
        Assert.Equal(-1, sut.Config.CurrentIndex);

        sut.OnRunReset("Elden Ring");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnACyclesMultirunOfAnotherGame_IsIgnored()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.OnRunReset("Sekiro");

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnAMultirunOfSeveralGames_StartsItOver()
    {
        var sut = CreateSut(true, "DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);

        sut.OnRunReset("DS2");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnNewGameStarted_OnACyclesMultirunWithoutHits_MovesOnToTheNextCycle()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.OnNewGameStarted("Elden Ring");

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnNewGameStarted_OnACyclesMultirunAfterAHit_StartsItOver()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: true);

        sut.OnNewGameStarted("Elden Ring");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_OnACyclesMultirunAlreadyUnderWay_KeepsTheCycleItIsOn()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.OnGameTracked("Elden Ring");

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_OnAFinishedCyclesMultirun_StartsItOver()
    {
        var sut = CreateCyclesSut("Elden Ring", 2);
        sut.CompleteGame("Elden Ring", hasHits: false);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.OnGameTracked("Elden Ring");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void Randomize_OnACyclesMultirun_LeavesItAlone()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        sut.Randomize();

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void UpdateConfig_OnACyclesMultirun_KeepsTheProgressOfEachCycleApart()
    {
        var sut = CreateCyclesSut("Elden Ring", 3);
        sut.CompleteGame("Elden Ring", hasHits: false);

        // Same entries with a renamed abbreviation, as the settings panel would send them.
        var config = sut.Config.Clone();
        config.Entries[2].Abbreviation = "NG+2!";
        sut.UpdateConfig(config);

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(MultirunStatus.Pending, StatusAt(sut, 1));
        Assert.Equal(MultirunStatus.Pending, StatusAt(sut, 2));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void UpdateConfig_AddingACycle_LeavesTheNewOnePending()
    {
        var sut = CreateCyclesSut("Elden Ring", 2);
        sut.CompleteGame("Elden Ring", hasHits: false);

        var config = sut.Config.Clone();
        config.CycleCount = 3;
        config.Entries.Add(new MultirunEntry
        {
            Id = Guid.NewGuid().ToString(),
            GameName = "Elden Ring",
            Abbreviation = "NG+2"
        });
        sut.UpdateConfig(config);

        Assert.Equal(MultirunStatus.Completed, StatusAt(sut, 0));
        Assert.Equal(MultirunStatus.Pending, StatusAt(sut, 2));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Theory]
    [InlineData(0, "NG")]
    [InlineData(1, "NG+1")]
    [InlineData(6, "NG+6")]
    public void GetDefaultCycleAbbreviation_LabelsEachCycle(int cycleIndex, string expected)
    {
        var sut = CreateSut();

        Assert.Equal(expected, sut.GetDefaultCycleAbbreviation(cycleIndex));
    }

    #endregion

    #region Practising a multirun

    [Fact]
    public void OnGameTracked_AfterAHit_WhenProgressIsKept_CarriesOnWithoutRestarting()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3");
        sut.SyncHits("DS1", hasHits: true);

        sut.OnGameTracked("DS2");

        Assert.Equal(new[] { "DS1", "DS2", "DS3" }, Order(sut));
        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_AbandoningAGameWithHits_WhenProgressIsKept_LeavesItBehindInRed()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3", "ER");
        sut.CompleteGame("DS1", hasHits: false);
        sut.SyncHits("DS2", hasHits: true);

        sut.OnGameTracked("ER");

        Assert.Equal(new[] { "DS1", "DS2", "ER", "DS3" }, Order(sut));
        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS2"));
        Assert.Equal(2, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_AfterCompletingAGameWithHits_WhenProgressIsKept_MovesItToTheCurrentSpot()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: true);

        sut.OnGameTracked("DS3");

        Assert.Equal(new[] { "DS1", "DS3", "DS2" }, Order(sut));
        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS1"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnNewGameStarted_AfterAHit_WhenProgressIsKept_LeavesTheMultirunAsItIs()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);
        sut.SyncHits("DS2", hasHits: true);

        sut.OnNewGameStarted("DS2");

        Assert.Equal(new[] { "DS1", "DS2", "DS3" }, Order(sut));
        Assert.Equal(MultirunStatus.Completed, StatusOf(sut, "DS1"));
        Assert.Equal(MultirunStatus.Hit, StatusOf(sut, "DS2"));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnAMultirunOfSeveralGames_WhenProgressIsKept_StillStartsItOver()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: true);

        sut.OnRunReset("DS2");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_OnACyclesMultirunAfterAHit_WhenProgressIsKept_MovesOnToTheNextCycle()
    {
        var sut = CreateCyclesSut("Elden Ring", 3, keepProgressWithFailedGames: true);
        sut.CompleteGame("Elden Ring", hasHits: true);

        sut.OnRunReset("Elden Ring");

        Assert.Equal(MultirunStatus.Hit, StatusAt(sut, 0));
        Assert.Equal(MultirunStatus.Pending, StatusAt(sut, 1));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnNewGameStarted_OnACyclesMultirunAfterAHit_WhenProgressIsKept_MovesOnToTheNextCycle()
    {
        var sut = CreateCyclesSut("Elden Ring", 3, keepProgressWithFailedGames: true);
        sut.CompleteGame("Elden Ring", hasHits: true);

        sut.OnNewGameStarted("Elden Ring");

        Assert.Equal(MultirunStatus.Hit, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnGameTracked_OnACyclesMultirunAfterAHit_WhenProgressIsKept_KeepsTheCycleItIsOn()
    {
        var sut = CreateCyclesSut("Elden Ring", 3, keepProgressWithFailedGames: true);
        sut.CompleteGame("Elden Ring", hasHits: true);

        sut.OnGameTracked("Elden Ring");

        Assert.Equal(MultirunStatus.Hit, StatusAt(sut, 0));
        Assert.Equal(1, sut.Config.CurrentIndex);
    }

    [Fact]
    public void OnRunReset_AfterCompletingTheLastCycleWithHits_WhenProgressIsKept_StillStartsTheMultirunOver()
    {
        var sut = CreateCyclesSut("Elden Ring", 2, keepProgressWithFailedGames: true);
        sut.CompleteGame("Elden Ring", hasHits: true);
        sut.CompleteGame("Elden Ring", hasHits: true);
        Assert.Equal(-1, sut.Config.CurrentIndex);

        sut.OnRunReset("Elden Ring");

        Assert.All(sut.Entries, e => Assert.Equal(MultirunStatus.Pending, e.Status));
        Assert.Equal(0, sut.Config.CurrentIndex);
    }

    [Fact]
    public void Broadcast_WhileBeingPractised_SendsTheGamesLeftBehindGreenAndRed()
    {
        var sut = CreatePracticeSut("DS1", "DS2", "DS3");
        sut.CompleteGame("DS1", hasHits: false);
        sut.CompleteGame("DS2", hasHits: true);

        MultirunState state = null;
        _overlayServerService.BroadcastMultirun(Arg.Do<MultirunState>(s => state = s));
        sut.Broadcast();

        Assert.NotNull(state);
        Assert.Equal(new[] { "completed", "hit", "pending" }, state.Entries.Select(e => e.Status));
        Assert.Equal(new[] { false, false, true }, state.Entries.Select(e => e.IsCurrent));
    }

    #endregion

    private class FakeMultirunStore : IMultirunStore
    {
        public MultirunConfig Config { get; set; } = MultirunConfig.CreateDefault();

        public MultirunConfig Load() => Config;

        public void Save(MultirunConfig config) => Config = config;
    }
}
