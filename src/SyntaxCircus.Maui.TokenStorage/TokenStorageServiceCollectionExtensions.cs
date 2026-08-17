namespace SyntaxCircus.Maui.TokenStorage;

public static class TokenStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISecureTokenStorage"/> (backed by <see cref="SecureStorage.Default"/>),
    /// <see cref="IPreferences"/> (backed by <see cref="Preferences.Default"/>), and
    /// <see cref="SessionTokenStore"/> (also exposed as <see cref="ISessionTokenStore"/>, same
    /// singleton instance) as singletons.
    /// </summary>
    public static IServiceCollection AddSecureTokenStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(SecureStorage.Default);
        services.AddSingleton(Preferences.Default);
        services.AddSingleton<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<SessionTokenStore>();
        services.AddSingleton<ISessionTokenStore>(sp => sp.GetRequiredService<SessionTokenStore>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="InstallationIdentityStore"/> as a singleton, along with
    /// <see cref="ISecureTokenStorage"/>/<see cref="IPreferences"/> if not already registered
    /// (idempotent — safe to call alongside <see cref="AddSecureTokenStorage"/>).
    /// </summary>
    public static IServiceCollection AddInstallationIdentityStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(SecureStorage.Default);
        services.TryAddSingleton(Preferences.Default);
        services.TryAddSingleton<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<InstallationIdentityStore>();

        return services;
    }

    /// <summary>
    /// Registers a <see cref="KeyedSessionStore{TSession}"/> singleton for
    /// <typeparamref name="TSession"/>, keyed under <paramref name="keyPrefix"/>. Also registers
    /// <see cref="ISecureTokenStorage"/>/<see cref="IPreferences"/> if not already registered
    /// (idempotent). Call once per distinct <typeparamref name="TSession"/> usage — calling this
    /// again for the same <typeparamref name="TSession"/> with a different
    /// <paramref name="keyPrefix"/> replaces the earlier registration rather than adding a second
    /// one; use a distinct <typeparamref name="TSession"/> type per use case, or construct extra
    /// instances directly with <c>new KeyedSessionStore&lt;TSession&gt;(...)</c>.
    /// </summary>
    public static IServiceCollection AddKeyedSessionStore<TSession>(
        this IServiceCollection services,
        string keyPrefix,
        TimeSpan? clockSkewMargin = null,
        JsonSerializerOptions? jsonSerializerOptions = null)
        where TSession : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        services.TryAddSingleton(SecureStorage.Default);
        services.TryAddSingleton(Preferences.Default);
        services.TryAddSingleton<ISecureTokenStorage, SecureTokenStorage>();

        services.AddSingleton(provider => new KeyedSessionStore<TSession>(
            provider.GetRequiredService<ISecureTokenStorage>(),
            provider.GetRequiredService<IPreferences>(),
            provider.GetRequiredService<ILogger<KeyedSessionStore<TSession>>>(),
            keyPrefix,
            clockSkewMargin,
            jsonSerializerOptions));

        return services;
    }
}
