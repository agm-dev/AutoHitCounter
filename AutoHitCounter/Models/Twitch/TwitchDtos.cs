//

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoHitCounter.Models.Twitch;

/// <summary>Response of POST https://id.twitch.tv/oauth2/device.</summary>
public class DeviceCodeResponse
{
    [JsonPropertyName("device_code")] public string DeviceCode { get; set; }
    [JsonPropertyName("user_code")] public string UserCode { get; set; }
    [JsonPropertyName("verification_uri")] public string VerificationUri { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("interval")] public int Interval { get; set; }
}

/// <summary>
/// Response of POST https://id.twitch.tv/oauth2/token. On failure Twitch returns the same shape
/// with <see cref="Message"/> filled in (authorization_pending, slow_down, access_denied...).
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; }
}

/// <summary>Every Helix endpoint wraps its payload in a "data" array.</summary>
public class HelixList<T>
{
    [JsonPropertyName("data")] public List<T> Data { get; set; }
}

public class HelixUser
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("login")] public string Login { get; set; }
    [JsonPropertyName("display_name")] public string DisplayName { get; set; }
}

public class HelixGame
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class HelixStream
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
}
