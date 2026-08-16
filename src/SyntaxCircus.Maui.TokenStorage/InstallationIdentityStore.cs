namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>
/// Gets or creates a per-device <see cref="InstallationIdentity"/> (a random <see cref="Guid"/>
/// plus a random base64 credential), persisting both in <see cref="ISecureTokenStorage"/> on
/// first access. Useful for installation-credential auth models, where a device authenticates
/// with a self-generated identity rather than an OIDC access/refresh token pair — see
/// <see cref="KeyedSessionStore{TSession}"/> for persisting the session obtained from that
/// identity.
/// </summary>
public sealed class InstallationIdentityStore(ISecureTokenStorage secureTokenStorage) : IDisposable
{
    private const string IdKey = "syntaxcircus_installation_id";
    private const string CredentialKey = "syntaxcircus_installation_credential";
    private const int CredentialByteLength = 32;

    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Returns the previously persisted <see cref="InstallationIdentity"/>, or generates,
    /// persists, and returns a new one on first call. Safe to call concurrently from within the
    /// same process — a lock guards the generate-and-persist path so concurrent first calls don't
    /// race and produce two different identities.
    /// </summary>
    public async Task<InstallationIdentity> GetOrCreateAsync()
    {
        var existing = await TryReadExistingAsync().ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock: another in-process caller may have created the
            // identity while this call was waiting.
            existing = await TryReadExistingAsync().ConfigureAwait(false);
            if (existing is not null)
            {
                return existing;
            }

            var identity = new InstallationIdentity(Guid.NewGuid(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(CredentialByteLength)));
            await secureTokenStorage.StoreAsync(IdKey, identity.Id.ToString()).ConfigureAwait(false);
            await secureTokenStorage.StoreAsync(CredentialKey, identity.Credential).ConfigureAwait(false);
            return identity;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<InstallationIdentity?> TryReadExistingAsync()
    {
        var id = await secureTokenStorage.RetrieveAsync(IdKey).ConfigureAwait(false);
        var credential = await secureTokenStorage.RetrieveAsync(CredentialKey).ConfigureAwait(false);
        return Guid.TryParse(id, out var parsed) && !string.IsNullOrWhiteSpace(credential)
            ? new InstallationIdentity(parsed, credential)
            : null;
    }

    public void Dispose() => gate.Dispose();
}
