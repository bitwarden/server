const { BodyComponent } = require("mjml-core");
class MjBwLearnMoreFooter extends BodyComponent {
  static dependencies = {
    // Tell the validator which tags are allowed as our component's parent
    "mj-column": ["mj-bw-learn-more-footer"],
    "mj-wrapper": ["mj-bw-learn-more-footer"],
    // Tell the validator which tags are allowed as our component's children
    "mj-bw-learn-more-footer": [],
  };

  static allowedAttributes = {};

  static defaultAttributes = {};

  componentHeadStyle = (breakpoint) => {
    return `
      @media only screen and (max-width:${breakpoint}) {
        .mj-bw-learn-more-footer-responsive-img {
          display: none !important;
        }
      }
    `;
  };

  render() {
    return this.renderMJML(
      `
      <mj-section border-radius="0px 0px 4px 4px" background-color="#f6f6f6" padding="5px 10px 10px 10px">
        <mj-column width="70%">
          <mj-text line-height="24px">
            <p style="font-size: 18px; line-height: 28px; font-weight: bold;">
              Learn more about Bitwarden
            </p>
            Find user guides, product documentation, and videos on the
            <a href="https://bitwarden.com/help/" class="link"> Bitwarden Help Center</a>.
          </mj-text>
        </mj-column>
        <mj-column width="30%">
          <mj-image
            src="https://assets.bitwarden.com/email/v1/spot-community.png"
            css-class="mj-bw-learn-more-footer-responsive-img"
            width="94px"
          />
        </mj-column>
      </mj-section>
    `,
    );
  }
}

module.exports = MjBwLearnMoreFooter;
