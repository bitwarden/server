# Plan: Extend Account Recovery to Support 2FA Reset

## Context

The Account Recovery feature currently only supports resetting a user's master password via `PUT /organizations/{orgId}/users/{id}/reset-password`. We are extending this existing endpoint and command to also support resetting (clearing) a member's 2FA methods. Both actions can be performed independently or together in a single request.

## Design Decisions

- **Rename endpoint** `PUT reset-password` → `PUT recover-account` (keep old route as alias for backward compat); add `ResetMasterPassword` and `ResetTwoFactor` booleans to the request
- **Extend existing command** `AdminRecoverAccountCommand` — add 2FA reset as a conditional action alongside password reset
- **Internal request record** — command accepts a `RecoverAccountRequest` record instead of primitive parameters; API model maps via `ToCommandRequest()`
- **Separate validator class** — `AdminRecoverAccountValidator` following the `DeleteClaimedOrganizationUserAccountValidator` pattern, returning `ValidationResult<RecoverAccountRequest>`; uses v2 `Error` types (`BadRequestError`, `NotFoundError`)
- **Auth-owned `IResetUserTwoFactorCommand`** — 2FA data mutation centralized in Auth team's domain; bumps revision dates via `TimeProvider`, sets recovery code to `null`
- **Single dynamic email** — update existing template to describe which action(s) were taken
- **New EventType** `OrganizationUser_AdminResetTwoFactor = 1519`
- **Feature flag** — `ResetTwoFactor = true` only honored when flag is enabled
- **Stays on existing patterns** (exceptions + `IdentityResult`) for the password reset path to minimize churn, but new validation uses v2 `ValidationResult` pattern

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

### 6. Create Separate Validator Class

Following the `DeleteClaimedOrganizationUserAccountValidator` pattern (see `src/Core/AdminConsole/OrganizationFeatures/OrganizationUsers/DeleteClaimedAccount/`).

