//

using System;
using System.Threading.Tasks;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.Services.Twitch;

public class TwitchCategoryService : ITwitchCategoryService
{
    private readonly ITwitchAuthService _auth;
    private readonly ITwitchApiClient _api;

    private string _lastAppliedGameId;

    public event Action<string> StatusChanged;

    public TwitchCategoryService(ITwitchAuthService auth, ITwitchApiClient api)
    {
        _auth = auth;
        _api = api;

        // A different account knows nothing about what we last set.
        _auth.ConnectionChanged += () => _lastAppliedGameId = null;
    }

    public async Task SyncCategoryAsync(Game game)
    {
        try
        {
            if (game == null) return;
            if (!SettingsManager.Default.TwitchIntegrationEnabled) return;

            if (!_auth.IsConnected)
            {
                Report("Not connected to Twitch.");
                return;
            }

            var category = await ResolveCategoryAsync(game);
            if (category == null || string.IsNullOrEmpty(category.Id))
            {
                Report($"No Twitch category set for \"{game.GameName}\".");
                return;
            }

            if (SettingsManager.Default.TwitchOnlyWhenLive)
            {
                var live = await SendAsync(token => _api.IsLiveAsync(token, _auth.BroadcasterId));

                if (!live.IsSuccess)
                {
                    Report("Could not check whether you are live.");
                    return;
                }

                if (!live.Value)
                {
                    Report("You are not live - category left unchanged.");
                    return;
                }
            }

            if (category.Id == _lastAppliedGameId)
            {
                Report($"Category is already {category.Name}.");
                return;
            }

            var updated = await SendAsync(token =>
                _api.UpdateChannelCategoryAsync(token, _auth.BroadcasterId, category.Id));

            if (!updated.IsSuccess)
            {
                Report("Could not change the category - see the log for details.");
                return;
            }

            _lastAppliedGameId = category.Id;
            Report($"Category changed to {category.Name}.");
        }
        catch (Exception ex)
        {
            // Changing a category is never worth breaking a run over.
            Logger.Error(ex, "Twitch category sync failed");
            Report("Twitch sync failed - see the log for details.");
        }
    }

    /// <summary>
    /// A user override wins over the built-in map. An override that only carries a name (what the
    /// settings panel writes) is resolved against Helix once and then cached with its id.
    /// </summary>
    private async Task<TwitchCategory> ResolveCategoryAsync(Game game)
    {
        var overrides = TwitchCategoryStore.Load();

        if (!overrides.TryGetValue(game.GameName, out var configured) || configured == null)
            return TwitchCategoryMap.ForGameName(game.GameName);

        if (!string.IsNullOrWhiteSpace(configured.Id)) return configured;
        if (string.IsNullOrWhiteSpace(configured.Name)) return null;

        var found = await SendAsync(token => _api.FindGameByNameAsync(token, configured.Name));
        if (!found.IsSuccess || found.Value == null)
        {
            Report($"Twitch has no category called \"{configured.Name}\".");
            return null;
        }

        configured.Id = found.Value.Id;
        configured.Name = found.Value.Name;
        overrides[game.GameName] = configured;
        TwitchCategoryStore.Save(overrides);

        return configured;
    }

    /// <summary>Runs a call, and retries it once against a fresh token if Twitch says 401.</summary>
    private async Task<TwitchApiResult<T>> SendAsync<T>(Func<string, Task<TwitchApiResult<T>>> call)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return TwitchApiResult<T>.Fail("No valid Twitch token");

        var result = await call(token);
        if (!result.IsUnauthorized) return result;

        if (!await _auth.RefreshAsync()) return result;

        token = await _auth.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return result;

        return await call(token);
    }

    private void Report(string status) => StatusChanged?.Invoke(status);
}
