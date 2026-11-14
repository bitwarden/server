# Invite Link Coded Errors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `/organizations/users/invite-link/accept` and `/invite` return coded RFC 7807 problem responses like
`/confirm` does, and the type system stops these flows from returning an uncoded error.

**Architecture:** A new generic `Result<T, TError>` replaces `CommandResult<T>` in the accept, confirm and invite
flows, with `TError` fixed to a new abstract `InviteLinkValidationError : Error, IValidationError`. Invite-link
helpers produce coded errors directly. One runtime-checked mapper in `InviteLinkValidationService` converts the
shared policy and free-admin errors. A constrained controller `Handle` overload renders the result.

**Tech Stack:** .NET / C#, OneOf, ASP.NET Core minimal `TypedResults`, xUnit, NSubstitute, `SutProvider` /
`BitAutoData`.

**Spec:** `docs/superpowers/specs/2026-11-14-invite-link-coded-errors-design.md`

## Global Constraints

- Every `type` code, `PropertyName` and message `/confirm` sends today stays exactly the same (table in Task 4).
- No change to any endpoint other than the three user invite-link endpoints. The admin endpoints in
  `OrganizationInviteLinksController` keep their plain `ErrorResponseModel` errors.
- 400 problem responses keep `type` `validation_error` and `title` "One or more validation errors occurred."
- 404 problem responses use `type` `not_found` and `title` "The requested resource was not found."
- No feature flag.
- No `#region`. Use .NET nullable reference types. Unit tests use `SutProvider` / `BitAutoData` with NSubstitute.
- Test names and comments describe external behaviour, not implementation details.
- Commit message prefix: `[PM-43102]` (the branch covers PM-43102 and PM-40926).
- Inside `BaseAdminConsoleController`, `Error` means the nested static class. Use the existing `CommandError`
  alias for `Bit.Core.AdminConsole.Utilities.v2.Error`.

## Review Focus

- **Message text drift.** A coded record that no longer inherits from the old record could change its message,
  which is shown to users as `detail`. Task 4's record table test pins every message.
- **Default interface member not re-mapped.** If `StatusCode` only exists as a default interface member, a
  derived record's `StatusCode => 404` is not picked up through `IValidationError`, and not-found would go out as
  a 400. Task 4 tests the status through the interface.
- **Admin endpoints changing by accident.** The admin endpoints still return plain `InviteLinkNotAvailable` and
  `InviteLinkNotFound`. Task 8 tests that they still render as `ErrorResponseModel`.
- **A shared error the mapper doesn't know.** It must throw (a 500), not pass through as a plain 400. Task 6
  tests this with an unknown error.
- **Other `IValidationError` users.** `EmailValidationError` and `NameValidationError` must still render as 400s
  with the old `type` and `title`. Task 2 tests this.

---

### Task 1: Status-aware problem helper

**Files:**
- Modify: `src/HttpExtensions/BitwardenTypedResultsExtensions.cs`
- Test: `test/HttpExtensions.Test/BitwardenTypedResultsExtensionsTests.cs`

**Interfaces:**
- Produces: `TypedResults.BitwardenValidationProblem(IDictionary<string, ErrorCode[]> errors, string? detail = null, string? instance = null, string? title = null, string? type = null, IDictionary<string, object?>? extensions = null, int statusCode = StatusCodes.Status400BadRequest)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void BitwardenValidationProblem_WithNotFoundStatus_ReturnsNotFoundTypeAndTitle()
{
    var result = TypedResults.BitwardenValidationProblem(errors, statusCode: StatusCodes.Status404NotFound);
    Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    Assert.Equal("not_found", result.ProblemDetails.Type);
    Assert.Equal("The requested resource was not found.", result.ProblemDetails.Title);
}

[Fact]
public void BitwardenValidationProblem_WithExplicitTypeAndTitle_UsesThem()
{
    var result = TypedResults.BitwardenValidationProblem(errors, title: "Custom", type: "custom",
        statusCode: StatusCodes.Status409Conflict);
    Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    Assert.Equal("custom", result.ProblemDetails.Type);
    Assert.Equal("Custom", result.ProblemDetails.Title);
}

[Fact]
public void BitwardenValidationProblem_WithUnsupportedStatusAndNoTypeOrTitle_Throws()
{
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        TypedResults.BitwardenValidationProblem(errors, statusCode: StatusCodes.Status409Conflict));
}
```

