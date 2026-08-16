namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>
/// <see cref="ISecureTokenStorage"/> over <see cref="ISecureStorage"/>, constructor-injected
/// rather than reaching for the static <see cref="SecureStorage.Default"/> facade so consumers
/// (and tests) can substitute their own implementation.
/// </summary>
public sealed class SecureTokenStorage(ISecureStorage secureStorage) : ISecureTokenStorage
{
    public Task StoreAsync(string key, string value) => secureStorage.SetAsync(key, value);

    public Task<string?> RetrieveAsync(string key) => secureStorage.GetAsync(key);

    public Task RemoveAsync(string key)
    {
        secureStorage.Remove(key);
        return Task.CompletedTask;
    }
}
