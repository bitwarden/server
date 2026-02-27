# Plan: Extend Account Recovery to Support 2FA Reset

## Context

The Account Recovery feature currently only supports resetting a user's master password via `PUT /organizations/{orgId}/users/{id}/reset-password`. We are extending this existing endpoint and command to also support resetting (clearing) a member's 2FA methods. Both actions can be performed independently or together in a single request.

## Design Decisions

- **Rename endpoint** `PUT reset-password` → `PUT recover-account` (keep old route as alias for backward compat); add `ResetMasterPassword` and `ResetTwoFactor` booleans to the request
- **Extend existing command** `AdminRecoverAccountCommand` — add 2FA reset as a conditional action alongside password reset
- **Internal request record** — command accepts a `RecoverAccountRequest` record instead of primitive parameters; API model maps via `ToCommandRequest()`
- **Validation inside the command** — all validation stays in `AdminRecoverAccountCommand` (current practice), not a separate validator class, so inputs are always guaranteed validated
- **Auth-owned `IResetUserTwoFactorCommand`** — 2FA data mutation centralized in Auth team's domain; bumps revision dates via `TimeProvider`, sets recovery code to `null`
- **Single dynamic email** — update existing template to describe which action(s) were taken
- **New EventType** `OrganizationUser_AdminResetTwoFactor = 1519`
- **Feature flag** — `ResetTwoFactor = true` only honored when flag is enabled
- **Keeps existing patterns** — throws `BadRequestException`/`NotFoundException` for validation, returns `IdentityResult` for password update result (limits scope)

---

## Implementation Steps

### 1. Add Feature Flag

**Modify:** `src/Core/Constants.cs`
- Add under `/* Admin Console Team */`:
  ```csharp
  public const string AdminResetTwoFactor = "pm-XXXXX-admin-reset-two-factor";
  ```

### 2. Add New EventType

**Modify:** `src/Core/Dirt/Enums/EventType.cs`
- Add after `OrganizationUser_SelfRevoked = 1518`:
  ```csharp
  OrganizationUser_AdminResetTwoFactor = 1519,
  ```

### 3. Create Auth-owned `IResetUserTwoFactorCommand` (Auth team)

The Auth team owns 2FA provider data. This simple command centralizes the reset logic.

**New file:** `src/Core/Auth/UserFeatures/TwoFactorAuth/IResetUserTwoFactorCommand.cs`
```csharp
public interface IResetUserTwoFactorCommand
{
    Task ResetAsync(User user);
}
```

**New file:** `src/Core/Auth/UserFeatures/TwoFactorAuth/Implementations/ResetUserTwoFactorCommand.cs`
```csharp
public class ResetUserTwoFactorCommand(
    IUserRepository userRepository,
    TimeProvider timeProvider) : IResetUserTwoFactorCommand
{
    public async Task ResetAsync(User user)
    {
        user.TwoFactorProviders = null;
        user.TwoFactorRecoveryCode = null;
        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        await userRepository.ReplaceAsync(user);
    }
}
```

Key decisions:
- **`TwoFactorRecoveryCode = null`** — no 2FA methods exist, so a recovery code is meaningless
- **Bumps `RevisionDate` + `AccountRevisionDate`** — ensures clients sync and reflect the 2FA removal
- **Uses `TimeProvider`** — consistent with `AdminRecoverAccountCommand` and testable

**Modify:** `src/Core/Auth/UserFeatures/UserServiceCollectionExtensions.cs`
- Add in `AddTwoFactorCommandsQueries()` (line 79):
  ```csharp
  services.AddScoped<IResetUserTwoFactorCommand, ResetUserTwoFactorCommand>();
  ```

**New file:** `test/Core.Test/Auth/UserFeatures/TwoFactorAuth/ResetUserTwoFactorCommandTests.cs`
- Test that `TwoFactorProviders` is set to `null`
- Test that `TwoFactorRecoveryCode` is set to `null`
- Test that `RevisionDate` and `AccountRevisionDate` are bumped
- Test that `userRepository.ReplaceAsync` is called

### 4. Create Internal Request Record

**New file:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/RecoverAccountRequest.cs`
```csharp
namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public record RecoverAccountRequest
{
    public required Guid OrgId { get; init; }
    public required OrganizationUser OrganizationUser { get; init; }
    public required bool ResetMasterPassword { get; init; }
    public required bool ResetTwoFactor { get; init; }
    public string? NewMasterPasswordHash { get; init; }
    public string? Key { get; init; }
}
```

### 5. Update API Request Model with `ToCommandRequest()`

**Modify:** `src/Api/Models/Request/Organizations/OrganizationUserResetPasswordRequestModel.cs`

From:
```csharp
public class OrganizationUserResetPasswordRequestModel
{
    [Required]
    [StringLength(300)]
    public string NewMasterPasswordHash { get; set; }
    [Required]
    public string Key { get; set; }
}
```

To:
```csharp
public class OrganizationUserResetPasswordRequestModel
{
    public bool ResetMasterPassword { get; set; }
    public bool ResetTwoFactor { get; set; }

    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    public string? Key { get; set; }

