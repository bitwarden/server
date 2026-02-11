# Organization Ability Flags

## Overview

Many Bitwarden features are tied to specific subscription plans. For example, SCIM and SSO are Enterprise features,
while Event Logs are available to Teams and Enterprise plans. When developing features that require plan-based access
control, we use **Organization Ability Flags** (or simply _abilities_) — explicit boolean properties on the Organization
entity that indicate whether an organization can use a specific feature.

## The Rule

**Never check plan types to control feature access.** Always use a dedicated ability flag on the Organization entity.

### ❌ Don't Do This

```csharp
// Checking plan type directly
if (organization.PlanType == PlanType.Enterprise ||
    organization.PlanType == PlanType.Teams ||
    organization.PlanType == PlanType.Family)
{
    // allow feature...
}
```

### ❌ Don't Do This

```csharp
// Piggybacking off another feature's ability
if (organization.PlanType == PlanType.Enterprise && organization.UseEvents)
{
    // assume they can use some other feature...
}
```

### ✅ Do This Instead

```csharp
// Check the explicit ability flag
if (!organization.UseEvents)
{
    throw new BadRequestException("Your organization does not have access to this feature.");
}
// proceed with feature logic...
```

## Why This Pattern Matters

Using explicit ability flags instead of plan type checks provides several benefits:

1. **Simplicity** — A single boolean check is cleaner and less error-prone than maintaining lists of plan types.

2. **Centralized Control** — Feature access is managed in one place: the ability assignment during organization
   creation/upgrade. No need to hunt through the codebase for scattered plan type checks.

3. **Flexibility** — Abilities can be set independently of plan type, enabling:
   - Early access programs for features not yet tied to a plan
   - Trial access to help customers evaluate a feature before upgrading
   - Custom arrangements for specific customers (can be manually toggled in Bitwarden Portal)
   - A/B testing of features across different cohorts
   - Gating high-risk features behind internal support teams (e.g., Key Connector)

4. **Safe Refactoring** — When plans change (e.g., adding a new plan tier, renaming plans, or moving features between
   tiers), we only update the ability assignment logic—not every place the feature is used.

5. **Graceful Downgrades** — When an organization downgrades, we update their abilities. All feature checks
   automatically respect the new access level.

6. **Semantic Code** — The code clearly expresses what capability is being checked, making it more maintainable.

## How It Works

### Ability Assignment at Signup/Upgrade

When an organization is created or changes plans, the ability flags are set based on the plan's capabilities:

```csharp
// During organization creation or plan change
organization.UseGroups = plan.HasGroups;
organization.UseSso = plan.HasSso;
organization.UseScim = plan.HasScim;
organization.UsePolicies = plan.HasPolicies;
organization.UseEvents = plan.HasEvents;
// ... etc
```

### Accessing Abilities in Code

**Server-side:**

- If you already have the full `Organization` object in scope, use it directly: `organization.UseMyFeature`
- If not, use the in-memory cache to avoid hitting the database: `IApplicationCacheService.GetOrganizationAbilityAsync(orgId)`
  - This returns an `OrganizationAbility` object - a simplified, cached representation of the ability flags
  - Note: some older flags may be missing from `OrganizationAbility` but can be added if needed

**Client-side:**

- Get the organization object from `OrganizationService`, then use it directly: `organization.useMyFeature`

### Manual Override via Bitwarden Portal

Organization abilities can be manually toggled for specific customers via the Bitwarden Portal → Organizations page.
This is useful for custom arrangements, early access, or internal testing.

## Adding a New Ability

When developing a new plan-gated feature, follow these steps. We use `MyFeature` as a placeholder for your feature name
(e.g., `UseEvents`).

### 1. Update Core Entities

- `src/Core/AdminConsole/Entities/Organization.cs` — Add `UseMyFeature` boolean property
- `src/Core/AdminConsole/OrganizationFeatures/OrganizationAbility/OrganizationAbility.cs` — Add to ability object

