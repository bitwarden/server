using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(SecretVersionsController))]
[SutProviderCustomize]
[SecretCustomize]
public class SecretVersionsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_SecretNotFound_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Guid secretId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secretId).Returns((Secret?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetVersionsBySecretIdAsync(secretId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_NoAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_NoReadAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        Guid userId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id));

        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_Success(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        List<SecretVersion> versions,
        Guid userId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));

        foreach (var version in versions)
        {
            version.SecretId = secret.Id;
        }
        sutProvider.GetDependency<ISecretVersionRepository>().GetManyBySecretIdAsync(secret.Id).Returns(versions);

        var result = await sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id);

        Assert.Equal(versions.Count, result.Data.Count());
        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1)
            .GetManyBySecretIdAsync(Arg.Is(secret.Id));

        // Version history exposes past values, so reading it is audited as a secret retrieval.
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserSecretsEventAsync(userId, Arg.Is<IEnumerable<Secret>>(s => s.Single().Id == secret.Id),
                EventType.Secret_Retrieved);
    }

    [Theory]
    [BitAutoData]
    public async Task GetById_VersionNotFound_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Guid versionId)
    {
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(versionId).Returns((SecretVersion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByIdAsync(versionId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetById_Success(
        SutProvider<SecretVersionsController> sutProvider,
        SecretVersion version,
        Secret secret,
        Guid userId)
    {
        version.SecretId = secret.Id;
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(version.Id).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));

        var result = await sutProvider.Sut.GetByIdAsync(version.Id);

        Assert.Equal(version.Id, result.Id);
        Assert.Equal(version.SecretId, result.SecretId);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserSecretsEventAsync(userId, Arg.Is<IEnumerable<Secret>>(s => s.Single().Id == secret.Id),
                EventType.Secret_Retrieved);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_NoWriteAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid userId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RestoreVersionAsync(secret.Id, request));

        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_VersionNotFound_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        RestoreSecretVersionRequestModel request,
        Guid userId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns((SecretVersion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RestoreVersionAsync(secret.Id, request));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_VersionBelongsToDifferentSecret_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid userId)
    {
        version.SecretId = Guid.NewGuid(); // Different secret
        request.VersionId = version.Id;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RestoreVersionAsync(secret.Id, request));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_Success(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid userId,
        OrganizationUser organizationUser)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;
        var versionValue = version.Value;
        organizationUser.OrganizationId = secret.OrganizationId;
        organizationUser.UserId = userId;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, userId).Returns(organizationUser);
        sutProvider.GetDependency<ISecretRepository>().UpdateAsync(Arg.Any<Secret>()).Returns(x => x.Arg<Secret>());

        var originalValue = secret.Value;
        var originalRevisionDate = secret.RevisionDate;

        var result = await sutProvider.Sut.RestoreVersionAsync(secret.Id, request);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(Arg.Is<Secret>(s => s.Value == versionValue));

        // The displaced value is snapshotted with the date it was set, not the restore time.
        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1)
            .CreateAsync(Arg.Is<SecretVersion>(v =>
                v.SecretId == secret.Id &&
                v.Value == originalValue &&
                v.VersionDate == originalRevisionDate &&
                v.EditorOrganizationUserId == organizationUser.Id));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserSecretsEventAsync(userId, Arg.Is<IEnumerable<Secret>>(s => s.Single().Id == secret.Id),
                EventType.Secret_Edited);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_EmptyIds_Throws(
        SutProvider<SecretVersionsController> sutProvider)
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.BulkDeleteAsync(new List<Guid>()));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_VersionNotFound_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<Guid> ids)
    {
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(ids[0]).Returns((SecretVersion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.BulkDeleteAsync(ids));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_NoWriteAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Secret secret,
        Guid userId)
    {
        var ids = versions.Select(v => v.Id).ToList();
        foreach (var version in versions)
        {
            version.SecretId = secret.Id;
        }

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(ids).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Secret> { secret });
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(new Dictionary<Guid, (bool Read, bool Write)> { { secret.Id, (true, false) } });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.BulkDeleteAsync(ids));

        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs()
            .DeleteManyByIdAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_Success(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Secret secret,
        Guid userId)
    {
        var ids = versions.Select(v => v.Id).ToList();
        foreach (var version in versions)
        {
            version.SecretId = secret.Id;
        }

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(ids).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Secret> { secret });
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(new Dictionary<Guid, (bool Read, bool Write)> { { secret.Id, (true, true) } });

        await sutProvider.Sut.BulkDeleteAsync(ids);

        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1)
            .DeleteManyByIdAsync(Arg.Is<IEnumerable<Guid>>(x => x.SequenceEqual(ids)));
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_ServiceAccount_NoReadAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        Guid serviceAccountId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, serviceAccountId, AccessClientType.ServiceAccount)
            .Returns((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id));

        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyBySecretIdAsync(default);
        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_ServiceAccount_NoWriteAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid serviceAccountId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, serviceAccountId, AccessClientType.ServiceAccount)
            .Returns((true, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RestoreVersionAsync(secret.Id, request));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_ServiceAccount_Success_RecordsSnapshotAsServiceAccount(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid serviceAccountId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;
        var originalValue = secret.Value;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, serviceAccountId, AccessClientType.ServiceAccount)
            .Returns((true, true));
        sutProvider.GetDependency<ISecretRepository>().UpdateAsync(Arg.Any<Secret>()).Returns(x => x.Arg<Secret>());

        await sutProvider.Sut.RestoreVersionAsync(secret.Id, request);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(Arg.Is<Secret>(s => s.Value == version.Value));
        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1)
            .CreateAsync(Arg.Is<SecretVersion>(v =>
                v.SecretId == secret.Id &&
                v.Value == originalValue &&
                v.EditorServiceAccountId == serviceAccountId &&
                v.EditorOrganizationUserId == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(default, default);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogServiceAccountSecretsEventAsync(serviceAccountId,
                Arg.Is<IEnumerable<Secret>>(s => s.Single().Id == secret.Id), EventType.Secret_Edited);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_ServiceAccount_NoWriteAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Secret secret,
        Guid serviceAccountId)
    {
        var ids = versions.Select(v => v.Id).ToList();
        foreach (var version in versions)
        {
            version.SecretId = secret.Id;
        }

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(ids).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Secret> { secret });
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), serviceAccountId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)> { { secret.Id, (true, false) } });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.BulkDeleteAsync(ids));

        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs()
            .DeleteManyByIdAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task GetById_ServiceAccount_NoReadAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        SecretVersion version,
        Secret secret,
        Guid serviceAccountId)
    {
        version.SecretId = secret.Id;

        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(version.Id).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, serviceAccountId, AccessClientType.ServiceAccount)
            .Returns((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByIdAsync(version.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetManyByIds_ServiceAccount_MissingAccessToOneSecret_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Guid serviceAccountId,
        Guid organizationId)
    {
        var accessibleSecretId = Guid.NewGuid();
        var inaccessibleSecretId = Guid.NewGuid();
        versions[0].SecretId = accessibleSecretId;
        for (var i = 1; i < versions.Count; i++)
        {
            versions[i].SecretId = inaccessibleSecretId;
        }
        var versionIds = versions.Select(v => v.Id).ToList();
        var secrets = new List<Secret>
        {
            new() { Id = accessibleSecretId, OrganizationId = organizationId },
            new() { Id = inaccessibleSecretId, OrganizationId = organizationId },
        };

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(versionIds).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>()).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), serviceAccountId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)>
            {
                { accessibleSecretId, (true, false) },
                { inaccessibleSecretId, (false, false) },
            });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetManyByIdsAsync(versionIds));
    }

    [Theory]
    [BitAutoData]
    public async Task GetManyByIds_AccessResultMissingSecret_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Guid serviceAccountId,
        Guid organizationId)
    {
        var accessibleSecretId = Guid.NewGuid();
        var otherSecretId = Guid.NewGuid();
        versions[0].SecretId = accessibleSecretId;
        for (var i = 1; i < versions.Count; i++)
        {
            versions[i].SecretId = otherSecretId;
        }
        var versionIds = versions.Select(v => v.Id).ToList();
        var secrets = new List<Secret>
        {
            new() { Id = accessibleSecretId, OrganizationId = organizationId },
            new() { Id = otherSecretId, OrganizationId = organizationId },
        };

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(versionIds).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>()).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        // Only one of the two derived secrets produces a row, even though it grants read.
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), serviceAccountId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)>
            {
                { accessibleSecretId, (true, false) },
            });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetManyByIdsAsync(versionIds));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkDelete_AccessResultMissingSecret_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Guid serviceAccountId,
        Guid organizationId)
    {
        var accessibleSecretId = Guid.NewGuid();
        var otherSecretId = Guid.NewGuid();
        versions[0].SecretId = accessibleSecretId;
        for (var i = 1; i < versions.Count; i++)
        {
            versions[i].SecretId = otherSecretId;
        }
        var ids = versions.Select(v => v.Id).ToList();
        var secrets = new List<Secret>
        {
            new() { Id = accessibleSecretId, OrganizationId = organizationId },
            new() { Id = otherSecretId, OrganizationId = organizationId },
        };

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(ids).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>()).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        // Only one of the two derived secrets produces a row, even though it grants write.
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), serviceAccountId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)>
            {
                { accessibleSecretId, (true, true) },
            });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.BulkDeleteAsync(ids));

        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs()
            .DeleteManyByIdAsync(default!);
    }
    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_ServiceAccount_Success_LogsRetrievedAsServiceAccount(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        List<SecretVersion> versions,
        Guid serviceAccountId)
    {
        foreach (var version in versions)
        {
            version.SecretId = secret.Id;
        }

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, serviceAccountId, AccessClientType.ServiceAccount)
            .Returns((true, false));
        sutProvider.GetDependency<ISecretVersionRepository>().GetManyBySecretIdAsync(secret.Id).Returns(versions);

        var result = await sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id);

        Assert.Equal(versions.Count, result.Data.Count());
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogServiceAccountSecretsEventAsync(serviceAccountId,
                Arg.Is<IEnumerable<Secret>>(s => s.Single().Id == secret.Id), EventType.Secret_Retrieved);
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_NoVersions_LogsNothing(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        Guid userId)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));
        sutProvider.GetDependency<ISecretVersionRepository>().GetManyBySecretIdAsync(secret.Id)
            .Returns(new List<SecretVersion>());

        var result = await sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id);

        // Nothing was disclosed, so there is nothing to audit.
        Assert.Empty(result.Data);
        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_OrganizationApiKey_NoReadAccess_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        Guid organizationId)
    {
        // Organization API keys resolve to AccessClientType.Organization, which the access query
        // never grants secret access. They must not bypass the check.
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.Organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(organizationId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, organizationId, AccessClientType.Organization)
            .Returns((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id));

        await sutProvider.GetDependency<ISecretVersionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyBySecretIdAsync(default);
        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GetManyByIds_Success_LogsRetrievedForEachSecret(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Guid userId,
        Guid organizationId)
    {
        var firstSecretId = Guid.NewGuid();
        var secondSecretId = Guid.NewGuid();
        versions[0].SecretId = firstSecretId;
        for (var i = 1; i < versions.Count; i++)
        {
            versions[i].SecretId = secondSecretId;
        }
        var versionIds = versions.Select(v => v.Id).ToList();
        var secrets = new List<Secret>
        {
            new() { Id = firstSecretId, OrganizationId = organizationId },
            new() { Id = secondSecretId, OrganizationId = organizationId },
        };

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(versionIds).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>()).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), userId, AccessClientType.User)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)>
            {
                { firstSecretId, (true, false) },
                { secondSecretId, (true, false) },
            });

        var result = await sutProvider.Sut.GetManyByIdsAsync(versionIds);

        Assert.Equal(versions.Count, result.Data.Count());
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserSecretsEventAsync(userId,
                Arg.Is<IEnumerable<Secret>>(s => s.Select(x => x.Id).OrderBy(x => x)
                    .SequenceEqual(new[] { firstSecretId, secondSecretId }.OrderBy(x => x))),
                EventType.Secret_Retrieved);
    }

    [Theory]
    [BitAutoData]
    public async Task GetManyByIds_ServiceAccount_MissingAccessToOneSecret_LogsNothing(
        SutProvider<SecretVersionsController> sutProvider,
        List<SecretVersion> versions,
        Guid serviceAccountId,
        Guid organizationId)
    {
        var accessibleSecretId = Guid.NewGuid();
        var inaccessibleSecretId = Guid.NewGuid();
        versions[0].SecretId = accessibleSecretId;
        for (var i = 1; i < versions.Count; i++)
        {
            versions[i].SecretId = inaccessibleSecretId;
        }
        var versionIds = versions.Select(v => v.Id).ToList();
        var secrets = new List<Secret>
        {
            new() { Id = accessibleSecretId, OrganizationId = organizationId },
            new() { Id = inaccessibleSecretId, OrganizationId = organizationId },
        };

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyByIdsAsync(versionIds).Returns(versions);
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(Arg.Any<IEnumerable<Guid>>()).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.ServiceAccount);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(serviceAccountId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), serviceAccountId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write)>
            {
                { accessibleSecretId, (true, false) },
                { inaccessibleSecretId, (false, false) },
            });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetManyByIdsAsync(versionIds));

        await AssertNoSecretEventLoggedAsync(sutProvider);
    }

    private static async Task AssertNoSecretEventLoggedAsync(SutProvider<SecretVersionsController> sutProvider)
    {
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogUserSecretsEventAsync(default, default!, default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogServiceAccountSecretsEventAsync(default, default!, default);
    }
}
