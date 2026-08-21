//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.Services.Twitch;

/// <summary>Thin wrapper over the four Helix endpoints this feature needs.</summary>
public class TwitchApiClient : ITwitchApiClient
{
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<TwitchApiResult<HelixUser>> GetCurrentUserAsync(string accessToken)
    {
        // No parameters: /users returns the account the token belongs to.
        var result = await GetListAsync<HelixUser>(accessToken, "/users");
        return Project(result, users => users.FirstOrDefault());
    }

    public async Task<TwitchApiResult<HelixGame>> FindGameByNameAsync(string accessToken, string name)
    {
        var result = await GetListAsync<HelixGame>(
            accessToken, "/games?name=" + Uri.EscapeDataString(name));
        return Project(result, games => games.FirstOrDefault());
    }

    /// <summary>A channel that is not streaming simply comes back with an empty data array.</summary>
    public async Task<TwitchApiResult<bool>> IsLiveAsync(string accessToken, string broadcasterId)
    {
        var result = await GetListAsync<HelixStream>(
            accessToken, "/streams?user_id=" + Uri.EscapeDataString(broadcasterId));
        return Project(result, streams => streams.Count > 0);
    }

    public async Task<TwitchApiResult<bool>> UpdateChannelCategoryAsync(
        string accessToken, string broadcasterId, string gameId)
    {
        try
        {
            var url = TwitchConfig.HelixBaseUrl +
                      "/channels?broadcaster_id=" + Uri.EscapeDataString(broadcasterId);

            // HttpMethod.Patch does not exist on .NET Framework.
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), url))
            {
                AddHeaders(request, accessToken);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { game_id = gameId }),
                    Encoding.UTF8,
                    "application/json");

                using (var response = await _httpClient.SendAsync(request))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                        return TwitchApiResult<bool>.Unauthorized();

                    if (response.IsSuccessStatusCode)
                        return TwitchApiResult<bool>.Ok(true);

                    var body = await response.Content.ReadAsStringAsync();
                    return TwitchApiResult<bool>.Fail(Describe(response, body));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Twitch PATCH /channels failed");
            return TwitchApiResult<bool>.Fail(ex.Message);
        }
    }

    private async Task<TwitchApiResult<List<T>>> GetListAsync<T>(string accessToken, string path)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, TwitchConfig.HelixBaseUrl + path))
            {
                AddHeaders(request, accessToken);

                using (var response = await _httpClient.SendAsync(request))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                        return TwitchApiResult<List<T>>.Unauthorized();

                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        return TwitchApiResult<List<T>>.Fail(Describe(response, body));

                    var payload = JsonSerializer.Deserialize<HelixList<T>>(body);
                    return TwitchApiResult<List<T>>.Ok(payload?.Data ?? new List<T>());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Twitch GET " + path + " failed");
            return TwitchApiResult<List<T>>.Fail(ex.Message);
        }
    }

    private static TwitchApiResult<TOut> Project<TIn, TOut>(
        TwitchApiResult<TIn> source, Func<TIn, TOut> selector)
    {
        if (source.IsUnauthorized) return TwitchApiResult<TOut>.Unauthorized();
        if (!source.IsSuccess) return TwitchApiResult<TOut>.Fail(source.Error);
        return TwitchApiResult<TOut>.Ok(selector(source.Value));
    }

    private static void AddHeaders(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Add("Client-Id", TwitchConfig.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static string Describe(HttpResponseMessage response, string body) =>
        $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";
}
