﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.OrganizationConnectionConfigs;

public class BillingSyncConfig : IConnectionConfig
{
    public string BillingSyncKey { get; set; }
    public Guid CloudOrganizationId { get; set; }
    public DateTime? LastLicenseSync { get; set; }

    public bool Validate(out string exception)
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
