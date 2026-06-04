# Protected Columns

Inventory of columns whose contents are encrypted ciphertext or structured JSON. Provider-agnostic — column names and roles match across MSSQL/MySQL/PostgreSQL/SQLite; only the type names differ (`NVARCHAR(MAX)` vs `LONGTEXT` vs `TEXT`).

## Encrypted opaque columns

AES256-CBC-HMAC-SHA256 ciphertext or wrapped key material. Useful for `IS NULL` / `IS NOT NULL` / `LEN(...)` validation. `LIKE`, content equality, `ORDER BY`, and `GROUP BY` are meaningless.

| Column                                                                                             | Holds                                                             |
| -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| `Cipher.Data`                                                                                      | Encrypted vault item payload (JSON of name, login, notes, fields) |
| `Cipher.Favorites` / `Folders` / `Archives`                                                        | Per-user JSON, encrypted (keys are GUIDs — see grounding rule 7)  |
| `Cipher.Attachments`                                                                               | Encrypted attachment metadata                                     |
| `Cipher.Key`                                                                                       | Per-cipher encryption key (wrapped)                               |
| `Folder.Name` / `Collection.Name` / `Group.Name`                                                   | Encrypted names                                                   |
| `Send.Data` / `Send.Key`                                                                           | Encrypted payload + wrapped key                                   |
| `Send.Password`                                                                                    | Server-side hashed access password (one-way)                      |
| `Send.Emails`                                                                                      | Encrypted recipient emails                                        |
| `Organization.PublicKey` / `PrivateKey`                                                            | Org RSA keypair (PrivateKey wrapped to org key)                   |
| `Organization.TwoFactorProviders`                                                                  | 2FA provider config (encrypted/serialized)                        |
| `OrganizationUser.Key` / `ResetPasswordKey`                                                        | Wrapped org key per member                                        |
| `User.Key` / `PrivateKey` / `PublicKey`                                                            | User's wrapped symmetric key + RSA keypair                        |
| `User.MasterPassword`                                                                              | Server-side hash of stretched master password (PHC string)        |
| `User.SecurityStamp`                                                                               | Identity rotation token                                           |
| `User.TwoFactorProviders` / `TwoFactorRecoveryCode`                                                | 2FA secrets / recovery code                                       |
| `User.SecurityState` / `SignedPublicKey` / `V2UpgradeToken` / `MasterPasswordSalt`                 | Encryption v2 migration state                                     |
| `OrganizationInviteLink.EncryptedInviteKey` / `EncryptedOrgKey`                                    | Org key wrapped for an invite recipient                           |
| `UserSignatureKeyPair.SigningKey` / `VerifyingKey`                                                 | Signature keypair (SigningKey wrapped)                            |
| `Device.EncryptedUserKey` / `EncryptedPublicKey` / `EncryptedPrivateKey`                           | Per-device wrapped key material                                   |
| `WebAuthnCredential.PublicKey` / `EncryptedUserKey` / `EncryptedPublicKey` / `EncryptedPrivateKey` | Passkey/credential material                                       |
| `EmergencyAccess.KeyEncrypted`                                                                     | Grantor's key wrapped to grantee                                  |
| `Secret.Key` / `Value` / `Note`, `Project.Name`, `ServiceAccount.Name`, `ApiKey.ClientSecret`      | Secrets Manager Protected Data                                    |

[sources.md#cipher-table](sources.md#cipher-table), [#send-table](sources.md#send-table), [#organization-table](sources.md#organization-table), [#organization-user-table](sources.md#organization-user-table), [#user-table](sources.md#user-table)

## JSON columns

Query via `JSON_VALUE` in MSSQL, `JSON_EXTRACT`/`->>` in MySQL/PostgreSQL, `json_extract()` in SQLite.

| Column                                                                      | Shape                                                                                                                                        |
| --------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Policy.Data`                                                               | Type-specific policy config (`MinComplexity`, `RequireUpper`, etc.)                                                                          |
| `OrganizationUser.Permissions`                                              | Only meaningful when `Type = 4` (Custom). Shape in [enums.md](enums.md#custompermissions-json--organizationuserpermissions-nvarcharmax-null) |
| `Organization.ReferenceData`                                                | Free-form JSON                                                                                                                               |
| `User.TwoFactorProviders` / `User.ReferenceData` / `User.EquivalentDomains` | User config JSON, plaintext                                                                                                                  |
| `OrganizationInviteLink.AllowedDomains`                                     | JSON array of allowed email domains                                                                                                          |
| `Cipher.Favorites` / `Folders` / `Archives`                                 | Per-user JSON, **encrypted contents** but keys are GUIDs. See grounding rule 7                                                               |
| `SsoConfig.Data`                                                            | SAML/OIDC config JSON                                                                                                                        |