The existing `..._Returns400WithErrorsExtension` test already pins the 400 defaults. Leave it unchanged.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/HttpExtensions.Test --filter "FullyQualifiedName~BitwardenTypedResultsExtensionsTests"`
Expected: compile error, `statusCode` parameter does not exist.

- [ ] **Step 3: Implement**

Make `title` and `type` nullable with a `null` default, and add `int statusCode = StatusCodes.Status400BadRequest` as
the last parameter. When `type` or `title` is `null`, take the missing one from a switch on `statusCode`:
400 gives `validation_error` / "One or more validation errors occurred."; 404 gives `not_found` / "The requested
resource was not found."; any other status throws `ArgumentOutOfRangeException(nameof(statusCode))`. Pass
`statusCode` to `TypedResults.Problem`. Update the XML doc: it no longer always returns 400.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/HttpExtensions.Test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/HttpExtensions/BitwardenTypedResultsExtensions.cs test/HttpExtensions.Test/BitwardenTypedResultsExtensionsTests.cs
git commit -m "[PM-43102] feat: Support a status code in BitwardenValidationProblem"
```

### Task 2: Status code on `IValidationError`

**Files:**
- Modify: `src/Core/AdminConsole/Utilities/v2/Validation/IValidationError.cs`
- Modify: `src/Api/AdminConsole/Controllers/ValidationErrorTypedResultsExtensions.cs`
- Test: `test/Api.Test/AdminConsole/Controllers/ValidationErrorTypedResultsExtensionsTests.cs`

**Interfaces:**
- Consumes: Task 1's `statusCode` parameter.
- Produces: `int IValidationError.StatusCode => 400;` (default interface member).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void BitwardenValidationProblem_WithErrorUsingDefaultStatus_Returns400ValidationProblem()
{
    var result = TypedResults.BitwardenValidationProblem(new TestValidationError("email", "Invalid", "invalid"));
    Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    Assert.Equal("validation_error", result.ProblemDetails.Type);
}

[Fact]
public void BitwardenValidationProblem_WithNotFoundError_Returns404()
{
    var result = TypedResults.BitwardenValidationProblem(new TestNotFoundValidationError());
    Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    Assert.Equal("not_found", result.ProblemDetails.Type);
}

[Fact]
public void BitwardenValidationProblem_WithEmailValidationError_Returns400()
{
    var result = TypedResults.BitwardenValidationProblem(new MemberNotClaimedError());
    Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
}
```

`TestNotFoundValidationError` is a private test record that implements `IValidationError` with
`public int StatusCode => 404`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Api.Test --filter "FullyQualifiedName~ValidationErrorTypedResultsExtensionsTests"`
Expected: FAIL. The not-found test gets 400 (or a compile error if `StatusCode` is missing).

- [ ] **Step 3: Implement**

