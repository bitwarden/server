# MJML Email Templates - Claude Code Configuration

## Project Overview

This directory contains MJML source templates that compile to `.html.hbs` (Handlebars) files consumed by Bitwarden's `IMailer` infrastructure. MJML provides responsive, component-based email markup that reduces the complexity of cross-client HTML emails.

**Pipeline**: `.mjml` → `build.js` → `.html.hbs` → Handlebars runtime → delivered email

## Common Commands

```shell
npm ci                   # Install dependencies
npm run build            # Compile *.mjml → *.html (HTML preview)
npm run build:hbs        # Compile *.mjml → *.html.hbs (Handlebars, for IMailer)
npm run build:minify     # Compile → minified *.html.hbs (production deliverable)
npm run build:watch      # Watch mode (does NOT track new files; restart after adding files)
npm run prettier         # Format all files
```

## Directory Structure

```
Mjml/
├── components/                        # Global reusable components (shared across all emails)
│   ├── head.mjml                      # Shared font, color, button styling — include in ALL templates
│   ├── footer.mjml                    # Social links, copyright, security notice
│   ├── logo.mjml                      # Bitwarden logo
│   ├── mj-bw-hero.js                  # Blue header with logo, title, image, optional button
│   ├── mj-bw-simple-hero.js           # Simplified hero variant
│   ├── mj-bw-icon-row.js              # Icon + text row; icons hidden on mobile
│   └── mj-bw-learn-more-footer.js     # "Learn more" section with image
│
├── emails/                            # Template source files organized by product area
│   ├── Auth/                          # Authentication: OTP, 2FA, emergency access, onboarding
│   ├── AdminConsole/                  # Org management: invitations, confirmations
│   │   └── components/                # AdminConsole-specific component overrides
│   ├── Billing/                       # Renewals, payment updates, business unit invites
│   ├── Provider/                      # Provider-specific emails
│   └── invite.mjml                    # Generic invite
│
├── out/                               # Build output — DO NOT edit manually (git-ignored)
├── build.js                           # Build orchestration script
├── .mjmlconfig                        # Registers all custom JS components
├── package.json
└── README.md
```

## Adding a New Email Template

1. Create `your-email-name.mjml` in the appropriate team subdirectory under `emails/`.
2. Run `npm run build:watch` and iterate in a browser.
3. Run `npm run build:hbs` to generate the `.html.hbs` artifact.
4. Copy the built `*.html.hbs` from `out/` to the matching path in `/src/Core/MailTemplates/Mjml/` (alongside the corresponding ViewModel and `.txt.hbs`).
5. The minified `html.hbs` is the production deliverable — run `npm run build:minify` for final output.

## Template Structure Pattern

Every template must follow this structure:

```xml
<mjml>
  <mj-head>
    <!-- ALWAYS include shared styles -->
    <mj-include path="../../components/head.mjml" />
    <!-- Optional template-specific styles -->
    <mj-style>/* ... */</mj-style>
  </mj-head>
  <mj-body>
    <mj-wrapper>
      <mj-bw-hero img-src="..." title="..." />
    </mj-wrapper>
    <!-- Content sections -->
    <mj-wrapper>
      <mj-section>
        <mj-column><!-- content --></mj-column>
      </mj-section>
    </mj-wrapper>
    <!-- ALWAYS include footer -->
    <mj-include path="../../components/footer.mjml" />
  </mj-body>
</mjml>
```

## Handlebars Variables

Use standard double-brace syntax directly in MJML — Handlebars resolves these at runtime, not at compile time:

```html
{{VariableName}}
{{#if Condition}}...{{/if}}
```

Common variables: `{{Token}}`, `{{Url}}`, `{{InviterEmail}}`, `{{ExpirationDate}}`, `{{CurrentYear}}`

## Custom Components

### Creating a Component

1. Create `components/mj-bw-your-component.js` extending `BodyComponent` from `mjml-core`.
2. Register it in `.mjmlconfig` under `"packages"`.
3. Use `static allowedAttributes` and `static dependencies` to define the interface.
4. Return MJML markup string from `render()` via `this.renderMJML(...)`.

### Registered Components

| Tag | File | Notes |
|-----|------|-------|
| `mj-bw-hero` | `components/mj-bw-hero.js` | Blue header; `img-src` and `title` required |
| `mj-bw-simple-hero` | `components/mj-bw-simple-hero.js` | Simplified variant |
| `mj-bw-icon-row` | `components/mj-bw-icon-row.js` | Icon hidden on mobile |
| `mj-bw-learn-more-footer` | `components/mj-bw-learn-more-footer.js` | |
| `mj-bw-ac-hero` | `emails/AdminConsole/components/` | AdminConsole variant |
| `mj-bw-ac-icon-row` | `emails/AdminConsole/components/` | With bullet points |
| `mj-bw-ac-icon-row-without-bulletins` | `emails/AdminConsole/components/` | No bullets |
| `mj-bw-ac-learn-more-footer` | `emails/AdminConsole/components/` | AC styling |
| `mj-bw-inviter-info` | `emails/AdminConsole/components/` | Shows inviter details |

## Styling Conventions

- **Primary blue**: `#175ddc`
- **Background gray**: `#e6e9ef`
- **Content background**: `#fff`
- **Font stack**: Helvetica Neue, Arial, sans-serif; 16px base
- **Base width**: 660px; mobile responsive via `componentHeadStyle` media queries
- **Image CDN**: `https://assets.bitwarden.com/email/v1/`
- Decorative images should be hidden on mobile

## Integration with IMailer

The `.html.hbs` build artifacts must reside alongside their ViewModel and `.txt.hbs` in `/src/Core/MailTemplates/Mjml/<Team>/`. The `IMailer` (see `src/Core/Platform/Mail/README.md`) loads these three files together. `IMailService` is deprecated — use `IMailer`.

## Critical Rules

- **ALWAYS** include `components/head.mjml` in every template's `<mj-head>`.
- **ALWAYS** include `components/footer.mjml` at the end of every `<mj-body>`.
- **NEVER** edit files in `out/` directly — they are generated artifacts.
- **NEVER** commit `out/` files; copy minified artifacts to the correct `MailTemplates/` path.
- Register every new custom component in `.mjmlconfig` before referencing it in a template.
- Run `npm run prettier` before committing `.mjml` or `.js` changes.
- `build:watch` does not detect new files — restart it after creating new `.mjml` files.
- **NEVER** use inline CSS (`style="..."`); define styles in `<mj-style>` within `<mj-head>`.
- **AVOID** style attributes on MJML elements (e.g. `font-size`, `color`, `padding`); prefer class-based styles defined in `<mj-head>`.
