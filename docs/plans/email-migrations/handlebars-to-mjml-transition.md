# Handlebars-to-MJML Email Transition Guide

## Context

Bitwarden's `HandlebarsMailService` is deprecated in favor of the new `IMailer` system. New emails should be authored as `.mjml` source files that compile to `.html.hbs` artifacts consumed by Handlebars at runtime. This document provides a systematic reference for converting existing Handlebars email templates to MJML, based on the official MJML documentation, the official Handlebars documentation, and Bitwarden's established MJML patterns.

## Related Documentation

Before converting templates, familiarize yourself with these key resources:

- **[MailTemplates README](../../../src/Core/MailTemplates/README.md)** - Overview of the mail template system and directory structure
- **[Mjml README](../../../src/Core/MailTemplates/Mjml/README.md)** - MJML build process, custom components, and development workflow
- **[Platform Mail README](../../../src/Core/Platform/Mail/README.md)** - IMailer system architecture and integration guide

---

## 1. Architecture Overview

### Current (Deprecated) Pipeline

```
Template (.html.hbs) + Layout partial (Full.html.hbs / ProviderFull.html.hbs)
    → HandlebarsDotNet compiles at runtime
    → Final HTML delivered to email client
```

Templates use `{{#>FullHtmlLayout}}` or `{{#>ProviderFull}}` block partials that inject content into a hand-coded HTML layout containing all styling, the logo, the footer, and email-client workarounds.

### New Pipeline

```
Source (.mjml) + shared components (head.mjml, footer.mjml, mj-bw-* custom components)
    → `npm run build:hbs` compiles MJML to .html.hbs
    → .html.hbs artifact placed next to ViewModel class
    → HandlebarsDotNet compiles at runtime with ViewModel data
    → Final HTML delivered to email client
```

Key insight: **MJML and Handlebars are not competing—they run in sequence.** MJML compiles structural markup at build time; Handlebars resolves `{{ variables }}` at runtime. The `{{ }}` expressions pass through MJML compilation untouched because MJML does not interpret them.

---

## 2. Structural Mapping Reference

### Layout → MJML Skeleton

The old layout partials (`Full.html.hbs`, `ProviderFull.html.hbs`) are replaced by a standard MJML skeleton that uses `mj-include` for shared components:

```xml
<mjml>
  <mj-head>
    <mj-include path="../../components/head.mjml" />
  </mj-head>

  <mj-body>
    <!-- Logo (replaces the header table in Full.html.hbs) -->
    <mj-include path="../../components/logo.mjml" />

    <!-- Main content area (replaces the {{>@partial-block}} region) -->
    <mj-wrapper background-color="#fff" border="1px solid #e9e9e9"
                css-class="border-fix" padding="0">
      <mj-section>
        <mj-column>
          <!-- Template-specific content goes here -->
        </mj-column>
      </mj-section>
    </mj-wrapper>

    <!-- Footer (replaces the social icons + copyright table) -->
    <mj-include path="../../components/footer.mjml" />
  </mj-body>
</mjml>
```

For "Provider" style emails (blue header bar), use the `<mj-bw-simple-hero />` or `<mj-bw-hero />` custom components instead of the logo include.

### Element-by-Element Mapping

