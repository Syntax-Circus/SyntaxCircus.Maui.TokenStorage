# SyntaxCircus.Maui.TokenStorage

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Maui.TokenStorage/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Maui.TokenStorage/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Maui.TokenStorage.svg)](https://www.nuget.org/packages/SyntaxCircus.Maui.TokenStorage)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

Secure auth token storage for MAUI apps: an injectable wrapper over `ISecureStorage`, and a session layer that splits sensitive tokens (secure storage) from non-sensitive metadata (`IPreferences`) so expiry checks don't need a keystore round-trip on every read.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Targets

`net10.0-android` and `net10.0-ios`. Depends on `Microsoft.Maui.Controls` — as of current .NET MAUI there's no narrower package that exposes `ISecureStorage`/`IPreferences` on their own, so a storage-only library still pulls in the full Controls surface. If your app already references `Microsoft.Maui.Controls` (nearly all MAUI apps do), this adds nothing new.

## Setup

```csharp
// MauiProgram.cs
builder.Services.AddSecureTokenStorage();
```

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

## Why expiry lives in Preferences, not secure storage

`HasValidAccessToken()` is called far more often than the token is actually read — every outgoing API call, typically — and a keystore round-trip on each of those is unnecessary overhead for a non-sensitive value. Expiry is also stored as a **string**, not a raw Unix-seconds integer: iOS's `NSUserDefaults` (what `IPreferences` maps to under the hood) stores integers as `double`, which silently loses precision for large epoch values. Storing the string form and parsing it back avoids that class of bug entirely.

## Testing

`ISecureTokenStorage` is the seam — substitute your own implementation (or a test double) instead of `SecureTokenStorage` to unit test code that depends on `SessionTokenStore` without touching the platform keystore.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
