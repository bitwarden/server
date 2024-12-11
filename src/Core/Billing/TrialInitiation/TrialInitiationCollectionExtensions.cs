using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Core.Billing.TrialInitiation.Registration.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.TrialInitiation;

public static class TrialInitiationCollectionExtensions
{
    public static void AddTrialInitiationServices(this IServiceCollection services)
    {
        services.AddSingleton<
            ISendTrialInitiationEmailForRegistrationCommand,
            SendTrialInitiationEmailForRegistrationCommand
        >();
    }
}
