const { BodyComponent } = require("mjml-core");

class MjBwInviterInfo extends BodyComponent {

    static dependencies = {
        "mj-column": ["mj-bw-inviter-info"],
        "mj-wrapper": ["mj-bw-inviter-info"],
        "mj-bw-inviter-info": [],
    };

    static allowedAttributes = {
    "expiration-date": "string", // REQUIRED: Date to display
    "email-address": "string", // Optional: Email address to display
    };

  render() {
    const emailAddressText = this.getAttribute("email-address")
      ? `This invitation was sent by <a href="mailto:${this.getAttribute("email-address")}" class="link">${this.getAttribute("email-address")}</a> and expires `
      : "Expires ";

    return this.renderMJML(
      `
      <mj-section background-color="#fff" padding="15px 10px 10px 10px">
        <mj-column>
          <mj-text font-size="12px" line-height="24px" padding="10px 15px" color="#1B2029">
            ${emailAddressText + this.getAttribute("expiration-date")}
          </mj-text>
        </mj-column>
      </mj-section>
      `
    );
  }
}

module.exports = MjBwInviterInfo;
