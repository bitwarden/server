# Source Files

Where to read the truth for every concept this skill names. Pointers fail loudly when a file moves — if one misses, `Glob` for the filename and update this registry.

## Canonical access-control functions

Prefer these over hand-rolled joins — they encode member status, org enablement, and direct-over-group grant precedence.

| Concept                                                        | Source                                                          |
| -------------------------------------------------------------- | --------------------------------------------------------------- |
| What ciphers can user X see                                    | `src/Sql/dbo/Vault/Functions/UserCipherDetails.sql`             |
| Per-user cipher projection (computed per-user ArchivedDate)    | `src/Sql/dbo/Vault/Functions/CipherDetails.sql`                 |
| What collections can user X access, with effective permissions | `src/Sql/dbo/AdminConsole/Functions/UserCollectionDetails.sql`  |
| How archive state is written (JSON_MODIFY on Archives)         | `src/Sql/dbo/Vault/Stored Procedures/Cipher/Cipher_Archive.sql` |
| Org feature flags in one row                                   | `src/Sql/dbo/Views/OrganizationAbilityView.sql`                 |

## Tables (SSDT schema)

| Table            | Source                                               |
| ---------------- | ---------------------------------------------------- |
| Cipher           | `src/Sql/dbo/Vault/Tables/Cipher.sql`                |
| OrganizationUser | `src/Sql/dbo/Tables/OrganizationUser.sql`            |
| CollectionUser   | `src/Sql/dbo/AdminConsole/Tables/CollectionUser.sql` |
| GroupUser        | `src/Sql/dbo/Tables/GroupUser.sql`                   |
| Send             | `src/Sql/dbo/Tools/Tables/Send.sql`                  |
| Organization     | `src/Sql/dbo/Tables/Organization.sql`                |
| User             | `src/Sql/dbo/Tables/User.sql`                        |

Anything not listed: browse `src/Sql/dbo/` (tables live under both `Tables/` and per-domain folders like `AdminConsole/Tables/`, `Vault/Tables/`) or introspect live via [schema-discovery-queries.md](schema-discovery-queries.md).

## C# enums (integer values + lifecycle semantics in XML docs)

| Enum                          | Used at                                                          | Source                                                      |
| ----------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------- |
| OrganizationUserStatusType    | `OrganizationUser.Status` (SMALLINT — Revoked is -1)             | `src/Core/AdminConsole/Enums/OrganizationUserStatusType.cs` |
| OrganizationUserType          | `OrganizationUser.Type`                                          | `src/Core/AdminConsole/Enums/OrganizationUserType.cs`       |
| OrganizationStatusType        | `Organization.Status`                                            | `src/Core/AdminConsole/Enums/OrganizationStatusType.cs`     |
| CipherType                    | `Cipher.Type`                                                    | `src/Core/Vault/Enums/CipherType.cs`                        |
| CipherRepromptType            | `Cipher.Reprompt`                                                | `src/Core/Vault/Enums/CipherRepromptType.cs`                |
| SendType                      | `Send.Type`                                                      | `src/Core/Tools/Enums/SendType.cs`                          |
| PolicyType                    | `Policy.Type`                                                    | `src/Core/AdminConsole/Enums/PolicyType.cs`                 |
| AuthRequestType               | `AuthRequest.Type`                                               | `src/Core/Auth/Enums/AuthRequestType.cs`                    |
| RevocationReason              | `OrganizationUser.RevocationReason`                              | `src/Core/AdminConsole/Enums/RevocationReason.cs`           |
| PlanType                      | `Organization.PlanType`                                          | `src/Core/Billing/Enums/PlanType.cs`                        |
| DeviceType                    | `Device.Type`, `AuthRequest.RequestDeviceType`                   | `src/Core/Enums/DeviceType.cs`                              |
| EventType                     | `Event.Type`                                                     | `src/Core/Dirt/Enums/EventType.cs`                          |
| Custom permissions JSON shape | `OrganizationUser.Permissions` (only meaningful when `Type = 4`) | `src/Core/AdminConsole/Models/Data/Permissions.cs`          |
