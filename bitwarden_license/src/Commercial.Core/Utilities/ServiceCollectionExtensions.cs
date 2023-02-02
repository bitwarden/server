﻿using Bit.Commercial.Core.Services;
using Bit.Core.AdminFeatures.Providers;
using Bit.Core.AdminFeatures.Providers.Interfaces;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommercialCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<ICreateProviderCommand, CreateProviderCommand>();
    }
}
