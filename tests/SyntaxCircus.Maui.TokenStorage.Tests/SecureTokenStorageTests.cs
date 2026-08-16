namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class SecureTokenStorageTests
{
    [Fact]
    public async Task StoreAsync_DelegatesToSecureStorageSetAsync()
    {
        var secureStorage = Substitute.For<ISecureStorage>();
        var storage = new SecureTokenStorage(secureStorage);

        await storage.StoreAsync("key1", "value1");

        await secureStorage.Received(1).SetAsync("key1", "value1");
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsValueFromSecureStorage()
    {
        var secureStorage = Substitute.For<ISecureStorage>();
        secureStorage.GetAsync("key1").Returns("value1");
        var storage = new SecureTokenStorage(secureStorage);

        var result = await storage.RetrieveAsync("key1");

        result.ShouldBe("value1");
    }

    [Fact]
    public async Task RetrieveAsync_NoValueStored_ReturnsNull()
    {
        var secureStorage = Substitute.For<ISecureStorage>();
        secureStorage.GetAsync("key1").Returns((string?)null);
        var storage = new SecureTokenStorage(secureStorage);

        var result = await storage.RetrieveAsync("key1");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_CallsSecureStorageRemove()
    {
        var secureStorage = Substitute.For<ISecureStorage>();
        var storage = new SecureTokenStorage(secureStorage);

        await storage.RemoveAsync("key1");

        secureStorage.Received(1).Remove("key1");
    }
}
