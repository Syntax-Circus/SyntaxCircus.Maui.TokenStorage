namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>
/// Persists a <see cref="SessionTokens"/> set, splitting sensitive tokens (secure storage) from
/// non-sensitive metadata (<see cref="IPreferences"/>) so expiry checks don't need a keystore
/// round-trip.
/// </summary>
public sealed partial class SessionTokenStore(ISecureTokenStorage secureTokenStorage, IPreferences preferences, ILogger<SessionTokenStore> logger)
{
    private const string AccessTokenKey = "syntaxcircus_access_token";
    private const string RefreshTokenKey = "syntaxcircus_refresh_token";
    private const string ExpiresAtKey = "syntaxcircus_expires_at";
    private const string UserIdKey = "syntaxcircus_user_id";
    private const string EmailKey = "syntaxcircus_email";
    private const string IsAnonymousKey = "syntaxcircus_is_anonymous";

    public async Task StoreAsync(SessionTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        await secureTokenStorage.StoreAsync(AccessTokenKey, tokens.AccessToken).ConfigureAwait(false);

        if (tokens.RefreshToken is not null)
        {
            await secureTokenStorage.StoreAsync(RefreshTokenKey, tokens.RefreshToken).ConfigureAwait(false);
        }

        // Store expiry as a string, not a raw epoch number — iOS NSUserDefaults maps long to
        // double under the hood, which loses precision for large Unix-seconds values.
        var expiresAtSeconds = tokens.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Set(ExpiresAtKey, expiresAtSeconds);
        preferences.Set(UserIdKey, tokens.UserId ?? string.Empty);
        preferences.Set(EmailKey, tokens.Email ?? string.Empty);
        preferences.Set(IsAnonymousKey, tokens.IsAnonymous);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the stored access token's expiry (read from
    /// <see cref="IPreferences"/>, no keystore round-trip) is still valid, with a 60-second
    /// early-expiry margin for clock skew.
    /// </summary>
    public bool HasValidAccessToken()
    {
        var raw = preferences.Get(ExpiresAtKey, string.Empty);
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAtSeconds) || expiresAtSeconds == 0)
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds);
        return DateTimeOffset.UtcNow.AddSeconds(60) < expiresAt;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await secureTokenStorage.RetrieveAsync(AccessTokenKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRetrieveFailed(logger, AccessTokenKey, ex);
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await secureTokenStorage.RetrieveAsync(RefreshTokenKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRetrieveFailed(logger, RefreshTokenKey, ex);
            return null;
        }
    }

    public bool IsAnonymous() => preferences.Get(IsAnonymousKey, defaultValue: true);

    public string? GetUserId()
    {
        var value = preferences.Get(UserIdKey, string.Empty);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public string? GetEmail()
    {
        var value = preferences.Get(EmailKey, string.Empty);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Removes all stored tokens and session metadata.</summary>
    public async Task ClearAsync()
    {
        await secureTokenStorage.RemoveAsync(AccessTokenKey).ConfigureAwait(false);
        await secureTokenStorage.RemoveAsync(RefreshTokenKey).ConfigureAwait(false);
        preferences.Remove(ExpiresAtKey);
        preferences.Remove(UserIdKey);
        preferences.Remove(EmailKey);
        preferences.Remove(IsAnonymousKey);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to retrieve stored token for key '{Key}'.")]
    private static partial void LogRetrieveFailed(ILogger logger, string key, Exception exception);
}