Add `int StatusCode => 400;` to `IValidationError`, with an XML doc saying it defaults to 400 Bad Request. Use an
`int` literal; Core must not take an ASP.NET dependency for this. In `ValidationErrorTypedResultsExtensions`, pass
`statusCode: validationError.StatusCode`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/Api.Test --filter "FullyQualifiedName~ValidationErrorTypedResultsExtensionsTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/AdminConsole/Utilities/v2/Validation/IValidationError.cs src/Api/AdminConsole/Controllers/ValidationErrorTypedResultsExtensions.cs test/Api.Test/AdminConsole/Controllers/ValidationErrorTypedResultsExtensionsTests.cs
git commit -m "[PM-43102] feat: Let validation errors set their HTTP status"
```

### Task 3: `Result<T, TError>` and the constrained `Handle` overload

**Files:**
- Create: `src/Core/AdminConsole/Utilities/v2/Results/Result.cs`
- Modify: `src/Api/AdminConsole/Controllers/BaseAdminConsoleController.cs`
- Test: `test/Core.Test/AdminConsole/Utilities/v2/Results/ResultTests.cs`
- Test: `test/Api.Test/AdminConsole/Controllers/BaseAdminConsoleControllerTests.cs`

**Interfaces:**
- Produces: `public class Result<T, TError>(OneOf<TError, T> result) : OneOfBase<TError, T>(result) where TError : Error`
  with `bool IsError`, `bool IsSuccess`, `TError AsError`, `T AsSuccess`, and implicit conversions from `T` and
  from `TError`. Namespace `Bit.Core.AdminConsole.Utilities.v2.Results`.
- Produces: `protected static IResult Handle<T, TError>(Result<T, TError> result, Func<T, IResult> success) where TError : CommandError, IValidationError`
  on `BaseAdminConsoleController`. On error it returns `TypedResults.BitwardenValidationProblem(error)`.

- [ ] **Step 1: Write the failing tests**

`ResultTests`: converting from a value gives `IsSuccess` and `AsSuccess` equal to it; converting from an error
gives `IsError` and `AsError` the same instance.

`BaseAdminConsoleControllerTests` uses a private test controller that derives from `BaseAdminConsoleController`
and exposes `Handle<T, TError>` through a public method:

```csharp
[Fact]
public void Handle_WithSuccess_ReturnsSuccessResponse()
    // Result from "value" → the success delegate's TypedResults.Ok("value")

[Fact]
public void Handle_WithBadRequestValidationError_Returns400ValidationProblem()
    // Assert.IsType<BitwardenValidationProblemResult>; StatusCode == 400; errors[property][0].Type == code

[Fact]
public void Handle_WithNotFoundValidationError_Returns404ValidationProblem()
    // StatusCode == 404; ProblemDetails.Type == "not_found"
```

Use private test error records that derive from `Error` and implement `IValidationError`, one with
`StatusCode => 404`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~ResultTests"` and
`dotnet test test/Api.Test --filter "FullyQualifiedName~BaseAdminConsoleControllerTests"`
Expected: compile errors, `Result` and the overload do not exist.

- [ ] **Step 3: Implement both** as in the Interfaces block. Mirror the XML doc style of `CommandResult<T>` and
the existing `Handle<T>`.

- [ ] **Step 4: Run tests to verify they pass**