| Old Handlebars/HTML | MJML Equivalent | Notes |
|---|---|---|
| `{{#>FullHtmlLayout}}...{{/FullHtmlLayout}}` | `<mjml>` skeleton + `mj-include` for head/logo/footer | Layout is now implicit in the MJML structure |
| `{{#>ProviderFull}}...{{/ProviderFull}}` | Skeleton with `<mj-bw-hero />` or `<mj-bw-simple-hero />` | Custom component renders the blue header bar |
| `<table>` with `content-block` rows | `<mj-section>` + `<mj-column>` + `<mj-text>` | Each `<tr><td class="content-block">` → one `<mj-text>` block |
| Inline-styled `<a>` button | `<mj-button href="...">` | Inherits `background-color: #175ddc` from `head.mjml` defaults |
| `<br />` spacers | `<mj-spacer height="Xpx" />` or padding on `mj-text` | Prefer padding attributes over spacer elements |
| Social icon `<table>` | `<mj-include path="...footer.mjml" />` | Already handled by shared footer component with `mj-social` |
| `<style>` blocks in layout | `<mj-style>` in `head.mjml` | Global styles defined once via `mj-include` |
| CSS classes (`content-block`, `main`, etc.) | `mj-class` or `css-class` attributes | Use `mj-class` for MJML-level attribute grouping; `css-class` to add HTML classes to generated output |
| `style="font-family: ..."` on every element | `<mj-all font-family="...">` in `head.mjml` | Defined once; inherited by all components |

---

## 3. Handling Handlebars Logic Inside MJML

This is the critical section. MJML compiles at **build time**, but Handlebars expressions evaluate at **runtime**. The `{{ }}` syntax passes through MJML compilation as plain text. The complexity arises when Handlebars block helpers (`{{#if}}`, `{{#each}}`, `{{#unless}}`) need to **wrap MJML structural elements**.

### Case 1: Variables Inside Content (Simple)

Handlebars expressions inside `<mj-text>` content work with zero changes:

```xml
<mj-text>
  Your subscription renews on <b>{{date DueDate 'MMMM dd, yyyy'}}</b>.
</mj-text>
```

Triple-stash for unescaped URLs also works:

```xml
<mj-button href="{{{Url}}}">Click Here</mj-button>
```

### Case 2: Conditional Text Within a Single Block (Medium)

When `{{#if}}` controls which **text** to show but doesn't change the MJML structure, place it inside `<mj-text>`:

```xml
<mj-text>
  {{#if HasPaymentMethod}}
    Please ensure your {{PaymentMethodDescription}} can be charged.
  {{else}}
    Please add a payment method.
  {{/if}}
</mj-text>
```

This works because `<mj-text>` is an "ending tag"—its contents are treated as raw HTML by MJML and passed through untouched.

### Case 3: Conditional MJML Structural Elements (Hard — Use `mj-raw`)

When Handlebars conditionals need to **show or hide entire MJML sections, buttons, or table rows**, you cannot place `{{#if}}` around `<mj-section>` or `<mj-button>` tags directly—MJML would try to parse the `{{#if}}` as part of its structure and fail validation.

**Strategy: Wrap Handlebars block helpers in `<mj-raw>` tags.**

```xml
<mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>

<mj-section>
  <mj-column>
    <mj-text font-size="32px" font-weight="bold">
      {{usd AmountDue}}
    </mj-text>
  </mj-column>
</mj-section>

<mj-raw>{{/unless}}</mj-raw>
```

The `<mj-raw>` tags tell MJML to emit their contents as literal text without parsing. At build time, the compiled `.html.hbs` will contain the raw Handlebars helpers wrapping the compiled HTML table structures. At runtime, Handlebars evaluates the conditionals normally.

**Important caveats:**
- If using `--minify` in the build, Handlebars `<` characters (rare but possible in subexpressions) may conflict with the HTML minifier. Wrap them in `<!-- htmlmin:ignore -->` tags:
  ```xml
  <mj-raw><!-- htmlmin:ignore -->{{#if (eq CollectionMethod "send_invoice")}}<!-- htmlmin:ignore --></mj-raw>
  ```
- Validation: Use `validationLevel: "soft"` or `"skip"` if `mj-raw` placement causes strict validation errors. Bitwarden's `build.js` currently uses `"strict"`, but `mj-raw` in legal positions (direct child of `mj-body`, `mj-wrapper`, or `mj-section`) should pass validation.

### Case 4: Conditional Buttons/CTAs

A common pattern in the existing templates is showing different buttons based on conditions. Convert like this:

