# Seeder Regression Testing

How to prove a Seeder change is behavior-preserving. Written for Claude Code working with a developer: **Claude drives the CLI, the SeederApi, and the SQL checks; the developer smoke-tests the web vault** against the credentials Claude reports back.

The developer and Claude Code run every check against a live local SQL Server database: Claude seeds it with the Seeder and queries it with the `exploring-bitwarden-data` skill. Use this whenever a change touches `Factories/`, `Steps/`, `Scenes/`, or `Recipes/`.

## Preflight

Compose services (`dev/docker-compose.yml`) — container names vary by compose project.

| Service                       | Needed for                                                             |
| ----------------------------- | ---------------------------------------------------------------------- |
| `mssql`                       | Everything                                                             |
| `storage` (Azurite)           | Attachment presets                                                     |
| `idp` (`--profile idp`)       | `features.local-sso` — cert is fetched from live metadata              |
| SeederApi on `:5047`          | Scene tests. Basic auth from `seederSettings:accounts` in user-secrets |
| SeederApi-SelfHost on `:5048` | Self-hosted scenes. `--launch-profile SeederApi-SelfHost`              |

Attachment and IdP steps throw rather than degrade, so a green seed means they worked.

If MSSQL runs **outside** compose, give it the `mssql` network alias or the Service Bus emulator never boots — and then _successful_ logins return HTTP 500, because the login event can't publish. The alias does not survive recreating the container.

**Run the CLI from `util/SeederUtility`.** `GlobalSettingsFactory` reads `Directory.GetCurrentDirectory()`, and that directory's `appsettings.Development.json` holds the storage connection strings. There is no `launchSettings.json`, so set the environment explicitly. Always `--mangle` so runs stay additive.

```bash
cd util/SeederUtility
ASPNETCORE_ENVIRONMENT=Development dotnet run -- preset --name <name> --mangle
```

## Changed code → what to seed → what to assert

