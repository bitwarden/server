# Admin Portal SSO sign-in

How the Admin Portal admits users signing in through upstream OIDC, and how it keeps authentication,
tenancy, and authorization distinct. Audience: engineers modifying `LoginController`,
`SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`, or `AdminSettings:Oidc:*`; operators
provisioning Admin Portal access through an IdP.

## Sign-in paths

The Admin Portal supports two sign-in paths. An operator can enable either or both.

| Path | Enabled by | Who signs in | Who gets admin capability |
|---|---|---|---|
| Passwordless email | `AdminSettings:EnablePasswordlessLogin = true` (default) | Emails on the `AdminSettings:Admins` CSV allowlist | On self-hosted: everyone signed in (`SelfHosted` short-circuit). On cloud: users whose email appears in `adminSettings:role:{roleName}` CSV sub-config. |
| Upstream OIDC SSO | `AdminSettings:Oidc:*` configured | IdP-authenticated users whose `groups` claim maps to at least one Admin Portal role via `AdminSettings:Oidc:GroupPrefix` | On self-hosted: everyone signed in (`SelfHosted` short-circuit). On cloud: the union of permissions across every mapped role. |

The passwordless path is unchanged by this work. Everything below describes the SSO path.

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
   - Applies the **tenancy gate** below.
   - Signs the principal into `AdminAuthenticationSchemes.SsoCookie` with
     `IsPersistent = false` and the `id_token` stored on the auth properties (used later as
     `id_token_hint` on RP-initiated logout).
   - Signs out of `IdentityConstants.ExternalScheme`.

Any failure in steps 1-3 logs a warning and redirects to `GET /login?error=5`, which renders the
generic "SSO sign-in failed. Try again or use the email link." message. The error message is
deliberately generic so unsuccessful attempts cannot distinguish between "no such user", "wrong
group", and "IdP returned an error."

## Tenancy gate

After `SsoSignInPrincipalBuilder.Build(...)` returns, `LoginController.SsoSignIn` checks whether the
built principal carries at least one `ClaimTypes.Role` claim. If it does not, the sign-in is
rejected.

This gate exists because the SSO path does not consult `AdminSettings:Admins`. The passwordless
path uses that CSV as its membership check. The SSO path uses "has at least one mapped admin role"
as its membership check instead. Without this gate, an IdP-authenticated user whose groups do not
map to any admin role would receive an application cookie and could reach controller actions
decorated with bare `[Authorize]` but no `[RequirePermission]` - specifically `UsersController.
View/Edit` and `OrganizationsController.View/Edit/TriggerBillingSync/ResendOwnerInvite`.

Pinned by `LoginControllerTests.SsoSignIn_RedirectsError5_WhenPrincipalHasNoRoleClaims`.

Adding `[RequirePermission(...)]` to the bare `[Authorize]` actions is a desirable defense-in-depth
follow-up but is not a prerequisite - the tenancy gate makes them unreachable to role-less
principals regardless.

## Authentication, tenancy, and authorization

These three concerns are kept distinct on the SSO path:

- **Authentication** is the IdP proving the user owns the claimed email. Enforced by the OIDC
  handler plus the email-verified check in `LoginController.SsoSignIn`.
- **Tenancy** is whether the authenticated user is a user of this app at all. Enforced by the
  role-presence check in `LoginController.SsoSignIn`.
- **Authorization** is what capabilities the authenticated member has. Enforced by
  `AccessControlService.UserHasPermission` against the role claims on the principal, using
  `RolePermissionMapping`.

The role mapping configuration serves both tenancy and authorization: "has any mapped role" is the
tenancy signal, "which permissions those roles grant" is the authorization decision. The two use
the same data source but answer different questions.

## Cookie schemes

The Admin app registers two cookie schemes. Both coexist so the two sign-in paths can carry
independent session rules.

| Scheme | Set by | SecurityStampValidator |
|---|---|---|
| `IdentityConstants.ApplicationScheme` | Passwordless path (`SignInManager`) | Yes, 30s re-check against the backing `ReadOnlyEnvIdentityUserStore` |
| `AdminAuthenticationSchemes.SsoCookie` | SSO path (`HttpContext.SignInAsync` in `LoginController.SsoSignIn`) | No - the SSO principal has no backing user record to re-fetch |

The two schemes exist because the Identity cookie's `SecurityStampValidator` re-fetches the user
from `IUserStore` every 30 seconds. The SSO principal has no corresponding row in that store, so
routing it through the Identity cookie would eject the session on the first re-check.

Bare `[Authorize]` works for either path because the authorization `DefaultPolicy` lists both
schemes. ASP.NET Core's `PolicyEvaluator` calls `AuthenticateAsync` on each scheme and sets
`HttpContext.User` to the first successful result. `DefaultAuthenticateScheme` and
`DefaultChallengeScheme` stay at Identity's defaults, so bare `[Authorize]` still redirects to
`/login` and `SignInManager` still writes to the Identity cookie.

`FallbackPolicy` is deliberately not set, so endpoints without `[Authorize]` stay anonymous. This
keeps the login pages, `/alive`, `/error`, and the version endpoint reachable without a cookie.

## Logout

`LoginController.Logout` reads `id_token` from `AdminAuthenticationSchemes.SsoCookie` before signing
out, then signs out of both cookie schemes (sign-out against a scheme the user is not currently
signed into is a no-op). If `id_token` was present and `OidcEnabled` is true, it triggers
RP-initiated logout against `AdminAuthenticationSchemes.UpstreamOidc` with the saved token as
`id_token_hint`, so the upstream IdP can honor the app-level Allowed Logout URLs list.

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

`AddAdminUpstreamOidc` fails loud at boot if `GroupPrefix` is empty while OIDC is enabled, because
without it `GroupClaimRoleMapper` would match no groups and every SSO sign-in would fail the tenancy
gate.

IdP groups are expected to be named `<GroupPrefix><role>` where `<role>` is one of `owner`, `admin`,
`billing`, `cs`, or `sales` (the keys of `RolePermissionMapping.RolePermissions`). Case is
insensitive. A principal in multiple matching groups receives the union of their permissions.
