namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>Session-layer token persistence, abstracted so consumers can constructor-inject and test against it directly.</summary>
public interface ISessionTokenStore
{
    Task StoreAsync(SessionTokens tokens);

    bool HasValidAccessToken();

    Task<string?> GetAccessTokenAsync();

    Task<string?> GetRefreshTokenAsync();

    bool IsAnonymous();

    string? GetUserId();

    string? GetEmail();

    DateTimeOffset? GetExpiresAt();

    Task ClearAsync();
}
