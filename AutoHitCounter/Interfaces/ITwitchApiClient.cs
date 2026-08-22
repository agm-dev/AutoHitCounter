//

using System.Threading.Tasks;
using AutoHitCounter.Models.Twitch;

namespace AutoHitCounter.Interfaces;

public interface ITwitchApiClient
{
    /// <summary>The account the token belongs to, which is where broadcaster_id comes from.</summary>
    Task<TwitchApiResult<HelixUser>> GetCurrentUserAsync(string accessToken);

    Task<TwitchApiResult<HelixGame>> FindGameByNameAsync(string accessToken, string name);

    Task<TwitchApiResult<bool>> IsLiveAsync(string accessToken, string broadcasterId);

    Task<TwitchApiResult<bool>> UpdateChannelCategoryAsync(string accessToken, string broadcasterId, string gameId);
}
