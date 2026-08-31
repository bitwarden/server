# Privileged Access Management

Privileged Access Management (PAM) governs a shared organization credential that no one holds
standing access to. It has two halves:

- **Access leasing** — a member requests access to a vault item, an access rule decides whether that
  needs human approval, and an approved request mints a time-bound lease. Outside a lease, the item
  is not readable.
- **Credential rotation** — the credential is rewritten at the system it belongs to, on a schedule,
  on demand, or as soon as a lease ends. An on-premises rotation daemon does the rewriting; the
  server only dispatches and records it.

**Audience:** server engineers working on PAM, and AI agents editing this subtree.

The two halves meet at one place: a lease ending can trigger a rotation, so the credential a member
just held stops working. See [`Rotation/README.md`](AccessConnector/README.md) for that half — this file is
the map.

## The invariant that shapes everything

Vault Data stays encrypted end to end. The server stores and moves the credential's ciphertext and
never decrypts it, including on the rotation path: the rotation daemon is handed the organization key
as ciphertext it unwraps locally, reads the cipher's opaque `Data` blob, and writes back a blob the
server also cannot read. No PAM code path — in this project or any of the projects listed below —
decrypts a cipher, and no PAM log line or audit event carries credential material.

## Where the code lives

PAM spans several projects, because the domain, the data layer, and the commercial logic are all
split by the repository's existing seams.

| Path                                              | Contents                                                                                 |
| ------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| `bitwarden_license/src/Services/Pam`              | This library — commands, queries, endpoints, and the rule engine. Namespace `Bit.Services.Pam`. |
| `src/Pam.Domain`                                  | Entities, enums, derived-predicate helpers, and repository interfaces. Namespace `Bit.Pam`. |
| `src/Infrastructure.Dapper/Pam`                   | Dapper repositories (Microsoft SQL Server).                                              |
| `src/Infrastructure.EntityFramework/Pam`          | Entity Framework Core repositories (PostgreSQL, MySQL, SQLite).                           |
| `src/Sql/dbo/Pam`                                 | Tables and stored procedures.                                                            |
| `src/Core/Pam`                                    | The `ICipherLeaseGate` seam, so the open-source vault code can compile without this library. |

Every state-changing operation goes through both data layers. The guarded, concurrency-sensitive
operations — claiming a rotation job, accepting a cipher write, resolving an attempt — are
hand-written on both sides rather than generated, and the two implementations must agree on outcome
for outcome. A change to one is incomplete until the other matches.

## HTTP surface

`MapPamEndpoints` in [`Api/Endpoints/PamEndpointsExtensions.cs`](Api/Endpoints/PamEndpointsExtensions.cs)
maps every route as a Minimal API group. There are three groups, and the difference between them is
the whole authorization story:

| Group                                        | Authorization policy                               | Feature flag         | Caller                             |
| -------------------------------------------- | -------------------------------------------------- | -------------------- | ---------------------------------- |
| Leases, access requests, rules, audit        | `Application`                                      | `Pam`                | A member's token                   |
| `organizations/{orgId}/access-connectors/...` | `Application` + `ManageAccessConnectorRequirement` | `PamAccessConnector` | An Owner or Admin                  |
| `access-connectors/rotation/...`             | `PamRotationDaemon`                                | `PamAccessConnector` | An access connector's machine token |

Each group shares one cross-cutting chain: authorization, exception translation to
`ErrorResponseModel`, the feature gate, and request-model validation. Nested groups inherit it, so a
new route under an existing group is gated identically without doing anything.

This library cannot reference `Api`, so route-based authorization comes from
[`OrganizationAuthorization`](../../../../src/Libraries/OrganizationAuthorization/README.md) —
implement `IOrganizationRequirement` rather than adding `ICurrentContext` checks to a handler.

## Registration

`AddPamServices(Configuration)` registers the handlers, commands, queries, and rotation options, and
`AddPamJobServices()` registers the Quartz sweep services and job classes — see
[`AccessConnector/Jobs`](AccessConnector/Jobs/PamJobsServiceCollectionExtensions.cs). Both are called from
`src/Api/Startup.cs` inside its non-open-source branch, as are `MapPamEndpoints()` and the sweep jobs'
Quartz triggers in `JobsHostedService`. PAM is commercial, so an open-source build has none of it; the
feature flags gate it again at runtime for builds that do.

## Tests

- `bitwarden_license/test/Services/Pam.Test` — unit tests over commands, queries, and endpoint
  wiring, using `SutProvider` and `BitAutoData`.
- `bitwarden_license/test/Services/Pam.IntegrationTest` — end-to-end tests through the real HTTP
  pipeline.
- `test/Infrastructure.IntegrationTest/Pam` — repository tests, run per database provider.
