# Seeder Regression Testing

How to prove a Seeder change is behavior-preserving. Written for Claude Code working with a developer: **Claude drives the CLI, the SeederApi, and the SQL checks; the developer smoke-tests the web vault** against the credentials Claude reports back.

Unit tests cover none of the CLI, the API, or a real database. Use this whenever a change touches `Factories/`, `Steps/`, `Scenes/`, or `Recipes/`.

## Preflight

Compose services (`dev/docker-compose.yml`) — container names vary by compose project.

| Service                 | Needed for                                                             |
| ----------------------- | ---------------------------------------------------------------------- |
| `mssql`                 | Everything                                                             |
| `storage` (Azurite)     | Attachment presets                                                     |
| `idp` (`--profile idp`) | `features.local-sso` — cert is fetched from live metadata              |
| SeederApi on `:5047`    | Scene tests. Basic auth from `seederSettings:accounts` in user-secrets |

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
| `SingleUserScene` license write | `POST :5047/seed` with `SingleUserScene`, `selfHosted: true`, `premium: true` | Response `result.premiumLicenseWritten` is `true` and `{LicenseDirectory}/user/{userId}.json` exists. With no trusted cert, `premiumLicenseWritten` is `false` and `premiumLicenseWarning` explains why |
| `UserSeeder` other fields         | `individual.free`                                                            | `Culture='en-US'`, `Premium=0`, expiry NULL                                                                                                                                        |
| Password plumbing                 | any preset `--password X`                                                    | **Developer logs in with X.** SQL can't help — key derivation and stored hash are both opaque; only a login proves they agree                                                      |
| `CreateRosterStep`                | `qa.dunder-mifflin-enterprise-full`                                          | `User.Name` is "First Last", never the mangled email local part                                                                                                                    |
| `CreateRosterStep` email override | `dev.playground`                                                             | The four role logins land verbatim (`owner@bw.example`, …), the other eight still derive `firstName.lastName@domain`; re-seed with `--owner-email X` and X wins for the owner only |
| `OrganizationSeeder` keys         | any org preset                                                               | `PublicKey` starts `MIIBIjANBg`, `PrivateKey` starts `2.`                                                                                                                          |
| `OrganizationSeeder` plans        | `qa.stark-free-basic`, `qa.paper-trail-partners-team`, `qa.enterprise-basic` | `Plan`, `PlanType`, `Seats`, feature flags per tier                                                                                                                                |
| Plan overrides                    | `SingleOrganizationScene` + `overrides`                                      | An override wins over the plan default — proves overrides still apply after `PlanFeatures.Apply`                                                                                   |
| `OrganizationSeeder` PAM seat     | `SingleOrganizationScene` + `overrides: { usePam: true }`                    | Every seeded `OrganizationUser` has `AccessPam=1`; with no override, `AccessPam=0`. Members seed unlicensed otherwise and `PamLicenseGuard` refuses submit/activate/extend          |
| `ProviderSeeder`                  | `SingleProviderScene` (API only)                                             | `Gateway=0` when the caller supplies none                                                                                                                                          |
| `SsoConfigSeeder`                 | `features.local-sso`                                                         | One `SsoConfig` row, `ConfigType=2`, non-empty `idpX509PublicCert`                                                                                                                 |
| SSO provider guard                | `features.sso-enterprise`                                                    | **Zero** `SsoConfig` rows — OIDC is skipped by design                                                                                                                              |
| Attachment steps                  | `individual.encryption-modes`                                                | `Cipher.Attachments` populated; every ID resolves to a blob                                                                                                                        |
| Cipher generation / density       | `scale.xs-central-perk` (fastest), plus one with personal ciphers            | Q1–Q11 in [verification.md](verification.md)                                                                                                                                       |

Only three presets set `density.personalCiphers`, so only they exercise `GeneratePersonalCiphersStep` at volume and only they have Q10 expected values: `scale.md-balanced-sterling-cooper`, `scale.lg-balanced-wayne-enterprises`, `scale.xl-highperm-weyland-yutani`. Sterling Cooper is the cheapest of the three.

Attachments only exist in the `enterprise-basic` and `encryption-modes` cipher fixtures — a preset reaches them only via those.

## Surfaces with no CLI or preset path

| Surface                                                             | Reached only by                                                                                    |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `SingleUserScene`, `SingleOrganizationScene`, `SingleProviderScene` | `POST :5047/seed`                                                                                  |
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
| Subscription page hangs for a seeded org          | Seeded gateway IDs are fake — the scenes link identifiers without calling Stripe.                                                                               |
| Premium accounts have no gateway columns          | Only `SingleUserScene` accepts them; CLI presets never set billing.                                                                                             |
| Cipher counts drift from `verification.md`        | The generator seed derives from the domain, which `--mangle` changes per run. Rates and clamps still hold.                                                      |
| `UseSecretsManager=1` but `SmSeats` NULL          | Teams and Enterprise plans set the flag; `EnableSecretsManager` provisions the seats.                                                                           |
| OIDC preset identifier is the mangled domain      | `CreateSsoConfigStep` returns before assigning `Organization.Identifier`.                                                                                       |

## Reporting back

Per seed, give the developer: org name and ID, owner and member emails, **master password**, and the SSO identifier where relevant. Separate what SQL confirmed from what still needs a human at the UI.

Browse at whatever host `globalSettings:baseServiceUri:vault` is set to — Identity derives the allowed `redirect_uri` from it, so reaching the vault by any other hostname fails SSO with `Invalid redirect_uri`. The plain dev default is `https://localhost:8080`; the Aspire AppHost uses `https://bitwarden.test:8080` and needs a matching `/etc/hosts` entry.