### 2. Database Changes (MSSQL)

Add a new `UseMyFeature` column to the Organization table:

**Files to modify:**

- `src/Sql/dbo/Tables/Organization.sql` — Add column with `NOT NULL` constraint and default of `0` (false) for EDD
  backward compatibility

**Stored procedures to update:**

- `src/Sql/dbo/Stored Procedures/Organization_Create.sql`
- `src/Sql/dbo/Stored Procedures/Organization_Update.sql`
- `src/Sql/dbo/Stored Procedures/Organization_ReadAbilities.sql`

**Views to update (add the new column):**

- `src/Sql/dbo/Views/OrganizationUserOrganizationDetailsView.sql`
- `src/Sql/dbo/Views/ProviderUserProviderOrganizationDetailsView.sql`
- `src/Sql/dbo/Views/OrganizationView.sql`

**Views to refresh (use `sp_refreshview`):**

After schema changes, the following views may need to be refreshed even though they don't explicitly include the new
column:

- `src/Sql/dbo/Views/OrganizationCipherDetailsCollectionsView.sql`
- `src/Sql/dbo/Views/ProviderOrganizationOrganizationDetailsView.sql`

**Create a migration script** for these database changes.

### 3. Entity Framework Changes

EF is primarily used for self-host. Implementations must be kept consistent.

**Generate EF migrations** for the new column.

**Update queries and initialization code:**

- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/OrganizationRepository.cs`
  - Update `GetManyAbilitiesAsync()` to initialize the new property
- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/Queries/OrganizationUserOrganizationDetailsViewQuery.cs`
  - Update the integration test: `test/Infrastructure.IntegrationTest/AdminConsole/Repositories/OrganizationUserRepository/OrganizationUserRepositoryTests.cs`
- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/Queries/ProviderUserOrganizationDetailsViewQuery.cs`

### 4. Data Migrations for Existing Organizations

If your feature should be enabled for existing organizations on certain plan types, create data migrations to set the ability flag:

**MSSQL migration:**

```sql
-- Example: Enable UseMyFeature for all Enterprise organizations
UPDATE [dbo].[Organization]
SET UseMyFeature = 1
WHERE PlanType IN (13, 14) -- EnterpriseMonthly = 13, EnterpriseAnnually = 14
```

**EF migration:**

Create a corresponding data migration for EF databases used by self-hosted instances.

### 5. Server Code Changes

Update the mapping code so models receive the new value and new organizations get the correct value.

**Response models:**

- `src/Api/AdminConsole/Models/Response/Organizations/OrganizationResponseModel.cs`
- `src/Api/AdminConsole/Models/Response/BaseProfileOrganizationResponseModel.cs`

**Data models:**

- `src/Core/AdminConsole/Models/Data/Organizations/OrganizationUsers/OrganizationUserOrganizationDetails.cs`
- `src/Core/AdminConsole/Models/Data/Provider/ProviderUserOrganizationDetails.cs`

**Plan definition and signup logic:**

If your feature should be automatically enabled based on plan type at signup (e.g., SSO for Enterprise plans), you'll
need to:

1. Work with the Billing Team to add a `HasMyFeature` property to the Plan model and configure which plans include it
2. Update `src/Core/AdminConsole/OrganizationFeatures/Organizations/CloudOrganizationSignUpCommand.cs` to map
   `plan.HasMyFeature` to `organization.UseMyFeature`

**Note:** This step is not required if your feature is enabled manually via the Admin Portal.

### 6. Client Changes

**TypeScript models to update:**

- `libs/common/src/admin-console/models/response/profile-organization.response.ts`
- `libs/common/src/admin-console/models/response/organization.response.ts`
- `libs/common/src/admin-console/models/domain/organization.ts`
- `libs/common/src/admin-console/models/data/organization.data.ts`
  - Update tests: `libs/common/src/admin-console/models/data/organization.data.spec.ts`

### 7. Bitwarden Portal Changes

For manual override capability in the admin portal:

- `src/Admin/AdminConsole/Models/OrganizationEditModel.cs` — Map the ability from the organization entity
- `src/Admin/AdminConsole/Views/Shared/_OrganizationForm.cshtml` — Add checkbox for the new ability
- `src/Admin/AdminConsole/Controllers/OrganizationsController.cs` — Update `UpdateOrganization()` method mapping

### 8. Self-Host Licensing

> ⚠️ **WARNING:** Mistakes in organization license changes can disable the entire organization for self-hosted customers!
> Double-check your work and ask for help if unsure.
>
> **Note:** Do not add new properties to the `OrganizationLicense` file - make sure you use the claims-based system below.

Organization features are now **claims-based**. You'll need to:

**Add claims for the new feature:**

- `src/Core/Billing/Licenses/LicenseConstants.cs` — Add constant for the new ability in `OrganizationLicenseConstants`
- `src/Core/Billing/Licenses/Services/Implementations/OrganizationLicenseClaimsFactory.cs`

**Update license verification:**

- `src/Core/Billing/Organizations/Models/OrganizationLicense.cs`
  - `VerifyData()` (line ~424) — Add claims validation

**Update license command:**

Map your feature property from the claim to the organization when creating or updating from the license file:

- `src/Core/AdminConsole/Services/OrganizationFactory.cs`
- `src/Core/Billing/Organizations/Commands/UpdateOrganizationLicenseCommand.cs`

**Update tests:**

- `test/Core.Test/Billing/Organizations/Commands/UpdateOrganizationLicenseCommandTests.cs`
  - Exclude from test comparison (line ~91)

### 9. Implement Business Logic Checks

In your feature's business logic, check the ability flag:

```csharp
// Retrieve the organization ability (uses cache, avoids DB hit)
var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);