| If you changed…                   | Seed                                                                         | Assert                                                                                                                                                                             |
| --------------------------------- | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UserSeeder` premium/billing      | `individual.premium`                                                         | `Premium=1`, expiry ≈ +365d, `MaxStorageGb` set                                                                                                                                    |
| `GenerateSelfHostUserLicenseStep` license write | `individual --self-hosted --subscription premium` (CLI) with a trusted licensing cert configured | `{LicenseDirectory}/user/{userId}.json` exists and its `token` is an RS256 JWT with issuer `bitwarden`, audience `user:{userId}`. With no trusted cert, no file is written and a warning is logged |
| `SingleUserScene` license write | `POST :5048/seed` with `SingleUserScene`, `selfHosted: true`, `premium: true` | Response `result.premiumLicenseWritten` is `true` and `{LicenseDirectory}/user/{userId}.json` exists. With no trusted cert, `premiumLicenseWritten` is `false` and `premiumLicenseWarning` explains why. The same request to `:5047` (cloud mode) returns HTTP 400 and persists no `User` row |
| `OrganizationStripeBillingScene` | `POST :5047/seed` `SingleUserScene` → `SingleOrganizationScene` (`planType` Teams) → `OrganizationStripeBillingScene` with the org ID | Response and `Organization` row carry `cus_`/`sub_`, `ExpirationDate` ≈ now + trial days. Repeating the billing POST returns HTTP 400 and the row is unchanged |
| `UserStripeBillingScene` | `POST :5047/seed` `SingleUserScene` → `UserStripeBillingScene` with the user ID | `User` row: `Premium=1`, `Gateway=0`, `cus_`/`sub_`, `PremiumExpirationDate` ≈ now + 1 year, `MaxStorageGb` = the premium plan's storage, `LEN(LicenseKey)=20`, `RevisionDate` moved. `address: "de-vat"` returns HTTP 400 and the row is unchanged |
| `UserSeeder` other fields         | `individual.free`                                                            | `Culture='en-US'`, `Premium=0`, expiry NULL                                                                                                                                        |
| Password plumbing                 | any preset `--password X`                                                    | **Developer logs in with X.** SQL can't help — key derivation and stored hash are both opaque; only a login proves they agree                                                      |
| `BulkCommitter` / `UserBulkRow` `--no-data-protection` | `scale.xs-central-perk`, with and without the flag                           | `LEFT([Key],2)` and `LEFT(MasterPassword,2)` are `P\|` for every seeded user without the flag and never `P\|` with it. **Developer logs in** as the flagged run's owner on the server that reads the database |
| `CreateRosterStep`                | `qa.dunder-mifflin-enterprise-full`                                          | `User.Name` is "First Last", never the mangled email local part                                                                                                                    |
| `CreateRosterStep` email override | `dev.playground`                                                             | The four role logins land verbatim (`owner@bw.example`, …), the other eight still derive `firstName.lastName@domain`; re-seed with `--owner-email X` and X wins for the owner only |
| `LoginCipherSeeder` URI checksum  | `dev.playground`                                                             | Zero `Type=1` rows match `Data LIKE '%"Uris":%' AND Data NOT LIKE '%"UriChecksum":"2.%'`; the keyed logins (`[Key] IS NOT NULL`: Figma, PgAdmin, AWS Console) keep their URIs. **Developer opens** those three in the web vault and sees their URIs |
| `OrganizationSeeder` keys         | any org preset                                                               | `PublicKey` starts `MIIBIjANBg`, `PrivateKey` starts `2.`                                                                                                                          |
| `OrganizationSeeder` plans        | `qa.stark-free-basic`, `qa.paper-trail-partners-team`, `qa.enterprise-basic`, `qa.families-basic` | `Plan`, `PlanType`, `Seats`, feature flags per tier                                                                                                                                |
| Plan overrides                    | `SingleOrganizationScene` + `overrides`                                      | An override wins over the plan default — proves overrides still apply after `PlanFeatures.Apply`                                                                                   |
| `OrganizationSeeder` PAM seat     | `SingleOrganizationScene` + `overrides: { usePam: true }`                    | Every seeded `OrganizationUser` has `AccessPam=1`; with no override, `AccessPam=0`. Members seed unlicensed otherwise and `PamLicenseGuard` refuses submit/activate/extend          |
| `ProviderSeeder`                  | `SingleProviderScene` (API only)                                             | `Gateway=0` when the caller supplies none                                                                                                                                          |
| `SingleProviderScene` managed clients / `ProviderPlanSeeder` | `SingleProviderSceneTests` (API only) | Each org linked to a Billable MSP or Business Unit provider has `Status=2` (Managed), the managed `PlanType` (TeamsMonthly/EnterpriseMonthly for MSP; the provider's Enterprise plan type for Business Unit) and its plan flags, the provider's `BillingEmail`, NULL `GatewaySubscriptionId`, and its original `GatewayCustomerId` and Secrets Manager settings. Each `ProviderPlan` has `AllocatedSeats` = the summed `Seats` of its linked clients and `PurchasedSeats` = `MAX(0, AllocatedSeats - SeatMinimum)`. A Pending MSP or Business Unit provider still links its orgs but leaves them unchanged, and every `ProviderPlan` has `AllocatedSeats=0`. A linked org with no matching provider plan returns HTTP 400 and persists no provider rows; so does a malformed `OrganizationKeyB64` on any link, and so does a type/status pair production cannot create (Reseller not `Created`, MSP or Business Unit `Created`), or a Pending provider with a `GatewayCustomerId` or `GatewaySubscriptionId`. The `Status` request field sets `Provider.Status` (Created for Reseller and Billable otherwise when omitted) |
| `ProviderUserScene`               | `SingleProviderSceneTests` (API only)                                        | The `ProviderUser` has the requested `Type` and `Status`; `UserId` is NULL only when Invited, and `Key` is a `4.` (RSA) EncString only when Confirmed                                     |
| SM `Organization*Scene`s          | `SecretsManagerSceneTests` (API only)                                        | Project/Secret/ServiceAccount rows persist via the real commercial EF repos (not Noop); encrypted name/key/value/note round-trip under the org key; access policies link grantee↔grantable; a grant whose grantee or grantable belongs to another org returns HTTP 400 and persists no access-policy rows |
| `OrganizationAccessTokenScene` / `AccessTokenSeeder` | `SecretsManagerSceneTests` (API only)                    | An `ApiKey` persists for the service account with `Scope=["api.secrets"]`; the returned token matches `0.{apiKeyId}.{clientSecret}:{encryptionKeyB64}`; `EncryptedPayload` decrypts under the token-derived key to the org key; encrypted `Name` round-trips under the org key; a service account not in the org returns HTTP 400 and persists no `ApiKey` rows |
| `SsoConfigSeeder`                 | `features.local-sso`                                                         | One `SsoConfig` row, `ConfigType=2`, non-empty `idpX509PublicCert`                                                                                                                 |
| SSO provider guard                | `features.sso-enterprise`                                                    | **Zero** `SsoConfig` rows — OIDC is skipped by design                                                                                                                              |
| Attachment steps                  | `individual.encryption-modes`                                                | `Cipher.Attachments` populated; every ID resolves to a blob                                                                                                                        |
| Cipher generation / density       | `scale.xs-central-perk` (fastest), plus one with personal ciphers            | Q1–Q11 in [verification.md](verification.md)                                                                                                                                       |
| `CipherComposer` / `Distribution.Select` totals | `scale.xs-central-perk`, plus one with personal ciphers                      | No `ArgumentOutOfRangeException` from `Distribution.Select` in any step. Passwords are encrypted, so SQL cannot show the strength mix; `GeneratedLoginPasswordStrengthTests` proves that both generating steps give their logins all five strengths when logins lead the type mix. Login content (`Type=1`): ~32% match `Data LIKE '%"Uris":[[]{%'`, ~1% have two (`JSON_VALUE([Data], '$.Uris[1].Uri') IS NOT NULL`), ~37% `'%"Notes":"2.%'`, ~6% `'%"Fields":[[]{%'`, ~2% `'%"Totp":"2.%'`, ~1% `'%"Fido2Credentials":[[]{%'`, ~6% `'%"PasswordHistory":[[]{%'`; zero rows with `JSON_VALUE([Data], '$.Uri') IS NOT NULL` (no legacy top-level `Uri`) |
| Stripe billing path               | `organization --plan-type teams-monthly --stripe-billing`, then the same command **without** the flag | With the flag: `Gateway=0`, `cus_`/`sub_` prefixes, `Enabled=1`, `ExpirationDate` ≈ now + trial days. Without it: all four NULL/unchanged — the opt-in default is zero Stripe calls |
| User Stripe billing path | `individual --subscription premium --stripe-billing --mangle`, then the same command **without** the flag | With the flag: `Premium=1`, `Gateway=0`, `cus_`/`sub_` prefixes, `PremiumExpirationDate` ≈ now + 1 year, `MaxStorageGb` = the premium plan's provided storage (5 in billing-pricing today), `LEN(LicenseKey)=20`. Without it: gateway columns and `LicenseKey` NULL, `MaxStorageGb=1` — the opt-in default is zero Stripe calls |
| Stripe test card / address catalogs | `organization -n "Billing VAT" -u 1 -d billingvat.example --plan-type teams-monthly --stripe-billing --stripe-card mastercard --stripe-address de-vat --mangle` | Exit 0, `cus_`/`sub_` set — an uninferable tax ID would have thrown before Stripe. **Developer checks the web vault:** org billing shows Mastercard •••• 4444 and the DE address with VAT ID `DE123456789`. `individual … --stripe-address de-vat` exits 1 with no new `User` row |
| Stripe failing card | `individual --subscription premium --stripe-billing --stripe-card fails-on-charge --mangle` | Exit 0, `Premium=1`, `cus_`/`sub_` set. **Developer checks the web vault:** the subscription badge reads "Update payment" (Incomplete) |
| `BulkCommitter` bulk copy         | `scale.xl-highperm-weyland-yutani` against a remote SQL Server               | Seeds without `Execution Timeout Expired`. The 30 s `SqlBulkCopy` default only bites on a large insert over a slow link, so a local run proves no regression, not the fix          |

Only three presets set `density.personalCiphers`, so only they exercise `GeneratePersonalCiphersStep` at volume and only they have Q10 expected values: `scale.md-balanced-sterling-cooper`, `scale.lg-balanced-wayne-enterprises`, `scale.xl-highperm-weyland-yutani`. Sterling Cooper is the cheapest of the three.

Attachments only exist in the `enterprise-basic` and `encryption-modes` cipher fixtures — a preset reaches them only via those.

## Surfaces with no CLI or preset path

| Surface                                                             | Reached only by                                                                                    |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `SingleUserScene`, `SingleOrganizationScene`, `SingleProviderScene`, `ProviderUserScene` | `POST :5047/seed`                                                                                  |
| `OrganizationProjectScene`, `OrganizationSecretScene`, `OrganizationServiceAccountScene`, `OrganizationAccessPolicyScene`, `OrganizationAccessTokenScene` | `POST :5047/seed` (requires the commercial SM EF repos registered in `SeederApi/Startup.cs`)      |
| `OrganizationWithUsersRecipe`                                       | `test/Api.IntegrationTest/**/*PerformanceTests.cs`, all `[Theory(Skip = "…")]` — compile-time only |

## Checks the density queries don't cover

[verification.md](verification.md) has Q1–Q11 for distributions. These cover entity fields, run through the `exploring-bitwarden-data` skill.

```sql
-- Roster display names: LooksLikeEmail must be 0
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN U.[Name] LIKE '%+%' OR U.[Name] LIKE '%@%' THEN 1 ELSE 0 END) AS LooksLikeEmail
FROM [dbo].[OrganizationUser] OU WITH (NOLOCK)
JOIN [dbo].[User] U WITH (NOLOCK) ON U.Id = OU.UserId
WHERE OU.OrganizationId = @OrgId;

