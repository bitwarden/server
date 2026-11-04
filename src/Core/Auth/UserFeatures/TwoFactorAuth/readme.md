# Two-Factor Authentication

This area of the codebase covers enrollment and management of the multi-factor authentication providers Bitwarden supports — Authenticator (TOTP), YubiKey OTP, Duo (personal and organization), WebAuthn, and Email — plus supporting flows such as recovery codes, login-time challenges, and administrative resets. The sections below document specific aspects of the 2FA domain.

## 2FA remember tokens

### Issuance

When a user completes 2FA, they can optionally select the "Don't ask again on this device for 30 days" checkbox on the `/2fa` screen. When this box is checked, the server records a row in the `TwoFactorRememberToken` table — one row per `(UserId, DeviceId)` — and issues a 2FA remember token (see `TwoFactorRememberTokenable`) carrying a copy of that row's `Stamp`. On a later login the client presents the token as `TwoFactorProviderType.Remember` and skips the 2FA challenge.

### Revocation

A 2FA remember token is only honored while the `Stamp` it carries still matches its corresponding `TwoFactorRememberToken` row. `IRevokeTwoFactorRememberTokensCommand.RevokeAllForUserAsync` writes a new `Stamp` to every row for a user, which stops every 2FA remember token issued against these rows from being honored at once. The rows are kept, so `CreationDate` still records when each device was first issued a 2FA remember token and `RevisionDate` records when it was cut off.

Three flows revoke, all of them teardowns that leave the account with no effective second factor:

| Flow                                                   | Where                                       |
| ------------------------------------------------------ | ------------------------------------------- |
| An administrator resets a member's 2FA                 | `ResetUserTwoFactorCommand.ResetAsync`      |
| A user disables 2FA with a recovery code               | `UserService.RecoverTwoFactorAsync`         |
| A user removes a provider and 2FA is no longer enabled | `UserService.DisableTwoFactorProviderAsync` |

Removing a provider does **not** revoke if the user still has working 2FA, so their 2FA remember tokens stay valid. A provider that requires premium does not count when the user has no premium access, so removing the only non-premium provider revokes even though a premium-only provider remains.

A password change or account key rotation also stops every 2FA remember token from being honored, without a call to `RevokeAllForUserAsync`. Each `TwoFactorRememberTokenable` carries a copy of `User.SecurityStamp` from the time it was issued. The server compares that copy with the current `User.SecurityStamp` on every use. Both events rotate `User.SecurityStamp`, so no existing 2FA remember token matches.

Untrusting a device (Trusted Device Encryption) does not revoke 2FA remember tokens — that is a separate mechanism.

### Cleanup

Revoked `TwoFactorRememberToken` rows stay until their `ExpirationDate` passes. `DeleteExpiredTwoFactorRememberTokensJob` then deletes them.

## User Verification

When a user manages their own 2FA enrollment (configuring a new provider, updating an existing one, removing one), the server requires proof that the human at the keyboard is the account owner. This section covers how that proof is established and replayed across the read → write step of a per-provider management flow.

### The flow

```
              ┌─────────────────────────────────────────────┐
              │  GET /two-factor/get-<provider>             │
              │     authenticate with master password / OTP │
              │     →  returns { config, userVerificationToken }
              └─────────────────────────────────────────────┘
                                │
                                │  (client holds the token)
                                ▼
           ┌─────────────────────────────┬─────────────────────────────┐
           │                             │                             │
           ▼                             ▼                             ▼
    PUT /two-factor/<p>           DELETE /two-factor/<p>        token lifetime expires
     { …config, token }            { token }                    → user re-enters secret
     → updates enrollment          → removes enrollment            on the next GET
```

The GET endpoint is the only step that requires the master-password / OTP secret. After it succeeds, the server mints a short-lived **user-verification token** and returns it alongside the provider's current config. The client replays that token on subsequent management calls (enumerated in the endpoint table below); no secret is sent on those write steps.

### The token

`TwoFactorUserVerificationTokenable` (in [`src/Core/Auth/Models/Business/Tokenables/`](../../Models/Business/Tokenables/TwoFactorUserVerificationTokenable.cs)) carries:

| Field            | Purpose                                                                                                     |
| ---------------- | ----------------------------------------------------------------------------------------------------------- |
| `UserId`         | binds the token to one user                                                                                 |
| `ProviderType`   | binds the token to one provider (`Authenticator`, `YubiKey`, `Duo`, `OrganizationDuo`, `WebAuthn`, `Email`) |
| `ExpirationDate` | enforces the lifetime                                                                                       |
| `Identifier`     | distinguishes this tokenable from other token types that share the data-protector serialization layer       |

