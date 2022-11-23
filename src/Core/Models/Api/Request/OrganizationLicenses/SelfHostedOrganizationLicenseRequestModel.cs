namespace Bit.Core.Models.Api.OrganizationLicenses;

public class SelfHostedOrganizationLicenseRequestModel
{
    public string LicenseKey { get; set; }
    public string BillingSyncKey { get; set; }
    public Guid InstallationId { get; set; }
}
