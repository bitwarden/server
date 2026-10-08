# AgentFill

Owns agent fill approval requests: the server side of approving an AI agent's autofill from another device
(AI-136). The desktop app creates a sealed request, the server pushes its ID to the user's phones, and the first
device to answer wins.

See [LIBRARY.md](../LIBRARY.md) for the shape all libraries under `src/Libraries/` follow.

## Zero knowledge

The server stores and returns sealed strings without opening them. The tab URL, domain, connection name, browser
name, chosen item and decision travel only inside the sealed request and response, which the SDK seals with the user
key. Records, push payloads and logs hold only IDs, device IDs and timestamps. Never log, parse or inspect
`SealedRequest` or `SealedResponse`.

The server never knows the decision. `status` on the response is derived on read: `answered` when a response is
stored, `expired` when it isn't and the expiration date has passed, `pending` otherwise.

## Public surface

- `AddAgentFill()` registers the commands, the query, the repository (Dapper on SQL Server, Entity Framework
  otherwise) and the feature flag keys the library owns (`AgentFillFeatureFlags`) as known flags.
- `MapAgentFillEndpoints()` maps `POST /`, `GET /{id}` and `PUT /{id}`. The Api host mounts them at
  `/agent-fill/approvals`. Every endpoint requires the `Application` policy and the
  `ai-136-agent-fill-approvals` flag; with the flag off they return `404`.
- `IDeleteExpiredAgentFillApprovalRequestsCommand`, run by the Admin host's
  `DeleteExpiredAgentFillApprovalRequestsJob` every 15 minutes. It deletes requests that expired more than a day ago.

Everything else is internal.

## Behavior

| Endpoint | Result                                                                                                  |
| -------- | ------------------------------------------------------------------------------------------------------- |
| `POST`   | Stores the request for five minutes, pushes `AgentFillApprovalRequest` to mobile clients. `400` for an unknown calling device. |
| `GET`    | The record, or `404` when it doesn't exist or belongs to another user.                                  |
| `PUT`    | `200` for the first answer, then pushes `AgentFillApprovalResponse` to desktop clients except the caller. `409` with the stored record when already answered, `410` when expired, `404` when not found. `400` for an unknown calling device. |

Sealed fields are limited to 8 KiB; larger values return `400`.

The answer is recorded with one conditional update (`SealedResponse IS NULL AND ExpirationDate > now`), so concurrent
answers store exactly one response on every database.

## Data layer

This is the first library with a data layer, and it doesn't own all of it:

- The MSSQL schema is in `src/Sql/dbo/AgentFill/`, with its migration in `util/Migrator/DbScripts/`.
- The EF model is `Bit.Infrastructure.EntityFramework.AgentFill.Models.AgentFillApprovalRequest`, registered in the
  shared `DatabaseContext`, because EF migrations are generated from it.

The library owns the repository interface and both implementations. The Dapper implementation derives from
`Infrastructure.Dapper`'s `BaseRepository`, and the EF implementation uses `DatabaseContext`.

## Core debt

This library depends on `Core` as a documented deviation from the rule restricting Libraries from referencing Core,
per ADR-0032:

| From Core                                    | Used for                                                                      |
| -------------------------------------------- | ----------------------------------------------------------------------------- |
| `ICurrentContext`                            | The calling user and device identifier                                        |
| `IDeviceRepository`                          | Resolving the calling device's ID                                             |
| `IPushNotificationService`, `PushNotification<T>`, `NotificationTarget`, `ClientType` | Sending the two pushes                           |
| `PushType`                                   | `AgentFillApprovalRequest` (28) and `AgentFillApprovalResponse` (29)          |
| `AgentFillApprovalPushNotification`          | The push payload, which `PushType`'s `[NotificationInfo]` requires in Core    |
| `GlobalSettings`                             | The database provider and the SQL Server connection strings                   |
| `Policies`                                   | The `Application` authorization policy                                        |
| `BadRequestException`                        | `400` responses through `Bit.ExceptionHandling`                               |

Once `Bit.CurrentUser` ([#8382](https://github.com/bitwarden/server/pull/8382)) merges, the user ID comes from
`ICurrentUserContext.UserId` instead of `ICurrentContext`.
