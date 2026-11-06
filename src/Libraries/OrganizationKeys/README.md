# OrganizationKeys

Feature-tier library exposing the HTTP surface for an organization's own key material.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Public surface

`AddOrganizationKeys()` registers the scoped `SetOrganizationKeyIdHandler`, the
`ISetOrganizationKeyIdCommand` behind it, and the `Bit.OrganizationAuthorization` library the endpoints
authorize with. The host still registers the Core repositories the services consume.

`MapOrganizationKeysEndpoints()` creates the group, applies its cross-cutting chain (the `OrganizationKeys`
tag, the `internal` group name, the `Application` policy, exception handling), and maps the endpoints
below. The host mounts it at `/organizations/{orgId:guid}/keys`. The prefix must contain the `orgId`
route value, because the organization authorization requirements read it.

`OrganizationOwnerOrAdminRequirement` authorizes Owners and Admins of the organization only. It is
`public` because `OrganizationUsersKeysController` in the Api also uses it. When those endpoints move
into this library, make it `internal`.

Everything else is `internal`.

### Endpoints

| Route | Handler | Returns |
| --- | --- | --- |
| `POST .../organization-key-id` | `SetOrganizationKeyIdHandler.HandleAsync` | `200`, no body |

`POST organization-key-id` backfills the key id of the organization's current organization key, for
organizations that predate the key id. Only Owners and Admins can call it
(`OrganizationOwnerOrAdminRequirement`); other callers get `403`.

The endpoint's `RequestValidationEndpointFilter` runs the request's DataAnnotations (`[Required]`,
`[KeyId]`) before the handler and rejects a failure as a `400` with the model state errors. The handler
then confirms that the caller holds the organization key, because only such a member can know its key id. A caller without the key gets a `400`. The command sets the key id only when none is set yet:
reporting the same key id again succeeds, and reporting a different one is a `409`.

The endpoint name carries down through the handler, command, and request (`SetOrganizationKeyId`,
`SetOrganizationKeyIdHandler`, `ISetOrganizationKeyIdCommand`, `SetOrganizationKeyIdRequest`).

## Core debt

LIBRARY.md asks every library to record what it took from `Core` so those pieces can be prioritised for
extraction.

| From Core | Used for |
| --- | --- |
| `Bit.Core.Repositories.IOrganizationUserRepository`, `Bit.Core.Entities.OrganizationUser` | Confirming the caller holds the organization key |
| `Bit.Core.Repositories.IOrganizationRepository`, `Bit.Core.AdminConsole.Entities.Organization` | Storing and re-reading the key id |
| `Bit.Core.KeyManagement.Models.Data.KeyId` | The parsed key id |
| `Bit.Core.Utilities.KeyIdAttribute` | Validating the key id format |
| `Bit.Core.Auth.Identity.Policies` | The `Application` policy name |
| `Bit.Core.Context.CurrentContextOrganization`, `Bit.Core.Enums.OrganizationUserType` | Reading the caller's organization role in `OrganizationOwnerOrAdminRequirement` |
