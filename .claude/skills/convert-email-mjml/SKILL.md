---
name: convert-email-mjml
description: Convert a Handlebars email template to MJML format. Handles conversion, compilation, and artifact placement.
disable-model-invocation: false
user-invocable:true
---

# Handlebars-to-MJML Email Conversion

Convert an existing Handlebars email template to the new MJML-based email system.

## Input

The user will provide a path to an existing Handlebars email template:
- Example: `src/Core/MailTemplates/Handlebars/Billing/SomeEmail.html.hbs`
- Or they may just provide the template name

The path provided is: **$ARGUMENTS**

## Required Context

Before starting, read these files to understand the conversion process:

1. **@docs/plans/email-migrations/handlebars-to-mjml-transition.md** - Complete conversion guide
2. **@src/Core/MailTemplates/Mjml/README.md** - MJML build process and structure

## Conversion Process

### Step 1: Read and Analyze the Original Template

- Read the original Handlebars template
- Identify which layout partial it uses (`FullHtmlLayout`, `ProviderFull`, etc.)
- Extract all Handlebars variables (e.g., `{{Url}}`, `{{Name}}`)
- Identify conditional logic (`{{#if}}`, `{{#unless}}`, `{{#each}}`)
- Note any custom Handlebars helpers used (`eq`, `date`, `usd`, etc.)

### Step 2: Convert to MJML

Follow the mapping guide from the transition doc:

**Basic Structure:**
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
          <!-- Template content here -->
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

**Key Conversions:**
- `{{#>FullHtmlLayout}}` → Standard MJML skeleton with `mj-include` for logo
- `{{#>ProviderFull}}` → Use `<mj-bw-simple-hero />` or `<mj-bw-hero />` instead of logo
- Table rows → `<mj-section>` + `<mj-column>` + `<mj-text>`
- Inline-styled `<a>` buttons → `<mj-button href="...">`
- `<br />` spacers → `<mj-spacer height="Xpx" />` or padding attributes

**Default Styles (do NOT copy from Handlebars):**
`head.mjml` already sets these globally — do not add them as explicit attributes on `<mj-text>` or other elements unless you are intentionally overriding the default:
- `font-family`: `'Helvetica Neue', Helvetica, Arial, sans-serif`
- `font-size`: `16px`
- `color` (text): `#1B2029`
- `mj-button` background: `#175ddc`

Only add explicit style attributes when the value differs from these defaults.

**Handlebars Logic:**
- Simple variables inside content → Leave as-is (e.g., `{{Name}}`, `{{{Url}}}`)
- Conditionals wrapping MJML tags → Wrap with `<mj-raw>{{#if ...}}</mj-raw>` and `<mj-raw>{{/if}}</mj-raw>`
- Conditionals inside text → Place directly in `<mj-text>` content

**File Location:**
- Original: `src/Core/MailTemplates/Handlebars/{Category}/{EmailName}.html.hbs`
- New MJML: `src/Core/MailTemplates/Mjml/emails/{Category}/{EmailName}.mjml`

Write the new MJML file to the correct location.

### Step 3: Compile and Test

1. **Compile MJML to HTML:**
   ```bash
   cd src/Core/MailTemplates/Mjml
   npm run build:hbs
   ```

2. **Verify compilation:**
   - Check that compilation succeeded with no errors
   - Read the output file: `src/Core/MailTemplates/Mjml/out/{Category}/{EmailName}.html.hbs`
   - Verify all Handlebars expressions are preserved (e.g., `{{{Url}}}`, `{{#if}}`)
   - Confirm responsive media queries are generated
   - Check email client compatibility code (Outlook VML) is included

### Step 4: Locate the ViewModel

Find the corresponding C# ViewModel class:

1. Search for files matching the email name pattern:
   ```bash
   find src/Core -name "*{EmailName}*Model.cs"
   ```

2. Or search for references to the email template:
   ```bash
   grep -r "{EmailName}" src/Core --include="*.cs"
   ```

3. The ViewModel is typically located at:
   - `src/Core/Models/Mail/{Category}/{EmailName}Model.cs`

### Step 5: Create Folder and Copy Compiled Artifact

Each new MJML-based email gets its **own dedicated subfolder** alongside the ViewModel (following the pattern of `Billing/Renewal/Premium/`, `Billing/Renewal/Families2019Renewal/`, etc.).

1. **Create the subfolder:**
   ```
   src/Core/Models/Mail/{Category}/{EmailName}/
   ```