**Before (Handlebars HTML):**
```handlebars
{{#unless (eq CollectionMethod "send_invoice")}}
<tr>
  <td>
    <table><tr><td style="background-color: #175DDC; border-radius: 25px; padding: 12px 24px;">
      <a href="{{{UpdateBillingInfoUrl}}}" style="color: #ffffff; ...">Update payment method</a>
    </td></tr></table>
  </td>
</tr>
{{/unless}}
```

**After (MJML):**
```xml
<mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>

<mj-section>
  <mj-column>
    <mj-button href="{{{UpdateBillingInfoUrl}}}" border-radius="25px">
      Update payment method
    </mj-button>
  </mj-column>
</mj-section>

<mj-raw>{{/unless}}</mj-raw>
```

### Case 5: Loops (`{{#each}}`)

Same pattern as conditionals—wrap the block helpers in `<mj-raw>`:

```xml
<mj-raw>{{#if Items}}</mj-raw>
<mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>

<mj-section>
  <mj-column>
    <mj-text>
      <strong>Summary Of Charges</strong>
      <div style="border-bottom: 1px solid #ddd; margin: 5px 0 10px 0;"></div>
      {{#each Items}}
        <div>{{this}}</div>
      {{/each}}
    </mj-text>
  </mj-column>
</mj-section>

<mj-raw>{{/unless}}</mj-raw>
<mj-raw>{{/if}}</mj-raw>
```

Note that `{{#each}}` inside `<mj-text>` (iterating over text content) does **not** need `mj-raw` wrapping—it's already inside an ending tag where MJML passes content through as-is.

### Case 6: Custom Handlebars Helpers

Bitwarden registers custom helpers in HandlebarsDotNet (e.g., `eq`, `date`, `usd`). These work identically in MJML templates since they're resolved at runtime:

```xml
<mj-text>
  On <strong>{{date DueDate 'MMMM dd, yyyy'}}</strong> we'll send you an invoice.
</mj-text>

<mj-text font-size="32px" font-weight="bold">
  {{usd AmountDue}}
</mj-text>
```

---

## 4. Worked Examples

### Example A: Simple Email (BusinessUnitConversionInvite)

**Original** — uses `{{#>FullHtmlLayout}}`, two table rows, one CTA button:

```handlebars
{{#>FullHtmlLayout}}
  <table width="100%" ...>
    <tr><td class="content-block" ...>
      You have been invited to set up a new Business Unit Portal...
    </td></tr>
    <tr><td class="content-block" ... align="center">
      <a href="{{{Url}}}" style="color: #ffffff; ... background-color: #175DDC; ...">
        Set Up Business Unit Portal Now
      </a>
    </td></tr>
  </table>
{{/FullHtmlLayout}}
```

**Converted to MJML:**

```xml
<mjml>
  <mj-head>
    <mj-include path="../../components/head.mjml" />
  </mj-head>

  <mj-body>
    <mj-include path="../../components/logo.mjml" />

    <mj-wrapper background-color="#fff" border="1px solid #e9e9e9"
                css-class="border-fix" padding="0">
      <mj-section>
        <mj-column>
          <mj-text padding="0 0 10px">
            You have been invited to set up a new Business Unit Portal
            within Bitwarden.
          </mj-text>

          <mj-button href="{{{Url}}}" border-radius="5px">
            Set Up Business Unit Portal Now
          </mj-button>
        </mj-column>
      </mj-section>
    </mj-wrapper>

    <mj-wrapper>
      <mj-bw-learn-more-footer />
    </mj-wrapper>

    <mj-include path="../../components/footer.mjml" />
  </mj-body>
</mjml>
```

What changed:
- 45 lines of inline-styled HTML → 28 lines of semantic MJML
- All font-family, font-size, color, line-height declarations eliminated (inherited from `head.mjml`)
- Button styling reduced from 8 inline CSS properties to 1 attribute
- Layout partial replaced by `mj-include` composition
- Responsive behavior is now automatic (columns stack on mobile)

