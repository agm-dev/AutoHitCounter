//

namespace AutoHitCounter.Utilities;

public static class TwitchConfig
{
    /// <summary>
    /// Client id of the registered Auto Hit Counter Twitch application. The app is registered as a
    /// public client, so this identifies the application and is not a secret: every user still
    /// authorizes their own channel and gets their own token.
    /// </summary>
    public const string DefaultClientId = "c45ffymxwm2vi5xfpkk7to8ton06hh";

    /// <summary>The only scope needed to change the channel category.</summary>
    public const string Scopes = "channel:manage:broadcast";

    public const string DeviceEndpoint = "https://id.twitch.tv/oauth2/device";
    public const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";
    public const string RevokeEndpoint = "https://id.twitch.tv/oauth2/revoke";
    public const string HelixBaseUrl = "https://api.twitch.tv/helix";

    public const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    /// <summary>The user's own client id when they set one, otherwise the built-in application.</summary>
    public static string ClientId
    {
        get
        {
            var configured = SettingsManager.Default.TwitchClientId;
            return string.IsNullOrWhiteSpace(configured) ? DefaultClientId : configured.Trim();
        }
    }
}
