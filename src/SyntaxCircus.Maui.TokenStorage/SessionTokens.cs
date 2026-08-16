namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>The token set and session metadata <see cref="SessionTokenStore"/> persists.</summary>
public sealed record SessionTokens(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string? UserId,
    string? Email,
    bool IsAnonymous);
