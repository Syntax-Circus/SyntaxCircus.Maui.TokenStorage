namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class SessionTokensTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var tokens = new SessionTokens("access", "refresh", expiresAt, "user1", "user@example.com", IsAnonymous: false);

        tokens.AccessToken.ShouldBe("access");
        tokens.RefreshToken.ShouldBe("refresh");
        tokens.ExpiresAt.ShouldBe(expiresAt);
        tokens.UserId.ShouldBe("user1");
        tokens.Email.ShouldBe("user@example.com");
        tokens.IsAnonymous.ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var a = new SessionTokens("access", null, expiresAt, null, null, IsAnonymous: true);
        var b = new SessionTokens("access", null, expiresAt, null, null, IsAnonymous: true);

        a.ShouldBe(b);
    }
}
