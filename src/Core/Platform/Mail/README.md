
# Overriding email templates from disk

The mail services support loading the mail template from disk. This is intended to be used by self-hosted customers who want to modify their email appearance. These overrides are not intended to be used during local development, as any changes there would not be reflected in the templates used in a normal deployment configuration.

Any customer using this override has worked with Bitwarden support on an approved implementation and has acknowledged that they are responsible for reacting to any changes made to the templates as a part of the Bitwarden development process. This includes, but is not limited to, changes in Handlebars property names, removal of properties from the `ViewModel` classes, and changes in template names.  **Bitwarden is not responsible for maintaining backward compatibility between releases in order to support any overridden emails.**