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
    "url-text": "string",
    "url": "string",
    "text": "string",
  };

  static defaultAttributes = {};

  render() {
      return this.renderMJML(`
      <mj-section background-color="#fff" padding="10px 20px">
        <mj-column width="15%" vertical-align="middle">
          <mj-image
            src="${this.getAttribute("icon-src")}"
            alt="${this.getAttribute("icon-alt")}"
            width="50px"
            padding="0"
            border-radius="8px"
          />
        </mj-column>
        <mj-column width="85%" vertical-align="middle">
          <mj-text padding-left="0">
            <a href="${this.getAttribute("url")}" class="link">
              ${this.getAttribute("url-text")}
              <span style="text-decoration: none">&#x2197;</span>
            </a>
          </mj-text>
          <mj-text font-size="14px" padding-top="0" padding-left="0">
            ${this.getAttribute("text")}
          </mj-text>
        </mj-column>
      </mj-section>
    `);
  }
}

module.exports = MjBwIconRow;