### Example B: Complex Email (ProviderInvoiceUpcoming)

**Original** — uses `{{#>ProviderFull}}`, nested `{{#if}}` / `{{#unless}}` / `{{#each}}`, conditional buttons, two divergent content paths based on `CollectionMethod`:

**Converted to MJML:**

```xml
<mjml>
  <mj-head>
    <mj-include path="../../components/head.mjml" />
  </mj-head>

  <mj-body css-class="border-fix">
    <mj-wrapper css-class="border-fix" padding="20px 20px 0px 20px">
      <mj-bw-simple-hero />
    </mj-wrapper>

    <mj-wrapper padding="0px 20px 0px 20px">
      <!-- Heading and intro text — diverges based on CollectionMethod -->
      <mj-section background-color="#fff" padding="15px 10px 10px 10px">
        <mj-column>
          <mj-text>
            {{#if (eq CollectionMethod "send_invoice")}}
              <div style="font-weight: 600; font-size: 24px; line-height: 32px; margin: 0 0 8px 0;">
                Your subscription will renew soon
              </div>
              <div>
                On <strong>{{date DueDate 'MMMM dd, yyyy'}}</strong> we'll send
                you an invoice with a summary of the charges including tax.
              </div>
            {{else}}
              <div style="font-weight: 600; font-size: 24px; line-height: 32px; margin: 0 0 8px 0;">
                Your subscription will renew on {{date DueDate 'MMMM dd, yyyy'}}
              </div>
              {{#if HasPaymentMethod}}
                <div>
                  To avoid any interruption in service, please ensure your
                  {{PaymentMethodDescription}} can be charged for the following amount:
                </div>
              {{else}}
                <div>
                  To avoid any interruption in service, please add a payment method
                  that can be charged for the following amount:
                </div>
              {{/if}}
            {{/if}}
          </mj-text>
        </mj-column>
      </mj-section>

      <!-- Amount due — only shown for non-invoice collection -->
      <mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>
      <mj-section background-color="#fff" padding="0 10px">
        <mj-column>
          <mj-text font-size="32px" font-weight="bold" padding="0 15px 20px">
            {{usd AmountDue}}
          </mj-text>
        </mj-column>
      </mj-section>
      <mj-raw>{{/unless}}</mj-raw>

      <!-- Line items — only for non-invoice with items -->
      <mj-raw>{{#if Items}}</mj-raw>
      <mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>
      <mj-section background-color="#fff" padding="0 10px 10px 10px">
        <mj-column>
          <mj-text padding="0 15px">
            <strong>Summary Of Charges</strong>
            <div style="border-bottom: 1px solid #ddd; margin: 5px 0 10px 0; padding-bottom: 5px;"></div>
            {{#each Items}}
              <div>{{this}}</div>
            {{/each}}
          </mj-text>
        </mj-column>
      </mj-section>
      <mj-raw>{{/unless}}</mj-raw>
      <mj-raw>{{/if}}</mj-raw>

      <!-- Invoice-only: pay by due date message -->
      <mj-raw>{{#if (eq CollectionMethod "send_invoice")}}</mj-raw>
      <mj-section background-color="#fff" padding="0 10px">
        <mj-column>
          <mj-text padding="0 15px 20px">
            To avoid any interruption in service for you or your clients,
            please pay the invoice by the due date, or contact Bitwarden
            Customer Support to sign up for auto-pay.
          </mj-text>
        </mj-column>
      </mj-section>
      <mj-raw>{{/if}}</mj-raw>

      <!-- Non-invoice: update payment method CTA -->
      <mj-raw>{{#unless (eq CollectionMethod "send_invoice")}}</mj-raw>
      <mj-section background-color="#fff" padding="0 10px 10px 10px">
        <mj-column>
          <mj-button href="{{{UpdateBillingInfoUrl}}}" border-radius="25px">
            Update payment method
          </mj-button>
        </mj-column>
      </mj-section>
      <mj-raw>{{/unless}}</mj-raw>

      <!-- Invoice-only: contact support CTA -->
      <mj-raw>{{#if (eq CollectionMethod "send_invoice")}}</mj-raw>
      <mj-section background-color="#fff" padding="0 10px 10px 10px">
        <mj-column>
          <mj-button href="{{{ContactUrl}}}" border-radius="25px">
            Contact Bitwarden Support
          </mj-button>
        </mj-column>
      </mj-section>
      <mj-raw>{{/if}}</mj-raw>

      <!-- Help center footer — both paths show this -->
      <mj-section background-color="#fff" padding="0 10px 20px 10px">
        <mj-column>
          <mj-text font-size="14px" line-height="20px" padding="0 15px">
            For assistance managing your subscription, please visit
            <a href="https://bitwarden.com/help/update-billing-info" class="link">
              <strong>the Help Center</strong>
            </a>
            or
            <a href="https://bitwarden.com/contact/" class="link">
              <strong>contact Bitwarden Customer Support</strong>
            </a>.
          </mj-text>
        </mj-column>
      </mj-section>
    </mj-wrapper>

    <!-- Learn More -->
    <mj-wrapper padding="0px 20px 10px 20px">
      <mj-bw-learn-more-footer />
    </mj-wrapper>

    <!-- Footer -->
    <mj-include path="../../components/footer.mjml" />
  </mj-body>
</mjml>
```

