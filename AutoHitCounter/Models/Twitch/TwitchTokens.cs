//

using System;

namespace AutoHitCounter.Models.Twitch;

/// <summary>
/// Everything needed to keep talking to Twitch on the user's behalf. Never written to
/// settings.txt: see <see cref="Utilities.TwitchTokenStore"/>.
/// </summary>
public class TwitchTokens
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string BroadcasterId { get; set; }
    public string BroadcasterLogin { get; set; }
}
