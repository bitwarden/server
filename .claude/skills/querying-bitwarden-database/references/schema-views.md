# Bitwarden Schema: Views

Views are answers the application already computes — like premium status across personal and org sources (`UserPremiumAccessView`) or every org feature flag in one place (`OrganizationAbilityView`). When a view fits the question, prefer it over hand-rolling the joins: it carries canonical logic — computed flags, revocation state, the `Enabled` gate — that's easy to rebuild subtly wrong, so it's safer and less code. Check the Notes column first, though: a view's filtering can drift (`OrganizationCipherDetailsCollectionsView` no longer excludes disabled orgs), so confirm it still encodes the rule you need.

## Detail / enrichment

| View                                              | Joins                                          | Purpose                                   |
| ------------------------------------------------- | ---------------------------------------------- | ----------------------------------------- |
| `OrganizationUserOrganizationDetailsView`         | OrgUser + Org + SSO + Sponsorship + Revocation | A user's orgs with full context           |
| `OrganizationUserUserDetailsView`                 | OrgUser + User + SSO                           | Org members with user details             |
| `EmergencyAccessDetailsView`                      | EmergencyAccess + User (grantor and grantee)   | Emergency access with names               |
| `UserPremiumAccessView`                           | User + OrgUser + Org (aggregated)              | Premium check (personal OR org)           |
| `NotificationStatusDetailsView`                   | NotificationStatus + Notification              | Notification state with context           |
| `AuthRequestPendingDetailsView`                   | AuthRequest + Device (filtered to pending)     | Pending passwordless login requests       |
| `OrganizationIntegrationConfigurationDetailsView` | IntegrationConfig + Integration                | Integration configs with provider context |

## Ability views

| View                      | Content                                                                                        |
| ------------------------- | ---------------------------------------------------------------------------------------------- |
| `OrganizationAbilityView` | Every `Use*` flag plus computed `Using2fa`. Canonical "what features does org X have?" lookup. |

## Business-logic views

| View                                       | Notes                                                                                                                  |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `OrganizationCipherDetailsCollectionsView` | Org vault items + collection mappings. **No longer filters disabled orgs** — add `Organization.Enabled = 1` if needed. |
| `UserEmailDomainView`                      | Extracts email domain from `User.Email`.                                                                               |
