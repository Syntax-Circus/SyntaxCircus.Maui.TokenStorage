namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class KeyedSessionStoreTests
{
    private sealed record TestSession(string Name, int Count);

    private static (KeyedSessionStore<TestSession> Store, ISecureTokenStorage SecureStorage, IPreferences Preferences) CreateStore(
        string keyPrefix = "test", TimeSpan? clockSkewMargin = null)
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var preferences = Substitute.For<IPreferences>();
        var store = new KeyedSessionStore<TestSession>(secureStorage, preferences, NullLogger<KeyedSessionStore<TestSession>>.Instance, keyPrefix, clockSkewMargin);
        return (store, secureStorage, preferences);
    }

    [Fact]
    public void Constructor_NullKeyPrefix_ThrowsArgumentException()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var preferences = Substitute.For<IPreferences>();

        Should.Throw<ArgumentException>(() =>
            new KeyedSessionStore<TestSession>(secureStorage, preferences, NullLogger<KeyedSessionStore<TestSession>>.Instance, null!));
    }

    [Fact]
    public void Constructor_WhitespaceKeyPrefix_ThrowsArgumentException()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var preferences = Substitute.For<IPreferences>();

        Should.Throw<ArgumentException>(() =>
            new KeyedSessionStore<TestSession>(secureStorage, preferences, NullLogger<KeyedSessionStore<TestSession>>.Instance, "   "));
    }

    [Fact]
    public async Task StoreAsync_NullSession_ThrowsArgumentNullException()
    {
        var (store, _, _) = CreateStore();

        await Should.ThrowAsync<ArgumentNullException>(() => store.StoreAsync(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task StoreAsync_SerializesSessionAndStoresUnderPrefixedKey()
    {
        var (store, secureStorage, _) = CreateStore();
        var session = new TestSession("alice", 3);

        await store.StoreAsync(session, DateTimeOffset.UtcNow.AddHours(1));

        await secureStorage.Received(1).StoreAsync("test_session", Arg.Is<string>(json => JsonSerializer.Deserialize<TestSession>(json) == session));
    }

    [Fact]
    public async Task StoreAsync_SetsExpiryAsUnixSecondsStringUnderPrefixedKey()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

        await store.StoreAsync(new TestSession("alice", 3), expiresAt);

        preferences.Received(1).Set("test_expires_at", "2000000000");
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsDeserializedSession()
    {
        var (store, secureStorage, _) = CreateStore();
        var session = new TestSession("alice", 3);
        secureStorage.RetrieveAsync("test_session").Returns(JsonSerializer.Serialize(session));

        var result = await store.GetSessionAsync();

        result.ShouldBe(session);
    }

    [Fact]
    public async Task GetSessionAsync_NothingStored_ReturnsNull()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync("test_session").Returns((string?)null);

        var result = await store.GetSessionAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSessionAsync_StorageThrows_ReturnsNullInsteadOfThrowing()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync("test_session").Returns(Task.FromException<string?>(new InvalidOperationException("keystore unavailable")));

        var result = await store.GetSessionAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSessionAsync_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync("test_session").Returns("{not valid json");

        var result = await store.GetSessionAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public void HasValidSession_NoExpiryStored_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get("test_expires_at", string.Empty).Returns(string.Empty);

        store.HasValidSession().ShouldBeFalse();
    }

    [Fact]
    public void HasValidSession_ZeroExpiry_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get("test_expires_at", string.Empty).Returns("0");

        store.HasValidSession().ShouldBeFalse();
    }

    [Fact]
    public void HasValidSession_ExpiryFarInFuture_ReturnsTrue()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get("test_expires_at", string.Empty).Returns(expiresAtSeconds);

        store.HasValidSession().ShouldBeTrue();
    }

    [Fact]
    public void HasValidSession_AlreadyExpired_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get("test_expires_at", string.Empty).Returns(expiresAtSeconds);

        store.HasValidSession().ShouldBeFalse();
    }

    [Fact]
    public void HasValidSession_WithinDefaultClockSkewMargin_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get("test_expires_at", string.Empty).Returns(expiresAtSeconds);

        store.HasValidSession().ShouldBeFalse();
    }

    [Fact]
    public void HasValidSession_CustomClockSkewMargin_UsesOverride()
    {
        var (store, _, preferences) = CreateStore(clockSkewMargin: TimeSpan.FromSeconds(5));
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get("test_expires_at", string.Empty).Returns(expiresAtSeconds);

        store.HasValidSession().ShouldBeTrue();
    }

    [Fact]
    public void HasValidSession_NeverCallsSecureTokenStorage()
    {
        var (store, secureStorage, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get("test_expires_at", string.Empty).Returns(expiresAtSeconds);

        store.HasValidSession();

        secureStorage.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearAsync_RemovesSessionFromSecureStorage()
    {
        var (store, secureStorage, _) = CreateStore();

        await store.ClearAsync();

        await secureStorage.Received(1).RemoveAsync("test_session");
    }

    [Fact]
    public async Task ClearAsync_RemovesExpiryFromPreferences()
    {
        var (store, _, preferences) = CreateStore();

        await store.ClearAsync();

        preferences.Received(1).Remove("test_expires_at");
    }

    [Fact]
    public async Task TwoInstancesWithDifferentKeyPrefixes_DoNotCollide()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var preferences = Substitute.For<IPreferences>();
        var storeA = new KeyedSessionStore<TestSession>(secureStorage, preferences, NullLogger<KeyedSessionStore<TestSession>>.Instance, "a");
        var storeB = new KeyedSessionStore<TestSession>(secureStorage, preferences, NullLogger<KeyedSessionStore<TestSession>>.Instance, "b");

        await storeA.StoreAsync(new TestSession("alice", 1), DateTimeOffset.UtcNow.AddHours(1));
        await storeB.StoreAsync(new TestSession("bob", 2), DateTimeOffset.UtcNow.AddHours(1));

        await secureStorage.Received(1).StoreAsync("a_session", Arg.Any<string>());
        await secureStorage.Received(1).StoreAsync("b_session", Arg.Any<string>());
    }
}