    public RecoverAccountRequest ToCommandRequest(Guid orgId, OrganizationUser organizationUser) => new()
    {
        OrgId = orgId,
        OrganizationUser = organizationUser,
        ResetMasterPassword = ResetMasterPassword,
        ResetTwoFactor = ResetTwoFactor,
        NewMasterPasswordHash = NewMasterPasswordHash,
        Key = Key
    };
}
```

### 6. Update `IAdminRecoverAccountCommand` Interface

**Modify:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountCommand.cs`

Update method signature to accept the request record:
```csharp
Task<IdentityResult> RecoverAccountAsync(RecoverAccountRequest request);
```

### 7. Update `AdminRecoverAccountCommand` Implementation

**Modify:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommand.cs`

Add `IResetUserTwoFactorCommand` and `IFeatureService` to constructor dependencies.

Updated flow:
1. **Validation** (throws `BadRequestException`/`NotFoundException` — same pattern as today):
   - At least one action must be requested
   - If `request.ResetMasterPassword`, `NewMasterPasswordHash` and `Key` must be present
   - If `request.ResetTwoFactor`, feature flag must be enabled
   - Org must exist and allow reset password (existing)
   - Enterprise policy must be enabled (existing)
   - Org User must be confirmed with `ResetPasswordKey` (existing)
   - User must exist — **use `userRepository.GetByIdAsync()`** not `userService.GetUserByIdAsync()` (avoids `CurrentContext.User` side effect)
   - Key Connector check — only when `request.ResetMasterPassword` is true
2. **Conditional password reset** (if `request.ResetMasterPassword`):
   - `userService.UpdatePasswordHash(user, request.NewMasterPasswordHash)` — existing logic
   - Set `ForcePasswordReset = true`, update `Key`, revision dates
   - `userRepository.ReplaceAsync(user)`
3. **Conditional 2FA reset** (if `request.ResetTwoFactor`):
   - Call `resetUserTwoFactorCommand.ResetAsync(user)`
4. **Email:** updated `SendAdminResetPasswordEmailAsync(...)` with flags indicating action(s) taken
5. **Event logging:**
   - If `resetMasterPassword`: log `OrganizationUser_AdminResetPassword` (1508)
   - If `resetTwoFactor`: log `OrganizationUser_AdminResetTwoFactor` (1519)
6. **Push logout** — unchanged
7. **Policy compliance** (if `resetTwoFactor`): check RequireTwoFactor policy → revoke if needed
   - Duplicated from `UserService.CheckPoliciesOnTwoFactorRemovalAsync()` (line 1046)

Note: Returns `IdentityResult` (existing pattern). Validation failures throw exceptions. Password hash update failure returns `IdentityResult.Failed`.

### 8. Update Email Template + Mail Service

**Modify:** `src/Core/Models/Mail/AdminResetPasswordViewModel.cs`
- Add: `public bool ResetMasterPassword { get; set; }` and `public bool ResetTwoFactor { get; set; }`

**Modify:** `src/Core/Platform/Mail/IMailService.cs`
- Update: `Task SendAdminResetPasswordEmailAsync(string email, string? userName, string orgName, bool resetMasterPassword, bool resetTwoFactor);`

**Modify:** `src/Core/Platform/Mail/HandlebarsMailService.cs`
- Pass new booleans to view model

**Modify:** `src/Core/Platform/Mail/NoopMailService.cs`
- Update method signature

**Modify:** `src/Core/MailTemplates/Handlebars/AdminResetPassword.html.hbs` + `.text.hbs`
- Handlebars conditionals for dynamic message:
  - Both: "The master password and two-step login methods for **{UserName}** have been changed by an administrator in your **{OrgName}** organization..."
  - Password only: "The master password for **{UserName}** has been changed..." (existing message)
  - 2FA only: "The two-step login methods for **{UserName}** have been reset by an administrator in your **{OrgName}** organization..."

### 9. Update Controller — Rename Route + Use Request Object

**Modify:** `src/Api/AdminConsole/Controllers/OrganizationUsersController.cs`

Rename the route and add the old route as a backward-compat alias:
```csharp
[HttpPut("{id}/recover-account")]
[HttpPut("{id}/reset-password")]  // backward compat alias — remove after clients migrate
[Authorize<ManageAccountRecoveryRequirement>]
public async Task<IResult> PutRecoverAccount(Guid orgId, Guid id, [FromBody] OrganizationUserResetPasswordRequestModel model)
```

Updated flow:
```csharp
// Lookup org user (existing)
var targetOrganizationUser = await _organizationUserRepository.GetByIdAsync(id);

// Authorization (existing)
await _authorizationService.AuthorizeOrThrowAsync(User, new RecoverAccountAuthorizationRequirement(orgId, targetOrganizationUser));

// Map to internal request record and execute (validation happens inside the command)
var commandRequest = model.ToCommandRequest(orgId, targetOrganizationUser);
var result = await _adminRecoverAccountCommand.RecoverAccountAsync(commandRequest);

