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

  render() {
    let headAnchorElement = "";
    if (this.getAttribute("head-url-text") && this.getAttribute("head-url")) {
      headAnchorElement = `<a href="${this.getAttribute("head-url")}" class="link">
                ${this.getAttribute("head-url-text")}
                <span style="text-decoration: none">
                  <img src="https://assets.bitwarden.com/email/v1/bwi-external-link-16px.png"
                    alt="External Link Icon"
                    width="16px"
                    style="vertical-align: middle;"
                  />
                </span>
              </a>`;
    }
    let footAnchorElement = "";
    if (this.getAttribute("foot-url-text") && this.getAttribute("foot-url")) {
      footAnchorElement = `<a href="${this.getAttribute("foot-url")}" class="link">
                ${this.getAttribute("foot-url-text")}
                <span style="text-decoration: none">
                  <img src="https://assets.bitwarden.com/email/v1/bwi-external-link-16px.png"
                    alt="External Link Icon"
                    width="16px"
                    style="vertical-align: middle;"
                  />
                </span>
              </a>`;
    }
    return this.renderMJML(
      `
      <mj-section background-color="#fff" padding="10px 20px">
        <mj-group>
          <mj-column width="15%" vertical-align="middle">
            <mj-image
              src="${this.getAttribute("icon-src")}"
              alt="${this.getAttribute("icon-alt")}"
              width="50px"
              padding="0"
              padding-right="10px"
              border-radius="8px"
            />
          </mj-column>
          <mj-column width="85%" vertical-align="middle">
            <mj-text padding="0" line-height="24px">
              ` +
        headAnchorElement +
        `
              </mj-text>
              <mj-text padding="0" line-height="24px">
                ${this.getAttribute("text")}
              </mj-text>
              <mj-text padding="0" line-height="24px">
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
