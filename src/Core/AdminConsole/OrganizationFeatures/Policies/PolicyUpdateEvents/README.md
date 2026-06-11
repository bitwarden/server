# IPolicyUpdateEvent

This is the policy update pattern that our system follows.
This directory contains the interfaces and infrastructure for the policy save workflow used by `ISavePolicyCommand`.

---

## Overview

When an organization policy is created or updated, the save workflow runs a series of ordered steps. A policy handler can hook into any step by implementing the corresponding Policy Update Event interface.

Note: If you do not need to hook into any step, you do not need to create a policy handler. The policy will simply upsert to the database with an audit log event.

```
SaveAsync()
  ├─ 1. Validate organization can use policies
  ├─ 2. Validate policy dependencies     ← IEnforceDependentPoliciesEvent
  ├─ 3. Run policy-specific validation   ← IPolicyValidationEvent
  ├─ 4. Execute pre-save side effects    ← IOnPolicyPreUpdateEvent
  ├─ 5. Upsert policy + log event
  └─ 6. Execute post-save side effects  ← IOnPolicyPostUpdateEvent
```

The `PolicyEventHandlerHandlerFactory` resolves the correct handler for a given `PolicyType` and interface at each step. A handler is matched by its `IPolicyUpdateEvent.Type` property. At most one handler of each interface type is permitted per `PolicyType`.

---

## Limitations

The save workflow is not atomic. If an unhandled exception occurs at any step, changes made by prior steps are not rolled back. For example, pre-save side effects that have already executed will not be undone if the upsert subsequently fails.

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

---

### `IEnforceDependentPoliciesEvent`

Declares prerequisite policies that must be enabled before this policy can be enabled. Also prevents a required policy from being disabled while a dependent policy is active.

```csharp
public interface IEnforceDependentPoliciesEvent : IPolicyUpdateEvent
{
    IEnumerable<PolicyType> RequiredPolicies { get; }
}
```

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

Executes side effects **before** the policy is upserted to the database. If an exception is thrown, the policy will not be saved.

```csharp
public interface IOnPolicyPreUpdateEvent : IPolicyUpdateEvent
{
    Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy);
}
```

Typical uses: revoking non-compliant users.

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

Note: This is more useful for enabling a policy than for disabling a policy, since when the policy is disabled, it does not have any effect.

---

### `IPolicyEventHandlerFactory`

Resolves the correct handler for a given `PolicyType` and event interface type.

```csharp
OneOf<T, None> GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent;
```

Returns the matching handler, or `None` if the policy type does not implement the requested interface. Throws `InvalidOperationException` if more than one handler is registered for the same `PolicyType` and interface.

---

## Adding a New Policy Handler

1. Create a class in `PolicyValidators/` implementing any combination of the event interfaces above.
2. Set `Type` to the appropriate `PolicyType`.
3. Register the class as `IPolicyUpdateEvent` in `PolicyServiceCollectionExtensions.AddPolicyUpdateEvents()`.

Note: No changes to `SavePolicyCommand` or `PolicyEventHandlerHandlerFactory` are required.

### Example

`AutomaticUserConfirmationPolicyEventHandler` is a good reference. It requires `SingleOrg`, validates organization compliance before enabling, and removes emergency access grants as a pre-save side effect.

**Step 1: Create the handler** (`PolicyValidators/AutomaticUserConfirmationPolicyEventHandler.cs`):

```csharp
public class AutomaticUserConfirmationPolicyEventHandler(
    IAutomaticUserConfirmationOrganizationPolicyComplianceValidator validator,
    IOrganizationUserRepository organizationUserRepository,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    : IPolicyValidationEvent, IEnforceDependentPoliciesEvent, IOnPolicyPreUpdateEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    // IEnforceDependentPoliciesEvent: SingleOrg must be enabled before this policy can be enabled
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

**Step 2: Register the handler** in `PolicyServiceCollectionExtensions.AddPolicyUpdateEvents()`:

```csharp
services.AddScoped<IPolicyUpdateEvent, AutomaticUserConfirmationPolicyEventHandler>();
```

---

## Adding a New Event Interface

Use this when the existing interfaces don't cover your use case and you need a new hook in the save workflow.

### Step 1: Define the interface in `PolicyUpdateEvents/Interfaces/`:

```csharp
public interface IMyNewEvent : IPolicyUpdateEvent
{
    Task ExecuteMyNewEventAsync(SavePolicyModel policyRequest, Policy? currentPolicy);
}
```

It must extend `IPolicyUpdateEvent`.

### Step 2: Add a step to `SavePolicyCommand.SaveAsync()`

1. Call your method at the appropriate position in the workflow
2. You can use the existing `ExecutePolicyEventAsync<T>` helper or have your method use `policyEventHandlerFactory` directly to retrieve the handlers.
3. **Note on cross-policy logic:** `IEnforceDependentPoliciesEvent` is a special case. It scans *all* registered handlers (not just the targeted policy's handler) to find dependents when disabling a policy. If your new interface requires similar cross-policy scanning, you will need to add that logic directly to `SavePolicyCommand.SaveAsync()` rather than using `ExecutePolicyEventAsync<T>`.

### Step 3: Document the interface in the [Interfaces](#interfaces) section of this README and add it to the workflow diagram.
