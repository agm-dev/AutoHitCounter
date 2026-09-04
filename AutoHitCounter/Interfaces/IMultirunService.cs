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

    /// <summary>
    /// Moves a game that is being tracked to the current position of the list and marks it as current,
    /// or restarts the multirun from that game when any game is marked with a hit. While the games
    /// already lost are being kept, the multirun carries on instead, leaving the abandoned game behind.
    /// </summary>
    void OnGameTracked(string gameName);

    /// <summary>
    /// A new game was detected. Restarts the multirun from the given game when any game is marked with a hit;
    /// in a cycles multirun a clean new game is just the start of the next cycle and leaves the progress alone.
    /// The restart never happens while the games already lost are being kept.
    /// </summary>
    void OnNewGameStarted(string gameName);

    /// <summary>
    /// The run of the tracked game was reset by hand. Restarts the multirun, except in a cycles multirun where
    /// the reset is how the next cycle is started: there it only starts over on a hit taken, and not even then
    /// while the games already lost are being kept.
    /// </summary>
    void OnRunReset(string gameName);

    /// <summary>Sends the current state to the overlay.</summary>
    void Broadcast();

    /// <summary>Default abbreviations for the games known to the app, used when a game is added to a multirun.</summary>
    string GetDefaultAbbreviation(string gameName);

    /// <summary>Default label of a cycle of a same game multirun: "NG", "NG+1", "NG+2"...</summary>
    string GetDefaultCycleAbbreviation(int cycleIndex);

    IReadOnlyList<MultirunEntry> Entries { get; }
}
