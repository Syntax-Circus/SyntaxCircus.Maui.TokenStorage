namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class SessionTokenStoreTests
{
    private const string AccessTokenKey = "syntaxcircus_access_token";
    private const string RefreshTokenKey = "syntaxcircus_refresh_token";
    private const string ExpiresAtKey = "syntaxcircus_expires_at";
    private const string UserIdKey = "syntaxcircus_user_id";
    private const string EmailKey = "syntaxcircus_email";
    private const string IsAnonymousKey = "syntaxcircus_is_anonymous";

    private static (SessionTokenStore Store, ISecureTokenStorage SecureStorage, IPreferences Preferences) CreateStore()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var preferences = Substitute.For<IPreferences>();
        var store = new SessionTokenStore(secureStorage, preferences, NullLogger<SessionTokenStore>.Instance);
        return (store, secureStorage, preferences);
    }

    [Fact]
    public async Task StoreAsync_NullTokens_ThrowsArgumentNullException()
    {
        var (store, _, _) = CreateStore();

        await Should.ThrowAsync<ArgumentNullException>(() => store.StoreAsync(null!));
    }

    [Fact]
    public async Task StoreAsync_StoresAccessTokenInSecureStorage()
    {
        var (store, secureStorage, _) = CreateStore();
        var tokens = new SessionTokens("access-1", null, DateTimeOffset.UtcNow.AddHours(1), null, null, IsAnonymous: true);

        await store.StoreAsync(tokens);

        await secureStorage.Received(1).StoreAsync(AccessTokenKey, "access-1");
    }

    [Fact]
    public async Task StoreAsync_NullRefreshToken_DoesNotStoreRefreshToken()
    {
        var (store, secureStorage, _) = CreateStore();
        var tokens = new SessionTokens("access-1", null, DateTimeOffset.UtcNow.AddHours(1), null, null, IsAnonymous: true);

        await store.StoreAsync(tokens);

        await secureStorage.DidNotReceive().StoreAsync(RefreshTokenKey, Arg.Any<string>());
    }

    [Fact]
    public async Task StoreAsync_RefreshTokenProvided_StoresRefreshToken()
    {
        var (store, secureStorage, _) = CreateStore();
        var tokens = new SessionTokens("access-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1), null, null, IsAnonymous: true);

        await store.StoreAsync(tokens);

        await secureStorage.Received(1).StoreAsync(RefreshTokenKey, "refresh-1");
    }

    [Fact]
    public async Task StoreAsync_SetsExpiryAsUnixSecondsString()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
        var tokens = new SessionTokens("access-1", null, expiresAt, null, null, IsAnonymous: true);

        await store.StoreAsync(tokens);

        preferences.Received(1).Set(ExpiresAtKey, "2000000000");
    }

    [Fact]
    public async Task StoreAsync_NullUserIdAndEmail_StoresEmptyStrings()
    {
        var (store, _, preferences) = CreateStore();
        var tokens = new SessionTokens("access-1", null, DateTimeOffset.UtcNow.AddHours(1), null, null, IsAnonymous: true);

        await store.StoreAsync(tokens);

        preferences.Received(1).Set(UserIdKey, string.Empty);
        preferences.Received(1).Set(EmailKey, string.Empty);
    }

    [Fact]
    public async Task StoreAsync_SetsIsAnonymousFlag()
    {
        var (store, _, preferences) = CreateStore();
        var tokens = new SessionTokens("access-1", null, DateTimeOffset.UtcNow.AddHours(1), "user1", "user@example.com", IsAnonymous: false);

        await store.StoreAsync(tokens);

        preferences.Received(1).Set(UserIdKey, "user1");
        preferences.Received(1).Set(EmailKey, "user@example.com");
        preferences.Received(1).Set(IsAnonymousKey, false);
    }

    [Fact]
    public void HasValidAccessToken_NoExpiryStored_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns(string.Empty);

        store.HasValidAccessToken().ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_ZeroExpiry_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns("0");

        store.HasValidAccessToken().ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_ExpiryFarInFuture_ReturnsTrue()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get(ExpiresAtKey, string.Empty).Returns(expiresAtSeconds);

        store.HasValidAccessToken().ShouldBeTrue();
    }

    [Fact]
    public void HasValidAccessToken_AlreadyExpired_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get(ExpiresAtKey, string.Empty).Returns(expiresAtSeconds);

        store.HasValidAccessToken().ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_WithinClockSkewMargin_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        var expiresAtSeconds = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get(ExpiresAtKey, string.Empty).Returns(expiresAtSeconds);

        store.HasValidAccessToken().ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_UnparseableExpiry_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns("not-a-number");

        store.HasValidAccessToken().ShouldBeFalse();
    }

    [Fact]
    public void GetExpiresAt_NothingStored_ReturnsNull()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns(string.Empty);

        store.GetExpiresAt().ShouldBeNull();
    }

    [Fact]
    public void GetExpiresAt_ZeroExpiry_ReturnsNull()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns("0");

        store.GetExpiresAt().ShouldBeNull();
    }

    [Fact]
    public void GetExpiresAt_UnparseableExpiry_ReturnsNull()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns("not-a-number");

        store.GetExpiresAt().ShouldBeNull();
    }

    [Fact]
    public void GetExpiresAt_ValueStored_ReturnsParsedValue()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(ExpiresAtKey, string.Empty).Returns("2000000000");

        store.GetExpiresAt().ShouldBe(DateTimeOffset.FromUnixTimeSeconds(2_000_000_000));
    }

    [Fact]
    public void GetExpiresAt_TruncatesToSeconds_MatchesStoreAsyncFormat()
    {
        var (store, _, preferences) = CreateStore();
        // Sub-second precision is lost by the Unix-seconds string storage format StoreAsync
        // uses — assert against the truncated value, not the original.
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).AddMilliseconds(456);
        var storedValue = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        preferences.Get(ExpiresAtKey, string.Empty).Returns(storedValue);

        store.GetExpiresAt().ShouldBe(DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds()));
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsStoredValue()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync(AccessTokenKey).Returns("access-1");

        var result = await store.GetAccessTokenAsync();

        result.ShouldBe("access-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_StorageThrows_ReturnsNullInsteadOfThrowing()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync(AccessTokenKey).Returns(Task.FromException<string?>(new InvalidOperationException("keystore unavailable")));

        var result = await store.GetAccessTokenAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRefreshTokenAsync_ReturnsStoredValue()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync(RefreshTokenKey).Returns("refresh-1");

        var result = await store.GetRefreshTokenAsync();

        result.ShouldBe("refresh-1");
    }

    [Fact]
    public async Task GetRefreshTokenAsync_StorageThrows_ReturnsNullInsteadOfThrowing()
    {
        var (store, secureStorage, _) = CreateStore();
        secureStorage.RetrieveAsync(RefreshTokenKey).Returns(Task.FromException<string?>(new InvalidOperationException("keystore unavailable")));

        var result = await store.GetRefreshTokenAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public void IsAnonymous_DefaultsToTrue()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(IsAnonymousKey, true).Returns(true);

        store.IsAnonymous().ShouldBeTrue();
    }

    [Fact]
    public void IsAnonymous_FalseStored_ReturnsFalse()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(IsAnonymousKey, true).Returns(false);

        store.IsAnonymous().ShouldBeFalse();
    }

    [Fact]
    public void GetUserId_EmptyStored_ReturnsNull()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(UserIdKey, string.Empty).Returns(string.Empty);

        store.GetUserId().ShouldBeNull();
    }

    [Fact]
    public void GetUserId_NonEmptyStored_ReturnsValue()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(UserIdKey, string.Empty).Returns("user1");

        store.GetUserId().ShouldBe("user1");
    }

    [Fact]
    public void GetEmail_EmptyStored_ReturnsNull()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(EmailKey, string.Empty).Returns(string.Empty);

        store.GetEmail().ShouldBeNull();
    }

    [Fact]
    public void GetEmail_NonEmptyStored_ReturnsValue()
    {
        var (store, _, preferences) = CreateStore();
        preferences.Get(EmailKey, string.Empty).Returns("user@example.com");

        store.GetEmail().ShouldBe("user@example.com");
    }

    [Fact]
    public async Task ClearAsync_RemovesTokensFromSecureStorage()
    {
        var (store, secureStorage, _) = CreateStore();

        await store.ClearAsync();

        await secureStorage.Received(1).RemoveAsync(AccessTokenKey);
        await secureStorage.Received(1).RemoveAsync(RefreshTokenKey);
    }

    [Fact]
    public async Task ClearAsync_RemovesMetadataFromPreferences()
    {
        var (store, _, preferences) = CreateStore();

        await store.ClearAsync();

        preferences.Received(1).Remove(ExpiresAtKey);
        preferences.Received(1).Remove(UserIdKey);
        preferences.Received(1).Remove(EmailKey);
        preferences.Received(1).Remove(IsAnonymousKey);
    }
}
