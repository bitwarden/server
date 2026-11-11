# Auth Integration Tests

This page extends [Integration Testing](INTEGRATION_TEST.md). Follow that guide. This page lists only where the Auth suites add to it or differ from it. It applies to `test/Identity.IntegrationTest/`, `bitwarden_license/test/Sso.IntegrationTest/`, and the `Auth/` folder of other integration test projects, such as `test/Api.IntegrationTest/Auth/`.

## Folders

The repo guide names test classes after scenarios. Auth also organizes folders:

1. Name a folder after the domain that its tests cover, such as master password, email, registration, or two-step login. Do not name a folder after a controller or a route prefix: one prefix, such as `/accounts`, can hold many domains.
2. Exception: name a folder after a protocol surface when that surface serves several domains through shared machinery. The token endpoint (`/connect/token`) issues tokens for user login, session renewal, machine clients, and Send access, and one validation pipeline applies the same login rules (two-step login, new device verification, required SSO, client version, decryption options) to several grant types. Put its tests in one folder for the endpoint, grouped by grant type and by shared rule, so that the shared pipeline stays visible. SAML handler routes follow the same reasoning.
3. A folder name becomes a namespace segment. Do not use a name that hides an existing type, such as `AuthenticationSchemes` or `ApiKey`.

## Names

1. Inside a protocol folder, the protocol noun can lead the class name, for example `RefreshTokenGrantTests` or `Saml2AcsRequestPathTests`.
2. Keep class names unique within a project.
3. Many existing Auth test methods start with the controller action, for example `PostPassword_…`. You do not need to rename them.

## Scope of a test class

1. Line count alone is not a reason to split a class.
2. Tests in a class share one database, so each test seeds its own users and organizations, with unique email addresses.
3. When a behavior includes observable side effects (events, push notifications, email, a security stamp change), assert them.
4. For WebAuthn, use `FakeWebAuthnAuthenticator` from `test/Common` instead of a substitute.

## Helpers outside the application factory

The repo guide puts test operations in intent methods on the application factory. For helpers that are not intent methods, use these name suffixes, and keep each helper in the narrowest folder that all its callers share:

1. `TestHelpers`: static arrange helpers.
2. `TestDataBuilder`: a fluent builder that seeds related records in order and returns them.
3. `Assertions`: static assert helpers.
4. `Payloads`: request body builders.

## Existing suites that predate the repo guide

Most existing Auth suites use application factories built on the shared `WebApplicationFactoryBase` in `IntegrationTestCommon`, and some tests read services directly. The repo guide lists both as anti-patterns. Leave these tests as they are when you move or rename them. New tests follow the repo guide.

## Behavior whose intent is not confirmed

Do not add a test that asserts behavior that may be a bug or whose intent nobody has confirmed. List it in the pull request description and confirm the intended behavior first. A test that pins the current behavior makes a later fix look like a regression.

## Moves and renames

Put file moves, splits, and class renames in pull requests that change no test behavior. When both are large, put moves and renames in separate pull requests, so that reviewers can check each one mechanically.
