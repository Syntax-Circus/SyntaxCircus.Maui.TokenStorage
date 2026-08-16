namespace SyntaxCircus.Maui.TokenStorage;

public static class TokenStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISecureTokenStorage"/> (backed by <see cref="SecureStorage.Default"/>),
    /// <see cref="IPreferences"/> (backed by <see cref="Preferences.Default"/>), and
    /// <see cref="SessionTokenStore"/> as singletons.
    /// </summary>
    public static IServiceCollection AddSecureTokenStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(SecureStorage.Default);
        services.AddSingleton(Preferences.Default);
        services.AddSingleton<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<SessionTokenStore>();

        return services;
    }
}