Run: the two commands from Step 2.
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/AdminConsole/Utilities/v2/Results/Result.cs src/Api/AdminConsole/Controllers/BaseAdminConsoleController.cs test/Core.Test/AdminConsole/Utilities/v2/Results/ResultTests.cs test/Api.Test/AdminConsole/Controllers/BaseAdminConsoleControllerTests.cs
git commit -m "[PM-43102] feat: Add Result with a typed error and a validation-only Handle overload"
```

### Task 4: Coded invite-link error records

**Files:**
- Create: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/InviteLinkValidationErrors.cs`
- Delete: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/ConfirmOrganizationInviteLinkErrors.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/ConfirmOrganizationInviteLinkValidator.cs`,
  `ConfirmOrganizationInviteLinkCommand.cs` (switch every `Confirm*` record to its new name)
- Modify: tests that reference `Confirm*` records (`ConfirmOrganizationInviteLinkValidatorTests.cs`,
  `ConfirmOrganizationInviteLinkCommandTests.cs`)
- Test: `test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks/InviteLinkValidationErrorsTests.cs`

**Interfaces:**
- Produces: `public abstract record InviteLinkValidationError(string Message) : Error(Message), IValidationError`
  with `public abstract string PropertyName { get; }`, `public abstract string Type { get; }` and
  `public virtual int StatusCode => 400;`. The `virtual` member on the record is required: a derived record's
  override is only seen through `IValidationError` if the base record implements the member itself.
- Produces these sealed records, all deriving from `InviteLinkValidationError`:

| Record | `Type` | `PropertyName` | Message | Status |
|---|---|---|---|---|
| `InviteLinkNotFoundError()` | `invite_link_not_found` | `code` | Invite link not found. | 404 |
| `InviteLinkNotAvailableError()` | `invite_link_not_available` | `code` | Your organization's plan does not support invite links. | 400 |
| `InviteLinkConfirmationNotSupportedError()` | `invite_link_confirmation_not_supported` | `code` | This invite link does not support confirmation. | 400 |
| `EmailNotVerifiedError()` | `email_not_verified` | `organizationId` | You must verify your email address before joining an organization. | 400 |
| `EmailDomainNotAllowedError(string OrgName)` | `email_domain_not_allowed` | `code` | You're not allowed to join the {OrgName} vault with your email domain. | 400 |
| `ProviderUsersCannotJoinError()` | `provider_users_cannot_join` | `code` | Provider users cannot join organization vaults via invite link. | 400 |
| `OrganizationAccessRevokedError(string OrgName)` | `organization_access_revoked` | `code` | Your access to the {OrgName} vault has been revoked. | 400 |
| `AlreadyOrganizationMemberError(string OrgName)` | `already_organization_member` | `code` | You're already a member of {OrgName}. | 400 |
| `OrganizationHasNoAvailableSeatsError(string OrgName)` | `organization_has_no_available_seats` | `code` | The {OrgName} vault has no available seats. | 400 |
| `SeatAddFailedError()` | `seat_add_failed` | `code` | Unable to join this vault right now. Please contact your organization admin. | 400 |
| `ResetPasswordKeyRequiredError()` | `reset_password_key_required` | `resetPasswordKey` | Master Password reset is required, but not provided. | 400 |
| `MemberOfAnotherOrganizationError()` | `member_of_another_organization` | `organizationId` | Member cannot join this organization vault until they leave all other organization vaults. | 400 |
| `SingleOrganizationPolicyError()` | `single_organization_policy` | `organizationId` | Member cannot join this organization's vault because they are a member of another organization which forbids it. | 400 |
| `TwoFactorRequiredForMembershipError()` | `two_factor_required_for_membership` | `organizationId` | You cannot join this organization vault until you enable two-step login on your user account. | 400 |
| `AutoConfirmPolicyViolationOtherOrgError(string Email)` | `auto_confirm_policy_violation_other_org` | `organizationId` | Cannot confirm {Email} because they are a member of another organization which forbids it. | 400 |
| `AutoConfirmPolicyViolationTargetOrgError(string Email)` | `auto_confirm_policy_violation_target_org` | `organizationId` | Cannot confirm {Email} until they leave all other organization vaults. | 400 |
| `OnlyOneFreeOrganizationAdminAllowedError()` | `only_one_free_organization_admin_allowed` | `organizationId` | You can only be an admin of 1 free organization vault. | 400 |

Every row except `InviteLinkNotFoundError` matches what a `Confirm*` record sends today. Before deleting
`ConfirmOrganizationInviteLinkErrors.cs`, check each message against the base record it inherits from today.

- [ ] **Step 1: Write the failing test**

One `[Theory]` with a row per record (`MemberData` building each instance), asserting through the interface:

```csharp
[Theory]
[MemberData(nameof(Errors))]
public void InviteLinkValidationError_ExposesStableWireContract(
    IValidationError error, string type, string propertyName, string message, int statusCode)
{
    Assert.Equal(type, error.Type);
    Assert.Equal(propertyName, error.PropertyName);
    Assert.Equal(message, error.Message);
    Assert.Equal(statusCode, error.StatusCode);
}
```

Use `OrgName` "Acme" and `Email` "user@example.com" for the parameterised rows, with the expected messages
interpolated accordingly.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinkValidationErrorsTests"`
Expected: compile error, the records do not exist.

