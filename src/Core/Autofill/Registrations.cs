using Bit.Core.Autofill.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Autofill;

public static class Registrations
{
    public static void AddAutofillOperations(this IServiceCollection services)
    {
        services.AddTransient<ICreateAutofillTriageReportCommand, CreateAutofillTriageReportCommand>();
    }
}
