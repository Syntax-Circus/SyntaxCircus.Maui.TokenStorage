namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>
/// Persists an arbitrary <typeparamref name="TSession"/> session payload the same way
/// <see cref="SessionTokenStore"/> persists <see cref="SessionTokens"/>: the payload as a single
/// JSON blob in <see cref="ISecureTokenStorage"/>, and its expiry separately in
/// <see cref="IPreferences"/>, so <see cref="HasValidSession"/> is a synchronous check with no
/// keystore round-trip. Use this when a session doesn't fit the access/refresh-token shape
/// <see cref="SessionTokenStore"/> is built around — e.g. an installation-credential session (see
/// <see cref="InstallationIdentityStore"/>) — and construct one instance per distinct session
/// shape/use case, each with its own key prefix (see the constructor).
/// </summary>
public sealed partial class KeyedSessionStore<TSession>
    where TSession : class
{
    private readonly ISecureTokenStorage secureTokenStorage;
    private readonly IPreferences preferences;
    private readonly ILogger<KeyedSessionStore<TSession>> logger;
    private readonly string sessionKey;
    private readonly string expiresAtKey;
    private readonly TimeSpan clockSkewMargin;
    private readonly JsonSerializerOptions? jsonSerializerOptions;

    /// <param name="keyPrefix">
    /// Distinguishes this store's secure-storage/Preferences keys from other stores. Must not
    /// collide with <see cref="SessionTokenStore"/>'s fixed keys, <see cref="InstallationIdentityStore"/>'s
    /// fixed keys, or another <see cref="KeyedSessionStore{TSession}"/> instance's prefix — this
    /// isn't enforced at runtime.
    /// </param>
    /// <param name="clockSkewMargin">Defaults to 60 seconds if not specified.</param>
    /// <param name="jsonSerializerOptions">
    /// An escape hatch for trimmed/NativeAOT apps that need a source-generated
    /// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for
    /// <typeparamref name="TSession"/>; the default uses reflection-based serialization.
    /// </param>
    public KeyedSessionStore(
        ISecureTokenStorage secureTokenStorage,
        IPreferences preferences,
        ILogger<KeyedSessionStore<TSession>> logger,
        string keyPrefix,
        TimeSpan? clockSkewMargin = null,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(secureTokenStorage);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        this.secureTokenStorage = secureTokenStorage;
        this.preferences = preferences;
        this.logger = logger;
        sessionKey = $"{keyPrefix}_session";
        expiresAtKey = $"{keyPrefix}_expires_at";
        this.clockSkewMargin = clockSkewMargin ?? TimeSpan.FromSeconds(60);
        this.jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <summary>Serializes and stores <paramref name="session"/>, along with its expiry.</summary>
    public async Task StoreAsync(TSession session, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(session);

        var json = JsonSerializer.Serialize(session, jsonSerializerOptions);
        await secureTokenStorage.StoreAsync(sessionKey, json).ConfigureAwait(false);

        // Store expiry as a string, not a raw epoch number — iOS NSUserDefaults maps long to
        // double under the hood, which loses precision for large Unix-seconds values.
        var expiresAtSeconds = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Set(expiresAtKey, expiresAtSeconds);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the stored session's expiry (read from
    /// <see cref="IPreferences"/>, no keystore round-trip) is still valid, with a clock-skew
    /// margin (60 seconds by default; see the constructor's <c>clockSkewMargin</c> parameter).
    /// </summary>
    public bool HasValidSession()
    {
        var raw = preferences.Get(expiresAtKey, string.Empty);
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAtSeconds) || expiresAtSeconds == 0)
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds);
        return DateTimeOffset.UtcNow.Add(clockSkewMargin) < expiresAt;
    }

    /// <summary>
    /// Retrieves and deserializes the stored session. Returns <see langword="null"/>, rather than
    /// throwing, if nothing is stored, storage access fails, or the stored value can't be
    /// deserialized as <typeparamref name="TSession"/>.
    /// </summary>
    public async Task<TSession?> GetSessionAsync()
    {
        string? json;
        try
        {
            json = await secureTokenStorage.RetrieveAsync(sessionKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRetrieveFailed(logger, sessionKey, ex);
            return null;
        }

        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TSession>(json, jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            LogDeserializeFailed(logger, sessionKey, ex);
            return null;
        }
    }

    /// <summary>Removes the stored session and its expiry.</summary>
    public async Task ClearAsync()
    {
        await secureTokenStorage.RemoveAsync(sessionKey).ConfigureAwait(false);
        preferences.Remove(expiresAtKey);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to retrieve stored session for key '{Key}'.")]
    private static partial void LogRetrieveFailed(ILogger logger, string key, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize stored session for key '{Key}'.")]
    private static partial void LogDeserializeFailed(ILogger logger, string key, Exception exception);
}
