﻿using Bit.Api.Auth.Jobs;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Api.Jobs;

public class JobsHostedService : BaseJobsHostedService
{
    public JobsHostedService(
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        ILogger<JobsHostedService> logger,
        ILogger<JobListener> listenerLogger)
        : base(globalSettings, serviceProvider, logger, listenerLogger) { }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var everyTopOfTheHourTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryTopOfTheHourTrigger")
            .StartNow()
            .WithCronSchedule("0 0 * * * ?")
            .Build();
        var emergencyAccessNotificationTrigger = TriggerBuilder.Create()
            .WithIdentity("EmergencyAccessNotificationTrigger")
            .StartNow()
            .WithCronSchedule("0 0 * * * ?")
            .Build();
        var emergencyAccessTimeoutTrigger = TriggerBuilder.Create()
            .WithIdentity("EmergencyAccessTimeoutTrigger")
            .StartNow()
            .WithCronSchedule("0 0 * * * ?")
            .Build();
        var everyTopOfTheSixthHourTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryTopOfTheSixthHourTrigger")
            .StartNow()
            .WithCronSchedule("0 0 */6 * * ?")
            .Build();
        var everyTwelfthHourAndThirtyMinutesTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryTwelfthHourAndThirtyMinutesTrigger")
            .StartNow()
            .WithCronSchedule("0 30 */12 * * ?")
            .Build();
        var smTrashCleanupTrigger = TriggerBuilder.Create()
            .WithIdentity("SMTrashCleanupTrigger")
            .StartNow()
            .WithCronSchedule("0 0 22 * * ?")
            .Build();
        var randomDailySponsorshipSyncTrigger = TriggerBuilder.Create()
            .WithIdentity("RandomDailySponsorshipSyncTrigger")
            .StartAt(DateBuilder.FutureDate(new Random().Next(24), IntervalUnit.Hour))
            .WithSimpleSchedule(x => x
                .WithIntervalInHours(24)
                .RepeatForever())
            .Build();
        var validateOrganizationDomainTrigger = TriggerBuilder.Create()
            .WithIdentity("ValidateOrganizationDomainTrigger")
            .StartNow()
            .WithCronSchedule("0 0 * * * ?")
            .Build();
        var updatePhishingDomainsTrigger = TriggerBuilder.Create()
            .WithIdentity("UpdatePhishingDomainsTrigger")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInHours(24)
                .RepeatForever())
            .Build();


        var jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(AliveJob), everyTopOfTheHourTrigger),
            new Tuple<Type, ITrigger>(typeof(EmergencyAccessNotificationJob), emergencyAccessNotificationTrigger),
            new Tuple<Type, ITrigger>(typeof(EmergencyAccessTimeoutJob), emergencyAccessTimeoutTrigger),
            new Tuple<Type, ITrigger>(typeof(ValidateUsersJob), everyTopOfTheSixthHourTrigger),
            new Tuple<Type, ITrigger>(typeof(ValidateOrganizationsJob), everyTwelfthHourAndThirtyMinutesTrigger),
            new Tuple<Type, ITrigger>(typeof(ValidateOrganizationDomainJob), validateOrganizationDomainTrigger),
            new Tuple<Type, ITrigger>(typeof(UpdatePhishingDomainsJob), updatePhishingDomainsTrigger),
        };

        if (_globalSettings.SelfHosted && _globalSettings.EnableCloudCommunication)
        {
            jobs.Add(new Tuple<Type, ITrigger>(typeof(SelfHostedSponsorshipSyncJob), randomDailySponsorshipSyncTrigger));
        }

#if !OSS
        jobs.Add(new Tuple<Type, ITrigger>(typeof(EmptySecretsManagerTrashJob), smTrashCleanupTrigger));
#endif

        Jobs = jobs;

        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services, bool selfHosted)
    {
        if (selfHosted)
        {
            services.AddTransient<SelfHostedSponsorshipSyncJob>();
        }
        services.AddTransient<AliveJob>();
        services.AddTransient<EmergencyAccessNotificationJob>();
        services.AddTransient<EmergencyAccessTimeoutJob>();
        services.AddTransient<ValidateUsersJob>();
        services.AddTransient<ValidateOrganizationsJob>();
        services.AddTransient<ValidateOrganizationDomainJob>();
        services.AddTransient<UpdatePhishingDomainsJob>();
    }

    public static void AddCommercialSecretsManagerJobServices(IServiceCollection services)
    {
        services.AddTransient<EmptySecretsManagerTrashJob>();
    }
}
