# Admin Portal sign-in

How the Admin Portal admits users. Audience: engineers modifying `LoginController`,
`PasswordlessSignInManager`, `SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`,
`ReadOnlyEnvIdentityUserStore`, or `AdminSettings`; operators provisioning Admin Portal access.

## Sign-in paths

The Admin Portal supports two sign-in paths. An operator can enable either or both.

| Path | Enabled by | Writes cookie |
|---|---|---|
| Passwordless email | `AdminSettings:EnablePasswordlessLogin = true` (default) | `IdentityConstants.ApplicationScheme` |
| Upstream OIDC SSO | `AdminSettings:Oidc:*` configured | `AdminAuthenticationSchemes.SsoCookie` |

## Passwordless email path

The user requests a magic link from the login page. `PasswordlessSignInManager` emails a
one-time URL that confirms the login via `GET /login/confirm?...`. Identity's user lookup goes
through `ReadOnlyEnvIdentityUserStore`, which reads from `AdminSettings:Admins` — any email not on
that CSV is unknown to the user store and cannot complete the magic-link exchange.

| | Cloud | Self-hosted |
|---|---|---|
| Tenancy gate | Email is on `AdminSettings:Admins` | Email is on `AdminSettings:Admins` |
| Authorization | Role claim comes from `adminSettings:role:{roleName}` CSV sub-config, matched against the authenticated email. Permissions come from `RolePermissionMapping` | Full access (`AccessControlService.UserHasPermission` short-circuits on `_globalSettings.SelfHosted`) |

### Operator configuration

```jsonc
"AdminSettings": {
  "EnablePasswordlessLogin": true,
  "Admins": "admin@example.com,another@example.com",
  // On cloud only — map each admin email to a role.
  "role": {
    "owner":   "admin@example.com",
    "billing": "another@example.com"
  }
}
```

`AdminSettings:Admins` is a comma-separated list where each entry is either `email` or
`email:stamp`. The optional stamp populates `IdentityUser.SecurityStamp`; rotating a stamp
invalidates the Identity cookie for that admin on the next 30s re-check.

## Upstream OIDC SSO path

The user is redirected to the configured IdP, authenticates there, and comes back with a verified
email claim. `SsoSignInPrincipalBuilder` hand-builds the principal (bypassing `SignInManager` /
`UserManager`, so there is no local user record and no `SecurityStamp` to track), stamping
`ClaimTypes.Role` claims from the IdP's `groups` claim via `GroupClaimRoleMapper`. The sign-in
cookie goes to a dedicated scheme (not the Identity cookie), and session lifetime is bounded by
cookie expiry and RP-initiated logout.

| | Cloud | Self-hosted |
|---|---|---|
| Tenancy gate | Principal has at least one `ClaimTypes.Role` claim | Authenticated email is on `AdminSettings:Admins` |
| Authorization | Union of permissions across role claims via `RolePermissionMapping` | Full access (`AccessControlService.UserHasPermission` short-circuits on `_globalSettings.SelfHosted`) |
| Role claims come from | `AdminSettings:Oidc:GroupPrefix` → `GroupClaimRoleMapper` maps IdP `groups` to roles | n/a — role claims are irrelevant on self-hosted |
| Boot-time required config | `AdminSettings:Oidc:GroupPrefix` | `AdminSettings:Admins` |

`AddAdminUpstreamOidc` fails loud at boot when the deployment-required config is missing.

### Operator configuration

Cloud:

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

IdP groups must be named `<GroupPrefix><role>` where `<role>` is one of `owner`, `admin`,
`billing`, `cs`, or `sales` (the keys of `RolePermissionMapping.RolePermissions`). Case is
insensitive. A principal in multiple matching groups receives the union of their permissions.

Self-hosted:

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

`GroupPrefix` is not required on self-hosted; role claims are not consulted.

## Authentication, tenancy, and authorization

Both paths keep three concerns distinct:

- **Authentication** — the sign-in path proves the user owns the claimed email. Passwordless does
  this via the one-time magic link; SSO does it via the IdP plus the `email_verified` check.
- **Tenancy** — whether the authenticated user is a user of the Admin Portal. Enforced by the
  per-path, per-deployment gates in the tables above.
- **Authorization** — what capabilities the authenticated member has. On cloud, enforced by
  `AccessControlService.UserHasPermission` against role claims via `RolePermissionMapping`. On
  self-hosted, the same method short-circuits to `true` on `_globalSettings.SelfHosted`.

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
