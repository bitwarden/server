const { BodyComponent } = require("mjml-core");
class MjBwIconRow extends BodyComponent {
  static dependencies = {
    "mj-column": ["mj-bw-icon-row"],
    "mj-wrapper": ["mj-bw-icon-row"],
    "mj-bw-icon-row": [],
  };

  static allowedAttributes = {
    "icon-src": "string",
    "icon-alt": "string",
    "head-url-text": "string",
    "head-url": "string",
    text: "string",
    "foot-url-text": "string",
    "foot-url": "string",
  };

  static defaultAttributes = {};

  headStyle = (breakpoint) => {
    return `
      @media only screen and (max-width:${breakpoint}) {
        .mj-bw-icon-row-text {
          padding-left: 5px !important;
          line-height: 20px;
        }
        .mj-bw-icon-row {
          padding: 10px 15px;
          width: fit-content !important;
        }
      }
    `;
  };

  render() {
    const headAnchorElement =
      this.getAttribute("head-url-text") && this.getAttribute("head-url")
        ? `<a href="${this.getAttribute("head-url")}" class="link">
                ${this.getAttribute("head-url-text")}
                <span style="text-decoration: none">
                  <img src="https://assets.bitwarden.com/email/v1/bwi-external-link-16px.png"
                    alt="External Link Icon"
                    width="16px"
                    style="vertical-align: middle;"
                  />
                </span>
              </a>`
        : "";

    const footAnchorElement =
      this.getAttribute("foot-url-text") && this.getAttribute("foot-url")
        ? `<a href="${this.getAttribute("foot-url")}" class="link">
                ${this.getAttribute("foot-url-text")}
                <span style="text-decoration: none">
                  <img src="https://assets.bitwarden.com/email/v1/bwi-external-link-16px.png"
                    alt="External Link Icon"
                    width="16px"
                    style="vertical-align: middle;"
                  />
                </span>
          </a>`
        : "";

    return this.renderMJML(
      `
      <mj-section background-color="#fff" padding="10px 10px 10px 10px">
        <mj-group css-class="mj-bw-icon-row">
          <mj-column width="15%" vertical-align="top">
            <mj-image
              src="${this.getAttribute("icon-src")}"
              alt="${this.getAttribute("icon-alt")}"
              width="48px"
              padding="0px"
              border-radius="8px"
            />
          </mj-column>
          <mj-column width="85%" vertical-align="top">
            <mj-text css-class="mj-bw-icon-row-text" padding="5px 10px 0px 10px">
              ` +
        headAnchorElement +
        `
              </mj-text>
              <mj-text css-class="mj-bw-icon-row-text" padding="5px 10px 0px 10px">
                ${this.getAttribute("text")}
              </mj-text>
              <mj-text css-class="mj-bw-icon-row-text" padding="5px 10px 0px 10px">
              ` +
        footAnchorElement +
        `
              </mj-text>
          </mj-column>
        </mj-group>
      </mj-section>
    `,
    );
  }
}

module.exports = MjBwIconRow;
