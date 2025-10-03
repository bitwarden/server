# MJML Email templates

This directory contains MJML templates for emails sent by the application. MJML is a markup language designed to reduce the pain of coding responsive email templates.

MJML stands for Mail Jet Markdown Language.

## Implementation considerations
These templates are compiled into HTML which will then be further consumed by our HandleBars service. We can continue to use this service to assign values from our View Models. This leverages the existing infrastructure. It also means we can continue to use the double brace (`{{}}`) syntax within MJML since Handlebars can be used to assign values to those `{{variables}}`.

There is no change on how we interact with our view models.

The new comes in when we compile the `*.mjml` to `*.html.hbs`. This is the format we use so the handlebars service can apply the variables. This build pipeline process is in progress and may need to be manual done.

### `txt.hbs`
There is no change to how we create the `txt.hbs`. MJML does not impact how we create these artifacts.

## Building MJML files

```powershell
npm ci

# Build once
npm run build

# To build on changes
npm run watch
```

### Building all MJML files
```powershell
npm run build:all # searches all sub directories for mjml files
```
This command will parse the email directory for all mjml files and attempt to compile them into `*html.hbs` files and output them into the `out/` directory. This command maintains the structure of the input directories. Meaning if an mjml template is located in `email/auth` then the compiled version will be in `out/auth`.

The script was generated and works as expected. It is more fully featured than it's usage here. If interested take a look.

## Development
MJML supports components and you can create your own components by adding them to `.mjmlconfig`. Components are simple JavaScipt that return HTML based on the attributes assigned. (see components/mj-bw-hero.js)

When using MJML templating you can use the above [commands](#usage) to compile the template and view it in a web browser.

Not all MJML tags have the same attributes, it is highly recommended to review the documentation on the official MJML website to understand the usages of each of the tags.

### Custom Tags
There is currently a `mj-bw-hero` tag that you can use from within your `*.mjml` templates. This is a good example of how to create a component that takes in attribute values allowing us to be more DRY in our development of emails. Since the attributes input is a string we are able to define whatever we need into the component, in this case `mj-bw-hero`.


In order to view the custom component you have written you will need to include it in the `.mjmlconfig` and reference it in an `mjml` template file.

```html
<!-- Custom component implementation-->
<mj-bw-hero
	img-src="https://assets.bitwarden.com/email/v1/business.png"
	title="Verify your email to access this Bitwarden Send"
/>
```

Attributes in Custom Components are defined by the developer. They can be required or optional depending on implementation. See the documentation for more information.

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
  "packages": [
    "components/mj-bw-hero"
  ]
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

## Implementation Considerations
We will be using the mjml templates to be generating