// Handle IdentityResult (existing pattern)
if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        ModelState.AddModelError(string.Empty, error.Description);
    }
    // existing error handling...
}
```

The controller is simpler — no validator call. Validation exceptions (`BadRequestException`/`NotFoundException`) are handled by the middleware as today.

### 10. Update Tests

**Modify:** `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommandTests.cs`
- Update all existing tests to pass `RecoverAccountRequest` instead of primitives
- New validation tests:
  1. `RecoverAccountAsync_NoActionRequested_ThrowsBadRequest`
  2. `RecoverAccountAsync_ResetMasterPassword_MissingHash_ThrowsBadRequest`
  3. `RecoverAccountAsync_ResetMasterPassword_MissingKey_ThrowsBadRequest`
  4. `RecoverAccountAsync_ResetTwoFactor_FeatureFlagDisabled_ThrowsBadRequest`
- New execution tests:
  5. `RecoverAccountAsync_ResetTwoFactorOnly_Success` — calls `ResetAsync`, sends email, logs event
  6. `RecoverAccountAsync_ResetBoth_Success` — both actions performed, both events logged
  7. `RecoverAccountAsync_ResetTwoFactor_RevokesUserWithRequireTwoFactorPolicy`
  8. `RecoverAccountAsync_ResetTwoFactor_KeyConnectorUser_Succeeds` (Key Connector check only applies to password reset)

**Modify:** `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs`
- Update existing `PutResetPassword` tests for new model shape

---

## Files Summary

### New files (4):
| File | Purpose |
|------|---------|
| `src/Core/Auth/UserFeatures/TwoFactorAuth/IResetUserTwoFactorCommand.cs` | Auth-owned command interface |
| `src/Core/Auth/UserFeatures/TwoFactorAuth/Implementations/ResetUserTwoFactorCommand.cs` | Auth-owned command impl |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/RecoverAccountRequest.cs` | Internal request record |
| `test/Core.Test/Auth/UserFeatures/TwoFactorAuth/ResetUserTwoFactorCommandTests.cs` | Auth command tests |

### Modified files (12):
| File | Change |
|------|--------|
| `src/Core/Constants.cs` | Add feature flag |
| `src/Core/Dirt/Enums/EventType.cs` | Add `OrganizationUser_AdminResetTwoFactor = 1519` |
| `src/Core/Auth/UserFeatures/UserServiceCollectionExtensions.cs` | Register `IResetUserTwoFactorCommand` |
| `src/Api/Models/Request/Organizations/OrganizationUserResetPasswordRequestModel.cs` | Add booleans, make password fields nullable, add `ToCommandRequest()` |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountCommand.cs` | Method takes `RecoverAccountRequest` |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommand.cs` | Extend validation + add 2FA reset flow + policy compliance |
| `src/Core/Models/Mail/AdminResetPasswordViewModel.cs` | Add boolean flags |
| `src/Core/Platform/Mail/IMailService.cs` | Update email method signature |
| `src/Core/Platform/Mail/HandlebarsMailService.cs` | Pass flags to view model |
| `src/Core/Platform/Mail/NoopMailService.cs` | Update method signature |
| `src/Core/MailTemplates/Handlebars/AdminResetPassword.html.hbs` | Dynamic email content |
| `src/Core/MailTemplates/Handlebars/AdminResetPassword.text.hbs` | Dynamic email content |

### Test files (2):
| File | Change |
|------|--------|
| `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommandTests.cs` | Update for request record + add 2FA tests |
| `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs` | Update for new model shape |

### Key reference files:
| File | Reference for |
|------|---------------|
| `src/Api/AdminConsole/Models/Request/Organizations/OrganizationUpdateRequestModel.cs` | `ToCommandRequest()` pattern |
| `src/Core/Services/Implementations/UserService.cs:1046` | `CheckPoliciesOnTwoFactorRemovalAsync` to duplicate |
| `src/Core/Services/Implementations/UserService.cs:720` | `RecoverTwoFactorAsync` for 2FA clearing pattern |

---

## Verification

1. **Build:** `dotnet build`
2. **Unit tests:** `dotnet test test/Core.Test` and `dotnet test test/Api.Test`
3. **Backward compat:** `{ "ResetMasterPassword": true, "NewMasterPasswordHash": "...", "Key": "..." }` works as before (old clients that don't send the new fields)
4. **New scenarios:**
   - `{ "ResetTwoFactor": true }` → 2FA reset only (requires feature flag)
   - `{ "ResetMasterPassword": true, "ResetTwoFactor": true, ... }` → both actions
   - `{}` → 400 Bad Request (no action requested)
5. **Email:** content varies based on action(s) taken
6. **Events:** correct type(s) logged per scenario
7. **Policy:** 2FA reset + RequireTwoFactor policy → user revoked
8. **Revision dates:** bumped by `ResetUserTwoFactorCommand` (via `TimeProvider`)
9. **Recovery code:** set to `null` after 2FA reset (no methods to recover)
