namespace Bit.Core.Models.Mail.Billing;

public class BusinessUnitConversionInviteModel : BaseMailModel
{
    public string OrganizationId { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }

    public string Url =>
        $"{WebVaultUrl}/providers/setup-business-unit?organizationId={OrganizationId}&email={Email}&token={Token}";
}