- [ ] **Step 3: Implement** the base and the records. Replace every `Confirm*` use with its new record, and
`InviteLinkNotFound => error` in `ToConfirmValidationError` with `InviteLinkNotFound => new InviteLinkNotFoundError()`.
Delete `ConfirmOrganizationInviteLinkErrors.cs`. Move its explanatory comment to the new file, rewritten for all
three endpoints.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinks"` and
`dotnet test test/Api.IntegrationTest --filter "FullyQualifiedName~ConfirmInviteLink"`
Expected: PASS. Confirm's wrong-code integration test still expects 404.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core/AdminConsole/OrganizationFeatures/InviteLinks test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks
git commit -m "[PM-43102] refactor: Share coded invite link errors across the user endpoints"
```

### Task 5: Account recovery check reports a missing key

**Files:**
- Modify: `src/Core/AdminConsole/OrganizationFeatures/Policies/AccountRecoveryPolicyValidationResult.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/Policies/Implementations/JoinOrganizationPolicyValidationService.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/Policies/IJoinOrganizationPolicyValidationService.cs` (XML docs)
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/Interfaces/IConfirmOrganizationInviteLinkValidator.cs` (XML doc on the account recovery check)
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/AcceptInviteLinkMembershipValidator.cs`,
  `ConfirmOrganizationInviteLinkValidator.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/Errors.cs` (delete `ResetPasswordKeyRequired`)
- Test: `test/Core.Test/AdminConsole/OrganizationFeatures/Policies/JoinOrganizationPolicyValidationServiceTests.cs`,
  `AcceptInviteLinkMembershipValidatorTests.cs`, `ConfirmOrganizationInviteLinkValidatorTests.cs`

**Interfaces:**
- Consumes: `ResetPasswordKeyRequiredError` (Task 4).
- Produces: `public sealed record AccountRecoveryPolicyValidationResult(bool ResetPasswordKeyMissing, bool AutoEnrollEnabled)`.
  `ResetPasswordKeyMissing` is true when auto-enroll is on and the key is missing or invalid.

- [ ] **Step 1: Update the tests**

- Rename `ValidateAccountRecoveryPolicy_WithAutoEnrollAndMissingKey_ReturnsError` to
  `..._ReportsMissingKey` and assert `result.ResetPasswordKeyMissing` is true and `AutoEnrollEnabled` is true.
- The other account recovery tests assert `ResetPasswordKeyMissing` is false.
- Accept validator test: when the policy service reports a missing key, `result.AsError` is
  `ResetPasswordKeyRequiredError`.
- Confirm validator tests: same assertion. Update the substitutes to return the new result shape.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~JoinOrganizationPolicyValidationServiceTests|FullyQualifiedName~AcceptInviteLinkMembershipValidatorTests|FullyQualifiedName~ConfirmOrganizationInviteLinkValidatorTests"`
Expected: compile error on `ResetPasswordKeyMissing`.

- [ ] **Step 3: Implement**

Change the record and both service methods. Remove the `IsError` property. In both validators, replace the
`accountRecoveryResult.IsError` branch with
`if (accountRecoveryResult.ResetPasswordKeyMissing) return new ResetPasswordKeyRequiredError();`. Remove the
`ResetPasswordKeyRequired` arm from `ToConfirmValidationError`. Delete the plain `ResetPasswordKeyRequired`
record.

- [ ] **Step 4: Run tests to verify they pass**

Run: the command from Step 2.
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A src/Core/AdminConsole/OrganizationFeatures test/Core.Test/AdminConsole/OrganizationFeatures
git commit -m "[PM-43102] refactor: Report a missing reset password key from the account recovery check"
```

### Task 6: Invite-link helpers return coded errors

**Files:**
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/InviteLinkRules.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/InviteLinkValidationService.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/Interfaces/IInviteLinkValidationService.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/JoinRequirementsValidationResult.cs`
- Modify: the three validators, only as far as needed to compile (their mappers shrink in Task 7)
- Test: `test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks/InviteLinkRulesTests.cs`,
  `InviteLinkValidationServiceTests.cs`

