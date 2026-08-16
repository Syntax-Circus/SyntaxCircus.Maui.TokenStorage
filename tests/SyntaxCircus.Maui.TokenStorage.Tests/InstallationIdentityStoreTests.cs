namespace SyntaxCircus.Maui.TokenStorage.Tests;

public class InstallationIdentityStoreTests
{
    private const string IdKey = "syntaxcircus_installation_id";
    private const string CredentialKey = "syntaxcircus_installation_credential";

    private static (InstallationIdentityStore Store, ISecureTokenStorage SecureStorage) CreateStore()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        var store = new InstallationIdentityStore(secureStorage);
        return (store, secureStorage);
    }

    [Fact]
    public async Task GetOrCreateAsync_NothingStored_GeneratesAndPersistsNewIdentity()
    {
        var (store, secureStorage) = CreateStore();

        var identity = await store.GetOrCreateAsync();

        identity.Id.ShouldNotBe(Guid.Empty);
        Convert.FromBase64String(identity.Credential).Length.ShouldBe(32);
        await secureStorage.Received(1).StoreAsync(IdKey, identity.Id.ToString());
        await secureStorage.Received(1).StoreAsync(CredentialKey, identity.Credential);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingValidIdentity_ReturnsStoredValuesWithoutWriting()
    {
        var (store, secureStorage) = CreateStore();
        var id = Guid.NewGuid();
        secureStorage.RetrieveAsync(IdKey).Returns(id.ToString());
        secureStorage.RetrieveAsync(CredentialKey).Returns("existing-credential");

        var identity = await store.GetOrCreateAsync();

        identity.ShouldBe(new InstallationIdentity(id, "existing-credential"));
        await secureStorage.DidNotReceive().StoreAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetOrCreateAsync_MalformedStoredId_TreatsAsAbsentAndGeneratesNew()
    {
        var (store, secureStorage) = CreateStore();
        secureStorage.RetrieveAsync(IdKey).Returns("not-a-guid");
        secureStorage.RetrieveAsync(CredentialKey).Returns("existing-credential");

        var identity = await store.GetOrCreateAsync();

        identity.Id.ShouldNotBe(Guid.Empty);
        await secureStorage.Received(1).StoreAsync(IdKey, identity.Id.ToString());
        await secureStorage.Received(1).StoreAsync(CredentialKey, identity.Credential);
    }

    [Fact]
    public async Task GetOrCreateAsync_MissingCredentialOnly_TreatsAsAbsentAndGeneratesNew()
    {
        var (store, secureStorage) = CreateStore();
        secureStorage.RetrieveAsync(IdKey).Returns(Guid.NewGuid().ToString());
        secureStorage.RetrieveAsync(CredentialKey).Returns((string?)null);

        var identity = await store.GetOrCreateAsync();

        await secureStorage.Received(1).StoreAsync(IdKey, identity.Id.ToString());
        await secureStorage.Received(1).StoreAsync(CredentialKey, identity.Credential);
    }

    [Fact]
    public async Task GetOrCreateAsync_CalledTwiceSequentially_SecondCallReusesFirstResult()
    {
        var secureStorage = Substitute.For<ISecureTokenStorage>();
        string? storedId = null;
        string? storedCredential = null;
        secureStorage.When(s => s.StoreAsync(IdKey, Arg.Any<string>())).Do(call => storedId = call.ArgAt<string>(1));
        secureStorage.When(s => s.StoreAsync(CredentialKey, Arg.Any<string>())).Do(call => storedCredential = call.ArgAt<string>(1));
        secureStorage.RetrieveAsync(IdKey).Returns(_ => storedId);
        secureStorage.RetrieveAsync(CredentialKey).Returns(_ => storedCredential);
        var store = new InstallationIdentityStore(secureStorage);

        var first = await store.GetOrCreateAsync();
        var second = await store.GetOrCreateAsync();

        second.ShouldBe(first);
        await secureStorage.Received(1).StoreAsync(IdKey, Arg.Any<string>());
        await secureStorage.Received(1).StoreAsync(CredentialKey, Arg.Any<string>());
    }
}
