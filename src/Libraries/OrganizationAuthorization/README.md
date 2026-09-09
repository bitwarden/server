# OrganizationAuthorization

Route-based authorization for organization- and provider-scoped HTTP endpoints. It exists so that
hosts which cannot reference `Api` — `Pam`, and any future service library — can authorize against
the same requirements the Admin Console API already uses, instead of hand-rolling
`ICurrentContext` checks inside endpoint handlers.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationAuthorization()` registers the two handlers that back the requirement interfaces.
The host is still responsible for registering its data layer — the handlers resolve
`IProviderUserRepository` from the container.

`IOrganizationRequirement` and `IProviderRequirement` are the extension points. A requirement
implementing either interface is dispatched by the matching handler, which pulls the organization or
provider ID off the route and hands the user's claims to the requirement. Requirements are pure
claims predicates; the one database-backed check (is this user a provider for this organization?) is
passed in as a lazy callback so requirements can avoid it unless the claims-based checks fail.

The library ships the common requirements — `MemberRequirement`, `MemberOrProviderRequirement`,
`BasePermissionRequirement` and the custom-permission requirements derived from it, plus the
provider equivalents.

### Controllers

```csharp
[Authorize<ManageUsersRequirement>]
[HttpGet("{id}")]
public async Task<UserResponseModel> Get(Guid orgId, Guid id) { ... }
```

### Minimal APIs

```csharp
endpoints.MapGroup("/organizations/{orgId:guid}/access-rules")
    .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>())
    .MapAccessRuleEndpoints();
```

If an endpoint needs more than one requirement, use `AddRequirements` instead:

```csharp
endpoints.MapGroup("/organizations/{orgId:guid}/access-rules")
    .RequireAuthorization(policy => policy
        .AddRequirements(new ManageAccessRulesRequirement())
        .AddRequirements(new SomeOtherRequirement()))
    .MapAccessRuleEndpoints();
```

Both forms require `{orgId}` or `{organizationId}` (respectively `{providerId}`) in the route;
the handler throws if it is absent, since a missing route parameter would otherwise silently
authorize.

## Why this is not in Core

These types read user claims and depend on how the host authenticates. Core sits below
authentication and must not grow that dependency, which is why this library exists rather than a
`Bit.Core.Authorization` namespace. See the remarks on `IOrganizationContext`.

## Core debt

LIBRARY.md asks every library to record what it took from `Core` so those pieces can be
prioritised for extraction. This library sits *above* Core rather than below it, and needs:

| From Core | Used for |
| --- | --- |
| `Bit.Core.Context.CurrentContextOrganization` | The shape handed to `IOrganizationRequirement` |
| `Bit.Core.AdminConsole.Context.CurrentContextProvider` | The shape handed to `IProviderRequirement` |
| `Bit.Core.Enums.OrganizationUserType` | Role comparisons in the requirements |
| `Bit.Core.Models.Data.Permissions` | Custom-permission comparisons in `BasePermissionRequirement` |
| `Bit.Core.Auth.Identity.Claims` | Claim type constants for parsing organization and provider claims |
| `Bit.Core.AdminConsole.Repositories.IProviderUserRepository` | The provider-for-organization database check |
| `Bit.Core.AdminConsole.Models.Data.Provider.ProviderUserOrganizationDetails` | Result of that check |
| `Bit.Core.AdminConsole.Enums.Provider.ProviderUserStatusType` | Filtering that check to confirmed provider users |
| `Bit.Core.Services.IUserService` | Reading the authenticated user's ID out of their claims |

Depending on Core is fine for now: unwinding it would mean extracting the Admin Console data models,
identity, and organizations, which is a big-ish effort in its own right. This table exists so those
pieces are known, not because they are queued up.

## What deliberately stayed in Api

Resource-based handlers — collection authorization, account recovery, and the other handlers that
authorize over a specific Api domain model rather than over the organization or provider on the
route. They are not generic plumbing and have no non-Api consumers.