2. **Copy the compiled artifact** and rename it to match the View class name:
   - **From:** `src/Core/MailTemplates/Mjml/out/{Category}/{EmailName}.html.hbs`
   - **To:** `src/Core/Models/Mail/{Category}/{EmailName}/{EmailName}MailView.html.hbs`

3. **Move the existing text template** to the same folder, renamed to match the View class:
   - **From:** `src/Core/MailTemplates/Handlebars/{Category}/{EmailName}.text.hbs`
   - **To:** `src/Core/Models/Mail/{Category}/{EmailName}/{EmailName}MailView.text.hbs`
   - **Do NOT modify its content** — copy it exactly as-is; text templates are hand-authored and not generated

The folder and all three files (`.cs`, `.html.hbs`, `.text.hbs`) must share the same directory so the `IMailer` system can discover the templates at runtime.

### Step 6: Migrate from HandlebarsMailService to IMailer

After the MJML template is created, you must migrate any code that uses the old `IMailService` / `HandlebarsMailService` to the new `IMailer` approach.

#### Step 6.1: Identify the Old Mail Service Method

Search for the method in `HandlebarsMailService` (or `IMailService` interface) that sends this email:

```bash
grep -n "Send.*{EmailName}" src/Core/Platform/Mail/IMailService.cs
grep -n "Send.*{EmailName}" src/Core/Platform/Mail/HandlebarsMailService.cs
```

**Example:** For `BusinessUnitConversionInvite`, the method is:
```csharp
// In IMailService.cs
Task SendBusinessUnitConversionInviteAsync(Organization organization, string token, string email);

// In HandlebarsMailService.cs (line ~1160)
public async Task SendBusinessUnitConversionInviteAsync(Organization organization, string token, string email)
{
    var message = CreateDefaultMessage("Set Up Business Unit", email);
    var model = new BusinessUnitConversionInviteModel
    {
        WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
        SiteName = _globalSettings.SiteName,
        OrganizationId = organization.Id.ToString(),
        Email = WebUtility.UrlEncode(email),
        Token = WebUtility.UrlEncode(token)
    };
    await AddMessageContentAsync(message, "Billing.BusinessUnitConversionInvite", model);
    message.Category = "BusinessUnitConversionInvite";
    await _mailDeliveryService.SendEmailAsync(message);
}
```

#### Step 6.2: Update the ViewModel and Create the Mail Class

The old ViewModel inherits from `BaseMailModel`. Update it to inherit from `BaseMailView` and add the Mail class in the **same file** (following the pattern in `BaseMail.cs`):

**Before:**
```csharp
using Bit.Core.Models.Mail;

namespace Bit.Core.Models.Mail.Billing;

public class BusinessUnitConversionInviteModel : BaseMailModel
{
    public string OrganizationId { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }

    public string Url =>
        $"{WebVaultUrl}/providers/setup-business-unit?organizationId={OrganizationId}&email={Email}&token={Token}";
}
```

**After (both classes in one file, new subfolder):**
```csharp
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.BusinessUnitConversionInvite;

#nullable enable

/// <summary>
/// Email sent to invite users to set up a Business Unit Portal.
/// </summary>
public class BusinessUnitConversionInviteMail : BaseMail<BusinessUnitConversionInviteMailView>
{
    public override string Subject { get; set; } = "Set Up Business Unit";
}

/// <summary>
/// View model for Business Unit Conversion Invite email template.
/// </summary>
public class BusinessUnitConversionInviteMailView : BaseMailView
{
    public required string OrganizationId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
    public required string WebVaultUrl { get; init; }

    public string Url =>
        $"{WebVaultUrl}/providers/setup-business-unit?organizationId={OrganizationId}&email={Email}&token={Token}";
}
```

**Key changes:**
- **New dedicated subfolder**: `src/Core/Models/Mail/{Category}/{EmailName}/` (e.g., `Billing/BusinessUnitConversionInvite/`)
- **Both classes in the same file** (follows existing Renewal email pattern)
- **View class name** uses `*MailView` suffix (e.g., `BusinessUnitConversionInviteMailView`)
- **Namespace** includes the subfolder (e.g., `Bit.Core.Models.Mail.Billing.BusinessUnitConversionInvite`)
- View inherits from `BaseMailView` instead of `BaseMailModel`
- Enable nullable reference types (`#nullable enable`)
- Properties use `required` and `init` for immutability
- `WebVaultUrl` is now a required property (no longer inherited from BaseMailModel)
- Remove `SiteName` if not used (BaseMailView only provides `CurrentYear`)
- Mail class defines `Subject` (Category is optional — only add if the old method set one explicitly)

