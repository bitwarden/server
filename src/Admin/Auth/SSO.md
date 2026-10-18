# Admin Portal SSO sign-in

How the Admin Portal admits users signing in through upstream OIDC. Audience: engineers modifying
`LoginController`, `SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`, or `AdminSettings:Oidc:*`;
operators provisioning Admin Portal access through an IdP.

## Sign-in paths

The Admin Portal supports two sign-in paths. An operator can enable either or both.

| Path | Enabled by | Membership gate | Role claims come from |
|---|---|---|---|
| Passwordless email | `AdminSettings:EnablePasswordlessLogin = true` (default) | Email must be on `AdminSettings:Admins` | `adminSettings:role:{roleName}` CSV sub-config, matched against the authenticated email |
| Upstream OIDC SSO | `AdminSettings:Oidc:*` configured | Every configured SSO tenancy gate must admit the user (see below) | `GroupClaimRoleMapper` maps the IdP `groups` claim via `AdminSettings:Oidc:GroupPrefix` |

Everything below describes the SSO path.

## SSO tenancy gates

The SSO path has two independent tenancy signals. Operators configure one or both. Every signal
that is configured must admit the user; signals that are not configured are skipped.

| Signal | Configured when | Admits a user when |
|---|---|---|
| `AdminSettings:Admins` (CSV allowlist) | `AdminSettings:Admins` is non-empty | The authenticated email appears in the CSV (case-insensitive, matches the email prefix of each `email` or `email:stamp` entry) |
| `AdminSettings:Oidc:GroupPrefix` (IdP group mapping) | `AdminSettings:Oidc:GroupPrefix` is non-empty | `SsoSignInPrincipalBuilder` produces a principal with at least one `ClaimTypes.Role` claim (i.e. at least one of the user's IdP groups has the configured prefix and a suffix matching a known role) |

`AddAdminUpstreamOidc` fails loud at boot if SSO is enabled with neither `Admins` nor `GroupPrefix`
configured, because every SSO sign-in would otherwise fail the gates.

### Configuration matrix

The three valid SSO configurations and what they mean for sign-in and authorization:

| `Admins` | `GroupPrefix` | SSO sign-in admits | Role claims on the principal | Cloud authorization | Self-hosted authorization |
|---|---|---|---|---|---|
| set | unset | Users whose email is on `Admins` | None | No permissions (`AccessControlService.UserHasPermission` returns `false`) | Full access (`SelfHosted` short-circuit) |
| unset | set | Users whose IdP groups map to at least one role | The mapped roles | Union of permissions across mapped roles | Full access |
| set | set | Users whose email is on `Admins` AND whose IdP groups map to at least one role | The mapped roles | Union of permissions across mapped roles | Full access |

The `Admins`-only configuration suits operators who already use an IdP for authentication but
prefer to manage the admin list in config rather than in IdP groups. On cloud, those operators
also need to populate `adminSettings:role:{roleName}` entries if they want anyone to have
permissions.

The `GroupPrefix`-only configuration suits operators who manage admin membership through IdP
groups and want a single source of truth.

The combined configuration is the tightest posture — a user must satisfy both the local allowlist
and the IdP group mapping. Appropriate when the two mechanisms are managed by different teams and
both approvals are required.

## SSO sign-in flow

1. The user hits `GET /login/sso` (`LoginController.Sso`). The controller issues a challenge against
   `AdminAuthenticationSchemes.UpstreamOidc` (the OpenID Connect handler registered by
   `AddAdminUpstreamOidc`). The browser is redirected to the IdP's authorize endpoint with
   `response_type=code`, `prompt=login`, `max_age=0`, and PKCE.
2. The IdP authenticates the user and redirects back to the configured callback. The OIDC handler
   validates the ID token (issuer, audience, signature, lifetime, and `auth_time` via
   `OnTokenValidated`) and stages the resulting principal into `IdentityConstants.ExternalScheme`.
3. The browser follows to `GET /login/sso-signin` (`LoginController.SsoSignIn`). The controller:
   - Reads the external principal from `IdentityConstants.ExternalScheme`.
   - Verifies the email claim is present and (unless `RequireEmailVerifiedClaim = false`) the
     `email_verified` claim is `true`.
   - Calls `SsoSignInPrincipalBuilder.Build(email, external.Principal)`, which stamps
     `ClaimTypes.Name` / `ClaimTypes.Email` / `ClaimTypes.NameIdentifier` claims and asks
     `GroupClaimRoleMapper` to translate the IdP's `groups` claim into `ClaimTypes.Role` claims.
   - Applies each configured tenancy gate (see above). Admits only if every configured gate passes.
   - Signs the principal into `AdminAuthenticationSchemes.SsoCookie` with
     `IsPersistent = false` and the `id_token` stored on the auth properties (used later as
     `id_token_hint` on RP-initiated logout).
   - Signs out of `IdentityConstants.ExternalScheme`.

Any failure in steps 1-3 logs a warning and redirects to `GET /login?error=5`, which renders the
generic "SSO sign-in failed. Try again or use the email link." message. The error message is
intentionally generic: unsuccessful attempts cannot distinguish between "no such user", "wrong
group", "email not on allowlist", and "IdP returned an error."

## Authentication, tenancy, and authorization

The SSO path keeps three concerns distinct:

- **Authentication** — the IdP proves the user owns the claimed email. Enforced by the OIDC handler
  plus the email-verified check in `LoginController.SsoSignIn`.
- **Tenancy** — whether the authenticated user is a user of the Admin Portal. Enforced by the
  configured tenancy gates in `LoginController.SsoSignIn`.
- **Authorization** — what capabilities the authenticated member has. Enforced by
  `AccessControlService.UserHasPermission` against the role claims on the principal, using
  `RolePermissionMapping`.

## Cookie schemes

The Admin app registers two cookie schemes. Each sign-in path writes its own.

| Scheme | Set by | SecurityStampValidator |
|---|---|---|
| `IdentityConstants.ApplicationScheme` | Passwordless path (`SignInManager`) | Yes, 30s re-check against the backing `ReadOnlyEnvIdentityUserStore` |
| `AdminAuthenticationSchemes.SsoCookie` | SSO path (`HttpContext.SignInAsync` in `LoginController.SsoSignIn`) | No |

Bare `[Authorize]` works for either path because the authorization `DefaultPolicy` lists both
schemes. ASP.NET Core's `PolicyEvaluator` calls `AuthenticateAsync` on each scheme and sets
`HttpContext.User` to the first successful result. `DefaultAuthenticateScheme` and
`DefaultChallengeScheme` are Identity's defaults, so bare `[Authorize]` redirects to `/login` and
`SignInManager` writes to the Identity cookie.

`FallbackPolicy` is unset, so endpoints without `[Authorize]` stay anonymous. This keeps the login
pages, `/alive`, `/error`, and the version endpoint reachable without a cookie.

## Logout

`LoginController.Logout` reads `id_token` from `AdminAuthenticationSchemes.SsoCookie` before signing
out, then signs out of both cookie schemes. If `id_token` was present and `OidcEnabled` is true, it
triggers RP-initiated logout against `AdminAuthenticationSchemes.UpstreamOidc` with the saved token
as `id_token_hint`, so the upstream IdP can honor the app-level Allowed Logout URLs list.

## Operator configuration

Minimum settings for SSO. Supply at least one of `AdminSettings:Admins` or
`AdminSettings:Oidc:GroupPrefix`.

```jsonc
"AdminSettings": {
  // Required when choosing the CSV allowlist as a tenancy signal. Comma-separated email
  // addresses. Entries may be "email" or "email:stamp".
  "Admins": "admin@example.com,another@example.com",

  "Oidc": {
    "Authority":   "https://idp.example.com",
    "ClientId":    "admin-portal",
    "ClientSecret": "...",

    // Required when choosing IdP group mapping as a tenancy signal. IdP groups must be named
    // <GroupPrefix><role> where <role> is one of owner, admin, billing, cs, or sales (the keys
    // of RolePermissionMapping.RolePermissions). Case is insensitive. A principal in multiple
    // matching groups receives the union of their permissions.
    "GroupPrefix": "us-prd-cloud-adminportal-"
  }
}
```