Key conversion decisions:
- **Text-level conditionals** (`{{#if}}` choosing between paragraphs) stay inside `<mj-text>` — no `mj-raw` needed
- **Structural conditionals** (entire sections/buttons appearing or disappearing) use `<mj-raw>{{#if ...}}</mj-raw>` wrappers
- **Nested conditionals** (the `{{#if Items}}` + `{{#unless (eq ...)}}` combination) use stacked `mj-raw` tags
- **`{{#each}}`** inside text content stays inside `<mj-text>` — no wrapper needed
- The help center footer text was identical in both `{{#if}}` and `{{#unless}}` branches in the original — consolidated into a single block
- The `{{else}}` empty block from the original (which rendered nothing) is dropped entirely

---

## 5. Decision Framework: When to Use `mj-raw` vs. Inline Logic

```
Is the Handlebars block helper wrapping MJML tags (mj-section, mj-button, etc.)?
  YES → Use <mj-raw>{{#if ...}}</mj-raw> ... <mj-raw>{{/if}}</mj-raw>
  NO  → Is it inside an "ending tag" component (mj-text, mj-button, mj-table, mj-raw)?
    YES → Place the helper directly in the content (no wrapping needed)
    NO  → Use <mj-raw>
```

**"Ending tag" components** (accept raw HTML, do not parse MJML inside them):
`mj-text`, `mj-button`, `mj-table`, `mj-raw`, `mj-navbar-link`, `mj-accordion-title`, `mj-accordion-text`, `mj-social-element`

---

## 6. Style Migration

### Before: Every Element Carries Full Inline Styles

```html
<td style="font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
    box-sizing: border-box; font-size: 16px; color: #333; line-height: 25px;
    margin: 0; -webkit-font-smoothing: antialiased; padding: 0 0 10px;
    -webkit-text-size-adjust: none; text-align: left;" valign="top">
```

### After: Defaults in `head.mjml`, Overrides as Attributes

The shared `head.mjml` defines:
```xml
<mj-attributes>
  <mj-all font-family="'Helvetica Neue', Helvetica, Arial, sans-serif" font-size="16px" />
  <mj-button background-color="#175ddc" />
  <mj-text color="#1B2029" />
  <mj-body background-color="#e6e9ef" width="660px" />
</mj-attributes>
<mj-style inline="inline">
  .link { text-decoration: none; color: #175ddc; font-weight: 600; }
</mj-style>
```

