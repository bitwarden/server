---
status: Accepted
date: 2026-10-17
tags: [server, admin, sso]
---

# Admin Portal SSO sign-in design

Design and rationale for how the Admin Portal admits users signing in via upstream OIDC
(`AdminAuthenticationSchemes.UpstreamOidc`). Audience: engineers modifying `LoginController`,
`SsoSignInPrincipalBuilder`, `GroupClaimRoleMapper`, or the `AdminSettings:Oidc:*` configuration
surface; operators evaluating how to provision Admin Portal access through an IdP.

## Context and problem statement

The Admin Portal has two sign-in paths:

- **Passwordless email** (self-hosted default). `AdminSettings:Admins` is a CSV allowlist that
  gates sign-in by email. Permissions on cloud come from `adminSettings:role:{roleName}` CSV
  sub-config; on self-hosted, `AccessControlService.UserHasPermission` short-circuits to `true` on
  `_globalSettings.SelfHosted`.
- **Upstream OIDC SSO** (added in PR #8508 and extended here). The IdP authenticates the user,
  and `GroupClaimRoleMapper` maps the IdP's `groups` claim to Admin Portal role names using the
  configured `AdminSettings:Oidc:GroupPrefix`.

The SSO path removed the `AdminSettings:Admins` allowlist check that the passwordless path still
uses, replacing membership with "has at least one mapped admin role." This introduces a tenancy
question: what signal admits an IdP-authenticated user as a member of the Admin Portal?

Several controller actions carry only bare `[Authorize]` and are not additionally gated by
`[RequirePermission]`:

- `UsersController.View`, `UsersController.Edit` (GET + POST)
- `OrganizationsController.View`, `OrganizationsController.Edit` (GET + POST),
  `TriggerBillingSync`, `ResendOwnerInvite`

These actions are reachable by any authenticated principal. Without a tenancy signal applied at
sign-in, an IdP-authenticated email whose groups do not map to any admin role would receive an
application cookie and could invoke these actions on cloud. On self-hosted the exposure is bounded
by the operator-controlled IdP app assignment, which is the first tenancy filter.

### Terminology

- **Authentication** — the IdP proving the user owns the claimed email.
- **Tenancy / membership** — whether an authenticated user is a user of *this* app at all.
- **Authorization** — what capabilities a member has within the app.

The three are distinct. Historically the passwordless path collapsed authentication and tenancy
into one step: `UserManager.FindByEmailAsync` against `AdminSettings:Admins`. The SSO path keeps
them separate: the IdP handles authentication, and this document is about how tenancy is enforced.

## Considered options

### Option 1 — No tenancy check; rely on per-action `[RequirePermission]`

Admit every IdP-authenticated principal and depend on `AccessControlService.UserHasPermission`
returning `false` to block the ungated actions.

#### Advantages

- Clean separation of concerns: authn at the door, authz at each action.
- No per-request tenancy work.

#### Disadvantages

- Does not address the actual exposure: bare `[Authorize]` actions have no permission check and
  remain reachable by any authenticated principal.
- Fragile — every new ungated action adds a new exposure. Review discipline alone is not a
  structural guard.
- Issues cookies to principals who have no business holding them, enlarging the attack surface of
  the cookie itself.

### Option 2 — Add `[RequirePermission]` to every bare `[Authorize]` action

Decorate every consequential controller action with a permission requirement, then rely on
`AccessControlService` to reject role-less principals at each action.

#### Advantages

- Pins per-action authorization explicitly, independent of upstream tenancy state.
- Catches the same class of regression (new ungated action) locally where the action is defined.

#### Disadvantages

- Cross-cutting work across six-plus actions in two controllers; each action requires a decision
  about which permission to require.
- Still issues cookies to role-less principals; defense in depth at the sign-in boundary is
  cheaper and more complete.
- Does not change the fact that any new ungated action reintroduces exposure.

### Option 3 — Tenancy gate at sign-in: reject when the principal has no mapped role

After `SsoSignInPrincipalBuilder.Build(...)` returns, verify the principal carries at least one
`ClaimTypes.Role` claim. If none, sign the external principal out and redirect to the generic SSO
failure message.

#### Advantages

- Addresses the exposure at the correct layer: a non-member never gets a cookie.
- Defense in depth remains possible — adding `[RequirePermission]` to ungated actions is a
  compatible follow-up, not a prerequisite.
- Honest about the Admin Portal model: there is no legitimate "authenticated but no capability"
  user. Every page is admin-only.
- Mirrors the role of `AdminSettings:Admins` on the passwordless path: a membership gate at the
  door, decoupled from per-action authz.

#### Disadvantages

- Admits an argument that authn and authz are being conflated. The rebuttal: the check is a
  *tenancy* gate, not an authz check, and groups happen to be the only tenancy signal the SSO
  path consumes today.
- Operators who want SSO without configuring IdP groups cannot use the SSO path yet. The
  `AdminSettings:Oidc:GroupPrefix` setting is already required at boot by `AddAdminUpstreamOidc`,
  so this is not a regression from the broader SSO work.

## Decision outcome

Chosen option: **option 3, tenancy gate at sign-in**.

`LoginController.SsoSignIn` evaluates `principal.FindAll(ClaimTypes.Role).Any()` after building
the principal. If false, the controller logs a warning, signs out
`IdentityConstants.ExternalScheme`, and redirects to the generic SSO-failed error. The gate is
test-pinned by `LoginControllerTests.SsoSignIn_RedirectsError5_WhenPrincipalHasNoRoleClaims`.

### Positive consequences

- Cloud SSO users whose IdP groups do not map to an admin role never receive a session, closing
  the exposure flagged by code review on the bare `[Authorize]` actions.
- The authn/authz/tenancy separation is honest at every boundary: the IdP handles authn, this
  gate handles tenancy, `AccessControlService` handles authz.
- Future SSO sign-in paths (e.g., adding CSV-based tenancy for operators who prefer "IdP as
  authn only") can be layered as additional tenancy signals under an all-gates-must-pass rule
  without changing the authz model.

### Negative consequences

- Operators enabling Admin Portal SSO must provision at least one IdP group matching
  `AdminSettings:Oidc:GroupPrefix` for intended admins. There is no "SSO without groups" path in
  the current PR.
- The bare `[Authorize]` actions on `UsersController` and `OrganizationsController` remain latent
  risks if a future change weakens the tenancy gate or introduces a new sign-in path. The
  follow-up to add `[RequirePermission]` to those actions is tracked separately.

### Plan

- Ship the tenancy gate in the SSO sign-in path and the matching unit test as part of
  PR #8587.
- Track as follow-up work: audit every `[Authorize]`-only action in `src/Admin` and either add
  `[RequirePermission(...)]` or document why no permission applies. This is defense in depth
  against future tenancy-gate regressions.
- Track as follow-up work: evaluate operator demand for CSV-based tenancy on the SSO path (where
  `AdminSettings:Admins` acts as the tenancy signal and `AdminSettings:Oidc:GroupPrefix` becomes
  optional). The all-gates-must-pass rule discussed in design review generalizes cleanly if
  demand materializes.
