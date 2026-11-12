# Auth Integration Tests

This page extends [Integration Testing](INTEGRATION_TEST.md). Follow that guide. This page lists only where the Auth suites add to it or differ from it. It applies to all auth integration tests.

## Folders

The repo guide names test classes after scenarios. Auth also organizes folders:

1. Name a folder after the domain that its tests cover, such as master password, email, registration, or two-step login. Do not name a folder after a controller or a route prefix: one prefix, such as `/accounts`, can hold many domains.
2. Exception: name a folder after a protocol surface when that surface serves several domains through shared machinery. The token endpoint (`/connect/token`) issues tokens for user login, session renewal, machine clients, and Send access, and one validation pipeline applies the same login rules (two-step login, new device verification, required SSO, client version, decryption options) to several grant types. Put its tests in one folder for the endpoint, grouped by grant type and by shared rule, so that the shared pipeline stays visible. SAML handler routes follow the same reasoning.
3. A folder name becomes a namespace segment, which hides any type with the same name across the project. For example, a folder named `AuthenticationSchemes` breaks references to the Core `AuthenticationSchemes` class. Choose another name, such as `Authentication`.
4. Put test code that several suites share, such as data builders, assertions, and host setup, in a `Helpers/` folder.

## Names

1. Inside a protocol folder, the protocol noun can lead the class name, for example `RefreshTokenGrantTests` or `Saml2AcsRequestPathTests`.
2. Keep class names unique within a project.
