# Bitwarden Schema: Tables

The domain's core entities, grouped by the security and functional domain they serve. Use the Purpose column to pick the right starting table and confirm it means what you assume — several names diverge from their generic namesakes (`Send` is a time-boxed share, `Collection` `Type` splits shared vs. "My Items", `Event` is a constraint-free audit log). A table's domain also signals the mandatory filters a valid query needs — active ciphers, confirmed members, enabled orgs (see the grounding rules in SKILL.md) — so the wrong entity or a missing filter yields SQL that runs cleanly but answers the wrong question.

## Vault

| Table              | Purpose                              |
| ------------------ | ------------------------------------ |
| `Cipher`           | Vault items                          |
| `Folder`           | Personal folders (`UserId NOT NULL`) |
| `CollectionCipher` | Junction: ciphers ↔ collections      |
| `Send`             | Temporary secure shares              |

## Collections & Access Control

| Table             | Purpose                                                                              |
| ----------------- | ------------------------------------------------------------------------------------ |
| `Collection`      | Org-scoped grouping (`Type=0` shared, `Type=1` `DefaultUserCollection` / "My Items") |
| `CollectionUser`  | Direct user-collection access                                                        |
| `CollectionGroup` | Group-collection access                                                              |

## Organization Management

| Table                                  | Purpose                                      |
| -------------------------------------- | -------------------------------------------- |
| `Organization`                         | Container                                    |
| `OrganizationUser`                     | Membership                                   |
| `Group`                                | User groups for bulk permissions             |
| `GroupUser`                            | Junction: `OrganizationUser` ↔ `Group`       |
| `Policy`                               | Org-wide enforcement (`Type` = `PolicyType`) |
| `OrganizationDomain`                   | Verified email domains                       |
| `OrganizationSponsorship`              | Families sponsorship (dual FK)               |
| `OrganizationInviteLink`               | Per-org invite link                          |
| `OrganizationIntegration`              | External integration providers               |
| `OrganizationIntegrationConfiguration` | Per-integration config                       |
| `OrganizationConnection`               | External service connections                 |
| `OrganizationApiKey`                   | Org-level API keys                           |
| `OrganizationInstallation`             | Org ↔ server installation                    |
| `PasswordHealthReportApplication`      | Password health tracking per org             |

## Identity & Authentication

| Table                  | Purpose                                       |
| ---------------------- | --------------------------------------------- |
| `User`                 | Central user                                  |
| `Device`               | Registered client devices                     |
| `AuthRequest`          | Passwordless login requests                   |
| `WebAuthnCredential`   | FIDO2/passkey credentials                     |
| `UserSignatureKeyPair` | Signature keypair (account encryption v2)     |
| `SsoConfig`            | SSO config per org (SAML/OIDC in `Data` JSON) |
| `SsoUser`              | External SSO identity → internal User         |
| `EmergencyAccess`      | Trusted emergency access delegates            |

## Notifications

| Table                | Purpose                       |
| -------------------- | ----------------------------- |
| `Notification`       | Push notifications            |
| `NotificationStatus` | Per-user read/dismissed state |

## Secrets Manager

| Table            | Purpose                                   |
| ---------------- | ----------------------------------------- |
| `Project`        | SM project/workspace (FK to Org)          |
| `Secret`         | Individual encrypted secret (FK to Org)   |
| `SecretVersion`  | Version history for a secret              |
| `ServiceAccount` | Non-human account for programmatic access |
| `ApiKey`         | API keys for service accounts             |
| `ProjectSecret`  | Junction: projects ↔ secrets              |
| `AccessPolicy`   | Fine-grained SM access control            |

## Audit, Billing & Infrastructure

| Table                  | Purpose                              |
| ---------------------- | ------------------------------------ |
| `Event`                | Audit log (no FK constraints)        |
| `Transaction`          | Financial transactions               |
| `TaxRate`              | Tax rate lookup                      |
| `SubscriptionDiscount` | Stripe coupon mirror                 |
| `Installation`         | Self-host installation registrations |
