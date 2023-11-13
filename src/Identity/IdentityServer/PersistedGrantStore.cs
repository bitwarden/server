﻿using Bit.Core;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Services;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Grant = Bit.Core.Auth.Entities.Grant;


namespace Bit.Identity.IdentityServer;

public class PersistedGrantStore : IPersistedGrantStore
{
    private readonly IGrantRepository _grantRepository;
    private readonly IFeatureService _featureService;
    private readonly ICurrentContext _currentContext;
    public PersistedGrantStore(
        IGrantRepository grantRepository,
        IFeatureService featureService,
        ICurrentContext currentContext)
    {
        _grantRepository = grantRepository;
        _featureService = featureService;
        _currentContext = currentContext;
    }

    public async Task<PersistedGrant> GetAsync(string key)
    {
        var grant = await _grantRepository.GetByKeyAsync(key);
        if (grant == null)
        {
            return null;
        }

        var pGrant = ToPersistedGrant(grant);
        return pGrant;
    }

    public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        var grants = await _grantRepository.GetManyAsync(filter.SubjectId, filter.SessionId,
            filter.ClientId, filter.Type);
        var pGrants = grants.Select(g => ToPersistedGrant(g));
        return pGrants;
    }

    public async Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        await _grantRepository.DeleteManyAsync(filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type);
    }

    public async Task RemoveAsync(string key)
    {
        await _grantRepository.DeleteByKeyAsync(key);
    }

    public async Task StoreAsync(PersistedGrant pGrant)
    {
        var GrantSaveOptimizationIsEnabled = _featureService.IsEnabled(FeatureFlagKeys.GrantSaveOptimization, _currentContext);
        var grant = ToGrant(pGrant);
        await _grantRepository.SaveAsync(grant, GrantSaveOptimizationIsEnabled);
    }

    private Grant ToGrant(PersistedGrant pGrant)
    {
        return new Grant
        {
            Key = pGrant.Key,
            Type = pGrant.Type,
            SubjectId = pGrant.SubjectId,
            SessionId = pGrant.SessionId,
            ClientId = pGrant.ClientId,
            Description = pGrant.Description,
            CreationDate = pGrant.CreationTime,
            ExpirationDate = pGrant.Expiration,
            ConsumedDate = pGrant.ConsumedTime,
            Data = pGrant.Data
        };
    }

    private PersistedGrant ToPersistedGrant(Grant grant)
    {
        return new PersistedGrant
        {
            Key = grant.Key,
            Type = grant.Type,
            SubjectId = grant.SubjectId,
            SessionId = grant.SessionId,
            ClientId = grant.ClientId,
            Description = grant.Description,
            CreationTime = grant.CreationDate,
            Expiration = grant.ExpirationDate,
            ConsumedTime = grant.ConsumedDate,
            Data = grant.Data
        };
    }
}
