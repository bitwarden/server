# Mailer

The Mailer feature provides a structured, type-safe approach to sending emails in the Bitwarden server application. It
uses Handlebars templates to render both HTML and plain text email content.

## Architecture

The Mailer system consists of four main components:

1. **IMailer** - Service interface for sending emails
2. **BaseMail<TView>** - Abstract base class defining email metadata (recipients, subject, category)
3. **BaseMailView** - Abstract base class for email template view models
4. **IMailRenderer** - Internal interface for rendering templates (implemented by `HandlebarMailRenderer`)

## How It Works

1. Define a view model that inherits from `BaseMailView` with properties for template data
2. Create Handlebars templates (`.html.hbs` and `.text.hbs`) as embedded resources, preferably using the MJML pipeline,
   `/src/Core/MailTemplates/Mjml`.
3. Define an email class that inherits from `BaseMail<TView>` with metadata like subject
4. Use `IMailer.SendEmail()` to render and send the email

## Creating a New Email

### Step 1: Define the Email & View Model

Create a class that inherits from `BaseMailView`:

```csharp
using Bit.Core.Platform.Mailer;

namespace MyApp.Emails;

public class WelcomeEmailView : BaseMailView
{
    public required string UserName { get; init; }
    public required string ActivationUrl { get; init; }
}

public class WelcomeEmail : BaseMail<WelcomeEmailView>
{
    public override string Subject => "Welcome to Bitwarden";
}
```

### Step 2: Create Handlebars Templates

Create two template files as embedded resources next to your view model. **Important**: The file names must be located
directly next to the `ViewClass` and match the name of the view.

**WelcomeEmailView.html.hbs** (HTML version):

```handlebars
<h1>Welcome, {{ UserName }}!</h1>
<p>Thank you for joining Bitwarden.</p>
<p>
    <a href="{{ ActivationUrl }}">Activate your account</a>
</p>
<p><small>&copy; {{ CurrentYear }} Bitwarden Inc.</small></p>
```

**WelcomeEmailView.text.hbs** (plain text version):

```handlebars
Welcome, {{ UserName }}!

Thank you for joining Bitwarden.

Activate your account: {{ ActivationUrl }}

� {{ CurrentYear }} Bitwarden Inc.
```

**Important**: Template files must be configured as embedded resources in your `.csproj`:

```xml

<ItemGroup>
    <EmbeddedResource Include="**\*.hbs" />
</ItemGroup>
```

### Step 3: Send the Email

Inject `IMailer` and send the email, this may be done in a service, command or some other application layer.

```csharp
public class SomeService
{
    private readonly IMailer _mailer;

    public SomeService(IMailer mailer)
    {
        _mailer = mailer;
    }

    public async Task SendWelcomeEmailAsync(string email, string userName, string activationUrl)
    {
        var mail = new WelcomeEmail
        {
            ToEmails = [email],
            View = new WelcomeEmailView
            {
                UserName = userName,
                ActivationUrl = activationUrl
            }
        };

        await _mailer.SendEmail(mail);
    }
}
```

## Advanced Features

### Multiple Recipients

Send to multiple recipients by providing multiple email addresses:

```csharp
var mail = new WelcomeEmail
{
    ToEmails = ["user1@example.com", "user2@example.com"],
    View = new WelcomeEmailView { /* ... */ }
};
```

### Bypass Suppression List

For critical emails like account recovery or email OTP, you can bypass the suppression list:

```csharp
public class PasswordResetEmail : BaseMail<PasswordResetEmailView>
{
    public override string Subject => "Reset Your Password";
    public override bool IgnoreSuppressList => true; // Use with caution
}
```

**Warning**: Only use `IgnoreSuppressList = true` for critical account recovery or authentication emails.

### Email Categories

Optionally categorize emails for processing at the upstream email delivery service:

```csharp
public class MarketingEmail : BaseMail<MarketingEmailView>
{
    public override string Subject => "Latest Updates";
    public string? Category => "marketing";
}
```

## Built-in View Properties

All view models inherit from `BaseMailView`, which provides:

- **CurrentYear** - The current UTC year (useful for copyright notices)

```handlebars

<footer>&copy; {{ CurrentYear }} Bitwarden Inc.</footer>
```

## Template Naming Convention

Templates must follow this naming convention:

- HTML template: `{ViewModelFullName}.html.hbs`
- Text template: `{ViewModelFullName}.text.hbs`

For example, if your view model is `Bit.Core.Auth.Models.Mail.VerifyEmailView`, the templates must be:

- `Bit.Core.Auth.Models.Mail.VerifyEmailView.html.hbs`
- `Bit.Core.Auth.Models.Mail.VerifyEmailView.text.hbs`

## Dependency Injection

Register the Mailer services in your DI container using the extension method:

```csharp
using Bit.Core.Platform.Mailer;

services.AddMailer();
```

Or manually register the services:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

services.TryAddSingleton<IMailRenderer, HandlebarMailRenderer>();
services.TryAddSingleton<IMailer, Mailer>();
```

## Performance Notes

- **Template caching** - `HandlebarMailRenderer` automatically caches compiled templates
- **Lazy initialization** - Handlebars is initialized only when first needed
- **Thread-safe** - The renderer is thread-safe for concurrent email rendering
