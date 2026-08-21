//

using System.Threading.Tasks;
using AutoHitCounter.Enums;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Services.Twitch;
using AutoHitCounter.Utilities;
using NSubstitute;
using Xunit;

namespace AutoHitCounter.Tests.Services;

/// <summary>
/// These drive SettingsManager.Default in memory only: no test here reaches a code path that
/// calls Save(), so nothing touches the real settings file.
/// </summary>
public class TwitchCategoryServiceTests
{
    private const string EldenRingCategoryId = "512953";

    private readonly ITwitchAuthService _auth = Substitute.For<ITwitchAuthService>();
    private readonly ITwitchApiClient _api = Substitute.For<ITwitchApiClient>();
    private readonly TwitchCategoryService _sut;

    private readonly Game _eldenRing = new()
    {
        Title = GameTitle.EldenRing,
        GameName = "Elden Ring",
        ProcessName = "eldenring"
    };

    public TwitchCategoryServiceTests()
    {
        SettingsManager.Default.TwitchIntegrationEnabled = true;
        SettingsManager.Default.TwitchOnlyWhenLive = true;
        SettingsManager.Default.TwitchCategoryMappings = "";

        _auth.IsConnected.Returns(true);
        _auth.BroadcasterId.Returns("42");
        _auth.GetAccessTokenAsync().Returns(Task.FromResult("token"));

        _api.IsLiveAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Ok(true)));
        _api.UpdateChannelCategoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Ok(true)));

        _sut = new TwitchCategoryService(_auth, _api);
    }

    [Fact]
    public async Task Sync_UsesTheDefaultCategoryForABuiltInGame()
    {
        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.Received(1).UpdateChannelCategoryAsync("token", "42", EldenRingCategoryId);
    }

    [Fact]
    public async Task Sync_IntegrationDisabled_DoesNothing()
    {
        SettingsManager.Default.TwitchIntegrationEnabled = false;

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_NotConnected_DoesNothing()
    {
        _auth.IsConnected.Returns(false);

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_NullGame_DoesNothing()
    {
        await _sut.SyncCategoryAsync(null);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_UnmappedCustomGame_DoesNotTouchTheChannel()
    {
        var custom = new Game { Title = GameTitle.Manual, GameName = "Hollow Knight", IsManual = true };

        await _sut.SyncCategoryAsync(custom);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_OnlyWhenLiveAndOffline_DoesNotTouchTheChannel()
    {
        _api.IsLiveAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Ok(false)));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_OfflineButOnlyWhenLiveIsOff_StillSetsTheCategory()
    {
        SettingsManager.Default.TwitchOnlyWhenLive = false;
        _api.IsLiveAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Ok(false)));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.Received(1).UpdateChannelCategoryAsync("token", "42", EldenRingCategoryId);
        await _api.DidNotReceiveWithAnyArgs().IsLiveAsync(default, default);
    }

    [Fact]
    public async Task Sync_LiveCheckFails_DoesNotTouchTheChannel()
    {
        _api.IsLiveAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Fail("boom")));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_SameGameTwice_OnlyCallsTwitchOnce()
    {
        await _sut.SyncCategoryAsync(_eldenRing);
        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.Received(1).UpdateChannelCategoryAsync("token", "42", EldenRingCategoryId);
    }

    [Fact]
    public async Task Sync_Unauthorized_RefreshesAndRetriesOnce()
    {
        _api.UpdateChannelCategoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(
                Task.FromResult(TwitchApiResult<bool>.Unauthorized()),
                Task.FromResult(TwitchApiResult<bool>.Ok(true)));
        _auth.RefreshAsync().Returns(Task.FromResult(true));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _auth.Received(1).RefreshAsync();
        await _api.Received(2).UpdateChannelCategoryAsync("token", "42", EldenRingCategoryId);
    }

    [Fact]
    public async Task Sync_UnauthorizedAndRefreshFails_GivesUp()
    {
        _api.UpdateChannelCategoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Unauthorized()));
        _auth.RefreshAsync().Returns(Task.FromResult(false));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.Received(1).UpdateChannelCategoryAsync("token", "42", EldenRingCategoryId);
    }

    [Fact]
    public async Task Sync_NoValidToken_DoesNotTouchTheChannel()
    {
        _auth.GetAccessTokenAsync().Returns(Task.FromResult<string>(null));

        await _sut.SyncCategoryAsync(_eldenRing);

        await _api.DidNotReceiveWithAnyArgs()
            .UpdateChannelCategoryAsync(default, default, default);
    }

    [Fact]
    public async Task Sync_ApiFailure_IsSwallowedAndReported()
    {
        string status = null;
        _sut.StatusChanged += s => status = s;

        _api.UpdateChannelCategoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(TwitchApiResult<bool>.Fail("500")));

        await _sut.SyncCategoryAsync(_eldenRing);

        Assert.NotNull(status);
    }
}
