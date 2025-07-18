﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Services;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Api.Auth.Jobs;

public class EmergencyAccessNotificationJob : BaseJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EmergencyAccessNotificationJob(IServiceScopeFactory serviceScopeFactory, ILogger<EmergencyAccessNotificationJob> logger)
        : base(logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var emergencyAccessService = scope.ServiceProvider.GetService(typeof(IEmergencyAccessService)) as IEmergencyAccessService;
        await emergencyAccessService.SendNotificationsAsync();
    }
}
