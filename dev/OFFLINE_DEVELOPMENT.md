# Offline Development

Steps to run the Bitwarden server stack without network access to internal Bitwarden services.
Each section below covers one service that reaches out by default — apply every step that
applies to the projects you run. **If you find another service that needs an offline workaround,
add a section here.**

## Pricing Service

Set `globalSettings.pricingUri` to `null` in your secrets. With the host environment set to
`Development`, DI resolves `LocalPricingClient` instead of the HTTP-backed one.
