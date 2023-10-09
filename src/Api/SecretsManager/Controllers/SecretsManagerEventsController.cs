﻿using Bit.Api.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[SelfHosted(NotSelfHostedOnly = true)]
public class SecretsManagerEventsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEventRepository _eventRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public SecretsManagerEventsController(
        IEventRepository eventRepository,
        IServiceAccountRepository serviceAccountRepository,
        IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
        _serviceAccountRepository = serviceAccountRepository;
        _eventRepository = eventRepository;
    }

    [HttpGet("sm/events/service-accounts/{serviceAccountId}")]
    public async Task<ListResponseModel<EventResponseModel>> GetServiceAccountEventsAsync(Guid serviceAccountId,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null,
        [FromQuery] string continuationToken = null)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccountId);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Update);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var dateRange = GetDateRange(start, end);

        var result = await _eventRepository.GetManyByServiceAccountAsync(serviceAccount.OrganizationId,
            serviceAccount.Id, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    private Tuple<DateTime, DateTime> GetDateRange(DateTime? start, DateTime? end)
    {
        if (!end.HasValue || !start.HasValue)
        {
            end = DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);
            start = DateTime.UtcNow.Date.AddDays(-30);
        }
        else if (start.Value > end.Value)
        {
            var newEnd = start;
            start = end;
            end = newEnd;
        }

        if (end.Value - start.Value > TimeSpan.FromDays(367))
        {
            throw new BadRequestException("Range too large.");
        }

        return new Tuple<DateTime, DateTime>(start.Value, end.Value);
    }
}
