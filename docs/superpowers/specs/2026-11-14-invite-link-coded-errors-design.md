# Invite link accept and invite endpoints return coded errors

Tickets: [PM-43102](https://bitwarden.atlassian.net/browse/PM-43102) (accept), [PM-40926](https://bitwarden.atlassian.net/browse/PM-40926) (invite)

## Intent

`POST /organizations/users/invite-link/accept` and `POST /organizations/users/invite-link/invite` must return
their failures in the same RFC 7807 shape that `POST /organizations/users/invite-link/confirm` already returns:
a problem body whose `errors` entry carries a stable, client-localizable `type` code. Auth asked for this so the
client handles one error shape across all three endpoints, instead of matching English message text.

On top of the tickets, the type system must enforce it: the commands behind these endpoints, and the
invite-link code they call, can only return a coded error. A plain `BadRequestError` added later, or a path we
missed, must fail to compile, not silently return the old body.

## Release order

The tickets set the order. Steps 1 and 2 are done:

1. SDK maps coded responses to typed `AcceptInviteLinkError` variants (PM-43030, sdk-internal #1514). The SDK
   has variants for every code `/confirm` sends, including the two auto-confirm codes (sdk-internal #1688).
2. Clients map each variant to a localized message (PM-44076, clients #24104). Clients still handle the old body
   through their legacy `Api` path.
3. This change.

PM-45890 lists "SDK has typed variants for the auto-confirm policy errors" as a prerequisite. That is already
done; the ticket is out of date. Nothing on the client side blocks this change.

Release timing: clients #24104 merged on 26 Oct, after `web-v2026.10.1` was cut, and is on the clients `rc`
branch, so it first ships in web 2026.11.0. Web 2026.10.1 does not understand the coded body and would show a
generic error. This server change must not reach cloud before web 2026.11.0. A change merged to server `main`
now goes out in a later server release than that web release, so the normal train covers it. It must not be
hotfixed into an earlier server release. Self-hosted installs ship server and web together, so they stay
matched. The join flow is web-only (`accept_and_optionally_confirm` is called from the web join flow), and the
SDK fetches the invite inside that call, so `/invite` is covered by the same client change as `/accept`.

## Goals

- Every failure that accept, invite and confirm return (rather than throw) is a coded problem response.
- The error codes, property names and messages match what `/confirm` sends today, so the SDK and clients need no
  changes.
- The compiler stops these flows from returning an error that is not coded.

## Non-goals

- 400s from ASP.NET model binding (malformed body) keep their current shape. They never reach the commands.
- The admin invite-link endpoints (`OrganizationInviteLinksController`: create, update, refresh, delete, status,
  policies, validate-email-domain) keep returning the plain errors.
- Client clean-up (PM-45890) is out of scope.
- Exceptions thrown after the membership is written keep their current shape (decided by the designer; check
  it). After accepting or confirming, both commands call
  `IUpdateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync`, which throws
  `BadRequestException`, and that becomes the old `ErrorResponseModel` 400. `/confirm` already behaves this way.
  The type system cannot cover thrown exceptions. In practice these cases can't be hit, or need a malformed
  request:
  - The user-mismatch, org-feature and policy-disabled checks can't fail. The membership was just written, and
    the call only happens when the auto-enroll policy is on.
  - The "cannot withdraw" check is already caught earlier as `ResetPasswordKeyRequired`, using the same key
    check.
  - `InvalidResetPasswordKeyError` needs a non-empty key that isn't a valid EncString. The SDK never sends one.

  Coding these would need new codes with no SDK variants, which breaks the "no SDK or client changes" goal.
  This is a known gap. The tests must not claim to cover every failure.
- No feature flag. Clients already handle both shapes; the release order above is the compatibility plan.

## Anti-goals

- Changing any code, property name or message that `/confirm` sends today. The SDK keys off them.
- Changing the response of any endpoint outside the three user invite-link endpoints.
- A missed shared error silently turning into a plain 400. If it is missed, it must fail loudly (a 500 with a
  correlation id), and tests must catch it first.

## Approaches considered

1. **Share one set of coded errors across all three endpoints.** Picked. One list of codes, so the endpoints
   cannot drift apart.
2. Copy the records per endpoint (`Accept*`, `Invite*` next to `Confirm*`). Rejected: about 45 near-identical
   records kept in step by hand.
3. Make the base records in `Errors.cs` coded. Rejected: the admin endpoints also return `InviteLinkNotAvailable`
   and `InviteLinkNotFound`, so their responses would change.

For how far the typed errors reach, the user picked: into the invite-link-only helpers too
(`InviteLinkValidationService`, `InviteLinkRules`), not just the commands, validators and endpoints. The
validators then never see a plain `Error`.

## Design (decided)

### Result type

Add `Result<T, TError>` in its own file, `src/Core/AdminConsole/Utilities/v2/Results/Result.cs`:

```csharp
public class Result<T, TError>(OneOf<TError, T> result) : OneOfBase<TError, T>(result)
    where TError : Error
```

It has the same members as `CommandResult<T>` (`IsError`, `IsSuccess`, `AsError`, `AsSuccess`, and implicit
conversions from `T` and from `TError`). It is generic, not invite-link-specific, so other endpoints can reuse it.

### Error base type

```csharp
public abstract record InviteLinkValidationError(string Message) : Error(Message), IValidationError
```

`PropertyName` and `Type` are abstract. Every error the user flows return derives from it, including not-found.

The `Confirm*` records in `ConfirmOrganizationInviteLinkErrors.cs` move onto this base and lose the `Confirm`
prefix. They keep their exact `Type`, `PropertyName` and message.

### Not-found is coded too

The user flows get a new coded not-found record: `Type` `invite_link_not_found`, `PropertyName` `code`, status
404, with the same message as the plain `InviteLinkNotFound` ("Invite link not found."). The admin endpoints keep
the plain `InviteLinkNotFound`.

The SDK and clients are unaffected: the SDK maps any 404 to `LinkNotFound` before reading the body, and the
clients' legacy path decides on the status alone.

### Status code on `IValidationError`

Add a status code to `IValidationError` that defaults to 400. The new not-found record overrides it to 404.
`EmailValidationError` and `NameValidationError` (`UpdateUser/v2/Errors.cs`) keep the default, so their
responses do not change.

`TypedResults.BitwardenValidationProblem` in `src/HttpExtensions/BitwardenTypedResultsExtensions.cs` gains an
optional status parameter, defaulting to 400. `ValidationErrorTypedResultsExtensions` passes the error's status
through. Its only callers today are AC code, so the default leaves them unchanged.

The helper picks the top-level `type` and `title` from the status with a switch, so they describe the
response accurately:

| Status | `type` | `title` |
|---|---|---|
| 400 | `validation_error` | "One or more validation errors occurred." |
| 404 | `not_found` | "The requested resource was not found." |
| anything else | throws `ArgumentOutOfRangeException` | |

The existing `type` and `title` parameters become nullable. An explicit value still wins; `null` means "use the
switch". 400 responses are byte-for-byte unchanged. The throw makes a new status fail loudly in tests rather
than send a mislabelled body. Supporting it means adding a row.

### Signatures

| Code | Today | After |
|---|---|---|
| `IAcceptOrganizationInviteLinkCommand.AcceptAsync` | `CommandResult<OrganizationUser>` | `Result<OrganizationUser, InviteLinkValidationError>` |
| `IConfirmOrganizationInviteLinkCommand.ConfirmAsync` | `CommandResult` | `Result<None, InviteLinkValidationError>` |
| `IGetOrganizationInviteQuery.GetInviteAsync` | `CommandResult<string>` | `Result<string, InviteLinkValidationError>` |
| `IAcceptInviteLinkMembershipValidator.ValidateAsync` | `CommandResult<AcceptInviteLinkMembershipValidationResult>` | `Result<…, InviteLinkValidationError>` |
| `IConfirmOrganizationInviteLinkValidator.ValidateAsync` | `CommandResult<ConfirmOrganizationInviteLinkValidationResult>` | `Result<…, InviteLinkValidationError>` |
| `IGetOrganizationInviteValidator.ValidateAsync` | `ValidationResult<OrganizationInviteLinkValidationRequest>` | `Result<OrganizationInviteLinkValidationRequest, InviteLinkValidationError>` |
| `IInviteLinkValidationService` methods, `InviteLinkRules` methods | `Error?` / `JoinRequirementsValidationResult(Error?, …)` | `InviteLinkValidationError?` |

The seat errors in `AcceptOrganizationInviteLinkCommand` and `ConfirmOrganizationInviteLinkCommand`
(no available seats, seat add failed) return the coded records directly.

### Controller

Add a `Handle` overload to `BaseAdminConsoleController`:

```csharp
protected static IResult Handle<T, TError>(Result<T, TError> result, Func<T, IResult> success)
    where TError : Error, IValidationError
```

On error it always returns `TypedResults.BitwardenValidationProblem(error)` with the error's status. The three
actions in `OrganizationUsersController` (`AcceptInviteLink`, `ConfirmInviteLink`, `GetInvite`) use it. Their
success responses do not change.

### The one runtime check

Some errors come from shared code that returns a plain `Error`:

- `IJoinOrganizationPolicyValidationService`: single org, two-step login, auto-confirm
- `IOrganizationUserValidationService.ValidateFreeOrgAdminLimitAsync`: free org admin limit

All of these reach the invite-link code through `InviteLinkValidationService.ValidateJoinRequirementsAsync`.
The error types are shared with other flows, so they cannot change. One mapper, private to
`InviteLinkValidationService`, turns each into its coded record. Its default branch throws
`UnreachableException`, as `ToConfirmValidationError` does today.

### Account recovery check reports a fact, not an error

`IJoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy` can only fail one way: auto-enroll is
on and the reset password key is missing or invalid. Change its result from
`AccountRecoveryPolicyValidationResult(Error? Error, bool AutoEnrollEnabled)` to one that reports that fact,
e.g. `(bool ResetPasswordKeyMissing, bool AutoEnrollEnabled)`. Change the async overload,
`ValidateAccountRecoveryPolicyAsync`, the same way.

The accept and confirm validators keep calling it directly and return the coded reset-key record from their own
layer when the key is missing. This needs no mapping and no runtime check. The only callers of
`IJoinOrganizationPolicyValidationService` are the invite-link validators, so nothing else changes. The plain
`ResetPasswordKeyRequired` in `InviteLinks/Errors.cs` then has no users and is deleted.

This replaces the three existing mappers: `ToConfirmValidationError`, `ToAcceptValidationError` and
`ToGetInviteValidationError` (and its PM-40926 TODO).

### Visible change on accept and invite

Only the body shape changes. The status codes stay the same (400 or 404), and the messages are the same text as
today: each coded record carries the message the endpoint already sends, which now appears as `detail`.

## Testing

- Unit tests (with mocking) for each changed validator, command, query and `InviteLinkValidationService`: each
  failure returns the expected coded record.
- A test for the mapper covering every shared error it must handle.
- `JoinOrganizationPolicyValidationService` tests: the account recovery result reports a missing key instead of
  an error.
- Unit tests for `BitwardenValidationProblem`: 400 keeps today's `type` and `title`; 404 gets `not_found` and its
  title; explicit values still win; any other status throws.
- Controller unit tests: the new `Handle` overload returns the problem body with the error's status, 400 and 404.
- Integration tests (`OrganizationUsersControllerAcceptInviteLinkTests`, `OrganizationUsersControllerGetInviteTests`,
  and the confirm equivalents): assert the problem body's `type` code and status, not just the status.
- `BaseAdminConsoleController` callers using `EmailValidationError` / `NameValidationError` still get a 400.

## Left to the builder

- Names for the coded records. They must not clash with the plain records that stay in `Errors.cs`
  (`InviteLinkNotAvailable`, `InviteLinkNotFound`, `EmailDomainNotAllowed`, which admin endpoints still use).
  Delete plain records that nothing uses any more.
- The exact name of the account recovery result's new field.
- Where the coded records live (one file or several), and whether `Result<T, TError>` gets helper methods beyond
  what `CommandResult<T>` has.
- Type of the status property on `IValidationError` (`int` or `HttpStatusCode`). Core must not take a new
  ASP.NET dependency to get it.
- Order of commits.