**File location:**
- Old: `src/Core/Models/Mail/Billing/BusinessUnitConversionInviteModel.cs`
- New: `src/Core/Models/Mail/Billing/BusinessUnitConversionInvite/BusinessUnitConversionInviteMailView.cs`

#### Step 6.3: Place Template Files in the New Subfolder

Template files must match the **View class name** and live in the **same subfolder** as the `.cs` file:

```
src/Core/Models/Mail/{Category}/{EmailName}/
├── {EmailName}MailView.cs
├── {EmailName}MailView.html.hbs   ← compiled MJML artifact (renamed)
└── {EmailName}MailView.text.hbs   ← copied from Handlebars source
```

This is exactly what Step 5 produces. If Step 5 was followed, no additional renaming is needed here.

#### Step 6.4: Find and Replace All Invocations

Search for all places where the old mail service method is called:

```bash
grep -rn "SendBusinessUnitConversionInviteAsync" src/ --include="*.cs"
```

**Old approach (HandlebarsMailService):**
```csharp
public class SomeService
{
    private readonly IMailService _mailService;

    public SomeService(IMailService mailService)
    {
        _mailService = mailService;
    }

    public async Task InviteToBusinessUnit(Organization org, string email, string token)
    {
        await _mailService.SendBusinessUnitConversionInviteAsync(org, token, email);
    }
}
```

**New approach (IMailer):**
```csharp
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Models.Mail.Billing;
using System.Net;

public class SomeService
{
    private readonly IMailer _mailer;
    private readonly IGlobalSettings _globalSettings;

    public SomeService(IMailer mailer, IGlobalSettings globalSettings)
    {
        _mailer = mailer;
        _globalSettings = globalSettings;
    }

    public async Task InviteToBusinessUnit(Organization org, string email, string token)
    {
        var mail = new BusinessUnitConversionInviteMail
        {
            ToEmails = [email],
            View = new BusinessUnitConversionInviteView
            {
                OrganizationId = org.Id.ToString(),
                Email = WebUtility.UrlEncode(email),
                Token = WebUtility.UrlEncode(token),
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash
            }
        };

        await _mailer.SendEmail(mail);
    }
}
```

**Key differences:**
- Inject `IMailer` instead of `IMailService`
- Inject `IGlobalSettings` to get `WebVaultUrl` (no longer on ViewModel base class)
- Create mail object with `ToEmails` and `View` properties
- Call `_mailer.SendEmail(mail)` instead of `_mailService.Send*Async(...)`
- ViewModel instantiation is explicit (better type safety)

#### Step 6.5: Update Dependency Injection

Ensure the consuming service has access to `IMailer`. In most cases, this is already registered globally.

If not registered, add to `ServiceCollectionExtensions.cs`:
```csharp
services.AddMailer(); // Registers IMailer and IMailRenderer
```

#### Step 6.6: Remove or Deprecate Old Method

Once all invocations are migrated:

1. **Mark the old method as obsolete** in `IMailService`:
   ```csharp
   [Obsolete("Use IMailer with BusinessUnitConversionInviteMail instead")]
   Task SendBusinessUnitConversionInviteAsync(Organization organization, string token, string email);
   ```

2. **Do NOT delete** the method from `HandlebarsMailService` yet—it may be in use by other environments
3. File a follow-up task to remove the method after a deprecation period

#### Step 6.7: Update Tests

Find all tests for services that were updated in Step 6.4 and replace `IMailService` mocks with `IMailer` mocks.

**Find affected test files:**
```bash
grep -rn "Send{EmailName}Async\|IMailService" test/ --include="*.cs" -l
```

**Old test pattern (mocking IMailService):**
```csharp
public class SomeServiceTests
{
    private readonly IMailService _mailService;
    private readonly SomeService _sut;

    public SomeServiceTests()
    {
        _mailService = Substitute.For<IMailService>();
        _sut = new SomeService(_mailService);
    }

    [Fact]
    public async Task InviteToBusinessUnit_SendsEmail()
    {
        // Act
        await _sut.InviteToBusinessUnit(org, email, token);

        // Assert
        await _mailService.Received(1)
            .SendBusinessUnitConversionInviteAsync(org, token, email);
    }
}
```

