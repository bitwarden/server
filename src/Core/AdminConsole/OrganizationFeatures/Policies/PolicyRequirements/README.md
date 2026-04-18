# Policy Requirements

## Overview

Organization policies are admin-configured rules that govern member behavior. For example: "all members must use SSO", or "members cannot create Sends".

There are two distinct phases in the policy lifecycle:

1. **Policy administration** — organization admins enable and configure policies via the admin console. This is owned by the Admin Console team.
2. **Policy enforcement** — feature code checks whether a policy applies to a user before permitting an action. This is owned by the team that owns the affected feature.

This directory provides the server-side framework for phase 2.

The two core operations the framework performs are:

- **Filtering** — determining which policies should be enforced against a given user, based on their role and membership status within each organization
- **Combining** — aggregating policies from multiple organizations into a single, coherent answer for the consuming feature

An `IPolicyRequirement` is a bridge between raw policy data (the policy domain) and the feature that enforces it (the feature domain). It expresses the business impact of a policy in terms meaningful to the consuming feature. For example, `DisableSend` is preferable to `Enabled` — the name describes what the policy means _for that feature_, not just that it exists. Feature teams define their own requirement classes and are free to choose whatever domain model best fits their needs.

## Design Principles

- **Domain agnostic** — The core framework (`IPolicyRequirementQuery`, `BasePolicyRequirementFactory`) has no knowledge of specific policies or feature domains. New policy requirements can be added without touching the core.
- **Separation of concerns** — Policy fetching (repository), filtering (`Enforce`), combining (`Create`), and enforcement (consumer) are four independent steps. Each can be understood and tested in isolation.
- **Team ownership** — Each policy requirement is self-contained. The team that owns the feature owns the requirement class and its factory, and chooses the combining strategy that fits their domain.
- **Always returns a value** — `GetAsync<T>` always returns a requirement, even when no policies should be enforced; this is also expressed by the requirement. Consumers never need to null-check.

## How It Works

### Architecture

```
IPolicyRepository (database)
        │
        ▼
PolicyRequirementQuery (implements IPolicyRequirementQuery)
  │
  │  For each query:
  │  1. Finds the registered factory for type T
  │  2. Fetches PolicyDetails rows for the user and policy type
  │  3. Calls factory.Enforce() on each row to filter inapplicable policies
  │  4. Calls factory.Create() on the remaining rows to produce T
  │
  └──▶ IPolicyRequirementFactory<T>
         ├─ Enforce(PolicyDetails) → bool
         └─ Create(IEnumerable<PolicyDetails>) → T : IPolicyRequirement
```

### Key types

| Type                              | Role                                                                                                           |
| --------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `IPolicyRequirement`              | Marker interface implemented by each requirement class.                                                        |
| `IPolicyRequirementFactory<T>`    | Defines `PolicyType`, `Enforce()`, and `Create()` to filter and combine policies into an `IPolicyRequirement`. |
| `BasePolicyRequirementFactory<T>` | Abstract base with sensible filtering defaults for most policies.                                              |
| `IPolicyRequirementQuery`         | The consumer-facing interface for querying requirements.                                                       |
| `PolicyRequirementQuery`          | Concrete implementation of the query.                                                                          |
| `PolicyDetails`                   | DTO representing one (user × organization × policy) row from the database.                                     |

### Filtering — `Enforce(PolicyDetails)`

`BasePolicyRequirementFactory` implements `Enforce()` and returns `true` if the policy should be enforced against this user. It filters out users who are exempt by default:

| Property          | Default exempt       | Meaning                                                                                |
| ----------------- | -------------------- | -------------------------------------------------------------------------------------- |
| `ExemptRoles`     | `Owner`, `Admin`     | These roles are not subject to enforcement.                                            |
| `ExemptStatuses`  | `Invited`, `Revoked` | Users with these membership statuses are not subject to enforcement.                   |
| `ExemptProviders` | `true`               | Users who are also provider users for the organization are not subject to enforcement. |

Concrete factories override these properties when the defaults are not appropriate for their policy.

> **Warning:** Take care when removing `Invited` or `Revoked` from `ExemptStatuses`. An invited user has not yet accepted membership and may never do so. A revoked user may also never have accepted — a user can be revoked from any status, including `Invited`. Policies should only be enforced against either status in specific, intentional scenarios. Check with the Admin Console team if you are unsure.

