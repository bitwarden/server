# Admin Portal SSO sign-in

How the Admin Portal admits users signing in through upstream OIDC. Audience: engineers modifying
`LoginController`, `SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`, or `AdminSettings:Oidc:*`;
operators provisioning Admin Portal access through an IdP.

## Sign-in paths

The Admin Portal supports two sign-in paths. An operator can enable either or both.

| Path | Enabled by | Who signs in | Who gets admin capability |
|---|---|---|---|
| Passwordless email | `AdminSettings:EnablePasswordlessLogin = true` (default) | Emails on the `AdminSettings:Admins` CSV allowlist | On self-hosted: everyone signed in. On cloud: users whose email appears in `adminSettings:role:{roleName}` CSV sub-config. |
| Upstream OIDC SSO | `AdminSettings:Oidc:*` configured | IdP-authenticated users whose `groups` claim maps to at least one Admin Portal role via `AdminSettings:Oidc:GroupPrefix` | On self-hosted: everyone signed in. On cloud: the union of permissions across every mapped role. |

Everything below describes the SSO path.

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
   - Applies the tenancy gate described below.
   - Signs the principal into `AdminAuthenticationSchemes.SsoCookie` with
     `IsPersistent = false` and the `id_token` stored on the auth properties (used later as
     `id_token_hint` on RP-initiated logout).
   - Signs out of `IdentityConstants.ExternalScheme`.

Any failure in steps 1-3 logs a warning and redirects to `GET /login?error=5`, which renders the
generic "SSO sign-in failed. Try again or use the email link." message. The error message is
intentionally generic: unsuccessful attempts cannot distinguish between "no such user", "wrong
group", and "IdP returned an error."

## Tenancy gate

After `SsoSignInPrincipalBuilder.Build(...)` returns, `LoginController.SsoSignIn` requires the built
principal to carry at least one `ClaimTypes.Role` claim. A principal with zero role claims is
rejected: the controller logs a warning, signs out of `IdentityConstants.ExternalScheme`, and
redirects to `GET /login?error=5`.

Pinned by `LoginControllerTests.SsoSignIn_RedirectsError5_WhenPrincipalHasNoRoleClaims`.

## Authentication, tenancy, and authorization

The SSO path keeps three concerns distinct:

- **Authentication** — the IdP proves the user owns the claimed email. Enforced by the OIDC handler
  plus the email-verified check in `LoginController.SsoSignIn`.
- **Tenancy** — whether the authenticated user is a user of the Admin Portal. Enforced by the
  role-presence check in `LoginController.SsoSignIn`.
- **Authorization** — what capabilities the authenticated member has. Enforced by
  `AccessControlService.UserHasPermission` against the role claims on the principal, using
  `RolePermissionMapping`.

The role mapping serves both tenancy and authorization: "has any mapped role" is the tenancy
signal; "which permissions those roles grant" is the authorization decision. Same data source, two
questions.

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

Minimum settings for SSO:

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

`AddAdminUpstreamOidc` fails loud at boot if `GroupPrefix` is empty while OIDC is enabled.

IdP groups are expected to be named `<GroupPrefix><role>` where `<role>` is one of `owner`, `admin`,
`billing`, `cs`, or `sales` (the keys of `RolePermissionMapping.RolePermissions`). Case is
insensitive. A principal in multiple matching groups receives the union of their permissions.
