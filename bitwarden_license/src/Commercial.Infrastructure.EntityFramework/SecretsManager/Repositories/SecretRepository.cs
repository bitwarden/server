using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class SecretRepository : Repository<Core.SecretsManager.Entities.Secret, Secret, Guid>, ISecretRepository
{
    public SecretRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.Secret)
    { }

    public override async Task<Core.SecretsManager.Entities.Secret?> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secret = await dbContext.Secret
                                    .Include("Projects")
                                    .Where(c => c.Id == id && c.DeletedDate == null)
                                    .FirstOrDefaultAsync();
            return Mapper.Map<Core.SecretsManager.Entities.Secret>(secret);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByIds(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                .Include(c => c.Projects)
                .ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByOrganizationIdAsync(
        Guid organizationId, Guid userId, AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret
            .Include(c => c.Projects)
            .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)),
            AccessClientType.ServiceAccount => query.Where(ServiceAccountHasReadAccessToSecret(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null)
        };

        var secrets = await query.OrderBy(c => c.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret
            .Include(c => c.Projects)
            .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
            .OrderBy(s => s.RevisionDate);

        var secrets = SecretToPermissionDetails(query, userId, accessType);

        return await secrets.ToListAsync();
    }

    public async Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await dbContext.Secret
                .CountAsync(ou => ou.OrganizationId == organizationId && ou.DeletedDate == null);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByOrganizationIdInTrashByIdsAsync(Guid organizationId, IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(s => ids.Contains(s.Id) && s.OrganizationId == organizationId && s.DeletedDate != null)
                                    .Include("Projects")
                                    .OrderBy(c => c.RevisionDate)
                                    .ToListAsync();

            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdInTrashAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(c => c.OrganizationId == organizationId && c.DeletedDate != null)
                                    .Include("Projects")
                                    .OrderBy(c => c.RevisionDate)
                                    .ToListAsync();

            // This should be changed if/when we allow non admins to access trashed items
            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets).Select(s => new SecretPermissionDetails
            {
                Secret = s,
                Read = true,
                Write = true,
            });
        }
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret.Include(s => s.Projects)
            .Where(s => s.Projects.Any(p => p.Id == projectId) && s.DeletedDate == null);

        var secrets = SecretToPermissionDetails(query, userId, accessType);

        return await secrets.ToListAsync();
    }

    public async Task<Core.SecretsManager.Entities.Secret> CreateAsync(
        Core.SecretsManager.Entities.Secret secret, SecretAccessPoliciesUpdates? accessPoliciesUpdates = null)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        secret.SetNewId();
        var entity = Mapper.Map<Secret>(secret);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        if (secret.Projects?.Count > 0)
        {
            foreach (var project in entity.Projects)
            {
                dbContext.Attach(project);
            }

            var projectIds = entity.Projects.Select(p => p.Id).ToList();
            await UpdateServiceAccountRevisionsByProjectIdsAsync(dbContext, projectIds);
        }

        await dbContext.AddAsync(entity);
        await UpdateSecretAccessPoliciesAsync(dbContext, entity, accessPoliciesUpdates);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return secret;
    }

    public async Task<Core.SecretsManager.Entities.Secret> UpdateAsync(Core.SecretsManager.Entities.Secret secret,
        SecretAccessPoliciesUpdates? accessPoliciesUpdates = null)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var mappedEntity = Mapper.Map<Secret>(secret);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var entity = await dbContext.Secret
            .Include(s => s.Projects)
            .Include(s => s.UserAccessPolicies)
            .Include(s => s.GroupAccessPolicies)
            .Include(s => s.ServiceAccountAccessPolicies)
            .FirstAsync(s => s.Id == secret.Id);

        dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

        if (secret.Projects != null)
        {
            entity = await UpdateProjectMappingAsync(dbContext, entity, mappedEntity);
        }

        if (accessPoliciesUpdates != null)
        {
            await UpdateSecretAccessPoliciesAsync(dbContext, entity, accessPoliciesUpdates);
        }

        await UpdateServiceAccountRevisionsBySecretIdsAsync(dbContext, [entity.Id]);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return Mapper.Map<Core.SecretsManager.Entities.Secret>(entity);
    }


    public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var secretIds = ids.ToList();
        await UpdateServiceAccountRevisionsBySecretIdsAsync(dbContext, secretIds);

        var utcNow = DateTime.UtcNow;

        await dbContext.Secret.Where(c => secretIds.Contains(c.Id))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(s => s.RevisionDate, utcNow)
                    .SetProperty(s => s.DeletedDate, utcNow));

        await transaction.CommitAsync();
    }

    public async Task HardDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var secretIds = ids.ToList();
        await UpdateServiceAccountRevisionsBySecretIdsAsync(dbContext, secretIds);

        await dbContext.Secret.Where(c => secretIds.Contains(c.Id))
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }

    public async Task RestoreManyByIdAsync(IEnumerable<Guid> ids)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var secretIds = ids.ToList();
        await UpdateServiceAccountRevisionsBySecretIdsAsync(dbContext, secretIds);

        var utcNow = DateTime.UtcNow;

        await dbContext.Secret.Where(c => secretIds.Contains(c.Id))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(s => s.RevisionDate, utcNow)
                    .SetProperty(s => s.DeletedDate, (DateTime?)null));

        await transaction.CommitAsync();
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> ImportAsync(IEnumerable<Core.SecretsManager.Entities.Secret> secrets)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = new List<Secret>();
            var projects = secrets
                .SelectMany(s => s.Projects ?? Enumerable.Empty<Core.SecretsManager.Entities.Project>())
                .DistinctBy(p => p.Id)
                .Select(p => Mapper.Map<Project>(p))
                .ToDictionary(p => p.Id, p => p);

            dbContext.AttachRange(projects.Values);

            foreach (var s in secrets)
            {
                var entity = Mapper.Map<Secret>(s);

                if (s.Projects?.Count > 0)
                {
                    entity.Projects = s.Projects.Select(p => projects[p.Id]).ToList();
                }

                entities.Add(entity);
            }
            await GetDbSet(dbContext).AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
        }
        return secrets;
    }

    public async Task<(bool Read, bool Write)> AccessToSecretAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var secret = dbContext.Secret
            .Where(s => s.Id == id);

        var query = BuildSecretAccessQuery(secret, userId, accessType);

        var policy = await query.FirstOrDefaultAsync();

        return policy == null ? (false, false) : (policy.Read, policy.Write);
    }

    public async Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToSecretsAsync(
        IEnumerable<Guid> ids,
        Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var secrets = dbContext.Secret
            .Where(s => ids.Contains(s.Id));

        var accessQuery = BuildSecretAccessQuery(secrets, userId, accessType);

        return await accessQuery.ToDictionaryAsync(sa => sa.Id, sa => (sa.Read, sa.Write));
    }

    public async Task EmptyTrash(DateTime currentDate, uint deleteAfterThisNumberOfDays)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await dbContext.Secret.Where(s => s.DeletedDate != null && s.DeletedDate < currentDate.AddDays(-deleteAfterThisNumberOfDays)).ExecuteDeleteAsync();

        await dbContext.SaveChangesAsync();
    }

    public async Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId, Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret.Where(s => s.OrganizationId == organizationId && s.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        return await query.CountAsync();
    }

    private IQueryable<SecretPermissionDetails> SecretToPermissionDetails(IQueryable<Secret> query, Guid userId, AccessClientType accessType)
    {
        var secrets = accessType switch
        {
            AccessClientType.NoAccessCheck => query.Select(s => new SecretPermissionDetails
            {
                Secret = Mapper.Map<Bit.Core.SecretsManager.Entities.Secret>(s),
                Read = true,
                Write = true,
            }),
            AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)).Select(SecretToPermissionsUser(userId, true)),
            AccessClientType.ServiceAccount => query.Where(ServiceAccountHasReadAccessToSecret(userId)).Select(s =>
                new SecretPermissionDetails
                {
                    Secret = Mapper.Map<Bit.Core.SecretsManager.Entities.Secret>(s),
                    Read = true,
                    Write = s.Projects.Any(p =>
                        p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Write)),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };
        return secrets;
    }

    private Expression<Func<Secret, SecretPermissionDetails>> SecretToPermissionsUser(Guid userId, bool read) =>
        s => new SecretPermissionDetails
        {
            Secret = Mapper.Map<Core.SecretsManager.Entities.Secret>(s),
            Read = read,
            Write =
                s.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                s.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write)) ||
                s.Projects.Any(p =>
                    p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write)))
        };

    private static Expression<Func<Secret, bool>> ServiceAccountHasReadAccessToSecret(Guid serviceAccountId) => s =>
        s.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == serviceAccountId && ap.Read) ||
        s.Projects.Any(p =>
            p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == serviceAccountId && ap.Read));

    private static Expression<Func<Secret, bool>> UserHasReadAccessToSecret(Guid userId) => s =>
        s.UserAccessPolicies.Any(ap => ap.OrganizationUser.UserId == userId && ap.Read) ||
        s.GroupAccessPolicies.Any(ap =>
            ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.UserId == userId && ap.Read)) ||
        s.Projects.Any(p =>
            p.UserAccessPolicies.Any(ap => ap.OrganizationUser.UserId == userId && ap.Read) ||
            p.GroupAccessPolicies.Any(ap =>
                ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.UserId == userId && ap.Read)));

    private static async Task UpdateServiceAccountRevisionsByProjectIdsAsync(DatabaseContext dbContext,
        List<Guid> projectIds)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        var serviceAccountIds = await dbContext.Project.Where(p => projectIds.Contains(p.Id))
            .Include(p => p.ServiceAccountAccessPolicies)
            .SelectMany(p => p.ServiceAccountAccessPolicies.Select(ap => ap.ServiceAccountId!.Value))
            .Distinct()
            .ToListAsync();

        await UpdateServiceAccountRevisionsAsync(dbContext, serviceAccountIds);
    }

    private static async Task UpdateServiceAccountRevisionsBySecretIdsAsync(DatabaseContext dbContext,
        List<Guid> secretIds)
    {
        if (secretIds.Count == 0)
        {
            return;
        }

        var projectAccessServiceAccountIds = await dbContext.Secret
            .Where(s => secretIds.Contains(s.Id))
            .SelectMany(s =>
                s.Projects.SelectMany(p => p.ServiceAccountAccessPolicies.Select(ap => ap.ServiceAccountId!.Value)))
            .Distinct()
            .ToListAsync();

        var directAccessServiceAccountIds = await dbContext.Secret
            .Where(s => secretIds.Contains(s.Id))
            .SelectMany(s => s.ServiceAccountAccessPolicies.Select(ap => ap.ServiceAccountId!.Value))
            .Distinct()
            .ToListAsync();

        var serviceAccountIds =
            directAccessServiceAccountIds.Concat(projectAccessServiceAccountIds).Distinct().ToList();

        await UpdateServiceAccountRevisionsAsync(dbContext, serviceAccountIds);
    }

    private static async Task UpdateServiceAccountRevisionsAsync(DatabaseContext dbContext,
        List<Guid> serviceAccountIds)
    {
        if (serviceAccountIds.Count > 0)
        {
            var utcNow = DateTime.UtcNow;
            await dbContext.ServiceAccount
                .Where(sa => serviceAccountIds.Contains(sa.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.RevisionDate, utcNow));
        }
    }

    private static IQueryable<SecretAccess> BuildSecretAccessQuery(IQueryable<Secret> secrets, Guid accessClientId,
        AccessClientType accessType) =>
        accessType switch
        {
            AccessClientType.NoAccessCheck => secrets.Select(s => new SecretAccess(s.Id, true, true)),
            AccessClientType.User => secrets.Select(s => new SecretAccess(
                s.Id,
                s.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == accessClientId && ap.Read) ||
                s.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == accessClientId && ap.Read)) ||
                s.Projects.Any(p =>
                    p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == accessClientId && ap.Read) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == accessClientId && ap.Read))),
                s.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == accessClientId && ap.Write) ||
                s.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == accessClientId && ap.Write)) ||
                s.Projects.Any(p =>
                    p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == accessClientId && ap.Write) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == accessClientId && ap.Write)))
            )),
            AccessClientType.ServiceAccount => secrets.Select(s => new SecretAccess(
                s.Id,
                s.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == accessClientId && ap.Read) ||
                s.Projects.Any(p =>
                    p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == accessClientId && ap.Read)),
                s.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == accessClientId && ap.Write) ||
                s.Projects.Any(p =>
                    p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == accessClientId && ap.Write))
            )),
            _ => secrets.Select(s => new SecretAccess(s.Id, false, false))
        };

    private static async Task<Secret> UpdateProjectMappingAsync(DatabaseContext dbContext, Secret currentEntity, Secret updatedEntity)
    {
        var projectsToRemove = currentEntity.Projects.Where(p => updatedEntity.Projects.All(mp => mp.Id != p.Id)).ToList();
        var projectsToAdd = updatedEntity.Projects.Where(p => currentEntity.Projects.All(ep => ep.Id != p.Id)).ToList();

        foreach (var p in projectsToRemove)
        {
            currentEntity.Projects.Remove(p);
        }

        foreach (var project in projectsToAdd)
        {
            var p = dbContext.AttachToOrGet<Project>(x => x.Id == project.Id, () => project);
            currentEntity.Projects.Add(p);
        }

        var projectIds = projectsToRemove.Select(p => p.Id).Concat(projectsToAdd.Select(p => p.Id)).ToList();
        if (projectIds.Count > 0)
        {
            await UpdateServiceAccountRevisionsByProjectIdsAsync(dbContext, projectIds);
        }

        return currentEntity;
    }

    private static async Task DeleteSecretAccessPoliciesAsync(DatabaseContext dbContext, Secret entity,
        SecretAccessPoliciesUpdates accessPoliciesUpdates)
    {
        var userAccessPoliciesIdsToDelete = entity.UserAccessPolicies.Where(uap => accessPoliciesUpdates
                .UserAccessPolicyUpdates
                .Any(apu => apu.Operation == AccessPolicyOperation.Delete &&
                            apu.AccessPolicy.OrganizationUserId == uap.OrganizationUserId))
            .Select(uap => uap.Id)
            .ToList();

        var groupAccessPoliciesIdsToDelete = entity.GroupAccessPolicies.Where(gap => accessPoliciesUpdates
                .GroupAccessPolicyUpdates
                .Any(apu => apu.Operation == AccessPolicyOperation.Delete && apu.AccessPolicy.GroupId == gap.GroupId))
            .Select(gap => gap.Id)
            .ToList();

        var serviceAccountAccessPoliciesIdsToDelete = entity.ServiceAccountAccessPolicies.Where(gap =>
                accessPoliciesUpdates.ServiceAccountAccessPolicyUpdates
                    .Any(apu => apu.Operation == AccessPolicyOperation.Delete &&
                                apu.AccessPolicy.ServiceAccountId == gap.ServiceAccountId))
            .Select(sap => sap.Id)
            .ToList();

        var accessPoliciesIdsToDelete = userAccessPoliciesIdsToDelete
            .Concat(groupAccessPoliciesIdsToDelete)
            .Concat(serviceAccountAccessPoliciesIdsToDelete)
            .ToList();

        await dbContext.AccessPolicies
            .Where(ap => accessPoliciesIdsToDelete.Contains(ap.Id))
            .ExecuteDeleteAsync();
    }

    private static async Task UpsertSecretAccessPolicyAsync(DatabaseContext dbContext, BaseAccessPolicy updatedEntity,
        AccessPolicyOperation accessPolicyOperation, AccessPolicy? currentEntity, DateTime currentDate)
    {
        switch (accessPolicyOperation)
        {
            case AccessPolicyOperation.Create when currentEntity == null:
                updatedEntity.SetNewId();
                await dbContext.AddAsync(updatedEntity);
                break;

            case AccessPolicyOperation.Update when currentEntity != null:
                dbContext.AccessPolicies.Attach(currentEntity);
                currentEntity.Read = updatedEntity.Read;
                currentEntity.Write = updatedEntity.Write;
                currentEntity.RevisionDate = currentDate;
                break;
            default:
                throw new InvalidOperationException("Policy updates failed due to unexpected state.");
        }
    }

    private async Task UpsertSecretAccessPoliciesAsync(DatabaseContext dbContext,
        Secret entity,
        SecretAccessPoliciesUpdates policyUpdates)
    {
        var currentDate = DateTime.UtcNow;

        foreach (var policyUpdate in policyUpdates.UserAccessPolicyUpdates.Where(apu =>
                     apu.Operation != AccessPolicyOperation.Delete))
        {
            var currentEntity = entity.UserAccessPolicies?.FirstOrDefault(e =>
                e.OrganizationUserId == policyUpdate.AccessPolicy.OrganizationUserId!.Value);

            await UpsertSecretAccessPolicyAsync(dbContext, MapToEntity(policyUpdate.AccessPolicy),
                policyUpdate.Operation,
                currentEntity,
                currentDate);
        }

        foreach (var policyUpdate in policyUpdates.GroupAccessPolicyUpdates.Where(apu =>
                     apu.Operation != AccessPolicyOperation.Delete))
        {
            var currentEntity = entity.GroupAccessPolicies?.FirstOrDefault(e =>
                e.GroupId == policyUpdate.AccessPolicy.GroupId!.Value);

            await UpsertSecretAccessPolicyAsync(dbContext, MapToEntity(policyUpdate.AccessPolicy),
                policyUpdate.Operation,
                currentEntity,
                currentDate);
        }

        foreach (var policyUpdate in policyUpdates.ServiceAccountAccessPolicyUpdates.Where(apu =>
                     apu.Operation != AccessPolicyOperation.Delete))
        {
            var currentEntity = entity.ServiceAccountAccessPolicies?.FirstOrDefault(e =>
                e.ServiceAccountId == policyUpdate.AccessPolicy.ServiceAccountId!.Value);

            await UpsertSecretAccessPolicyAsync(dbContext, MapToEntity(policyUpdate.AccessPolicy),
                policyUpdate.Operation,
                currentEntity,
                currentDate);
        }
    }

    private async Task UpdateSecretAccessPoliciesAsync(DatabaseContext dbContext,
        Secret entity,
        SecretAccessPoliciesUpdates? accessPoliciesUpdates)
    {
        if (accessPoliciesUpdates == null || !accessPoliciesUpdates.HasUpdates())
        {
            return;
        }

        if ((entity.UserAccessPolicies != null && entity.UserAccessPolicies.Count != 0) ||
            (entity.GroupAccessPolicies != null && entity.GroupAccessPolicies.Count != 0) ||
            (entity.ServiceAccountAccessPolicies != null && entity.ServiceAccountAccessPolicies.Count != 0))
        {
            await DeleteSecretAccessPoliciesAsync(dbContext, entity, accessPoliciesUpdates);
        }

        await UpsertSecretAccessPoliciesAsync(dbContext, entity, accessPoliciesUpdates);

        await UpdateServiceAccountRevisionsAsync(dbContext,
            accessPoliciesUpdates.ServiceAccountAccessPolicyUpdates
                .Select(sap => sap.AccessPolicy.ServiceAccountId!.Value).ToList());
    }

    private BaseAccessPolicy MapToEntity(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy) =>
        baseAccessPolicy switch
        {
            Core.SecretsManager.Entities.UserSecretAccessPolicy accessPolicy => Mapper.Map<UserSecretAccessPolicy>(
                accessPolicy),
            Core.SecretsManager.Entities.GroupSecretAccessPolicy accessPolicy => Mapper.Map<GroupSecretAccessPolicy>(
                accessPolicy),
            Core.SecretsManager.Entities.ServiceAccountSecretAccessPolicy accessPolicy => Mapper
                .Map<ServiceAccountSecretAccessPolicy>(accessPolicy),
            _ => throw new ArgumentException("Unsupported access policy type")
        };

    private record SecretAccess(Guid Id, bool Read, bool Write);
}
