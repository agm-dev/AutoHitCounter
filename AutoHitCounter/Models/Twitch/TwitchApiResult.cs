//

namespace AutoHitCounter.Models.Twitch;

/// <summary>
/// Outcome of a Helix call. <see cref="IsUnauthorized"/> is kept apart from the other failures
/// because it is the one case worth retrying after refreshing the token.
/// </summary>
public class TwitchApiResult<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsUnauthorized { get; private set; }
    public string Error { get; private set; }
    public T Value { get; private set; }

    public static TwitchApiResult<T> Ok(T value) =>
        new TwitchApiResult<T> { IsSuccess = true, Value = value };

    public static TwitchApiResult<T> Unauthorized() =>
        new TwitchApiResult<T> { IsUnauthorized = true, Error = "Unauthorized" };

    public static TwitchApiResult<T> Fail(string error) =>
        new TwitchApiResult<T> { Error = error };
}