**New test pattern (mocking IMailer):**
```csharp
public class SomeServiceTests
{
    private readonly IMailer _mailer;
    private readonly IGlobalSettings _globalSettings;
    private readonly SomeService _sut;

    public SomeServiceTests()
    {
        _mailer = Substitute.For<IMailer>();
        _globalSettings = Substitute.For<IGlobalSettings>();
        _sut = new SomeService(_mailer, _globalSettings);
    }

    [Fact]
    public async Task InviteToBusinessUnit_SendsEmail()
    {
        // Arrange
        var vaultUrl = "https://vault.example.com/#";
        _globalSettings.BaseServiceUri.VaultWithHash.Returns(vaultUrl);

        // Act
        await _sut.InviteToBusinessUnit(org, email, token);

        // Assert
        await _mailer.Received(1).SendEmail(
            Arg.Is<BusinessUnitConversionInviteMail>(m =>
                m.ToEmails.Contains(email) &&
                m.View.OrganizationId == org.Id.ToString() &&
                m.View.WebVaultUrl == vaultUrl));
    }
}
```

**Key changes in tests:**
- Replace `IMailService` field and constructor arg with `IMailer`
- Add `IGlobalSettings` mock if the service now requires it (set up `VaultWithHash` return value)
- Replace `Received(1).SendXxxAsync(...)` with `Received(1).SendEmail(Arg.Is<XxxMail>(...))`
- Use `Arg.Is<>` to assert the `ToEmails` list and critical `View` properties
- Update `using` directives: add the new mail namespace, remove old `IMailService` import if no longer used
- If the test class uses AutoFixture/`[Theory]` with `[BitAutoData]`, update `[Frozen]` attributes accordingly:
  ```csharp
  // Old
  [BitAutoData] SutProvider<SomeService> sutProvider, ...
  // sutProvider.GetDependency<IMailService>().Received(1).SendXxxAsync(...)

  // New
  [BitAutoData] SutProvider<SomeService> sutProvider, ...
  // sutProvider.GetDependency<IMailer>().Received(1).SendEmail(Arg.Is<XxxMail>(...))
  ```

### Step 7: Verification Checklist

Confirm the following before completing:

- [ ] MJML file created in correct location under `emails/` directory
- [ ] All Handlebars variables from original template are preserved
- [ ] Conditional logic (`{{#if}}`, `{{#unless}}`, `{{#each}}`) is properly wrapped with `<mj-raw>` where needed
- [ ] Custom helpers (`eq`, `date`, `usd`) are used identically to original
- [ ] MJML compilation completed with no errors
- [ ] Compiled `.html.hbs` contains Handlebars expressions intact
- [ ] Compiled artifact copied to location next to ViewModel
- [ ] ViewModel location identified and confirmed
- [ ] ViewModel updated to inherit from `BaseMailView`
- [ ] Mail class created inheriting from `BaseMail<TView>`
- [ ] Template files renamed to match View class name
- [ ] All invocations of old mail service method migrated to IMailer
- [ ] Tests updated: `IMailService` mocks replaced with `IMailer`, assertions updated to `SendEmail(Arg.Is<XxxMail>(...))`
- [ ] Old method marked as obsolete (if appropriate)

## Output Summary

Provide a clear summary including:

1. **Original template**: Full path to source Handlebars file
2. **New MJML file**: Location of created MJML source
3. **Compiled output**: Location of `.html.hbs` artifact
4. **ViewModel**: Location of associated ViewModel class
5. **Key conversions**: Summary of major structural changes
6. **Variables preserved**: List of Handlebars expressions carried over
7. **Next steps**: Any manual testing or integration work needed

## Important Notes

- **Do NOT create or modify text email templates** (`.text.hbs` files) — they are hand-authored separately; only copy the existing one to the new location
- **Do NOT copy inline styles from Handlebars templates**: `head.mjml` defines global defaults (`font-size: 16px`, `color: #1B2029`, `font-family`). Use plain `<mj-text>` without style attributes unless overriding a default.
- **Triple-stash URLs**: Always use `{{{Url}}}` for URLs to prevent HTML encoding
- **Validation level**: Build uses `strict` validation; `mj-raw` must be in legal positions
- **No backwards compatibility needed**: This is a new pipeline, not a replacement
- **Style inheritance**: Most styles come from `head.mjml` - only override what differs

## Reference Files

- Conversion guide: `docs/plans/email-migrations/handlebars-to-mjml-transition.md`
- MJML README: `src/Core/MailTemplates/Mjml/README.md`
- Platform Mail README: `src/Core/Platform/Mail/README.md`
- MailTemplates README: `src/Core/MailTemplates/README.md`
