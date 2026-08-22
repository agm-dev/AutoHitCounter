//

namespace AutoHitCounter.Models.Twitch;

/// <summary>
/// A Twitch stream category. <see cref="Id"/> is what the API actually needs; <see cref="Name"/>
/// is the label shown in settings and is also what the user types for a custom game, in which
/// case the id stays null until it is resolved once against /helix/games.
/// </summary>
public class TwitchCategory
{
    public string Id { get; set; }
    public string Name { get; set; }
}
