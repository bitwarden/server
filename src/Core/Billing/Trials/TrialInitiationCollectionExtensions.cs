using Bit.Core.Billing.Trials.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Trials;

public static class TrialInitiationCollectionExtensions
{
    public static void AddTrialInitiationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISendTrialInitiationEmailForRegistrationCommand, SendTrialInitiationEmailForRegistrationCommand>();
    }
}
