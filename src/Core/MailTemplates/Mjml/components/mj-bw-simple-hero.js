const { BodyComponent } = require("mjml-core");

class MjBwSimpleHero extends BodyComponent {
  static dependencies = {
    // Tell the validator which tags are allowed as our component's parent
    "mj-column": ["mj-bw-simple-hero"],
    "mj-wrapper": ["mj-bw-simple-hero"],
    // Tell the validator which tags are allowed as our component's children
    "mj-bw-simple-hero": [],
  };

  static allowedAttributes = {};

  static defaultAttributes = {};

  render() {
    return this.renderMJML(
      `
      <mj-section
        full-width="full-width"
        background-color="#175ddc"
        border-radius="4px 4px 0px 0px"
        padding="20px 20px"
      >
        <mj-column width="100%">
          <mj-image
            align="left"
            src="https://bitwarden.com/images/logo-horizontal-white.png"
            width="150px"
            height="30px"
            padding="10px 5px"
          ></mj-image>
        </mj-column>
      </mj-section>
    `,
    );
  }
}

module.exports = MjBwSimpleHero;
