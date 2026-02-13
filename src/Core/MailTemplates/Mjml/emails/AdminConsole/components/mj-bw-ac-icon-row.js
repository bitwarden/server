const { BodyComponent } = require("mjml-core");

const BODY_TEXT_STYLES = `
  font-family="'Helvetica Neue', Helvetica, Arial, sans-serif"
  font-size="16px"
  font-weight="400"
  line-height="24px"
`;

class MjBwAcIconRow extends BodyComponent {
  static dependencies = {
    "mj-column": ["mj-bw-ac-icon-row"],
    "mj-wrapper": ["mj-bw-ac-icon-row"],
    "mj-bw-ac-icon-row": [],
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
        .mj-bw-ac-icon-row-text {
          padding-left: 15px !important;
          padding-right: 15px !important;
          line-height: 20px;
        }
        .mj-bw-ac-icon-row-icon {
          display: none !important;
          width: 0 !important;
          max-width: 0 !important;
        }
        .mj-bw-ac-icon-row-text-column {
          width: 100% !important;
        }
        .mj-bw-ac-icon-row-bullet {
          display: inline !important;
        }
      }
    `;
  };

  render() {
    const headAnchorElement =
      this.getAttribute("head-url-text") && this.getAttribute("head-url")
        ? `
            <mj-text css-class="mj-bw-ac-icon-row-text" padding="5px 10px 0px 10px" ${BODY_TEXT_STYLES}>
                <a href="${this.getAttribute("head-url")}" class="link">
                    ${this.getAttribute("head-url-text")}
                    <span style="text-decoration: none">
                      <img src="https://assets.bitwarden.com/email/v1/bwi-external-link-16px.png"
                        alt="External Link Icon"
                        width="16px"
                        style="vertical-align: middle;"
                      />
                    </span>
                  </a>
            </mj-text>`
        : "";

    const footAnchorElement =
      this.getAttribute("foot-url-text") && this.getAttribute("foot-url")
        ? `<mj-text css-class="mj-bw-ac-icon-row-text" padding="0px" ${BODY_TEXT_STYLES}>
                <a href="${this.getAttribute("foot-url")}" class="link">
                    ${this.getAttribute("foot-url-text")}
              </a>
          </mj-text>`
        : "";

    return this.renderMJML(
      `
      <mj-section background-color="#fff" padding="0px 10px 24px 10px">
        <mj-group css-class="mj-bw-ac-icon-row">
          <mj-column width="15%" vertical-align="middle" css-class="mj-bw-ac-icon-row-icon">
            <mj-image
              src="${this.getAttribute("icon-src")}"
              alt="${this.getAttribute("icon-alt")}"
              width="48px"
              padding="0px 10px 0px 5px"
              border-radius="8px"
            />
          </mj-column>
          <mj-column width="85%" vertical-align="middle" css-class="mj-bw-ac-icon-row-text-column">
              ${headAnchorElement}
              <mj-text css-class="mj-bw-ac-icon-row-text" padding="0px 0px 0px 0px" ${BODY_TEXT_STYLES}>
                <span class="mj-bw-ac-icon-row-bullet" style="display: none;">&#8226;&nbsp;</span>${this.getAttribute("text")}
              </mj-text>
              ${footAnchorElement}
          </mj-column>
        </mj-group>
      </mj-section>
    `,
    );
  }
}

module.exports = MjBwAcIconRow;