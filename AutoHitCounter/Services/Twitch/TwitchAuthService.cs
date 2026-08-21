//

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.Services.Twitch;

/// <summary>
/// Device Code Grant flow. It is the only Twitch flow that suits a desktop app: no client secret
/// to ship, and unlike the implicit flow it hands back a refresh token, so the user connects once
/// instead of every few hours.
/// </summary>
public class TwitchAuthService : ITwitchAuthService
{
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
    private readonly ITwitchApiClient _api;

    private TwitchTokens _tokens;

    public event Action ConnectionChanged;

    public TwitchAuthService(ITwitchApiClient api)
    {
        _api = api;
        _tokens = TwitchTokenStore.Load();
    }

    public bool IsConnected => _tokens != null && !string.IsNullOrEmpty(_tokens.RefreshToken);

    public string BroadcasterId => _tokens?.BroadcasterId;

    public string BroadcasterLogin => _tokens?.BroadcasterLogin;

    public async Task<DeviceCodeResponse> StartDeviceAuthorizationAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = TwitchConfig.ClientId,
            ["scopes"] = TwitchConfig.Scopes
        };

        using (var content = new FormUrlEncodedContent(form))
        using (var response = await _httpClient.PostAsync(TwitchConfig.DeviceEndpoint, content))
        {
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Twitch refused the device code request ({(int)response.StatusCode}): {body}");

            return JsonSerializer.Deserialize<DeviceCodeResponse>(body);
        }
    }

    public async Task<bool> AwaitAuthorizationAsync(DeviceCodeResponse device, CancellationToken cancellationToken)
    {
        if (device == null || string.IsNullOrEmpty(device.DeviceCode)) return false;

        var intervalSeconds = Math.Max(device.Interval, 1);
        var deadline = DateTime.UtcNow.AddSeconds(device.ExpiresIn > 0 ? device.ExpiresIn : 1800);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var form = new Dictionary<string, string>
            {
                ["client_id"] = TwitchConfig.ClientId,
                ["scopes"] = TwitchConfig.Scopes,
                ["device_code"] = device.DeviceCode,
                ["grant_type"] = TwitchConfig.DeviceCodeGrantType
            };

            string body;
            bool succeeded;

            using (var content = new FormUrlEncodedContent(form))
            using (var response = await _httpClient.PostAsync(TwitchConfig.TokenEndpoint, content, cancellationToken))
            {
                succeeded = response.IsSuccessStatusCode;
                body = await response.Content.ReadAsStringAsync();
            }

            if (succeeded)
                return await StoreTokensAsync(JsonSerializer.Deserialize<TokenResponse>(body));

            var message = ReadMessage(body);

            if (message.IndexOf("authorization_pending", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (message.IndexOf("slow_down", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                intervalSeconds += 5;
                continue;
            }

            // access_denied, expired_token, invalid device code: there is nothing left to wait for.
            Logger.Error(new InvalidOperationException(body), "Twitch device authorization was refused");
            return false;
        }

        return false;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (_tokens == null || string.IsNullOrEmpty(_tokens.AccessToken)) return null;

        // Refresh a few minutes early so a call never races the expiry.
        if (DateTime.UtcNow < _tokens.ExpiresAtUtc.AddMinutes(-5))
            return _tokens.AccessToken;

        return await RefreshAsync() ? _tokens?.AccessToken : null;
    }

    public async Task<bool> RefreshAsync()
    {
        if (_tokens == null || string.IsNullOrEmpty(_tokens.RefreshToken)) return false;

        await _refreshLock.WaitAsync();
        try
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = TwitchConfig.ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _tokens.RefreshToken
            };

            string body;
            bool succeeded;

            using (var content = new FormUrlEncodedContent(form))
            using (var response = await _httpClient.PostAsync(TwitchConfig.TokenEndpoint, content))
            {
                succeeded = response.IsSuccessStatusCode;
                body = await response.Content.ReadAsStringAsync();
            }

            if (!succeeded)
            {
                // A refused refresh means the grant is gone for good, so stop pretending we are
                // connected and make the user reconnect.
                Logger.Error(new InvalidOperationException(body), "Twitch refused to refresh the token");
                Disconnect();
                return false;
            }

            var token = JsonSerializer.Deserialize<TokenResponse>(body);
            if (token == null || string.IsNullOrEmpty(token.AccessToken)) return false;

            _tokens.AccessToken = token.AccessToken;
            if (!string.IsNullOrEmpty(token.RefreshToken))
                _tokens.RefreshToken = token.RefreshToken;
            _tokens.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);

            TwitchTokenStore.Save(_tokens);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Twitch token refresh failed");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Disconnect()
    {
        var accessToken = _tokens?.AccessToken;

        _tokens = null;
        TwitchTokenStore.Clear();

        SettingsManager.Default.TwitchBroadcasterLogin = "";
        SettingsManager.Default.Save();

        if (!string.IsNullOrEmpty(accessToken))
            _ = RevokeAsync(accessToken);

        ConnectionChanged?.Invoke();
    }

    private async Task<bool> StoreTokensAsync(TokenResponse token)
    {
        if (token == null || string.IsNullOrEmpty(token.AccessToken)) return false;

        var tokens = new TwitchTokens
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600)
        };

        // The broadcaster id is required by every later call, so resolve it once here.
        var user = await _api.GetCurrentUserAsync(tokens.AccessToken);
        if (!user.IsSuccess || user.Value == null)
        {
            Logger.Error(
                new InvalidOperationException(user.Error ?? "Twitch returned no user"),
                "Could not read the Twitch account for the new token");
            return false;
        }

        tokens.BroadcasterId = user.Value.Id;
        tokens.BroadcasterLogin = user.Value.Login;

        _tokens = tokens;
        TwitchTokenStore.Save(tokens);

        SettingsManager.Default.TwitchBroadcasterLogin = tokens.BroadcasterLogin;
        SettingsManager.Default.Save();

        ConnectionChanged?.Invoke();
        return true;
    }

    private async Task RevokeAsync(string accessToken)
    {
        try
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = TwitchConfig.ClientId,
                ["token"] = accessToken
            };

            using (var content = new FormUrlEncodedContent(form))
            using (await _httpClient.PostAsync(TwitchConfig.RevokeEndpoint, content))
            {
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Revoking the Twitch token failed");
        }
    }

    /// <summary>Pulls the error slug out of a token response, falling back to the raw body.</summary>
    private static string ReadMessage(string body)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<TokenResponse>(body);
            return parsed?.Message ?? body ?? string.Empty;
        }
        catch
        {
            return body ?? string.Empty;
        }
    }
}
