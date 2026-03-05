# IPolicyUpdateEvent

This is the policy update pattern that we want our system’s end state to follow.
This directory contains the interfaces and infrastructure for the policy save workflow used by `IVNextSavePolicyCommand`.
Currently, we’re using `IVNextSavePolicyCommand` to transition from the old `IPolicyValidator` pattern.

## Overview

When an organization policy is created or updated, the save workflow runs a series of ordered steps. Each step acts like a hook that a handler may listen to by implementing the particular policy event interface.
Note: If you don’t want to hook into these events, you don’t need to create a handler, and your policy will simply upsert to the database with log events.

```
SaveAsync()
  │
  ├─ 1. Validate org can use policies
  ├─ 2. Validate policy dependencies     ← IEnforceDependentPoliciesEvent
  ├─ 3. Run policy-specific validation   ← IPolicyValidationEvent
  ├─ 4. Execute pre-save side effects    ← IOnPolicyPreUpdateEvent
  ├─ 5. Upsert policy + log event
  └─ 6. Execute post-save side effects  ← IOnPolicyPostUpdateEvent
```

The `PolicyEventHandlerHandlerFactory` resolves the correct handler for a given `PolicyType` and interface at each step. A handler is matched by its `IPolicyUpdateEvent.Type` property. At most one handler of each interface type is permitted per `PolicyType`.

---

## Interfaces

### `IPolicyUpdateEvent`

The base interface that all policy event handlers must implement.

```csharp
public interface IPolicyUpdateEvent
{
    PolicyType Type { get; }
}
```

Every handler declares which `PolicyType` it handles via `Type`. All other event interfaces extend this one.

---

### `IEnforceDependentPoliciesEvent`

Declares prerequisite policies that must be enabled before this policy can be enabled. Also prevents a required policy from being disabled while a dependent policy is active.

```csharp
public interface IEnforceDependentPoliciesEvent : IPolicyUpdateEvent
{
    IEnumerable<PolicyType> RequiredPolicies { get; }
}
```

- **Enabling** – Each `PolicyType` in `RequiredPolicies` must already be enabled, otherwise a `BadRequestException` is thrown.
- **Disabling a required policy** – If any other policy has this policy listed as a requirement and is currently enabled, the disable action is blocked.

---

### `IPolicyValidationEvent`

Runs custom validation logic before the policy is saved.

```csharp
public interface IPolicyValidationEvent : IPolicyUpdateEvent
{
    Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy);
}
```

Return an empty string to pass validation. Return a non-empty error message to throw a `BadRequestException` and abort the save.

---

### `IOnPolicyPreUpdateEvent`

Executes side effects **before** the policy is upserted to the database.

```csharp
public interface IOnPolicyPreUpdateEvent : IPolicyUpdateEvent
{
    Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy);
}
```

Typical uses: revoking non-compliant users, removing emergency access grants.

---

### `IOnPolicyPostUpdateEvent`

Executes side effects **after** the policy has been upserted to the database.

```csharp
public interface IOnPolicyPostUpdateEvent : IPolicyUpdateEvent
{
    Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState);
}
```

Typical uses: creating collections, sending notifications that depend on the new policy state.

Note: This is more useful for enabling a policy than for disabling a policy, since when the policy is disabled, there is no easy way to find the users the policy should be enforced on.

---

### `IPolicyEventHandlerFactory`

Resolves the correct handler for a given `PolicyType` and event interface type.

```csharp
OneOf<T, None> GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent;
```

Returns the matching handler, or `None` if the policy type does not implement the requested interface. Throws `InvalidOperationException` if more than one handler is registered for the same `PolicyType` and interface.

---

## Adding a New Policy Handler

1. Create a class in `PolicyValidators/` implementing `IPolicyUpdateEvent` and any combination of the event interfaces above.
2. Set `Type` to the appropriate `PolicyType`.
3. Register the class as `IPolicyUpdateEvent` (and the legacy interfaces if needed) in `PolicyServiceCollectionExtensions.AddPolicyUpdateEvents()`.

Note: No changes to `VNextSavePolicyCommand` or `PolicyEventHandlerHandlerFactory` are required.

### Example

`AutomaticUserConfirmationPolicyEventHandler` is a good reference. It requires `SingleOrg`, validates org compliance before enabling, and removes emergency access grants as a pre-save side effect.

**Step 1 – Create the handler** (`PolicyValidators/AutomaticUserConfirmationPolicyEventHandler.cs`):

```csharp
public class AutomaticUserConfirmationPolicyEventHandler(
    IAutomaticUserConfirmationOrganizationPolicyComplianceValidator validator,
    IOrganizationUserRepository organizationUserRepository,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    : IPolicyValidationEvent, IEnforceDependentPoliciesEvent, IOnPolicyPreUpdateEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    // IEnforceDependentPoliciesEvent — SingleOrg must be enabled before this policy can be enabled
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    // IPolicyValidationEvent: Validates org compliance
    public async Task<string> ValidateAsync(SavePolicyModel savePolicyModel, Policy? currentPolicy)
    {
        var policyUpdate = savePolicyModel.PolicyUpdate
        var isNotEnablingPolicy = policyUpdate is not { Enabled: true };
        var policyAlreadyEnabled = currentPolicy is { Enabled: true };
        if (isNotEnablingPolicy || policyAlreadyEnabled)
        {
            return string.Empty;
        }

        return (await validator.IsOrganizationCompliantAsync(
            new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId)))
            .Match(
                error => error.Message,
                _ => string.Empty);
    }

    // IOnPolicyPreUpdateEvent: Revokes non-compliant users, removes emergency access grants before enabling
    public async Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        var isNotEnablingPolicy = policyRequest.PolicyUpdate is not { Enabled: true };
        var policyAlreadyEnabled = currentPolicy is { Enabled: true };
        if (isNotEnablingPolicy || policyAlreadyEnabled)
        {
            return;
        }

        var orgUsers = await organizationUserRepository.GetManyByOrganizationAsync(policyRequest.PolicyUpdate.OrganizationId, null);
        var orgUserIds = orgUsers.Where(w => w.UserId != null).Select(s => s.UserId!.Value).ToList();

        await deleteEmergencyAccessCommand.DeleteAllByUserIdsAsync(orgUserIds);
    }

    // IOnPolicyPostUpdateEvent: No implementation is needed since this handler doesn’t require it.
}
```

**Step 2 – Register the handler** in `PolicyServiceCollectionExtensions.AddPolicyUpdateEvents()`:

```csharp
services.AddScoped<IPolicyUpdateEvent, AutomaticUserConfirmationPolicyEventHandler>();
```


