//

using System;
using System.Collections.Generic;
using AutoHitCounter.Enums;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.Services.Twitch;

/// <summary>
/// Default Twitch category for each built-in game.
///
/// Keyed by game name rather than by <see cref="GameTitle"/> on purpose: every custom game the
/// user creates is stored as <see cref="GameTitle.Manual"/>, so the name is the only thing that
/// tells games apart. It is also the key already used by LastSelectedGame and the profile store.
/// </summary>
public static class TwitchCategoryMap
{
    /// <remarks>
    /// Dark Souls II has two Twitch categories: "Dark Souls II" (91423, the original release) and
    /// Scholar of the First Sin (489170). SotFS is the default because it is what almost everyone
    /// runs; the settings panel lets anyone on the vanilla version change it.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, TwitchCategory> Defaults =
        new Dictionary<string, TwitchCategory>(StringComparer.OrdinalIgnoreCase)
        {
            [GameTitle.DarkSoulsRemastered.GetDescription()] =
                new TwitchCategory { Id = "1122982998", Name = "Dark Souls: Remastered" },

            [GameTitle.DarkSouls2.GetDescription()] =
                new TwitchCategory { Id = "489170", Name = "DARK SOULS II: Scholar of the First Sin" },

            [GameTitle.DarkSouls3.GetDescription()] =
                new TwitchCategory { Id = "490292", Name = "DARK SOULS III" },

            [GameTitle.Sekiro.GetDescription()] =
                new TwitchCategory { Id = "506415", Name = "SEKIRO: SHADOWS DIE TWICE" },

            [GameTitle.EldenRing.GetDescription()] =
                new TwitchCategory { Id = "512953", Name = "ELDEN RING" },
        };

    public static TwitchCategory ForGameName(string gameName) =>
        gameName != null && Defaults.TryGetValue(gameName, out var category) ? category : null;
}
