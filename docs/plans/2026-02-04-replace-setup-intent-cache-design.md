# Replace SetupIntent Cache with Customer-Based Approach

## Overview

Replace the `ISetupIntentCache` distributed cache with a simpler approach that relates Stripe `SetupIntent` objects directly to Stripe `Customer` objects. This eliminates infrastructure dependencies and simplifies the codebase by removing dead code paths.

## Problem

The current `SetupIntentDistributedCache` has infrastructural issues that break the bank account payment method flow for organizations and providers. The cache maintains a bidirectional mapping between subscriber IDs and SetupIntent IDs, which is fragile and adds complexity.

## Solution

Instead of caching the SetupIntent-to-subscriber relationship:

1. **Set the `customer` field on the SetupIntent** when we first retrieve it (during subscription creation or payment method update)
2. **Query subscribers by `GatewayCustomerId`** when webhooks arrive, using new repository methods
3. **Remove all cache-related code** and dead code paths that are no longer used

---

## Core Approach

### Current Flow (Cache-Based)
```
Create SetupIntent → Cache(subscriberId, setupIntentId)
Webhook arrives → Cache.GetSubscriberIdForSetupIntent(setupIntentId) → Query DB by subscriberId
```

### New Flow (Customer-Based)
```
Retrieve SetupIntent → Update SetupIntent with customer ID
Webhook arrives → setupIntent.CustomerId → Query DB by GatewayCustomerId
```

---

## Files to Modify

| File | Change |
|------|--------|
| `OrganizationBillingService.CreateCustomerAsync` | After creating customer, update SetupIntent with `customer` field |
| `ProviderBillingService.SetupCustomer` | After creating customer, update SetupIntent with `customer` field |
| `ProviderBillingService.SetupSubscription` | Query Stripe by customer ID instead of cache lookup to check for unverified bank account |
| `UpdatePaymentMethodCommand.Run` | After getting SetupIntent, update it with `customer` field |
| `SetupIntentSucceededHandler` | Query org/provider repository by `GatewayCustomerId` instead of cache |
| `GetPaymentMethodQuery` | Query Stripe by customer ID instead of cache lookup |
| `HasPaymentMethodQuery` | Query Stripe by customer ID instead of cache lookup |
| `StripeEventService.GetCustomerMetadataFromSetupIntentSucceededEvent` | Query repository by `GatewayCustomerId` instead of cache |

## Files to Remove

| File | Reason |
|------|--------|
| `src/Core/Billing/Caches/ISetupIntentCache.cs` | No longer needed |
| `src/Core/Billing/Caches/Implementations/SetupIntentDistributedCache.cs` | No longer needed |

## Methods to Remove (Dead Code)

| Location | Method(s) | Reason |
|----------|-----------|--------|
| `UserService` | `SignUpPremiumAsync`, `ReplacePaymentMethodAsync` | Orphaned at API layer |
| `IUserService` | Same interface methods | |
| `PremiumUserBillingService` | `Finalize`, `UpdatePaymentMethod`, `CreateCustomerAsync` | Only callers are orphaned methods |
| `IPremiumUserBillingService` | Same interface methods (keep only `Credit`) | |
| `OrganizationBillingService` | `UpdatePaymentMethod` | Replaced by `UpdatePaymentMethodCommand` |
| `IOrganizationBillingService` | Same interface method | |
| `ProviderBillingService` | `UpdatePaymentMethod` | Replaced by `UpdatePaymentMethodCommand` |
| `IProviderBillingService` | Same interface method | |
| `SubscriberService` | `UpdatePaymentSource` | Only callers are dead methods |
| `ISubscriberService` | Same interface method | |
| `CreatePremiumCloudHostedSubscriptionCommand` | Bank account case in switch + Revert logic | Premium users cannot use bank accounts |

## DI Registration Updates

Remove from `ServiceCollectionExtensions.cs`:
```csharp
services.AddTransient<ISetupIntentCache, SetupIntentDistributedCache>();
```

---

## Database Changes

### New Repository Methods

Add to `IOrganizationRepository`, `IProviderRepository`, `IUserRepository`:

```csharp
Task<T?> GetByGatewayCustomerIdAsync(string gatewayCustomerId);
Task<T?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId);
```

### SQL Server (Dapper)

**New Stored Procedures:**
- `User_ReadByGatewayCustomerId`
- `User_ReadByGatewaySubscriptionId`
- `Organization_ReadByGatewayCustomerId`
- `Organization_ReadByGatewaySubscriptionId`
- `Provider_ReadByGatewayCustomerId`
- `Provider_ReadByGatewaySubscriptionId`

**New Indexes:**
```sql
CREATE NONCLUSTERED INDEX [IX_User_GatewayCustomerId]
    ON [dbo].[User]([GatewayCustomerId]) WHERE [GatewayCustomerId] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_User_GatewaySubscriptionId]
    ON [dbo].[User]([GatewaySubscriptionId]) WHERE [GatewaySubscriptionId] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Organization_GatewayCustomerId]
    ON [dbo].[Organization]([GatewayCustomerId]) WHERE [GatewayCustomerId] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Organization_GatewaySubscriptionId]
    ON [dbo].[Organization]([GatewaySubscriptionId]) WHERE [GatewaySubscriptionId] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Provider_GatewayCustomerId]
    ON [dbo].[Provider]([GatewayCustomerId]) WHERE [GatewayCustomerId] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Provider_GatewaySubscriptionId]
    ON [dbo].[Provider]([GatewaySubscriptionId]) WHERE [GatewaySubscriptionId] IS NOT NULL;
```

