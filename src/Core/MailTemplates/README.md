Email templating
================

We use MJML to generate the HTML that our mail services use to send emails to users. To accomplish this, we use different file types depending on which part of the email generation process we're working with.

# File Types

## `*.html.hbs`
These are the compiled HTML email templates that serve as the foundation for all HTML emails sent by the Bitwarden platform. They are generated from MJML source files and enhanced with Handlebars templating capabilities.

### Generation Process
- **Source**: Built from `*.mjml` files in the `./mjml` directory using the MJML build pipeline
- **Build Tool**: Generated via PowerShell build script (`build.ps1`) or npm scripts
- **Output**: Cross-client compatible HTML with embedded CSS for maximum email client support
- **Template Engine**: Enhanced with Handlebars syntax for dynamic content injection

### Handlebars Integration
The templates use Handlebars templating syntax for dynamic content replacement:

```html
<!-- Example Handlebars usage -->
<h1>Welcome {{userName}}!</h1>
<p>Your organization {{organizationName}} has invited you to join.</p>
<a href="{{actionUrl}}">Accept Invitation</a>
```

**Variable Types:**
- **Simple Variables**: `{{userName}}`, `{{email}}`, `{{organizationName}}`

### Email Service Integration
The `IMailService` consumes these templates through the following process:

1. **Template Selection**: Service selects appropriate `.html.hbs` template based on email type
2. **Model Binding**: View model properties are mapped to Handlebars variables
3. **Compilation**: Handlebars engine processes variables and generates final HTML

### Development Guidelines

**Variable Naming:**
- Use camelCase for consistency: `{{userName}}`, `{{organizationName}}`
- Prefix URLs with descriptive names: `{{actionUrl}}`, `{{logoUrl}}`

**Testing Considerations:**
- Verify Handlebars variable replacement with actual view model data
- Ensure graceful degradation when variables are missing or null, if necessary
- Validate HTML structure and accessibility compliance

## `*.txt.hbs`
These files provide plain text versions of emails and are essential for email accessibility and deliverability. They serve several important purposes:

### Purpose and Usage
- **Accessibility**: Screen readers and assistive technologies often work better with plain text versions
- **Email Client Compatibility**: Some email clients prefer or only display plain text versions
- **Fallback Content**: When HTML rendering fails, the plain text version ensures the message is still readable

### Structure
Plain text email templates use the same Handlebars syntax (`{{variable}}`) as HTML templates for dynamic content replacement. They should:

- Contain the core message content without HTML formatting
- Use line breaks and spacing for readability
- Include all important links as full URLs
- Maintain logical content hierarchy using spacing and simple text formatting

### Email Service Integration
The `IMailService` automatically uses both versions when sending emails:
- The HTML version (from `*.html.hbs`) provides rich formatting and styling
- The plain text version (from `*.txt.hbs`) serves as the text alternative
- Email clients can choose which version to display based on user preferences and capabilities

### Development Guidelines
- Always create a corresponding `*.txt.hbs` file for each `*.html.hbs` template
- Keep the content concise but complete - include all essential information from the HTML version
- Test plain text templates to ensure they're readable and convey the same message

## `*.mjml`
This is a templating language we use to increase efficiency when creating email content. See the readme within the `./mjml` directory for more comprehensive information.