-- Key column placement: a swap is silent, nothing reads PublicKey back
SELECT LEFT(PublicKey,10) AS PubHead, LEFT(PrivateKey,10) AS PrivHead
FROM [dbo].[Organization] WITH (NOLOCK) WHERE Id = @OrgId;

-- Attachment scheme invariant: a v2 attachment requires a cipher-key host
SELECT SUM(CASE WHEN Attachments IS NOT NULL THEN 1 ELSE 0 END) AS WithAttachments,
       SUM(CASE WHEN Attachments IS NOT NULL AND [Key] IS NOT NULL THEN 1 ELSE 0 END) AS CipherKeyHosts
FROM [dbo].[Cipher] WITH (NOLOCK) WHERE UserId = @UserId;
```

Metadata and blobs are stored separately — confirm the blob too, against the Azurite container:

```bash
docker exec <storage-container> grep -c "<attachmentId>" /data/__azurite_db_blob__.json
```

## SSO login wiring

The seeder prints these after `features.local-sso`. The org GUID is new every seed, so `dev/.env` always needs updating.

1. `dev/.env` → `IDP_SP_ENTITY_ID` and `IDP_SP_ACS_URL` to `http://localhost:51822/saml2/<orgId>` (+ `/Acs`)
2. `dev/authsources.php` → an entry whose `email` matches the seeded member exactly, mangling included. Live-mounted; no IdP restart for this file alone.
3. `cd dev && docker compose --profile idp up -d`

