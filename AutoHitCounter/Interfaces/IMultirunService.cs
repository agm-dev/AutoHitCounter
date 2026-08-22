//

using System;
using System.Collections.Generic;
using AutoHitCounter.Models;

namespace AutoHitCounter.Interfaces;

public interface IMultirunService
{
    /// <summary>Raised whenever the multirun setup or its progress changes.</summary>
    event Action Changed;

    bool IsEnabled { get; }

    /// <summary>Read only view of the current setup and progress.</summary>
    MultirunConfig Config { get; }

    /// <summary>Replaces the setup (enabled flag, games, abbreviations, styling), keeping the progress of the games that remain.</summary>
    void UpdateConfig(MultirunConfig config);

    /// <summary>Shuffles the games, clears their progress and makes the first one current.</summary>
    void Randomize();

    /// <summary>Clears the progress of every game and makes the first one current.</summary>
    void ResetProgress();

    /// <summary>Marks the current game red while its run has hits, and clears that mark when it no longer does.</summary>
    void SyncHits(string gameName, bool hasHits);

    /// <summary>Marks the current game as completed (green when hitless, red otherwise) and moves on to the next one.</summary>
    void CompleteGame(string gameName, bool hasHits);

    /// <summary>Moves a game that is being tracked to the current position of the list and marks it as current.</summary>
    void OnGameTracked(string gameName);

    /// <summary>Restarts the multirun from the given game when any game is marked with a hit.</summary>
    void OnNewGameStarted(string gameName);

    /// <summary>Sends the current state to the overlay.</summary>
    void Broadcast();

    /// <summary>Default abbreviations for the games known to the app, used when a game is added to a multirun.</summary>
    string GetDefaultAbbreviation(string gameName);

    IReadOnlyList<MultirunEntry> Entries { get; }
}