**New file:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountValidator.cs`
```csharp
public class AdminRecoverAccountValidator(
    IOrganizationRepository organizationRepository,
    IPolicyQuery policyQuery,
    IFeatureService featureService,
    IUserRepository userRepository) : IAdminRecoverAccountValidator
{
    public async Task<ValidationResult<RecoverAccountRequest>> ValidateAsync(RecoverAccountRequest request)
    {
        // At least one action must be requested
        if (!request.ResetMasterPassword && !request.ResetTwoFactor)
        {
            return Invalid(request, new NoActionRequestedError());
        }

        // If resetting master password, hash and key are required
        if (request.ResetMasterPassword &&
            (string.IsNullOrEmpty(request.NewMasterPasswordHash) || string.IsNullOrEmpty(request.Key)))
        {
            return Invalid(request, new MissingPasswordFieldsError());
        }

        // If resetting 2FA, feature flag must be enabled
        if (request.ResetTwoFactor && !featureService.IsEnabled(FeatureFlagKeys.AdminResetTwoFactor))
        {
            return Invalid(request, new FeatureDisabledError());
        }

        // Org must allow reset password
        var org = await organizationRepository.GetByIdAsync(request.OrgId);
        if (org == null || !org.UseResetPassword)
        {
            return Invalid(request, new OrgDoesNotAllowResetError());
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy = await policyQuery.RunAsync(request.OrgId, PolicyType.ResetPassword);
        if (!resetPasswordPolicy.Enabled)
        {
            return Invalid(request, new PolicyNotEnabledError());
        }

        // Org User must be confirmed and have a ResetPasswordKey
        var orgUser = request.OrganizationUser;
        if (orgUser == null ||
            orgUser.Status != OrganizationUserStatusType.Confirmed ||
            orgUser.OrganizationId != request.OrgId ||
            string.IsNullOrEmpty(orgUser.ResetPasswordKey) ||
            !orgUser.UserId.HasValue)
        {
            return Invalid(request, new InvalidOrgUserError());
        }

        // User must exist
        var user = await userRepository.GetByIdAsync(orgUser.UserId.Value);
        if (user == null)
        {
            return Invalid(request, new UserNotFoundError());
        }

        // Key Connector check — only when resetting master password
        if (request.ResetMasterPassword && user.UsesKeyConnector)
        {
            return Invalid(request, new KeyConnectorUserError());
        }

        return Valid(request);
    }
}
```

**New file:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountValidator.cs`
```csharp
public interface IAdminRecoverAccountValidator
{
    Task<ValidationResult<RecoverAccountRequest>> ValidateAsync(RecoverAccountRequest request);
}
```

**New file:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/Errors.cs`
```csharp
using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public record NoActionRequestedError() : BadRequestError("At least one recovery action must be requested.");
public record MissingPasswordFieldsError() : BadRequestError("Master password hash and key are required when resetting master password.");
public record FeatureDisabledError() : BadRequestError("Two-factor reset is not available.");
public record OrgDoesNotAllowResetError() : BadRequestError("Organization does not allow password reset.");
public record PolicyNotEnabledError() : BadRequestError("Organization does not have the password reset policy enabled.");
public record InvalidOrgUserError() : BadRequestError("Organization User not valid.");
public record UserNotFoundError() : NotFoundError("User not found.");
public record KeyConnectorUserError() : BadRequestError("Cannot reset password of a user with Key Connector.");
```

Register in DI (in the same place `AdminRecoverAccountCommand` is registered):
```csharp
services.AddScoped<IAdminRecoverAccountValidator, AdminRecoverAccountValidator>();
```

### 7. Update `IAdminRecoverAccountCommand` Interface

**Modify:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountCommand.cs`

Update method signature to accept the request record:
```csharp
Task<IdentityResult> RecoverAccountAsync(RecoverAccountRequest request);
```

### 8. Update `AdminRecoverAccountCommand` Implementation

**Modify:** `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommand.cs`

Add `IResetUserTwoFactorCommand` to constructor dependencies. Remove validation logic (now in validator). Remove `IOrganizationRepository`, `IPolicyQuery` from constructor (moved to validator).

Updated flow (post-validation — validator has already run):
1. **Lookup user** via `userRepository.GetByIdAsync(request.OrganizationUser.UserId.Value)`
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

Note: The command still returns `IdentityResult` for the password reset path (from `userService.UpdatePasswordHash`). The validator handles all pre-condition checks.

### 9. Update Email Template + Mail Service

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

### 10. Update Controller — Rename Route + Use Request Object

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

// Map to internal request record
var commandRequest = model.ToCommandRequest(orgId, targetOrganizationUser);

// Validate
var validationResult = await _adminRecoverAccountValidator.ValidateAsync(commandRequest);
if (validationResult.IsError)
{
    return Handle(validationResult.AsError);  // maps BadRequestError → 400, NotFoundError → 404
}

// Execute
var result = await _adminRecoverAccountCommand.RecoverAccountAsync(commandRequest);
if (!result.Succeeded)
{
    foreach (var error in result.Errors)
    {
        ModelState.AddModelError(string.Empty, error.Description);
    }
    // existing error handling...
}
return TypedResults.NoContent();
```

`BaseAdminConsoleController.Handle(CommandResult)` handles `Error` types. We can add a small `Handle(Error)` overload or map inline:
```csharp
if (validationResult.IsError)
{
    var error = validationResult.AsError;
    return error switch
    {
        BadRequestError badRequest => TypedResults.BadRequest(new ErrorResponseModel(badRequest.Message)),
        NotFoundError notFound => TypedResults.NotFound(new ErrorResponseModel(notFound.Message)),
        _ => TypedResults.Json(new ErrorResponseModel(error.Message), statusCode: StatusCodes.Status500InternalServerError)
    };
}
```

### 11. Update Tests

**New file:** `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountValidatorTests.cs`
- Test all error paths:
  1. `ValidateAsync_NoActionRequested_ReturnsInvalid`
  2. `ValidateAsync_ResetMasterPassword_MissingHash_ReturnsInvalid`
  3. `ValidateAsync_ResetMasterPassword_MissingKey_ReturnsInvalid`
  4. `ValidateAsync_ResetTwoFactor_FeatureFlagDisabled_ReturnsInvalid`
  5. `ValidateAsync_OrgDoesNotExist_ReturnsInvalid`
  6. `ValidateAsync_OrgDoesNotAllowReset_ReturnsInvalid`
  7. `ValidateAsync_PolicyNotEnabled_ReturnsInvalid`
  8. `ValidateAsync_OrgUserNotConfirmed_ReturnsInvalid`
  9. `ValidateAsync_OrgUserMissingResetPasswordKey_ReturnsInvalid`
  10. `ValidateAsync_UserNotFound_ReturnsInvalid`
  11. `ValidateAsync_KeyConnectorUser_ResetMasterPassword_ReturnsInvalid`
  12. `ValidateAsync_ResetMasterPasswordOnly_Valid`
  13. `ValidateAsync_ResetTwoFactorOnly_Valid`
  14. `ValidateAsync_ResetBoth_Valid`

**Modify:** `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommandTests.cs`
- Update all existing tests to pass `RecoverAccountRequest` instead of primitives
- Remove validation tests (moved to validator tests)
- Keep command execution tests:
  1. `RecoverAccountAsync_ResetMasterPasswordOnly_Success` — updates password, sends email, logs event
  2. `RecoverAccountAsync_ResetTwoFactorOnly_Success` — calls `ResetAsync`, sends email, logs event
  3. `RecoverAccountAsync_ResetBoth_Success` — both actions performed, both events logged
  4. `RecoverAccountAsync_ResetTwoFactor_RevokesUserWithRequireTwoFactorPolicy`
  5. `RecoverAccountAsync_UpdatePasswordHashFails_ReturnsFailedIdentityResult`

**Modify:** `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs`
- Update existing `PutResetPassword` tests for new model shape and validator injection

---

## Files Summary

### New files (7):
| File | Purpose |
|------|---------|
| `src/Core/Auth/UserFeatures/TwoFactorAuth/IResetUserTwoFactorCommand.cs` | Auth-owned command interface |
| `src/Core/Auth/UserFeatures/TwoFactorAuth/Implementations/ResetUserTwoFactorCommand.cs` | Auth-owned command impl |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/RecoverAccountRequest.cs` | Internal request record |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountValidator.cs` | Validator interface |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountValidator.cs` | Validator implementation |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/Errors.cs` | Typed error records |
| `test/Core.Test/Auth/UserFeatures/TwoFactorAuth/ResetUserTwoFactorCommandTests.cs` | Auth command tests |

### Modified files (12):
| File | Change |
|------|--------|
| `src/Core/Constants.cs` | Add feature flag |
| `src/Core/Dirt/Enums/EventType.cs` | Add `OrganizationUser_AdminResetTwoFactor = 1519` |
| `src/Core/Auth/UserFeatures/UserServiceCollectionExtensions.cs` | Register `IResetUserTwoFactorCommand` |
| `src/Api/Models/Request/Organizations/OrganizationUserResetPasswordRequestModel.cs` | Add booleans, make password fields nullable, add `ToCommandRequest()` |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/IAdminRecoverAccountCommand.cs` | Method takes `RecoverAccountRequest` |
| `src/Core/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommand.cs` | Remove validation (moved to validator), add 2FA reset flow + policy compliance |
| `src/Core/Models/Mail/AdminResetPasswordViewModel.cs` | Add boolean flags |
| `src/Core/Platform/Mail/IMailService.cs` | Update email method signature |
| `src/Core/Platform/Mail/HandlebarsMailService.cs` | Pass flags to view model |
| `src/Core/Platform/Mail/NoopMailService.cs` | Update method signature |
| `src/Core/MailTemplates/Handlebars/AdminResetPassword.html.hbs` | Dynamic email content |
| `src/Core/MailTemplates/Handlebars/AdminResetPassword.text.hbs` | Dynamic email content |

### Test files (3):
| File | Change |
|------|--------|
| `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountValidatorTests.cs` | **New** — all validation tests |
| `test/Core.Test/AdminConsole/OrganizationFeatures/AccountRecovery/AdminRecoverAccountCommandTests.cs` | Update for request record, remove validation tests |
| `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs` | Update for new model shape + validator |

### Key reference files:
| File | Reference for |
|------|---------------|
| `src/Core/AdminConsole/OrganizationFeatures/OrganizationUsers/DeleteClaimedAccount/` | Validator pattern (validator, errors, request record) |
| `src/Api/AdminConsole/Models/Request/Organizations/OrganizationUpdateRequestModel.cs` | `ToCommandRequest()` pattern |
| `src/Core/AdminConsole/Utilities/v2/Validation/ValidationResult.cs` | `Valid()` / `Invalid()` helpers |
| `src/Core/AdminConsole/Utilities/v2/Errors.cs` | `Error`, `BadRequestError`, `NotFoundError` base types |
| `src/Api/AdminConsole/Controllers/BaseAdminConsoleController.cs` | `Handle(CommandResult)` for error→HTTP mapping |
| `src/Core/Services/Implementations/UserService.cs:1046` | `CheckPoliciesOnTwoFactorRemovalAsync` to duplicate |

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
