# Organization ability flags

## Overview

Many Bitwarden features are tied to specific subscription plans. For example, SCIM and SSO are Enterprise features,
while Event Logs are available to Teams and Enterprise plans. When developing features that require plan-based access
control, we use **Organization Ability Flags** (or simply _abilities_) — explicit boolean properties on the Organization
entity that indicate whether an organization can use a specific feature.

## The rule

**Never check plan types to control feature access.** Always use a dedicated ability flag on the Organization entity.

### ❌ Don't do this

```csharp
// Checking plan type directly
if (organization.PlanType == PlanType.Enterprise ||
    organization.PlanType == PlanType.Teams ||
    organization.PlanType == PlanType.Family)
{
    // allow feature...
}
```

### ❌ Don't do this

```csharp
// Piggybacking off another feature's ability
if (organization.PlanType == PlanType.Enterprise && organization.UseEvents)
{
    // assume they can use some other feature...
}
```

### ✅ Do this instead

```csharp
// Check the explicit ability flag
if (!organization.UseEvents)
{
    throw new BadRequestException("Your organization does not have access to this feature.");
}
// proceed with feature logic...
```

## Why this pattern matters

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

## Organization abilities and other features

Organization abilities work alongside other access control mechanisms. Understanding the differences helps you choose the right tool:

|                   | **Organization abilities** (this document)                                                         | **Feature flags**                                                | **Enterprise policies**                                                 |
|-------------------|----------------------------------------------------------------------------------------------------|------------------------------------------------------------------|-------------------------------------------------------------------------|
| **Purpose**       | Control whether an organization has **access** to a feature                                        | Control feature **rollout** and act as a killswitch if necessary | Control **behavior** of features the organization already has access to |
| **Set by**        | Subscription plan (automatically) or internal support teams (manual override via Bitwarden Portal) | Engineering teams                                                | Organization admins and owners                                          |
| **Lifecycle**     | Permanent - part of the core product                                                               | Temporary - removed once feature is stable                       | Permanent - part of the core product                                    |
| **Scope**         | Per organization                                                                                   | Global or targeted                                               | Per organization                                                        |
| **Toggle method** | Bitwarden Portal (single) or data migration (bulk)                                                 | LaunchDarkly                                                     | In-product via Admin Console                                            |
| **Examples**      | Can the org use SSO? Can they use SCIM? Can they use Events?                                       | Is the new API available? Is the redesigned UI enabled?          | Require 2FA for all users, enforce password complexity                  |

### When to use which?

**Use an organization ability** when the feature will be permanently gated behind a subscription tier or our support teams.

**Use a feature flag** when you need to control the release of a new feature.

**Use a policy** when you're adding configurable rules to a feature the organization can already access.

**Use multiple together** when appropriate. For example, a new enterprise feature might use all three: a feature flag to control initial rollout, an organization ability to restrict it to Enterprise plans, and a policy to let admins configure enforcement rules.

## How it works

### Ability assignment at signup/upgrade

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

### Accessing abilities in code

**Server-side:**

- If you already have the full `Organization` object in scope, use it directly: `organization.UseMyFeature`
- If not, use the in-memory cache to avoid hitting the database:
  `IApplicationCacheService.GetOrganizationAbilityAsync(orgId)`
    - This returns an `OrganizationAbility` object - a simplified, cached representation of the ability flags
    - Note: some older flags may be missing from `OrganizationAbility` but can be added if needed

**Client-side:**

- Get the organization object from `OrganizationService`, then use it directly: `organization.useMyFeature`

### Manual override via Bitwarden Portal

Organization abilities can be manually toggled for specific customers via the Bitwarden Portal → Organizations page.
This is useful for custom arrangements, early access, or internal testing.

## Adding a new ability

When developing a new plan-gated feature, follow these steps. We use `MyFeature` as a placeholder for your feature name
(e.g., `UseEvents`).

### 1. Update core entities

- `src/Core/AdminConsole/Entities/Organization.cs` — Add `UseMyFeature` boolean property
- `src/Core/AdminConsole/OrganizationFeatures/OrganizationAbility/OrganizationAbility.cs` — Add to ability object

### 2. Database changes (MSSQL)

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

### 3. Entity Framework changes

EF is primarily used for self-host. Implementations must be kept consistent.

**Generate EF migrations** for the new column.

**Update queries and initialization code:**

- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/OrganizationRepository.cs`
    - Update `GetManyAbilitiesAsync()` to initialize the new property
- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/Queries/OrganizationUserOrganizationDetailsViewQuery.cs`
    - Update the integration test:
      `test/Infrastructure.IntegrationTest/AdminConsole/Repositories/OrganizationUserRepository/OrganizationUserRepositoryTests.cs`
- `src/Infrastructure.EntityFramework/AdminConsole/Repositories/Queries/ProviderUserOrganizationDetailsViewQuery.cs`

### 4. Data migrations for existing organizations

If your feature should be enabled for existing organizations on certain plan types, create data migrations to set the
ability flag:

**MSSQL migration:**