No SSO API restart for a freshly seeded org — `DynamicAuthenticationSchemeProvider` caches per org GUID and resolves new ones lazily from the database.

## Known non-regressions

Do not chase these.

| Observation                                       | Cause                                                                                                                                                           |
| ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A cipher exceeds `maxCollectionsPerCipher` by one | `EnsureCollectionAssignment` grants the owner a collection on the first archived and first deleted-only cipher. ≤2 ciphers per org; `verification.md` omits it. |
| Subscription page hangs for a seeded org          | Expected without `--stripe-billing`: a default seed sets no gateway IDs, and the scenes link fake ones. Pass `--stripe-billing` for a real subscription.         |
| Premium accounts have no gateway columns          | Expected without `--stripe-billing`. `individual --subscription premium --stripe-billing` sets real ones; `SingleUserScene` accepts caller-supplied ones. |
| Cipher counts drift from `verification.md`        | The generator seed derives from the domain, which `--mangle` changes per run. Rates and clamps still hold.                                                      |
| `UseSecretsManager=1` but `SmSeats` NULL          | Teams and Enterprise plans set the flag; `EnableSecretsManager` provisions the seats.                                                                           |
| OIDC preset identifier is the mangled domain      | `CreateSsoConfigStep` returns before assigning `Organization.Identifier`.                                                                                       |

## Reporting back

Per seed, give the developer: org name and ID, owner and member emails, **master password**, and the SSO identifier where relevant. Separate what SQL confirmed from what still needs a human at the UI.

Browse at whatever host `globalSettings:baseServiceUri:vault` is set to — Identity derives the allowed `redirect_uri` from it, so reaching the vault by any other hostname fails SSO with `Invalid redirect_uri`. The plain dev default is `https://localhost:8080`; the Aspire AppHost uses `https://bitwarden.test:8080` and needs a matching `/etc/hosts` entry.
