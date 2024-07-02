using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Queries.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(SecretsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
[SecretCustomize]
public class SecretsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task GetSecretsByOrganization_ReturnsEmptyList(SutProvider<SecretsController> sutProvider, Guid id, Guid organizationId, Guid userId, AccessClientType accessType)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var result = await sutProvider.Sut.ListByOrganizationAsync(id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetManyDetailsByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), userId, accessType);

        Assert.Empty(result.Secrets);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetSecretsByOrganization_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, Secret resultSecret, Guid organizationId, Guid userId, Project mockProject, AccessClientType accessType)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<ISecretRepository>().GetManyDetailsByOrganizationIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<SecretPermissionDetails>
            {
                new() { Secret = resultSecret, Read = true, Write = true },
            });
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            resultSecret.Projects = new List<Project>() { mockProject };
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
                .Returns((true, true));
        }


        await sutProvider.Sut.ListByOrganizationAsync(resultSecret.OrganizationId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .GetManyDetailsByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.OrganizationId)), userId, accessType);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsByOrganization_AccessDenied_Throws(SutProvider<SecretsController> sutProvider, Secret resultSecret)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.ListByOrganizationAsync(resultSecret.OrganizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecret_NotFound(SutProvider<SecretsController> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(Guid.NewGuid()));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetSecret_Success(PermissionType permissionType, SutProvider<SecretsController> sutProvider, Secret resultSecret, Guid userId, Guid organizationId, Project mockProject)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        mockProject.OrganizationId = organizationId;
        resultSecret.Projects = new List<Project>() { mockProject };
        resultSecret.OrganizationId = organizationId;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(default).ReturnsForAnyArgs(resultSecret);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(default, default, default)
            .ReturnsForAnyArgs(Task.FromResult((true, true)));

        if (permissionType == PermissionType.RunAsAdmin)
        {
            resultSecret.OrganizationId = organizationId;
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.NoAccessCheck)
                .Returns((true, true));
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                .Returns((true, true));
        }

        await sutProvider.Sut.GetAsync(resultSecret.Id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSecret_NoAccess_Throws(SutProvider<SecretsController> sutProvider,
        SecretCreateRequestModel data, Guid organizationId)
    {
        data = SetupSecretCreateRequest(sutProvider, data, organizationId);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.ToSecret(organizationId),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, data));
        await sutProvider.GetDependency<ICreateSecretCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSecret_NoAccessPolicyUpdates_Success(SutProvider<SecretsController> sutProvider,
        SecretCreateRequestModel data, Guid organizationId)
    {
        data = SetupSecretCreateRequest(sutProvider, data, organizationId);

        await sutProvider.Sut.CreateAsync(organizationId, data);

        await sutProvider.GetDependency<ICreateSecretCommand>().Received(1)
            .CreateAsync(Arg.Any<Secret>(), null);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSecret_AccessPolicyUpdates_NoAccess_Throws(SutProvider<SecretsController> sutProvider,
        SecretCreateRequestModel data, Guid organizationId)
    {
        data = SetupSecretCreateRequest(sutProvider, data, organizationId, true);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<SecretAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, data));
        await sutProvider.GetDependency<ICreateSecretCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSecret_AccessPolicyUpdate_Success(SutProvider<SecretsController> sutProvider,
        SecretCreateRequestModel data, Guid organizationId)
    {
        data = SetupSecretCreateRequest(sutProvider, data, organizationId, true);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<SecretAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());


        await sutProvider.Sut.CreateAsync(organizationId, data);

        await sutProvider.GetDependency<ICreateSecretCommand>().Received(1)
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecret_NoAccess_Throws(SutProvider<SecretsController> sutProvider,
        SecretUpdateRequestModel data, Secret currentSecret)
    {
        data = SetupSecretUpdateRequest(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Secret>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(currentSecret.Id).ReturnsForAnyArgs(currentSecret);

        sutProvider.GetDependency<IUpdateSecretCommand>()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>())
            .ReturnsForAnyArgs(data.ToSecret(currentSecret));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSecretAsync(currentSecret.Id, data));
        await sutProvider.GetDependency<IUpdateSecretCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecret_SecretDoesNotExist_Throws(SutProvider<SecretsController> sutProvider,
        SecretUpdateRequestModel data, Secret currentSecret)
    {
        data = SetupSecretUpdateRequest(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Secret>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        sutProvider.GetDependency<IUpdateSecretCommand>()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>())
            .ReturnsForAnyArgs(data.ToSecret(currentSecret));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSecretAsync(currentSecret.Id, data));
        await sutProvider.GetDependency<IUpdateSecretCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecret_NoAccessPolicyUpdates_Success(SutProvider<SecretsController> sutProvider,
        SecretUpdateRequestModel data, Secret currentSecret)
    {
        data = SetupSecretUpdateRequest(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Secret>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(currentSecret.Id).ReturnsForAnyArgs(currentSecret);

        sutProvider.GetDependency<IUpdateSecretCommand>()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>())
            .ReturnsForAnyArgs(data.ToSecret(currentSecret));

        await sutProvider.Sut.UpdateSecretAsync(currentSecret.Id, data);
        await sutProvider.GetDependency<IUpdateSecretCommand>().Received(1)
            .UpdateAsync(Arg.Any<Secret>(), null);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecret_AccessPolicyUpdate_NoAccess_Throws(SutProvider<SecretsController> sutProvider,
        SecretUpdateRequestModel data, Secret currentSecret, SecretAccessPoliciesUpdates accessPoliciesUpdates)
    {
        data = SetupSecretUpdateAccessPoliciesRequest(sutProvider, data, currentSecret, accessPoliciesUpdates);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<SecretAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSecretAsync(currentSecret.Id, data));
        await sutProvider.GetDependency<IUpdateSecretCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecret_AccessPolicyUpdate_Access_Success(SutProvider<SecretsController> sutProvider,
        SecretUpdateRequestModel data, Secret currentSecret, SecretAccessPoliciesUpdates accessPoliciesUpdates)
    {
        data = SetupSecretUpdateAccessPoliciesRequest(sutProvider, data, currentSecret, accessPoliciesUpdates);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<SecretAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());

        await sutProvider.Sut.UpdateSecretAsync(currentSecret.Id, data);
        await sutProvider.GetDependency<IUpdateSecretCommand>().Received(1)
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_NoSecretsFound_ThrowsNotFound(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var ids = data.Select(s => s.Id).ToList();
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(new List<Secret>());
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
        await sutProvider.GetDependency<IDeleteSecretCommand>().DidNotReceiveWithAnyArgs().DeleteSecrets(Arg.Any<List<Secret>>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_SecretsFoundMisMatch_ThrowsNotFound(SutProvider<SecretsController> sutProvider, List<Secret> data, Secret mockSecret)
    {
        data.Add(mockSecret);
        var ids = data.Select(s => s.Id).ToList();
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(new List<Secret> { mockSecret });
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
        await sutProvider.GetDependency<IDeleteSecretCommand>().DidNotReceiveWithAnyArgs().DeleteSecrets(Arg.Any<List<Secret>>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_OrganizationMistMatch_ThrowsNotFound(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var ids = data.Select(s => s.Id).ToList();
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
        await sutProvider.GetDependency<IDeleteSecretCommand>().DidNotReceiveWithAnyArgs().DeleteSecrets(Arg.Any<List<Secret>>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_NoAccessToSecretsManager_ThrowsNotFound(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var ids = data.Select(s => s.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var s in data)
        {
            s.OrganizationId = organizationId;
        }
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.BulkDeleteAsync(ids));
        await sutProvider.GetDependency<IDeleteSecretCommand>().DidNotReceiveWithAnyArgs().DeleteSecrets(Arg.Any<List<Secret>>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_ReturnsAccessDeniedForSecretsWithoutAccess_Success(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var ids = data.Select(s => s.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var secret in data)
        {
            secret.OrganizationId = organizationId;
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), secret,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data.First(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids);

        Assert.Equal(data.Count, results.Data.Count());
        Assert.Equal("access denied", results.Data.First().Error);

        data.Remove(data.First());
        await sutProvider.GetDependency<IDeleteSecretCommand>().Received(1)
            .DeleteSecrets(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_Success(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var ids = data.Select(sa => sa.Id).ToList();
        var organizationId = data.First().OrganizationId;
        foreach (var secret in data)
        {
            secret.OrganizationId = organizationId;
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), secret,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId)).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids);

        await sutProvider.GetDependency<IDeleteSecretCommand>().Received(1)
            .DeleteSecrets(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
        Assert.Equal(data.Count, results.Data.Count());
        foreach (var result in results.Data)
        {
            Assert.Null(result.Error);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsByIds_NoSecretsFound_ThrowsNotFound(SutProvider<SecretsController> sutProvider,
        List<Secret> data)
    {
        var (ids, request) = BuildGetSecretsRequestModel(data);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(new List<Secret>());
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetSecretsByIdsAsync(request));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsByIds_SecretsFoundMisMatch_ThrowsNotFound(SutProvider<SecretsController> sutProvider,
        List<Secret> data, Secret mockSecret)
    {
        var (ids, request) = BuildGetSecretsRequestModel(data);
        ids.Add(mockSecret.Id);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids))
            .ReturnsForAnyArgs(new List<Secret> { mockSecret });
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetSecretsByIdsAsync(request));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsByIds_AccessDenied_ThrowsNotFound(SutProvider<SecretsController> sutProvider,
        List<Secret> data)
    {
        var (ids, request) = BuildGetSecretsRequestModel(data);
        var organizationId = SetOrganizations(ref data);

        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetSecretsByIdsAsync(request));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsByIds_Success(SutProvider<SecretsController> sutProvider, List<Secret> data)
    {
        var (ids, request) = BuildGetSecretsRequestModel(data);
        var organizationId = SetOrganizations(ref data);

        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Is(ids)).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        var results = await sutProvider.Sut.GetSecretsByIdsAsync(request);
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetSecretsSyncAsync_AccessSecretsManagerFalse_ThrowsNotFound(
        bool nullLastSyncedDate,
        SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        var lastSyncedDate = GetLastSyncedDate(nullLastSyncedDate);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId))
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetSecretsSyncAsync(organizationId, lastSyncedDate));
    }

    [Theory]
    [BitAutoData(true, AccessClientType.NoAccessCheck)]
    [BitAutoData(true, AccessClientType.User)]
    [BitAutoData(true, AccessClientType.Organization)]
    [BitAutoData(false, AccessClientType.NoAccessCheck)]
    [BitAutoData(false, AccessClientType.User)]
    [BitAutoData(false, AccessClientType.Organization)]
    public async Task GetSecretsSyncAsync_AccessClientIsNotAServiceAccount_ThrowsBadRequest(
        bool nullLastSyncedDate,
        AccessClientType accessClientType,
        SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        var lastSyncedDate = GetLastSyncedDate(nullLastSyncedDate);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId))
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>())
            .Returns((accessClientType, new Guid()));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetSecretsSyncAsync(organizationId, lastSyncedDate));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretsSyncAsync_LastSyncedInFuture_ThrowsBadRequest(
        List<Secret> secrets,
        SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        DateTime? lastSyncedDate = DateTime.UtcNow.AddDays(3);

        SetupSecretsSyncRequest(false, secrets, sutProvider, organizationId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetSecretsSyncAsync(organizationId, lastSyncedDate));
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetSecretsSyncAsync_AccessClientIsAServiceAccount_Success(
        bool nullLastSyncedDate,
        List<Secret> secrets,
        SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        var lastSyncedDate = SetupSecretsSyncRequest(nullLastSyncedDate, secrets, sutProvider, organizationId);

        var result = await sutProvider.Sut.GetSecretsSyncAsync(organizationId, lastSyncedDate);
        Assert.True(result.HasChanges);
        Assert.NotNull(result.Secrets);
        Assert.NotEmpty(result.Secrets.Data);
    }

    private static (List<Guid> Ids, GetSecretsRequestModel request) BuildGetSecretsRequestModel(
        IEnumerable<Secret> data)
    {
        var ids = data.Select(s => s.Id).ToList();
        var request = new GetSecretsRequestModel { Ids = ids };
        return (ids, request);
    }

    private static Guid SetOrganizations(ref List<Secret> data)
    {
        var organizationId = data.First().OrganizationId;
        foreach (var s in data)
        {
            s.OrganizationId = organizationId;
        }

        return organizationId;
    }

    private static DateTime? SetupSecretsSyncRequest(bool nullLastSyncedDate, List<Secret> secrets,
        SutProvider<SecretsController> sutProvider, Guid organizationId)
    {
        var lastSyncedDate = GetLastSyncedDate(nullLastSyncedDate);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Is(organizationId))
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>())
            .Returns((AccessClientType.ServiceAccount, new Guid()));
        sutProvider.GetDependency<ISecretsSyncQuery>().GetAsync(Arg.Any<SecretsSyncRequest>())
            .Returns((true, secrets));
        return lastSyncedDate;
    }

    private static DateTime? GetLastSyncedDate(bool nullLastSyncedDate)
    {
        return nullLastSyncedDate ? null : DateTime.UtcNow.AddDays(-1);
    }

    private static SecretCreateRequestModel SetupSecretCreateRequest(SutProvider<SecretsController> sutProvider, SecretCreateRequestModel data, Guid organizationId, bool accessPolicyRequest = false)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = [data.ProjectIds.ElementAt(0)];
        }

        if (!accessPolicyRequest)
        {
            data.AccessPoliciesRequests = null;
        }

        sutProvider.GetDependency<ICreateSecretCommand>()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>())
            .ReturnsForAnyArgs(data.ToSecret(organizationId));

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Secret>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());

        return data;
    }

    private static SecretUpdateRequestModel SetupSecretUpdateRequest(SecretUpdateRequestModel data, bool accessPolicyRequest = false)
    {
        // We currently only allow a secret to be in one project at a time
        if (data.ProjectIds != null && data.ProjectIds.Length > 1)
        {
            data.ProjectIds = [data.ProjectIds.ElementAt(0)];
        }

        if (!accessPolicyRequest)
        {
            data.AccessPoliciesRequests = null;
        }

        return data;
    }

    private static SecretUpdateRequestModel SetupSecretUpdateAccessPoliciesRequest(SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Secret currentSecret, SecretAccessPoliciesUpdates accessPoliciesUpdates)
    {
        data = SetupSecretUpdateRequest(data, true);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Secret>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(currentSecret.Id).ReturnsForAnyArgs(currentSecret);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ISecretAccessPoliciesUpdatesQuery>()
            .GetAsync(Arg.Any<SecretAccessPolicies>(), Arg.Any<Guid>())
            .ReturnsForAnyArgs(accessPoliciesUpdates);
        sutProvider.GetDependency<IUpdateSecretCommand>()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>())
            .ReturnsForAnyArgs(data.ToSecret(currentSecret));
        return data;
    }
}
