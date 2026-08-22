//

using System;
using System.Threading;
using System.Threading.Tasks;
using AutoHitCounter.Models.Twitch;

namespace AutoHitCounter.Interfaces;

public interface ITwitchAuthService
{
    /// <summary>Raised when the account is connected or disconnected.</summary>
    event Action ConnectionChanged;

    bool IsConnected { get; }
    string BroadcasterId { get; }
    string BroadcasterLogin { get; }

    /// <summary>Asks Twitch for a device code the user can type into twitch.tv.</summary>
    Task<DeviceCodeResponse> StartDeviceAuthorizationAsync();

    /// <summary>Polls until the user authorizes, the code expires or the token is refused.</summary>
    Task<bool> AwaitAuthorizationAsync(DeviceCodeResponse device, CancellationToken cancellationToken);

    /// <summary>A token that is good to use right now, refreshing first when it is about to expire.</summary>
    Task<string> GetAccessTokenAsync();

    Task<bool> RefreshAsync();

    void Disconnect();
}
