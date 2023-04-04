namespace Bit.Core.Models.OrganizationConnectionConfigs;

public interface IConnectionConfig
{
    bool Validate(out string exception);
}