The token is data-protected (encrypted + integrity-checked) by `IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>` before being handed to the client, and unprotected on the way back in.

Minting goes through `ITwoFactorUserVerificationTokenableFactory` so the `IGlobalSettings.TwoFactorUserVerificationTokenLifetimeInMinutes` value is honored consistently (default: 30 minutes). Constructing a `TwoFactorUserVerificationTokenable` directly via `new()` yields `ExpirationDate == default` and is always invalid — fail-closed by design.

### Validation rules

When a management endpoint receives a `userVerificationToken`, the controller's `ValidateUserVerificationTokenAsync` helper enforces:

1. **Unprotection** — the token decrypts and deserializes cleanly. Mangled or unknown tokens are rejected.
2. **Validity window** — `ExpirationDate > now`. Expired tokens are rejected.
3. **User binding** — `tokenable.UserId == currentUser.Id`. A token minted for User A is rejected when replayed against User B's endpoint.
4. **Provider binding** — `tokenable.ProviderType == expectedProviderType`. A token minted for YubiKey is rejected when replayed against the Duo PUT or the Email DELETE, etc.

Any failure produces `BadRequestException("UserVerificationToken", "User verification failed.")`.

Replays of a still-valid token against the same `(UserId, ProviderType)` are intentionally allowed — there's no server-side single-use enforcement. Expiration is the only bound on how long a UV session lasts.

### Which endpoints participate

Every per-provider 2FA management endpoint follows the same shape:

| Provider       | GET (mints)                                   | Chain (replays)                           | PUT (replays)                            | DELETE (replays)                                                                         |
| -------------- | --------------------------------------------- | ----------------------------------------- | ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| Authenticator  | `POST /two-factor/get-authenticator`          | —                                         | `PUT /two-factor/authenticator`          | `DELETE /two-factor/authenticator`                                                       |
| YubiKey        | `POST /two-factor/get-yubikey`                | —                                         | `PUT /two-factor/yubikey`                | `DELETE /two-factor/yubikey`                                                             |
| Duo (personal) | `POST /two-factor/get-duo`                    | —                                         | `PUT /two-factor/duo`                    | `DELETE /two-factor/duo`                                                                 |
| Duo (org)      | `POST /organizations/{id}/two-factor/get-duo` | —                                         | `PUT /organizations/{id}/two-factor/duo` | `DELETE /organizations/{id}/two-factor/duo`                                              |
| WebAuthn       | `POST /two-factor/get-webauthn`               | `POST /two-factor/get-webauthn-challenge` | `PUT /two-factor/webauthn`               | `DELETE /two-factor/webauthn` (per-credential), `DELETE /two-factor/webauthn/all` (bulk) |
| Email          | `POST /two-factor/get-email`                  | `POST /two-factor/send-email`             | `PUT /two-factor/email`                  | `DELETE /two-factor/email`                                                               |

The Chain column captures interior steps that replay an existing UV token and return data the next call needs — for WebAuthn, `get-webauthn-challenge` replays the `get-webauthn` token and returns FIDO2 registration options for the PUT; for Email, `send-email` replays the `get-email` token and ships an OTP to the user's inbox that the PUT then carries back alongside the same UV token. No new UV token is minted at the chain step; the original one stays valid through the PUT.

The login-time email endpoint `POST /two-factor/send-email-login` is **not** part of this flow. It's anonymous and authenticates the user via master password / SSO session / device-auth-request access code instead of a UV token, because the user hasn't completed login yet and has no authenticated session to mint a UV token from.

### Why Authenticator uses a different tokenable

`TwoFactorAuthenticatorUserVerificationTokenable` binds to `UserId + Key`, not `UserId + ProviderType`. The extra binding exists because, for Authenticator, the server **provides** the TOTP shared secret (`Key`) at GET time — either freshly generated for a new enrollment or read from the stored config for a re-fetch — and the user scans it into their app. The PUT body then carries both the user-entered TOTP code and the Key it was computed against. Without the Key baked into the UV token, a compromised client could swap the server-provided Key for an attacker-controlled one between GET and PUT; the PUT-time TOTP check alone wouldn't catch the substitution because the attacker can compute a valid TOTP for any Key they choose.

No other provider has this shape — Duo creds, YubiKey IDs, WebAuthn attestation, and Email addresses are all client-supplied, so `UserId + ProviderType` is enough.

The controller validates Authenticator's tokenable inline rather than through `ValidateUserVerificationTokenAsync` because it needs `Key` from the request body to complete the check.
