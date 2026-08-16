namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class TokenStorageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSecureTokenStorage_NullServices_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            TokenStorageServiceCollectionExtensions.AddSecureTokenStorage(null!));

    // SecureStorage.Default/Preferences.Default are the real MAUI Essentials singletons — their
    // GetAsync/Set/Get members throw NotImplementedInReferenceAssemblyException off-device (no
    // platform target here), so this only verifies the DI registration shape resolves; it doesn't
    // exercise actual storage I/O. Real storage behavior is covered one layer down by
    // SecureTokenStorageTests/SessionTokenStoreTests against NSubstitute fakes of the interfaces.
    [Fact]
    public void AddSecureTokenStorage_RegistersResolvableServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecureTokenStorage();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISecureTokenStorage>().ShouldBeOfType<SecureTokenStorage>();
        provider.GetRequiredService<SessionTokenStore>().ShouldNotBeNull();
        provider.GetRequiredService<ISecureStorage>().ShouldNotBeNull();
        provider.GetRequiredService<IPreferences>().ShouldNotBeNull();
    }

    [Fact]
    public void AddSecureTokenStorage_ServicesAreSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecureTokenStorage();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISecureTokenStorage>().ShouldBeSameAs(provider.GetRequiredService<ISecureTokenStorage>());
        provider.GetRequiredService<SessionTokenStore>().ShouldBeSameAs(provider.GetRequiredService<SessionTokenStore>());
    }
}
