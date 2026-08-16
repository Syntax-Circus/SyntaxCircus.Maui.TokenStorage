namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>
/// A per-device identity — a random <see cref="Guid"/> plus a random credential — generated once
/// by <see cref="InstallationIdentityStore"/> and persisted for the lifetime of the install.
/// </summary>
public sealed record InstallationIdentity(Guid Id, string Credential);
