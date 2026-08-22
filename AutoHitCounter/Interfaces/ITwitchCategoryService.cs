//

using System;
using System.Threading.Tasks;
using AutoHitCounter.Models;

namespace AutoHitCounter.Interfaces;

public interface ITwitchCategoryService
{
    /// <summary>
    /// Result of the last attempt, for the settings panel. Nothing here is ever shown as a
    /// dialog: changing the category must never interrupt a run.
    /// </summary>
    event Action<string> StatusChanged;

    /// <summary>
    /// Points the channel at the Twitch category mapped to <paramref name="game"/>. Does nothing
    /// when the integration is off, no account is connected, the game has no category or the
    /// channel is offline and "only when live" is set.
    /// </summary>
    Task SyncCategoryAsync(Game game);
}