**Interfaces:**
- Consumes: Task 4 records.
- Produces:
  - `InviteLinkValidationError? IInviteLinkValidationService.ValidateEligibility(...)` (same parameters)
  - `Task<InviteLinkValidationError?> IInviteLinkValidationService.ValidateSeatAvailabilityAsync(...)`
  - `JoinRequirementsValidationResult(InviteLinkValidationError? Error, bool AutoConfirmPolicyEnabled)`
  - `InviteLinkValidationError? InviteLinkRules.ValidateAcceptMembershipStatus(...)` and `ValidateConfirmMembershipStatus(...)`

- [ ] **Step 1: Update the tests**

- `InviteLinkRulesTests` and the eligibility and seat tests in `InviteLinkValidationServiceTests` assert the coded
  records: `InviteLinkNotFoundError`, `InviteLinkNotAvailableError`, `EmailDomainNotAllowedError`,
  `EmailNotVerifiedError`, `OrganizationAccessRevokedError`, `AlreadyOrganizationMemberError`,
  `OrganizationHasNoAvailableSeatsError`.
- Add a `[Theory]` to `InviteLinkValidationServiceTests` named
  `ValidateJoinRequirementsAsync_WhenAJoinRequirementFails_ReturnsTheMatchingCodedError`, one row per shared
  error, with the substituted dependency returning it:

| Shared error (source) | Expected coded record |
|---|---|
| `CannotBeAdminOfMultipleFreeOrganizations` (`IOrganizationUserValidationService.ValidateFreeOrgAdminLimitAsync`) | `OnlyOneFreeOrganizationAdminAllowedError` |
| `ProviderUsersCannotJoin` (auto-confirm result) | `ProviderUsersCannotJoinError` |
| `OtherOrganizationDoesNotAllowOtherMembership(email)` (auto-confirm result) | `AutoConfirmPolicyViolationOtherOrgError` with the same email |
| `UserCannotBelongToAnotherOrganization(email)` (auto-confirm result) | `AutoConfirmPolicyViolationTargetOrgError` with the same email |
| `UserIsAMemberOfAnotherOrganization` (single org) | `MemberOfAnotherOrganizationError` |
| `UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy` (single org) | `SingleOrganizationPolicyError` |
| `TwoFactorRequiredForMembership` (two-step login) | `TwoFactorRequiredForMembershipError` |

- Add `ValidateJoinRequirementsAsync_WhenAJoinRequirementFailsWithAnUnknownError_Throws`: the two-step login check
  returns a private test `BadRequestError`, and the call throws `UnreachableException`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinkRulesTests|FullyQualifiedName~InviteLinkValidationServiceTests"`
Expected: FAIL. Plain records are returned.

- [ ] **Step 3: Implement**

Return the coded records directly in `InviteLinkRules` and in `ValidateEligibility` and `ValidateSeatAvailabilityAsync`.
In `ValidateJoinRequirementsAsync`, pass each shared error through a private static
`InviteLinkValidationError ToInviteLinkValidationError(Error error)` switch covering the seven rows above. Its
default arm throws `UnreachableException($"Unmapped join requirement error: {error.GetType().Name}")`. In
`ToConfirmValidationError`, add `InviteLinkValidationError => error` as the first arm and remove the arms for plain
records the helpers no longer return. Without that first arm, its throwing default would fire on the
now-coded errors. `ToAcceptValidationError` and `ToGetInviteValidationError` already fall through with
`_ => error`; remove their now-dead arms. Update the XML docs on
`IInviteLinkValidationService` and `JoinRequirementsValidationResult`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinks"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A src/Core/AdminConsole/OrganizationFeatures/InviteLinks test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks
git commit -m "[PM-43102] refactor: Return coded errors from the invite link helpers"
```

### Task 7: Validators, commands and query return `Result<…, InviteLinkValidationError>`

**Files:**
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/AcceptInviteLinkMembershipValidator.cs`,
  `ConfirmOrganizationInviteLinkValidator.cs`, `GetOrganizationInviteValidator.cs`,
  `AcceptOrganizationInviteLinkCommand.cs`, `ConfirmOrganizationInviteLinkCommand.cs`, `GetOrganizationInviteQuery.cs`
