namespace SyntaxCircus.Maui.TokenStorage;

/// <summary>Generic key/value secure storage, abstracted so it can be constructor-injected and tested.</summary>
public interface ISecureTokenStorage
{
    Task StoreAsync(string key, string value);

    Task<string?> RetrieveAsync(string key);

    Task RemoveAsync(string key);
}
