# MJML email templating

This directory contains MJML templates for emails. MJML is a markup language designed to reduce the pain of coding responsive email templates. Component based development features in MJML improve code quality and reusability.

MJML stands for MailJet Markup Language.

## Implementation considerations

These `MJML` templates are compiled into HTML which will then be further consumed by our Handlebars mail service. We can continue to use this service to assign values from our View Models. This leverages the existing infrastructure. It also means we can continue to use the double brace (`{{}}`) syntax within MJML since Handlebars can be used to assign values to those `{{variables}}`.

There is no change on how we interact with our view models.

There is an added step where we compile `*.mjml` to `*.html.hbs`. `*.html.hbs` is the format we use so the handlebars service can apply the variables. This build pipeline process is in progress and may need to be manually done at times.

### `*.txt.hbs`

There is no change to how we create the `txt.hbs`. MJML does not impact how we create these artifacts.

## Building `MJML` files

```shell
npm ci

# Build *.html to ./out directory
npm run build

# To build on changes to *.mjml and *.js files, new files will not be tracked, you will need to run again
npm run build:watch

# Build *.html.hbs to ./out directory
npm run build:hbs

# Build minified *.html.hbs to ./out directory
npm run build:minify

# apply prettier formatting
npm run prettier
```

## Development

MJML supports components and you can create your own components by adding them to `.mjmlconfig`. Components are simple JavaScript that return MJML markup based on the attributes assigned, see components/mj-bw-hero.js. The markup is not a proper object, but contained in a string.

When using MJML templating you can use the above [commands](#building-mjml-files) to compile the template and view it in a web browser.

Not all MJML tags have the same attributes, it is highly recommended to review the documentation on the official MJML website to understand the usages of each of the tags.

### Recommended development - IMailService

#### Mjml email template development

1. create `cool-email.mjml` in appropriate team directory
2. run `npm run build:watch`
3. view compiled `HTML` output in a web browser
4. iterate -> while `build:watch`'ing you should be able to refresh the browser page after the mjml/js re-compile to see the changes

#### Testing with `IMailService`

After the email is developed from the [initial step](#mjml-email-template-development) make sure the email `{{variables}}` are populated properly by running it through an `IMailService` implementation.

1. run `npm run build:hbs`
2. copy built `*.html.hbs` files from the build directory to a location the mail service can consume them
  1. all files in the `Core/MailTemplates/Mjml/out` directory can be copied to the `src/Core/MailTemplates/Handlebars/MJML` directory. If a shared component is modified it is important to copy and overwrite all files in that directory to capture
  changes in the `*.html.hbs`.
3. run code that will send the email

The minified `html.hbs` artifacts are deliverables and must be placed into the correct `src/Core/MailTemplates/Handlebars/` directories in order to be used by `IMailService` implementations, see 2.1 above.

### Recommended development - IMailer

TBD - PM-26475

### Custom tags

There is currently a `mj-bw-hero` tag you can use within your `*.mjml` templates. This is a good example of how to create a component that takes in attribute values allowing us to be more DRY in our development of emails. Since the attribute's input is a string we are able to define whatever we need into the component, in this case `mj-bw-hero`.

In order to view the custom component you have written you will need to include it in the `.mjmlconfig` and reference it in an `mjml` template file.

```html
<!-- Custom component implementation-->
<mj-bw-hero
  img-src="https://assets.bitwarden.com/email/v1/business.png"
  title="Verify your email to access this Bitwarden Send"
/>
```

Attributes in Custom Components are defined by the developer. They can be required or optional depending on implementation. See the official MJML documentation for more information.

```js
static allowedAttributes = {
  "img-src": "string", // REQUIRED: Source for the image displayed in the right-hand side of the blue header area
  title: "string", // REQUIRED: large text stating primary purpose of the email
  "button-text": "string", // OPTIONAL: text to display in the button
  "button-url": "string", // OPTIONAL: URL to navigate to when the button is clicked
  "sub-title": "string", // OPTIONAL: smaller text providing additional context for the title
};

static defaultAttributes = {};
```

Custom components, such as `mj-bw-hero`, must be defined in the `.mjmlconfig` in order for them to be compiled and rendered properly in the templates.

```json
{
  "packages": ["components/mj-bw-hero"]
}
```

### `mj-include`

You are also able to reference other more static MJML templates in your MJML file simply by referencing the file within the MJML template.

```html
<!-- Example of reference to mjml template -->
<mj-wrapper padding="5px 20px 10px 20px">
  <mj-include path="../../components/learn-more-footer.mjml" />
</mj-wrapper>
```

#### `head.mjml`
Currently we include the `head.mjml` file in all MJML templates as it contains shared styling and formatting that ensures consistency across all email implementations.

In the future we may deviate from this practice to support different layouts. At that time we will modify the docs with direction.
