# SyntaxCircus.Maui.TokenStorage

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Maui.TokenStorage/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Maui.TokenStorage/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Maui.TokenStorage.svg)](https://www.nuget.org/packages/SyntaxCircus.Maui.TokenStorage)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Secure auth token storage for MAUI apps: an injectable wrapper over `ISecureStorage`, and a session layer that splits sensitive tokens (secure storage) from non-sensitive metadata (`IPreferences`) so expiry checks don't need a keystore round-trip on every read. Ships with `SessionTokenStore` for OIDC-style access/refresh-token sessions, `KeyedSessionStore<TSession>` for any other session shape, and `InstallationIdentityStore` for per-device identity generation.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Targets

`net10.0-android` and `net10.0-ios`. Depends on `Microsoft.Maui.Controls` — as of current .NET MAUI there's no narrower package that exposes `ISecureStorage`/`IPreferences` on their own, so a storage-only library still pulls in the full Controls surface. If your app already references `Microsoft.Maui.Controls` (nearly all MAUI apps do), this adds nothing new.

## Setup

```csharp
// MauiProgram.cs
builder.Services.AddSecureTokenStorage();
```

`AddSecureTokenStorage()` registers `ISecureTokenStorage` and `SessionTokenStore`. `KeyedSessionStore<TSession>` and `InstallationIdentityStore` (see below) are separate, opt-in registrations — `AddKeyedSessionStore<TSession>(keyPrefix)` and `AddInstallationIdentityStore()` — and can be combined with `AddSecureTokenStorage()` freely; all three share the same underlying `ISecureTokenStorage`/`IPreferences` registrations without duplicating them.

## Choosing a store

| Your session looks like... | Use |
|---|---|
| An OIDC-style access token + optional refresh token | `SessionTokenStore` (below) |
| Anything else with an expiry — e.g. an installation-credential session | `KeyedSessionStore<TSession>` |
| A per-device identity (no session/expiry involved) | `InstallationIdentityStore` |

All three share the same underlying idea: sensitive/opaque data goes in `ISecureTokenStorage` (the keystore), and anything needed for a fast, frequent validity check goes in `IPreferences` instead, so that check never touches the keystore.

## Usage

```csharp
await sessionTokenStore.StoreAsync(new SessionTokens(
    AccessToken: response.AccessToken,
    RefreshToken: response.RefreshToken,
    ExpiresAt: response.ExpiresAt,
    UserId: response.UserId,
    Email: response.Email,
    IsAnonymous: false));

if (sessionTokenStore.HasValidAccessToken()) // synchronous — reads Preferences, not the keystore
{
    var accessToken = await sessionTokenStore.GetAccessTokenAsync();
}

await sessionTokenStore.ClearAsync(); // sign out
```

### Custom session shapes — `KeyedSessionStore<TSession>`

For sessions that don't fit `SessionTokens` — for example an installation-credential model, where a device authenticates with a self-generated identity instead of an OIDC token pair:

```csharp
// MauiProgram.cs
builder.Services.AddKeyedSessionStore<InstallationSession>("installation_session");
```

```csharp
public sealed record InstallationSession(string AccessToken, DateTimeOffset ExpiresAt, Guid UserProfileId);

await installationSessionStore.StoreAsync(
    new InstallationSession(response.AccessToken, response.ExpiresAt, response.UserProfileId),
    response.ExpiresAt);

if (installationSessionStore.HasValidSession()) // synchronous — reads Preferences, not the keystore
{
    var session = await installationSessionStore.GetSessionAsync();
}

await installationSessionStore.ClearAsync();
```

`TSession` is serialized as a single JSON blob in secure storage; only its expiry is split out into `IPreferences`. Register one `AddKeyedSessionStore<TSession>(keyPrefix, ...)` per distinct session shape — the `keyPrefix` keeps its storage keys from colliding with `SessionTokenStore`'s or another `KeyedSessionStore<T>`'s. On a trimmed/NativeAOT build, pass a source-generated `JsonSerializerOptions` via the optional `jsonSerializerOptions` parameter if reflection-based serialization isn't available for `TSession`.

### Per-device identity — `InstallationIdentityStore`

For installation-credential auth, where the device itself needs a persistent random identity to authenticate with:

```csharp
// MauiProgram.cs
builder.Services.AddInstallationIdentityStore();
```

```csharp
var identity = await installationIdentityStore.GetOrCreateAsync(); // Guid Id + random base64 Credential
// first call generates and persists; later calls return the same identity
```

## Why expiry lives in Preferences, not secure storage

`HasValidAccessToken()` (and `KeyedSessionStore<TSession>.HasValidSession()`) is called far more often than the token/session is actually read — every outgoing API call, typically — and a keystore round-trip on each of those is unnecessary overhead for a non-sensitive value. Expiry is also stored as a **string**, not a raw Unix-seconds integer: iOS's `NSUserDefaults` (what `IPreferences` maps to under the hood) stores integers as `double`, which silently loses precision for large epoch values. Storing the string form and parsing it back avoids that class of bug entirely. Both `SessionTokenStore` and `KeyedSessionStore<TSession>` use this same trick.

## Testing

`ISecureTokenStorage` is the seam — substitute your own implementation (or a test double) instead of `SecureTokenStorage` to unit test code that depends on `SessionTokenStore`, `KeyedSessionStore<TSession>`, or `InstallationIdentityStore` without touching the platform keystore. `IPreferences` (also constructor-injected throughout) is the equivalent seam for expiry/metadata reads.

## API reference

Every public type in the package, for quick scanning by humans and AI agents alike.

### `ISecureTokenStorage` / `SecureTokenStorage`

Generic key/value secure storage, abstracted so it's constructor-injectable and testable.

| Member | Description |
|---|---|
| `Task StoreAsync(string key, string value)` | Stores a value under `key`. |
| `Task<string?> RetrieveAsync(string key)` | Returns the stored value, or `null` if nothing is stored. |
| `Task RemoveAsync(string key)` | Removes the value stored under `key`. |

`SecureTokenStorage(ISecureStorage secureStorage)` is the default implementation, wrapping `Microsoft.Maui.Storage.ISecureStorage`.

### `SessionTokens`

`record SessionTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt, string? UserId, string? Email, bool IsAnonymous)` — the OIDC-style token set and session metadata `SessionTokenStore` persists.

### `SessionTokenStore`

`SessionTokenStore(ISecureTokenStorage secureTokenStorage, IPreferences preferences, ILogger<SessionTokenStore> logger)`

| Member | Description |
|---|---|
| `Task StoreAsync(SessionTokens tokens)` | Persists the token set: access/refresh tokens go to secure storage, everything else to `IPreferences`. Throws `ArgumentNullException` if `tokens` is `null`. |
| `bool HasValidAccessToken()` | Synchronous. `true` if the stored expiry is more than 60 seconds in the future. Reads only `IPreferences`. |
| `Task<string?> GetAccessTokenAsync()` | Returns the stored access token, or `null` (logged as a warning) if storage access fails. |
| `Task<string?> GetRefreshTokenAsync()` | Returns the stored refresh token, or `null` (logged as a warning) if storage access fails or none was stored. |
| `bool IsAnonymous()` | Defaults to `true` if nothing has been stored yet. |
| `string? GetUserId()` | Returns the stored user id, or `null` if empty/unset. |
| `string? GetEmail()` | Returns the stored email, or `null` if empty/unset. |
| `Task ClearAsync()` | Removes all stored tokens and session metadata (sign-out). |

### `KeyedSessionStore<TSession>` where `TSession : class`

Generalizes `SessionTokenStore`'s secure-storage/`IPreferences` split to an arbitrary caller-defined session payload. `TSession` is JSON-serialized as a single blob in secure storage; only its expiry goes into `IPreferences`.

`KeyedSessionStore(ISecureTokenStorage secureTokenStorage, IPreferences preferences, ILogger<KeyedSessionStore<TSession>> logger, string keyPrefix, TimeSpan? clockSkewMargin = null, JsonSerializerOptions? jsonSerializerOptions = null)` — `keyPrefix` must be unique per store instance (see [Custom session shapes](#custom-session-shapes--keyedsessionstoretsession) above); `clockSkewMargin` defaults to 60 seconds.

| Member | Description |
|---|---|
| `Task StoreAsync(TSession session, DateTimeOffset expiresAt)` | Serializes and stores `session` and its expiry. Throws `ArgumentNullException` if `session` is `null`. |
| `bool HasValidSession()` | Synchronous. `true` if the stored expiry is beyond the clock-skew margin in the future. Reads only `IPreferences`, never secure storage. |
| `Task<TSession?> GetSessionAsync()` | Returns the deserialized session, or `null` (logged as a warning) if nothing is stored, storage access fails, or the stored JSON can't be deserialized. |
| `Task ClearAsync()` | Removes the stored session and its expiry. |

### `InstallationIdentity` / `InstallationIdentityStore`

For installation-credential auth models, where a device authenticates with a self-generated identity rather than an OIDC token.

`record InstallationIdentity(Guid Id, string Credential)`

`InstallationIdentityStore(ISecureTokenStorage secureTokenStorage)`

| Member | Description |
|---|---|
| `Task<InstallationIdentity> GetOrCreateAsync()` | Returns the previously persisted identity, or generates (random `Guid` + random 32-byte base64 credential), persists, and returns a new one on first call. Safe under concurrent in-process calls. |

### `TokenStorageServiceCollectionExtensions`

| Member | Description |
|---|---|
| `IServiceCollection AddSecureTokenStorage()` | Registers `ISecureTokenStorage`/`SessionTokenStore` (and their `ISecureStorage`/`IPreferences` dependencies) as singletons. |
| `IServiceCollection AddInstallationIdentityStore()` | Registers `InstallationIdentityStore` as a singleton; idempotent alongside `AddSecureTokenStorage()`. |
| `IServiceCollection AddKeyedSessionStore<TSession>(string keyPrefix, TimeSpan? clockSkewMargin = null, JsonSerializerOptions? jsonSerializerOptions = null)` | Registers a `KeyedSessionStore<TSession>` singleton for `keyPrefix`; idempotent alongside the other two. Call once per distinct `TSession`. |

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