### Combining — `Create(IEnumerable<PolicyDetails>)`

A user may be a member of multiple organizations, each with the same policy enabled. `Create()` receives all applicable `PolicyDetails` for that user — after filtering — and reduces them to a single requirement. Each factory chooses its own combining strategy:

| Strategy                    | Pattern                                                    |
| --------------------------- | ---------------------------------------------------------- |
| Any-present                 | Any applicable policy → feature is disabled                |
| OR aggregation              | Boolean flags set if any organization enables them         |
| Per-organization dictionary | `{ organizationId → setting }` for per-organization lookup |

### Data flow example

```csharp
// Consumer calls:
var requirement = await _policyRequirementQuery.GetAsync<DisableSendPolicyRequirement>(userId);

// Internally:
// 1. PolicyRequirementQuery finds DisableSendPolicyRequirementFactory
// 2. Queries: policyRepository.GetPolicyDetailsByUserIdsAndPolicyType([userId], PolicyType.DisableSend)
// 3. For each PolicyDetails row: factory.Enforce(row)
//    → filters out Owners, Admins, Invited/Revoked members, and provider users
// 4. factory.Create(filteredRows)
//    → DisableSendPolicyRequirement { DisableSend = filteredRows.Any() }
// 5. Returns: DisableSendPolicyRequirement { DisableSend = true }

// Consumer enforces:
if (requirement.DisableSend)
{
    throw new BadRequestException("Due to an Enterprise Policy, you are only able to delete an existing Send.");
}
```

## How to Add a New Policy Requirement

### Step 1 — Create the requirement class

Create `MyFeaturePolicyRequirement.cs` in this directory. Implement `IPolicyRequirement`.

Express the business impact of the policy in terms that are meaningful to your feature. Prefer domain-specific property names (e.g. `DisableSend`, `SsoRequired`) over generic ones (e.g. `Enabled`, `IsActive`).

```csharp
public class MyFeaturePolicyRequirement : IPolicyRequirement
{
    public bool DisableMyFeature { get; init; }
}
```

### Step 2 — Create the factory

Create `MyFeaturePolicyRequirementFactory` in the same file. Extend `BasePolicyRequirementFactory<T>` and implement `PolicyType` and `Create()`.

```csharp
public class MyFeaturePolicyRequirementFactory : BasePolicyRequirementFactory<MyFeaturePolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.MyFeature;

    // Override these only if the defaults don't fit your policy's requirements:
    // protected override IEnumerable<OrganizationUserType> ExemptRoles => [];
    // protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [];
    // protected override bool ExemptProviders => false;
    //
    // WARNING: Take care when removing Invited or Revoked from ExemptStatuses. An invited user has
    // not yet accepted membership and may never do so. A revoked user may also never have accepted —
    // a user can be revoked from any status, including Invited.

    public override MyFeaturePolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
        => new() { DisableMyFeature = policyDetails.Any() };
}
```

If your policy has configuration data, deserialize it from `PolicyDetails` using `policyDetails.GetDataModel<TDataModel>()`.

### Step 3 — Register in DI

In `PolicyServiceCollectionExtensions.cs`, add your factory to `AddPolicyRequirements()`:

```csharp
services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, MyFeaturePolicyRequirementFactory>();
```

### Step 4 — Consume

Inject `IPolicyRequirementQuery` into the service that performs enforcement and call `GetAsync<T>`:

```csharp
var requirement = await _policyRequirementQuery.GetAsync<MyFeaturePolicyRequirement>(userId);
if (requirement.DisableMyFeature)
{
    throw new BadRequestException("This action is not allowed due to an Enterprise Policy.");
}
```

### Step 5 — Write tests

Test your factory's `Create()` method directly with hand-crafted `PolicyDetails` lists. Cover:

- Empty input (no applicable policies)
- A single policy with representative data
- Multiple policies from different organizations, to verify the combining logic

Test any non-trivial domain logic on the requirement class itself separately.

See `test/Core.Test/AdminConsole/OrganizationFeatures/Policies/PolicyRequirements/` for existing examples, and `PolicyRequirementFixtures.cs` in the same directory for shared test helpers.