In templates, only specify what **differs** from defaults:
```xml
<!-- Standard text — inherits everything from head.mjml -->
<mj-text>Your subscription will renew soon.</mj-text>

<!-- Override only what's different -->
<mj-text font-size="32px" font-weight="bold">{{usd AmountDue}}</mj-text>

<!-- Link styling uses the shared .link class -->
<a href="..." class="link"><strong>the Help Center</strong></a>
```

### Reusable Style Classes

Define via `mj-class` in `head.mjml`:
```xml
<mj-class name="heading" font-size="24px" font-weight="600" line-height="32px" />
```

Apply via the `mj-class` attribute:
```xml
<mj-text mj-class="heading">Your subscription will renew soon</mj-text>
```

---

## 7. MJML Features That Simplify Development

### Responsive Design (Free)
MJML generates responsive HTML automatically. No need to write `@media` queries for column stacking. The old templates required hand-coded media queries in each layout partial.

### `mj-include` (Replaces Handlebars Partials for Structure)
```xml
<mj-include path="../../components/head.mjml" />
<mj-include path="../../components/footer.mjml" />
```
This replaces the `{{#>FullHtmlLayout}}` block partial pattern for layout composition. `mj-include` is resolved at MJML compile time.

### Custom Components (Replace Repeated Patterns)
Bitwarden already has: `mj-bw-hero`, `mj-bw-simple-hero`, `mj-bw-icon-row`, `mj-bw-learn-more-footer`. These are JS classes registered in `.mjmlconfig`. Consider creating new ones for patterns that repeat across templates (e.g., a "help center" footer link block).

### `mj-social` (Replaces Social Icon Tables)
The hand-built social icon tables (7 `<td>` elements with inline styles) are replaced by `mj-social` + `mj-social-element` in `footer.mjml`.

### `mj-button` (Replaces Button Table Hacks)
The old "table-in-table" button pattern with VML fallbacks is replaced by a single `<mj-button>` tag. MJML handles Outlook rendering internally.

---

## 8. Testing Strategy

### 1. Visual Parity Check
- Compile the original `.html.hbs` with sample data via the `IMailer` test harness
- Compile the new `.mjml` → `.html.hbs` via `npm run build:hbs`, then render with the same sample data
- Compare both outputs visually in multiple email clients (or use a tool like Litmus/Email on Acid)

### 2. Handlebars Variable Verification
- Ensure every `{{ variable }}` from the original template appears in the MJML version
- Ensure custom helpers (`eq`, `date`, `usd`) are used identically
- Run through the `IMailer` integration with the ViewModel to verify all variables resolve

### 3. Conditional Branch Coverage
For templates with logic (`{{#if}}`, `{{#unless}}`, `{{#each}}`):
- Test each branch by providing different ViewModel data
- For the ProviderInvoiceUpcoming example, test at minimum:
  - `CollectionMethod = "send_invoice"` with items
  - `CollectionMethod = "send_invoice"` without items
  - `CollectionMethod != "send_invoice"` with `HasPaymentMethod = true` and items
  - `CollectionMethod != "send_invoice"` with `HasPaymentMethod = false` and no items

### 4. Build Pipeline Verification
```shell
cd src/Core/MailTemplates/Mjml
npm ci
npm run build:hbs        # Compile to .html.hbs
npm run build:minify     # Compile minified .html.hbs
```
- Verify no MJML validation errors
- Verify the `.html.hbs` output contains the Handlebars expressions intact
- Copy artifacts to the correct directory per the README

### 5. Responsive Testing
- Open the compiled HTML in a browser and resize to mobile width (~375px)
- Verify columns stack correctly
- Verify images scale appropriately
- Verify buttons remain tappable at mobile size

---

## 9. Edge Cases and Gotchas

