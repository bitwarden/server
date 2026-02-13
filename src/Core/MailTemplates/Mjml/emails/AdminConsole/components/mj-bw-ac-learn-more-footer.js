const { BodyComponent } = require("mjml-core");
class MjBwAcLearnMoreFooter extends BodyComponent {
  static dependencies = {
    // Tell the validator which tags are allowed as our component's parent
    "mj-column": ["mj-bw-ac-learn-more-footer"],
    "mj-wrapper": ["mj-bw-ac-learn-more-footer"],
    // Tell the validator which tags are allowed as our component's children
    "mj-bw-ac-learn-more-footer": [],
  };

  static allowedAttributes = {};

  static defaultAttributes = {};

  componentHeadStyle = (breakpoint) => {
    return `
      @media only screen and (max-width:${breakpoint}) {
        .mj-bw-ac-learn-more-footer-responsive-img {
          display: none !important;
        }
      }
    `;
  };

  render() {
    return this.renderMJML(
      `
      <mj-section border-radius="0px 0px 4px 4px" background-color="#F3F6F9" padding="14px 10px 14px 10px">
        <mj-column width="70%">
          <mj-text padding="10px 15px 10px 15px">
            <p style="font-size: 18px; line-height: 28px; font-weight: 500; margin: 0 0 8px 0;">
              Learn more about Bitwarden
            </p>
            <p style="font-size: 16px; line-height: 24px; margin: 0;">
              Find user guides, product documentation, and videos on the
              <a href="https://bitwarden.com/help/" class="link"> Bitwarden Help Center</a>.
            </p>
          </mj-text>
        </mj-column>
        <mj-column width="30%" vertical-align="bottom">
          <mj-image
            src="https://assets.bitwarden.com/email/v1/spot-community.png"
            css-class="mj-bw-ac-learn-more-footer-responsive-img"
            width="94px"
            padding="0px 15px 0px 0px"
            align="right"
          />
        </mj-column>
      </mj-section>
    `,
    );
  }
}

module.exports = MjBwAcLearnMoreFooter;