### Entity Framework (MySQL, Postgres, SQLite)

**EntityTypeConfiguration updates:**
```csharp
// OrganizationEntityTypeConfiguration.cs
builder.HasIndex(o => o.GatewayCustomerId);
builder.HasIndex(o => o.GatewaySubscriptionId);

// UserEntityTypeConfiguration.cs, ProviderEntityTypeConfiguration.cs (same pattern)
```

**Generate migrations:**
```bash
./dev/ef_migrate.ps1 -Name AddGatewayIndexes
```

---

## SetupIntentSucceededHandler Flow

### New Flow

1. Get `setupIntent` from webhook payload (ensure customer is expanded)
2. Validate `setupIntent` has a US bank account payment method
3. Get `customerId = setupIntent.CustomerId`
4. If `customerId` is null → log warning and return
5. Query repositories in sequence:
   - `organization = await orgRepo.GetByGatewayCustomerIdAsync(customerId)`
   - If null: `provider = await providerRepo.GetByGatewayCustomerIdAsync(customerId)`
6. If neither found → log error and return
7. Attach payment method to customer (using `customerId` from SetupIntent)
8. Set as default payment method
9. Send notification to organization/provider

**Note:** Users are not queried because premium users cannot use bank account payment methods.

---

## Test Strategy

### New Tests to Write

| Component | Tests |
|-----------|-------|
| `OrganizationRepository.GetByGatewayCustomerIdAsync` | Returns org when found, returns null when not found |
| `OrganizationRepository.GetByGatewaySubscriptionIdAsync` | Same pattern |
| `ProviderRepository.GetByGateway*` | Same pattern |
| `UserRepository.GetByGateway*` | Same pattern |
| `SetupIntentSucceededHandler` | Finds org by customer ID, finds provider when org not found |
| `GetPaymentMethodQuery` | Queries Stripe by customer ID |
| `HasPaymentMethodQuery` | Queries Stripe by customer ID |
| `OrganizationBillingService.CreateCustomerAsync` | Updates SetupIntent with customer ID |
| `ProviderBillingService.SetupCustomer` | Updates SetupIntent with customer ID |
| `UpdatePaymentMethodCommand` | Updates SetupIntent with customer ID |

### Tests to Remove

| Dead Code | Associated Tests |
|-----------|------------------|
| `ISetupIntentCache` | Any tests mocking `ISetupIntentCache` |
| `SubscriberService.UpdatePaymentSource` | `SubscriberServiceTests.UpdatePaymentMethod_*` (~13 tests) |
| `PremiumUserBillingService.Finalize` | `PremiumUserBillingServiceTests.Finalize_*` |
| `PremiumUserBillingService.UpdatePaymentMethod` | `PremiumUserBillingServiceTests.UpdatePaymentMethod_*` |
| `UserService.SignUpPremiumAsync` | `UserServiceTests.SignUpPremiumAsync_*` |
| `UserService.ReplacePaymentMethodAsync` | `UserServiceTests.ReplacePaymentMethodAsync_*` |
| `OrganizationBillingService.UpdatePaymentMethod` | `OrganizationBillingServiceTests.UpdatePaymentMethod_*` |
| `ProviderBillingService.UpdatePaymentMethod` | `ProviderBillingServiceTests.UpdatePaymentMethod_*` |
| `CreatePremiumCloudHostedSubscriptionCommand` (bank account case) | Tests covering bank account switch case |

### Tests to Update

| File | Change |
|------|--------|
| `SetupIntentSucceededHandlerTests` | Replace `ISetupIntentCache` mocks with repository mocks |
| `GetPaymentMethodQueryTests` | Replace cache mocks with Stripe adapter mocks |
| `HasPaymentMethodQueryTests` | Same as above |
| `UpdatePaymentMethodCommandTests` | Replace cache mocks with Stripe adapter mocks |
| `OrganizationBillingServiceTests` | Update `CreateCustomerAsync` tests to verify SetupIntent update |
| `ProviderBillingServiceTests` | Update `SetupCustomer` and `SetupSubscription` tests to verify SetupIntent update / Stripe query |
| `StripeEventServiceTests` | Update to use repository instead of cache |

---

## Implementation Guidelines

1. **Use TDD** - Invoke `/superpowers:test-driven-development` skill when writing code
2. **Logical git commits** - Not too small, not too big; always group related code changes together
3. **No AI signatures** - Never use "Co-Authored-By: Claude..." or similar in commit messages
4. **Ask questions when stuck** - If there's a conflict or something causes confusion, stop and ask rather than guessing

---

## Implementation Order

Use TDD approach:

1. Write failing tests for new repository methods
2. Implement database changes (stored procedures, indexes, EF migrations)
3. Implement repository methods → tests pass
4. Write failing tests for updated handlers/queries
5. Implement handler/query changes → tests pass
6. Remove dead code and associated tests
7. Remove cache interface and implementation
8. Update DI registration
