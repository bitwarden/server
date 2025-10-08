# MJML email templating

This directory contains MJML templates for emails. MJML is a markup language designed to reduce the pain of coding responsive email templates. There are DRY features within the library which will improve code quality.

MJML stands for MailJet Markup Language.

## Implementation considerations

These `MJML` templates are compiled into HTML which will then be further consumed by our HandleBars mail service. We can continue to use this service to assign values from our View Models. This leverages the existing infrastructure. It also means we can continue to use the double brace (`{{}}`) syntax within MJML since Handlebars can be used to assign values to those `{{variables}}`.

There is no change on how we interact with our view models.

There is an added step where we compile `*.mjml` to `*.html.hbs`. `*.html.hbs` is the format we use so the handlebars service can apply the variables. This build pipeline process is in progress and may need to be manually done at times.

### `*.txt.hbs`

There is no change to how we create the `txt.hbs`. MJML does not impact how we create these artifacts.

## Building `MJML` files

```shell
npm ci

# Build *.html, output is the ./out directory
npm run build

# To build on changes to *.mjml and *.js files, new files will not be tracked, you will need to run again
npm run build:watch

# Build *.html.hbs once, output is the ./out directory
npm run build:hbs

# Build *.html.hbs in a minified ./out directory
npm run build:minify

# apply prettier formatting to all files
npm run prettier
```

## Development

MJML supports components and you can create your own components by adding them to `.mjmlconfig`. Components are simple JavaScript that return MJML markup based on the attributes assigned, see components/mj-bw-hero.js. The markup is not a proper object, but contained in a string.

When using MJML templating you can use the above [commands](#building-mjml-files) to compile the template and view it in a web browser.

Not all MJML tags have the same attributes, it is highly recommended to review the documentation on the official MJML website to understand the usages of each of the tags.

### Possible process

#### Initial email development might look something like:

1. create `cool-email.mjml`
2. run `npm run build:watch`
3. view compiled `HTML` output in a web browser
4. iterate -> while `build:watch`'ing you should be able to refresh the browser page after the mjml re-compile to see the changes

#### Testing with `IMailService`

After the email is developed from the [initial step](#initial-email-development-might-look-something-like) you'll probably want to make sure the email `{{variables}}` are populated properly by running it through an `IMailService`.

1. run `npm run build:minify`
2. copy built `*.html.hbs` files from the build directory to a location the mail service can consume them

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