### Triple-Stash URLs
Always use `{{{Url}}}` (triple-stash) for URLs in `href` attributes. Double-stash `{{Url}}` would HTML-encode `&` characters in query strings, breaking links. This is unchanged from the current Handlebars behavior.

### Handlebars Comments in CSS
The old layouts use `{{! Fix for Apple Mail }}` inside `<style>` blocks. In MJML, CSS goes into `<mj-style>` which passes through to a standard `<style>` tag. Handlebars comments in CSS will be stripped at runtime. If you need CSS comments, use standard `/* */` syntax. If you need Handlebars expressions in CSS (like the Apple Mail fix), place them in a `<mj-raw position="file-start">` or inside `<mj-style>`.

### `mj-raw` Placement Rules
`mj-raw` can be placed as a direct child of:
- `mj-body`
- `mj-wrapper`
- `mj-section`
- `mj-column`
- `mj-head`

It **cannot** be placed inside ending-tag components (`mj-text`, etc.)—but you don't need it there since those components already pass HTML through.

### Minification and Handlebars
When using `npm run build:minify`, the HTML minifier may strip or mangle Handlebars expressions that look like HTML (particularly `{{#if (gt value 0)}}`). If you encounter this, wrap the expression in `<!-- htmlmin:ignore -->`:
```xml
<mj-raw><!-- htmlmin:ignore -->{{#if (gt value 0)}}<!-- htmlmin:ignore --></mj-raw>
```

### Empty Conditional Branches
The original ProviderInvoiceUpcoming has an `{{else}}` block that renders nothing (empty whitespace). Don't replicate this—drop the empty branch entirely or restructure as `{{#unless}}`.

### `.text.hbs` Files
MJML does not affect plain-text templates. Continue authoring `.text.hbs` files by hand, identically to the current process.

### CSS-Class vs MJ-Class
- `css-class="foo"` adds a CSS class to the **generated HTML output** — useful for targeting elements with `<mj-style>` rules
- `mj-class="foo"` applies **MJML-level attribute presets** defined in `<mj-attributes>` — resolved at MJML compile time, not in CSS

---

## 10. Conversion Checklist

For each email template being converted:

- [ ] Identify the layout partial used (`Full`, `ProviderFull`, `FullUpdated`, etc.)
- [ ] Map it to the correct MJML skeleton and components
- [ ] Extract all Handlebars variables and helpers used
- [ ] Identify all conditional logic branches
- [ ] Classify each conditional as "text-level" (inside `mj-text`) or "structural" (needs `mj-raw`)
- [ ] Write the `.mjml` file in the appropriate `emails/` subdirectory
- [ ] Run `npm run build:hbs` and verify no compilation errors
- [ ] Inspect the compiled `.html.hbs` to confirm Handlebars expressions are intact
- [ ] Test all conditional branches with sample ViewModel data
- [ ] Create/update the `.text.hbs` file (manual, not from MJML)
- [ ] Copy the minified `.html.hbs` artifact to the correct ViewModel directory
- [ ] Run the full email through `IMailer` integration test

---

## Sources

- [MJML Official Documentation](https://documentation.mjml.io/)
- [Handlebars Official Guide](https://handlebarsjs.com/guide/)
- [MJML Templating Feature Discussion (Issue #1630)](https://github.com/mjmlio/mjml/issues/1630)
- [Building Templated Emails with MJML (Thoughtbot)](https://thoughtbot.com/blog/building-templated-emails-with-mjml)
- [Creating AWS Email Templates with Handlebars and MJML](https://blog.elmah.io/creating-aws-email-templates-with-handlebars-js-and-mjml/)
- [mjml-handlebars Library](https://github.com/edus44/mjml-handlebars)
- [Handlebars-MJML VS Code Extension](https://marketplace.visualstudio.com/items?itemName=rbremont.vscode-handlebars-mjml)
- Bitwarden server repo: `src/Core/MailTemplates/Mjml/README.md`, `src/Core/Platform/Mail/README.md`
