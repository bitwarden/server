# Enum Cheat Sheet

Integer values for the enums that surface in Bitwarden queries. Provider-agnostic — values match across all four databases.

## OrganizationUserStatusType = `OrganizationUser.Status` (SMALLINT)

[sources.md](sources.md#org-user-status-enum) — `SMALLINT` because `Revoked = -1` is signed.

| Value | Name      | Meaning                                                                |
|-------|-----------|------------------------------------------------------------------------|
| `-1`  | Revoked   | Administrator-suspended; restored to prior status if re-granted        |
| `0`   | Invited   | Invitation only — `UserId` still NULL                                  |
| `1`   | Accepted  | Account linked; key exchange not yet done                              |
| `2`   | Confirmed | Full member with org-key access — the canonical "active member" filter |

## OrganizationUserType = `OrganizationUser.Type` (TINYINT)

[sources.md](sources.md#org-user-type-enum)

| Value | Name    | Notes                                                       |
|-------|---------|-------------------------------------------------------------|
| `0`   | Owner   |                                                             |
| `1`   | Admin   |                                                             |
| `2`   | User    | Default role                                                |
| `3`   | _gap_   | Manager permanently removed; no rows should have `Type = 3` |
| `4`   | Custom  | Permissions read from `OrganizationUser.Permissions` JSON   |

## OrganizationStatusType = `Organization.Status` (TINYINT)

[sources.md](sources.md#org-status-enum) — Not the active flag — that's `Organization.Enabled`.

| Value | Name    | Meaning                            |
|-------|---------|------------------------------------|
| `0`   | Pending | Provisioning in progress           |
| `1`   | Created | Standard live org (column default) |
| `2`   | Managed | Under provider management          |

## CipherType = `Cipher.Type` (TINYINT)

[sources.md](sources.md#cipher-type-enum)

| Value | Name                              |
|-------|-----------------------------------|
| `0`   | _unused_ (was Folder, deprecated) |
| `1`   | Login                             |
| `2`   | SecureNote                        |
| `3`   | Card                              |
| `4`   | Identity                          |
| `5`   | SSHKey                            |
| `6`   | BankAccount                       |
| `7`   | DriversLicense                    |
| `8`   | Passport                          |

## CipherRepromptType = `Cipher.Reprompt` (TINYINT NULL)

[sources.md](sources.md#cipher-reprompt-type-enum)

| Value | Name     |
|-------|----------|
| `0`   | None     |
| `1`   | Password |

## SendType = `Send.Type` (TINYINT)

[sources.md](sources.md#send-type-enum)

| Value | Name |
|-------|------|
| `0`   | Text |
| `1`   | File |

## PolicyType = `Policy.Type` (TINYINT)

[sources.md](sources.md#policy-type-enum) — spans 0–21.

| Value | Name                              | Notes                                                                        |
|-------|-----------------------------------|------------------------------------------------------------------------------|
| `0`   | TwoFactorAuthentication           |                                                                              |
| `1`   | MasterPassword                    |                                                                              |
| `2`   | PasswordGenerator                 |                                                                              |
| `3`   | SingleOrg                         |                                                                              |
| `4`   | RequireSso                        |                                                                              |
| `5`   | OrganizationDataOwnership         |                                                                              |
| `6`   | DisableSend                       | Deprecated — superseded by `SendControls` (21) under `pm-31885-send-controls` flag |
| `7`   | SendOptions                       | Deprecated — superseded by `SendControls` (21)                               |
| `8`   | ResetPassword                     |                                                                              |
| `9`   | MaximumVaultTimeout               |                                                                              |
| `10`  | DisablePersonalVaultExport        |                                                                              |
| `11`  | ActivateAutofill                  |                                                                              |
| `12`  | AutomaticAppLogIn                 |                                                                              |
| `13`  | FreeFamiliesSponsorshipPolicy     |                                                                              |
| `14`  | RemoveUnlockWithPin               |                                                                              |
| `15`  | RestrictedItemTypesPolicy         |                                                                              |
| `16`  | UriMatchDefaults                  |                                                                              |
| `17`  | AutotypeDefaultSetting            |                                                                              |
| `18`  | AutomaticUserConfirmation         |                                                                              |
| `19`  | BlockClaimedDomainAccountCreation |                                                                              |
| `20`  | OrganizationUserNotification      |                                                                              |
| `21`  | SendControls                      | Active when `pm-31885-send-controls` feature flag is on                      |

## AuthRequestType = `AuthRequest.Type` (SMALLINT)

[sources.md](sources.md#auth-request-type-enum) — `AuthRequest.Approved BIT NULL` is tri-state: `NULL = pending`, `0 = denied`, `1 = approved`; pending also indicated by `ResponseDate IS NULL`.

| Value | Name                  |
|-------|-----------------------|
| `0`   | AuthenticateAndUnlock |
| `1`   | Unlock                |
| `2`   | AdminApproval         |

## RevocationReason = `OrganizationUser.RevocationReason` (TINYINT NULL)

[sources.md](sources.md#revocation-reason-enum) — Populated only when `Status = -1`.

| Value | Name                                         |
|-------|----------------------------------------------|
| `0`   | Unknown                                      |
| `1`   | Manual                                       |
| `2`   | TwoFactorPolicyNonCompliance                 |
| `3`   | OrganizationDataOwnershipPolicyNonCompliance |
| `4`   | SingleOrgPolicyNonCompliance                 |

## PlanType = `Organization.PlanType` (TINYINT)

[sources.md](sources.md#plan-type-enum) — 23 active values across legacy generations (2019/2020/2023/current). Read the file before filtering on specific plans. Aggregate on `PlanType`, not the `Organization.Plan` display string.

## CustomPermissions JSON — `OrganizationUser.Permissions NVARCHAR(MAX) NULL`

[sources.md](sources.md#permissions-model) — Meaningful only when `OrganizationUser.Type = 4` (Custom). For Owner/Admin/User the column is typically NULL or stale and must not be consulted for access decisions.

```json
{
  "AccessEventLogs": false,
  "AccessImportExport": false,
  "AccessReports": false,
  "CreateNewCollections": false,
  "EditAnyCollection": false,
  "DeleteAnyCollection": false,
  "ManageGroups": false,
  "ManagePolicies": false,
  "ManageSso": false,
  "ManageUsers": false,
  "ManageResetPassword": false,
  "ManageScim": false
}
```

## Large enums — read source

- `DeviceType` — `src/Core/Enums/DeviceType.cs`. Used at `Device.Type`, `AuthRequest.RequestDeviceType`.
- `EventType` — `src/Core/Dirt/Enums/EventType.cs`. Used at `Event.Type`.
- Billing enums (`Gateway`, etc.) — `src/Core/Billing/Enums/`.