if (!orgAbility.UseMyFeature)
{
    throw new BadRequestException("Your organization's plan does not support this feature.");
}

// Proceed with feature logic...
```

### 10. Feature Flags

Organization abilities do **not** replace feature flags. They serve different purposes:

- **Feature flags** — Short-lived flags that control feature release and can act as a killswitch for defective features.
  Can be toggled immediately without a new deployment.
- **Organization ability flags** — Permanent flags that control access to a feature based on plan type.
  Require a database migration to toggle in bulk.

You should still use a feature flag to control your feature release:

```csharp
if (!_featureService.IsEnabled(FeatureFlagKeys.MyFeature))
{
    throw new BadRequestException("This feature is not available.");
}

if (!orgAbility.UseMyFeature)
{
    throw new BadRequestException("Your organization's plan does not support this feature.");
}
```

## Existing Abilities

For reference, here are some current organization ability flags (not a complete list):

| Ability                  | Description                   | Typical Plans     |
| ------------------------ | ----------------------------- | ----------------- |
| `UseGroups`              | Group-based collection access | Teams, Enterprise |
| `UseDirectory`           | Directory Connector sync      | Teams, Enterprise |
| `UseEvents`              | Event logging                 | Teams, Enterprise |
| `UseTotp`                | Authenticator (TOTP)          | Teams, Enterprise |
| `UseSso`                 | Single Sign-On                | Enterprise        |
| `UseScim`                | SCIM provisioning             | Teams, Enterprise |
| `UsePolicies`            | Enterprise policies           | Enterprise        |
| `UseResetPassword`       | Admin password reset          | Enterprise        |
| `UseOrganizationDomains` | Domain verification/claiming  | Enterprise        |

## Questions?

If you're unsure whether your feature needs a new ability or which existing ability to use, reach out to your team lead
or members of the Admin Console or Architecture teams. When in doubt, adding an explicit ability is almost always the
right choice—it's easy to do and keeps our access control clean and maintainable.
