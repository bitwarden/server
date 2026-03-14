using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.BusinessUnitConversionInvite;

#nullable enable

/// <summary>
/// Email sent to invite users to set up a Business Unit Portal.
/// </summary>
public class BusinessUnitConversionInviteMail : BaseMail<BusinessUnitConversionInviteMailView>
{
    public override string Subject { get; set; } = "Set Up Business Unit";
}

/// <summary>
/// View model for Business Unit Conversion Invite email template.
/// </summary>
public class BusinessUnitConversionInviteMailView : BaseMailView
{
    public required string OrganizationId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
    public required string WebVaultUrl { get; init; }

    public string Url =>
        $"{WebVaultUrl}/providers/setup-business-unit?organizationId={OrganizationId}&email={Email}&token={Token}";
}
