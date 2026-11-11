# Auth Integration Tests

This guide extends [Integration Testing](INTEGRATION_TEST.md). Read that guide first. This page covers only what Auth adds to it. It applies to these test suites:

1. `test/Identity.IntegrationTest/`
2. `test/Api.IntegrationTest/Auth/`
3. `test/Admin.IntegrationTest/Auth/`
4. `bitwarden_license/test/Sso.IntegrationTest/`

## Folders: domain first, protocol where the protocol is the domain

1. Name a folder after the domain that its tests cover, for example `MasterPassword/`, `Email/`, `Registration/`, `TwoFactor/`, or `EmergencyAccess/`.
2. Name a folder after a protocol surface when that surface serves several domains through shared machinery. This is the one place where Auth differs from pure domain naming:
   1. `TokenEndpoint/`: `/connect/token` issues tokens for user login, session renewal, machine clients, and Send access. One validation pipeline applies the same login rules (two-step login, new device verification, required SSO, client version, decryption options) to several grant types. Tests go in `TokenEndpoint/Grants/{GrantType}/` and `TokenEndpoint/{Rule}/`. Domain folders would scatter these tests and hide the shared pipeline.
   2. `Saml2/`, `Oidc/`, `AuthorizeEndpoint/`, `DiscoveryEndpoint/`, `Authentication/{Scheme}/`, and `Middleware/` follow the same reasoning.
3. Put a journey that crosses domains or hosts in `Flows/{Journey}/`.
4. Do not name a folder after a controller or another production class. Do not name it after a route prefix either: one prefix, such as `/accounts`, can hold many domains.
5. A folder name becomes a namespace segment. Do not use a name that hides an existing type, for example `AuthenticationSchemes` or `ApiKey`.

## Names

| Level  | Answers                             | Example                                                  |
|--------|-------------------------------------|----------------------------------------------------------|
| Folder | Which domain or protocol surface?   | `MasterPassword/`                                        |
| Class  | Which scenario?                     | `PasswordChangeTests`                                    |
| Method | Which case?                         | `WrongCurrentPassword_ChangesPassword_ReturnsBadRequest` |

1. Class: `{Scenario}Tests`. Inside a protocol folder, the protocol noun can lead the name, for example `PasswordGrantAuthRequestTests` or `Saml2AcsRequestPathTests`. Journey classes are `{Journey}FlowTests`. Keep class names unique within a project.
2. Method: use the repo guide's form, `{StateOrActor}_{Action}_{Outcome}`. You do not need to rename existing methods that use a different form.
3. Route-table authorization checks for a domain (no token, wrong token type, deleted user) go in `{Domain}AuthorizationTests`.

## What goes in one file

1. One scenario and one fixture configuration per file. A class fixture shares one host across its tests, so a substitute or setting that one test needs leaks into the others. A test that needs a different setup goes in another class.
2. Split a file when it mixes scenarios or fixture configurations, not because of its length.
3. Each test seeds its own users and organizations, with unique email addresses. Do not share seeded rows across tests.
4. Substitute only external boundaries: Duo, Yubico, Stripe, mail and push transport, and outbound HTTP. Use fakes for authenticators, for example `FakeWebAuthnAuthenticator`.
5. When a behavior includes observable side effects (events, push notifications, email, a security stamp change), assert them.

## Helpers

1. Use one of four helper kinds:
   1. `{Domain}TestHelpers`: static arrange helpers.
   2. `{Domain}TestDataBuilder`: a fluent builder that seeds related records in order and returns them.
   3. `{Domain}Assertions`: static assert helpers.
   4. `{Domain}Payloads`: request body builders.
2. Keep a helper in the narrowest folder that all its callers share: `Auth/Helpers/` (Api, Admin), `Helpers/` (Identity), or `Utilities/` (Sso). Extract a helper at its second use, not before.
3. Do not add new code to `IntegrationTestCommon`. The repo guide treats it as legacy.

## Behavior whose intent is not confirmed

Do not add a test that asserts behavior that may be a bug or whose intent nobody has confirmed. List it in the pull request description and confirm the intended behavior first. A test that pins the current behavior makes a later fix look like a regression.

## Changing the layout

Keep file moves, splits, and class renames in pull requests that change no test behavior, so that reviewers can check them mechanically. Put moves and renames in separate pull requests when both are large.
