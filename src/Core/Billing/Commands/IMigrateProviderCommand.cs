namespace Bit.Core.Billing.Commands;

public interface IMigrateProviderCommand
{
    Task MigrateProvider(Guid providerId);
}
