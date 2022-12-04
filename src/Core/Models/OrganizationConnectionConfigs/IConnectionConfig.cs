namespace Bit.Core.Models.OrganizationConnectionConfigs;

public interface IConnectionConfig
{
    bool CanUse(out string exception);
}
