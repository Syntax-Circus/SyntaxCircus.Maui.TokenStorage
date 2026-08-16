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

    private sealed record TestSession(string Name);

    [Fact]
    public void AddInstallationIdentityStore_NullServices_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            TokenStorageServiceCollectionExtensions.AddInstallationIdentityStore(null!));

    [Fact]
    public void AddInstallationIdentityStore_RegistersResolvableSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInstallationIdentityStore();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<InstallationIdentityStore>()
            .ShouldBeSameAs(provider.GetRequiredService<InstallationIdentityStore>());
        provider.GetRequiredService<ISecureTokenStorage>().ShouldBeOfType<SecureTokenStorage>();
    }

    [Fact]
    public void AddKeyedSessionStore_NullServices_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            TokenStorageServiceCollectionExtensions.AddKeyedSessionStore<TestSession>(null!, "prefix"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKeyedSessionStore_NullOrWhitespaceKeyPrefix_ThrowsArgumentException(string? keyPrefix)
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddKeyedSessionStore<TestSession>(keyPrefix!));
    }

    [Fact]
    public void AddKeyedSessionStore_RegistersResolvableSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddKeyedSessionStore<TestSession>("test");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<KeyedSessionStore<TestSession>>()
            .ShouldBeSameAs(provider.GetRequiredService<KeyedSessionStore<TestSession>>());
    }

    [Fact]
    public void AddSecureTokenStorage_ThenAddInstallationIdentityStore_DoesNotDuplicateSecureStorageRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSecureTokenStorage();
        services.AddInstallationIdentityStore();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISecureTokenStorage>().Count().ShouldBe(1);
        provider.GetRequiredService<ISecureTokenStorage>().ShouldBeOfType<SecureTokenStorage>();
    }
}
