namespace Bit.Core.Models.OrganizationConnectionConfigs;

public class BillingSyncConfig : IConnectionConfig
{
    public string BillingSyncKey { get; set; }
    public Guid CloudOrganizationId { get; set; }

    public bool CanUse(out string exception)
    {
        if (string.IsNullOrWhiteSpace(BillingSyncKey))
        {
            exception = "Failed to get Billing Sync Key";
            return false;
        }

        exception = "";
        return true;
    }
}
