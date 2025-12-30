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
if (organization.UseEvents)
{
    // allow UseEvents feature...
}
```

## Why This Pattern Matters

Using explicit ability flags instead of plan type checks provides several benefits:

1. **Simplicity** — A single boolean check is cleaner and less error-prone than maintaining lists of plan types.

2. **Centralized Control** — Feature access is managed in one place: the ability assignment during organization
   creation/upgrade. No need to hunt through the codebase for scattered plan type checks.

3. **Flexibility** — Abilities can be set independently of plan type, enabling:

    - Early access programs for features not yet tied to a plan
    - Trial access to help customers evaluate a feature before upgrading
    - Custom arrangements for specific customers
    - A/B testing of features across different cohorts

4. **Safe Refactoring** — When plans change (e.g., adding a new plan tier, renaming plans, or moving features between
   tiers), we only update the ability assignment logic—not every place the feature is used.

5. **Graceful Downgrades** — When an organization downgrades, we update their abilities. All feature checks
   automatically respect the new access level.

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

### Modifying Abilities for Existing Organizations

To change abilities for existing organizations (e.g., rolling out a feature to a new plan tier), create a database
migration that updates the relevant flag:

```sql
-- Example: Enable UseEvents for all Teams organizations
UPDATE [dbo].[Organization]
SET UseEvents = 1
WHERE PlanType IN (17, 18) -- TeamsMonthly = 17, TeamsAnnually = 18
```

Then update the plan-to-ability assignment code so new organizations get the correct value.

## Adding a New Ability

When developing a new plan-gated feature:

1. **Add the ability to the Organization and OrganizationAbility entities** — Create a `Use[FeatureName]` boolean
   property.

2. **Add a database migration** — Add the new column to the Organization table.

3. **Update plan definitions** — Add a corresponding `Has[FeatureName]` property to the Plan model and configure which
   plans include it.

4. **Update organization creation/upgrade logic** — Ensure the ability is set based on the plan.

5. **Update the organization license claims** (if applicable) - to make the feature available on self-hosted instances.

6. **Implement checks throughout client and server** — Use the ability consistently everywhere the feature is accessed.
    - Clients: get the organization object from `OrganizationService`.
    - Server: if you already have the full `Organization` object in scope, you can use it directly. If not, use the
      `IApplicationCacheService` to retrieve the `OrganizationAbility`, which is a simplified, cached representation
      of the organization ability flags. Note that some older flags may be missing from `OrganizationAbility` but
      can be added if needed.

## Existing Abilities

For reference, here are some current organization ability flags (not a complete list):

| Ability                  | Description                   | Plans             |
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