- Modify: `Interfaces/IAcceptInviteLinkMembershipValidator.cs`, `IConfirmOrganizationInviteLinkValidator.cs`,
  `IGetOrganizationInviteValidator.cs`, `IAcceptOrganizationInviteLinkCommand.cs`,
  `IConfirmOrganizationInviteLinkCommand.cs`, `IGetOrganizationInviteQuery.cs`
- Modify: `src/Core/AdminConsole/OrganizationFeatures/InviteLinks/Errors.cs` (delete records with no users left)
- Test: the matching `*Tests.cs` files under `test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks/`

**Interfaces:**
- Consumes: `Result<T, TError>` (Task 3), Task 4 records, Task 6 helper signatures.
- Produces:
  - `Task<Result<AcceptInviteLinkMembershipValidationResult, InviteLinkValidationError>> IAcceptInviteLinkMembershipValidator.ValidateAsync(...)`
  - `Task<Result<ConfirmOrganizationInviteLinkValidationResult, InviteLinkValidationError>> IConfirmOrganizationInviteLinkValidator.ValidateAsync(...)`
  - `Task<Result<OrganizationInviteLinkValidationRequest, InviteLinkValidationError>> IGetOrganizationInviteValidator.ValidateAsync(...)`
  - `Task<Result<OrganizationUser, InviteLinkValidationError>> IAcceptOrganizationInviteLinkCommand.AcceptAsync(...)`
  - `Task<Result<None, InviteLinkValidationError>> IConfirmOrganizationInviteLinkCommand.ConfirmAsync(...)`
  - `Task<Result<string, InviteLinkValidationError>> IGetOrganizationInviteQuery.GetInviteAsync(...)`

- [ ] **Step 1: Update the tests**

- Accept command tests: when no seat is available, `result.AsError` is `OrganizationHasNoAvailableSeatsError`; when
  auto-add throws `BadRequestException`, it is `SeatAddFailedError`.
- Accept and invite validator tests: free-admin and provider-user failures now assert
  `OnlyOneFreeOrganizationAdminAllowedError` / `ProviderUsersCannotJoinError` (these were the plain records).
- Invite validator tests: replace `ValidationResult` assertions (`IsValid`, `Request`) with `IsSuccess` /
  `AsSuccess` and `AsError`.
- Update substitutes to return `Result<…>` values, including in
  `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs`, where the three commands are
  substituted. The solution must build at the end of this task.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinks"`
Expected: compile errors on the new signatures.

- [ ] **Step 3: Implement**

Change the signatures above. Return the coded seat records from both commands' `ReserveSeatAsync` /
`TryExpandSeatsAsync` (typed `InviteLinkValidationError?`). Delete `ToConfirmValidationError`,
`ToAcceptValidationError` and `ToGetInviteValidationError`, including its PM-40926 TODO. `GetOrganizationInviteQuery`
maps the validator's success to `valid.ExistingInviteLink!.Invite`. Delete records in `Errors.cs` that nothing uses
any more. `InviteLinkNotAvailable` and `InviteLinkNotFound` stay. Also check `EmailDomainNotAllowed`: it stays if
`ValidateOrganizationInviteLinkQuery` or `OrganizationInviteLinksController` still uses it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build && dotnet test test/Core.Test --filter "FullyQualifiedName~InviteLinks"`
Expected: build succeeds, tests PASS. The controller compiles unchanged: its `Handle(result, …)` calls now
resolve to Task 3's overload.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core/AdminConsole/OrganizationFeatures/InviteLinks test/Core.Test/AdminConsole/OrganizationFeatures/InviteLinks test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs
git commit -m "[PM-43102] feat: Type invite link user flows to return coded errors only"
```

### Task 8: Endpoints use the constrained `Handle`

