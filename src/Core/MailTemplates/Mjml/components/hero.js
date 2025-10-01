const { BodyComponent } = require("mjml-core");
class MjBwHero extends BodyComponent {
  static dependencies = {
    // Tell the validator which tags are allowed as our component's parent
    "mj-column": ["mj-bw-hero"],
    "mj-wrapper": ["mj-bw-hero"],
    // Tell the validator which tags are allowed as our component's children
    "mj-bw-hero": [],
  };

  static allowedAttributes = {
    "img-src": "string", // REQUIRED: Source for the image displayed in the right-hand side of the blue header area
    title: "string", // REQUIRED: large text stating primary purpose of the email
    "button-text": "string", // OPTIONAL: text to display in the button
    "button-url": "string", // OPTIONAL: URL to navigate to when the button is clicked
    "sub-title": "string", // OPTIONAL: smaller text providing additional context for the title
  };

  static defaultAttributes = {};

  render() {
    if (this.getAttribute("button-text") && this.getAttribute("button-url")) {
      return this.renderMJML(`
      <mj-section
        full-width="full-width"
        background-color="#175ddc"
        border-radius="4px 4px 0px 0px"
      >
        <mj-column width="70%">
          <mj-image
            align="left"
            src="https://bitwarden.com/images/logo-horizontal-white.png"
            width="150px"
            height="30px"
          ></mj-image>
          <mj-text color="#fff" padding-top="0" padding-bottom="0">
            <h1 style="font-weight: normal; font-size: 24px; line-height: 32px">
              ${this.getAttribute("title")}
            </h1>
          </mj-text>
          <mj-button
            href="${this.getAttribute("button-url")}"
            background-color="#fff"
            color="#1A41AC"
            border-radius="20px"
            align="left"
          >
              ${this.getAttribute("button-text")}
            </mj-button
          >
        </mj-column>
        <mj-column width="30%" vertical-align="bottom">
          <mj-image
            src="${this.getAttribute("img-src")}"
            alt=""
            width="140px"
            height="140px"
            padding="0px"
            css-class="hide-small-img"
          />
        </mj-column>
      </mj-section>
    `);
    } else {
      return this.renderMJML(`
      <mj-section
        full-width="full-width"
        background-color="#175ddc"
        border-radius="4px 4px 0px 0px"
      >
        <mj-column width="70%">
          <mj-image
            align="left"
            src="https://bitwarden.com/images/logo-horizontal-white.png"
            width="150px"
            height="30px"
          ></mj-image>
          <mj-text color="#fff" padding-top="0" padding-bottom="0">
            <h1 style="font-weight: normal; font-size: 24px; line-height: 32px">
              ${this.getAttribute("title")}
            </h1>
          </mj-text>
        </mj-column>
        <mj-column width="30%" vertical-align="bottom">
          <mj-image
            src="${this.getAttribute("img-src")}"
            alt=""
            width="140px"
            height="140px"
            padding="0px"
            css-class="hide-small-img"
          />
        </mj-column>
      </mj-section>
    `);
    }
  }
}

module.exports = MjBwHero;