```sql
-- Example: Enable UseMyFeature for all Enterprise organizations
UPDATE [dbo].[Organization]
SET UseMyFeature = 1
WHERE PlanType IN (13, 14) -- EnterpriseMonthly = 13, EnterpriseAnnually = 14
```

**EF migration:**

Create a corresponding data migration for EF databases used by self-hosted instances.

### 5. Server code changes

Update related models and mapping code so models receive the new value.

**Response models:**

- `src/Api/AdminConsole/Models/Response/Organizations/OrganizationResponseModel.cs`
- `src/Api/AdminConsole/Models/Response/BaseProfileOrganizationResponseModel.cs`

**Data models:**

- `src/Core/AdminConsole/Models/Data/Organizations/OrganizationUsers/OrganizationUserOrganizationDetails.cs`
- `src/Core/AdminConsole/Models/Data/Provider/ProviderUserOrganizationDetails.cs`
- `src/Core/AdminConsole/Models/Data/Organizations/SelfHostedOrganizationDetails.cs`
- `src/Core/AdminConsole/Models/Data/IProfileOrganizationDetails.cs`

**Plan definition and signup logic:**

If your feature should be automatically enabled based on plan type at signup (e.g., SSO for Enterprise plans), you'll
need to:

1. Work with the Billing Team to add a `HasMyFeature` property to the Plan model and configure which plans include it
2. Update `src/Core/AdminConsole/OrganizationFeatures/Organizations/CloudOrganizationSignUpCommand.cs` to map
   `plan.HasMyFeature` to `organization.UseMyFeature`

**Note:** This step is not required if your feature is enabled manually via the Admin Portal.

### 6. Client changes

**TypeScript models to update:**

- `libs/common/src/admin-console/models/response/profile-organization.response.ts`
- `libs/common/src/admin-console/models/response/organization.response.ts`
- `libs/common/src/admin-console/models/domain/organization.ts`
- `libs/common/src/admin-console/models/data/organization.data.ts`
    - Update tests: `libs/common/src/admin-console/models/data/organization.data.spec.ts`

### 7. Bitwarden Portal changes

For manual override capability in the admin portal:

- `src/Admin/AdminConsole/Models/OrganizationEditModel.cs` — Map the ability from the organization entity
- `src/Admin/AdminConsole/Views/Shared/_OrganizationForm.cshtml` — Add checkbox for the new ability
- `src/Admin/AdminConsole/Views/Shared/_OrganizationFormScripts.cshtml` — Add the new ability to the
  `togglePlanFeatures()` function so it's automatically set when a plan type is selected
- `src/Admin/AdminConsole/Controllers/OrganizationsController.cs` — Update `UpdateOrganization()` method mapping

### 8. Self-host licensing

> ⚠️ **WARNING:** Mistakes in organization license changes can disable the entire organization for self-hosted
> customers!
> Double-check your work and ask for help if unsure.
>
> **Note:** New properties must be added to both the `OrganizationLicense` class and the claims-based system.

**Update OrganizationLicense:**

- `src/Core/Billing/Organizations/Models/OrganizationLicense.cs`
    - Add the new property to the class
    - `VerifyData()` — Add claims validation
    - `GetDataBytes()` — Add the new property to the ignored fields section (below the comment
      `// any new fields added need to be added here so that they're ignored`)

**Add property to Organization entity mapper:**

- `src/Core/AdminConsole/Entities/Organization.cs` — Add the new property to the `UpdateFromLicense()` method

**Add claims for the new feature:**

- `src/Core/Billing/Licenses/LicenseConstants.cs` — Add constant for the new ability in `OrganizationLicenseConstants`
- `src/Core/Billing/Licenses/Services/Implementations/OrganizationLicenseClaimsFactory.cs`

**Update license command:**

Map your feature property from the claim to the organization when creating or updating from the license file:

- `src/Core/AdminConsole/Services/OrganizationFactory.cs`
- `src/Core/Billing/Organizations/Commands/UpdateOrganizationLicenseCommand.cs`

**Update tests:**

- `test/Core.Test/Billing/Organizations/Commands/UpdateOrganizationLicenseCommandTests.cs` - add the new property to
  `UpdateLicenseAsync_WithClaimsPrincipal_ExtractsAllPropertiesFromClaims` test

> **Tip:** Running tests in `UpdateOrganizationLicenseCommandTests.cs` will help identify any missing changes.
> Test failures will guide you to all areas that need updates.

### 9. Implement business logic checks

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

As explained above, organization abilities work alongside feature flags — they don't replace them.
For new features, you'll typically want both:

```csharp
// Check feature flag first (controls rollout)
if (!_featureService.IsEnabled(FeatureFlagKeys.MyFeature))
{
    throw new BadRequestException("This feature is not available.");
}

// Then check organization ability (controls plan-based access)
if (!orgAbility.UseMyFeature)
{
    throw new BadRequestException("Your organization's plan does not support this feature.");
}
```

## Existing abilities

For reference, here are some current organization ability flags (not a complete list):

| Ability                  | Description                   | Typical Plans     |
|--------------------------|-------------------------------|-------------------|
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