**Files:**
- Modify: `src/Api/AdminConsole/Controllers/OrganizationUsersController.cs` (`AcceptInviteLink`, `ConfirmInviteLink`, `GetInvite`)
- Test: `test/Api.Test/AdminConsole/Controllers/OrganizationUsersControllerTests.cs`
- Test: `test/Api.Test/AdminConsole/Controllers/OrganizationInviteLinksControllerTests.cs`
- Test: `test/Api.IntegrationTest/AdminConsole/Controllers/OrganizationUsersControllerAcceptInviteLinkTests.cs`,
  `OrganizationUsersControllerGetInviteTests.cs`, `OrganizationUsersControllerConfirmInviteLinkTests.cs`
- Create: `test/Api.IntegrationTest/Helpers/ValidationProblemAssertions.cs`

**Interfaces:**
- Consumes: `Handle<T, TError>` (Task 3), Task 7 signatures.
- Produces: `static Task ValidationProblemAssertions.AssertValidationProblemAsync(HttpResponseMessage response, HttpStatusCode status, string propertyName, string code)`.
  It asserts the status, then parses the body and asserts `errors[propertyName][0].type == code`.

- [ ] **Step 1: Write the failing tests**

Controller unit tests:
- Replace `ConfirmInviteLink_WhenCommandReturnsError_ReturnsBadRequest` with
  `ConfirmInviteLink_WhenCommandReturnsError_ReturnsValidationProblem` (`BitwardenValidationProblemResult`,
  status 400).
- Add the same test, plus `..._WhenNotFound_Returns404ValidationProblem`, for `AcceptInviteLink` and `GetInvite`.
- Add to `OrganizationInviteLinksControllerTests`: an admin action whose command returns plain
  `InviteLinkNotAvailable` still returns `BadRequest<ErrorResponseModel>`, if no such test exists already.

Integration tests (use the new helper):
- Accept: change the "rejected" tests to assert the code: two-step → `organizationId` /
  `two_factor_required_for_membership`; target single org → `member_of_another_organization`; other org single
  org → `single_organization_policy`; target auto-confirm → `auto_confirm_policy_violation_target_org`; other org
  auto-confirm → `auto_confirm_policy_violation_other_org`.
- Accept: add `AcceptInviteLink_WithWrongCode_ReturnsNotFoundProblem`, asserting 404, `code` /
  `invite_link_not_found`, and a top-level `type` of `not_found`.
- Invite: replace the two message tests and their TODO comment with code assertions: free admin →
  `organizationId` / `only_one_free_organization_admin_allowed`; provider user → `code` /
  `provider_users_cannot_join`. Delete `AssertBadRequestWithMessageAsync` if nothing else uses it.
- Invite: add `GetInvite_WithWrongCode_ReturnsNotFoundProblem`, with the same assertions as for accept.
- Confirm: switch the existing typed-code test to the helper. Assert the wrong-code test's body is
  `invite_link_not_found`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Api.Test --filter "FullyQualifiedName~OrganizationUsersControllerTests"`
Expected: compile error or FAIL until the actions use the new overload.

- [ ] **Step 3: Check the actions**

Since Task 7, the three actions' `Handle(result, …)` calls already resolve to the constrained overload, so no
controller change is expected. Check that each action's call binds to `Handle<T, TError>` (e.g. with the IDE).
Check that success responses are still `TypedResults.Ok()` / `TypedResults.Ok(new OrganizationInviteResponseModel(invite))`.
If the tests from Step 1 fail, fix the action, not the test.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build && dotnet test test/Api.Test --filter "FullyQualifiedName~AdminConsole.Controllers" && dotnet test test/Api.IntegrationTest --filter "FullyQualifiedName~InviteLink|FullyQualifiedName~GetInvite"`
Expected: PASS

- [ ] **Step 5: Run the full affected suites**

Run: `dotnet test test/Core.Test --filter "FullyQualifiedName~AdminConsole" && dotnet test test/Api.Test && dotnet test test/HttpExtensions.Test`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add -A src/Api test/Api.Test test/Api.IntegrationTest
git commit -m "[PM-43102] feat: Return coded errors from the invite link accept and invite endpoints"
```
