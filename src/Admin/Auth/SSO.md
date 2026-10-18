# Admin Portal SSO sign-in

How the Admin Portal admits users signing in through upstream OIDC. Audience: engineers modifying
`LoginController`, `SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`, or `AdminSettings:Oidc:*`;
operators provisioning Admin Portal access through an IdP.

## Sign-in paths

The Admin Portal supports two sign-in paths. An operator can enable either or both.

| Path | Enabled by |
|---|---|
| Passwordless email | `AdminSettings:EnablePasswordlessLogin = true` (default) |
| Upstream OIDC SSO | `AdminSettings:Oidc:*` configured |

Everything below describes the SSO path.

## Deployment-driven tenancy and authorization

The SSO sign-in gate and the authorization model are determined by deployment type. Operators do
not choose between them.

| | Cloud | Self-hosted |
|---|---|---|
| **Tenancy gate** (admits sign-in) | Principal has at least one `ClaimTypes.Role` claim | Authenticated email is on `AdminSettings:Admins` |
| **Authorization** (what the admitted user can do) | Union of permissions across role claims, via `RolePermissionMapping` | Full access (`AccessControlService.UserHasPermission` short-circuits on `_globalSettings.SelfHosted`) |
| **Role claims come from** | `AdminSettings:Oidc:GroupPrefix` → `GroupClaimRoleMapper` maps IdP `groups` claim to roles | n/a — role claims are irrelevant on self-hosted |
| **Boot-time required config** | `AdminSettings:Oidc:GroupPrefix` must be set | `AdminSettings:Admins` must be set |

`AddAdminUpstreamOidc` fails loud at boot when the required config for the deployment type is
missing.

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
   - Applies the deployment-specific tenancy gate (see table above).
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
  deployment-driven gate in `LoginController.SsoSignIn`.
- **Authorization** — what capabilities the authenticated member has. On cloud, enforced by
  `AccessControlService.UserHasPermission` against the role claims on the principal using
  `RolePermissionMapping`. On self-hosted, the same method short-circuits to `true` on
  `_globalSettings.SelfHosted`.

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

Required settings depend on deployment.

### Cloud

```jsonc
"AdminSettings": {
  "Oidc": {
    "Authority":   "https://idp.example.com",
    "ClientId":    "admin-portal",
    "ClientSecret": "...",
    "GroupPrefix": "us-prd-cloud-adminportal-"
  }
}
```

IdP groups must be named `<GroupPrefix><role>` where `<role>` is one of `owner`, `admin`, `billing`,
`cs`, or `sales` (the keys of `RolePermissionMapping.RolePermissions`). Case is insensitive. A
principal in multiple matching groups receives the union of their permissions.

### Self-hosted

```jsonc
"AdminSettings": {
  "Admins": "admin@example.com,another@example.com",
  "Oidc": {
    "Authority":   "https://idp.example.com",
    "ClientId":    "admin-portal",
    "ClientSecret": "..."
  }
}
```

`AdminSettings:Admins` is a comma-separated list where each entry is either `email` or
`email:stamp`. The SSO sign-in gate matches on the email prefix of each entry, case-insensitive.
`AdminSettings:Oidc:GroupPrefix` is not required on self-hosted; role claims are not consulted.
