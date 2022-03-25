﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class OrganizationApiKeyRepository : Repository<OrganizationApiKey, Models.OrganizationApiKey, Guid>, IOrganizationApiKeyRepository
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IMapper _mapper;

        public OrganizationApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, db => db.OrganizationApiKeys)
        {

        }

        public async Task<IEnumerable<OrganizationApiKey>> GetManyByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType? type = null)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var apiKeys = await dbContext.OrganizationApiKeys
                    .Where(o => o.OrganizationId == organizationId && (type == null || o.Type == type))
                    .ToListAsync();
                return _mapper.Map<List<OrganizationApiKey>>(apiKeys);
            }
        }
    }
}
