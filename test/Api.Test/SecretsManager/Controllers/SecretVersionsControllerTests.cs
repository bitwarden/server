using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
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
        var details = versions.Select(v => new SecretVersionDetails { SecretVersion = v }).ToList();
        sutProvider.GetDependency<ISecretVersionRepository>().GetManyDetailsBySecretIdAsync(secret.Id)
            .Returns(details);

        var result = await sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id);

        Assert.Equal(versions.Count, result.Data.Count());
        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1)
            .GetManyDetailsBySecretIdAsync(Arg.Is(secret.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task GetVersionsBySecretId_ResolvesEditorNames(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion namedMemberVersion,
        SecretVersion unnamedMemberVersion,
        SecretVersion serviceAccountVersion,
        SecretVersion unattributedVersion,
        Guid userId)
    {
        foreach (var version in new[] { namedMemberVersion, unnamedMemberVersion, serviceAccountVersion, unattributedVersion })
        {
            version.SecretId = secret.Id;
        }

        var details = new List<SecretVersionDetails>
        {
            new()
            {
                SecretVersion = namedMemberVersion,
                EditorUserName = "Ada Lovelace",
                EditorUserEmail = "ada@example.com",
            },
            // A member who never set a name falls back to their email for display.
            new()
            {
                SecretVersion = unnamedMemberVersion,
                EditorUserName = "   ",
                EditorUserEmail = "grace@example.com",
            },
            // Service account names stay encrypted; the server passes the encstring straight through.
            new()
            {
                SecretVersion = serviceAccountVersion,
                EditorServiceAccountName = "2.abc|def|ghi",
            },
            // Written before attribution existed, or the editor has since been removed.
            new() { SecretVersion = unattributedVersion },
        };

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));
        sutProvider.GetDependency<ISecretVersionRepository>().GetManyDetailsBySecretIdAsync(secret.Id)
            .Returns(details);

        var result = (await sutProvider.Sut.GetVersionsBySecretIdAsync(secret.Id)).Data.ToList();

        Assert.Equal("Ada Lovelace", result[0].EditorOrganizationUserName);
        Assert.Null(result[0].EditorServiceAccountName);

        Assert.Equal("grace@example.com", result[1].EditorOrganizationUserName);

        Assert.Equal("2.abc|def|ghi", result[2].EditorServiceAccountName);
        Assert.Null(result[2].EditorOrganizationUserName);

        Assert.Null(result[3].EditorOrganizationUserName);
        Assert.Null(result[3].EditorServiceAccountName);
    }

    [Theory]
    [BitAutoData]
    public async Task GetById_VersionNotFound_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Guid versionId)
    {
        sutProvider.GetDependency<ISecretVersionRepository>().GetDetailsByIdAsync(versionId)
            .Returns((SecretVersionDetails?)null);

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
        sutProvider.GetDependency<ISecretVersionRepository>().GetDetailsByIdAsync(version.Id)
            .Returns(new SecretVersionDetails { SecretVersion = version });
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, false));

        var result = await sutProvider.Sut.GetByIdAsync(version.Id);

        Assert.Equal(version.Id, result.Id);
        Assert.Equal(version.SecretId, result.SecretId);
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
        Guid userId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;
        var versionValue = version.Value;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default)
            .ReturnsForAnyArgs((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().UpdateAsync(Arg.Any<Secret>()).Returns(x => x.Arg<Secret>());

        await sutProvider.Sut.RestoreVersionAsync(secret.Id, request);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(Arg.Is<Secret>(s => s.Value == versionValue));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_User_Success_RecordsRestoredValueAsNewVersion(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid userId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;
        var restoredValue = version.Value;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, AccessClientType.User)
            .Returns((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().UpdateAsync(Arg.Any<Secret>()).Returns(x => x.Arg<Secret>());

        await sutProvider.Sut.RestoreVersionAsync(secret.Id, request);

        // A restore sets a new current value, so the version records the restored value rather
        // than the one it displaced.
        await sutProvider.GetDependency<ICreateSecretVersionCommand>().Received(1)
            .CreateAsync(Arg.Is<Secret>(x => x.Id == secret.Id && x.Value == restoredValue), userId);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_UnchangedValue_DoesNotRecordVersion(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid userId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;

        // Both sides are stored ciphertext rather than a fresh client encryption, so equal
        // ciphertext reliably means the value is unchanged and there is nothing to record.
        version.Value = secret.Value!;

        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(secret.Id).Returns(secret);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(secret.OrganizationId).Returns(false);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, AccessClientType.User)
            .Returns((true, true));
        sutProvider.GetDependency<ISecretVersionRepository>().GetByIdAsync(request.VersionId).Returns(version);
        sutProvider.GetDependency<ISecretRepository>().UpdateAsync(Arg.Any<Secret>()).Returns(x => x.Arg<Secret>());

        await sutProvider.Sut.RestoreVersionAsync(secret.Id, request);

        await sutProvider.GetDependency<ICreateSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_OrganizationApiKey_Throws(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        RestoreSecretVersionRequestModel request)
    {
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType
            .Returns(IdentityClientType.Organization);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RestoreVersionAsync(secret.Id, request));

        await sutProvider.GetDependency<ICreateSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().UpdateAsync(default!);
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
            .GetManyDetailsBySecretIdAsync(default);
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
        await sutProvider.GetDependency<ICreateSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreVersion_ServiceAccount_Success_RecordsRestoredValueAsNewVersion(
        SutProvider<SecretVersionsController> sutProvider,
        Secret secret,
        SecretVersion version,
        RestoreSecretVersionRequestModel request,
        Guid serviceAccountId)
    {
        version.SecretId = secret.Id;
        request.VersionId = version.Id;
        var restoredValue = version.Value;

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
            .UpdateAsync(Arg.Is<Secret>(s => s.Value == restoredValue));
        await sutProvider.GetDependency<ICreateSecretVersionCommand>().Received(1)
            .CreateAsync(Arg.Is<Secret>(x => x.Id == secret.Id && x.Value == restoredValue), serviceAccountId);
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

        sutProvider.GetDependency<ISecretVersionRepository>().GetDetailsByIdAsync(version.Id)
            .Returns(new SecretVersionDetails { SecretVersion = version });
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

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyDetailsByIdsAsync(versionIds)
            .Returns(versions.Select(v => new SecretVersionDetails { SecretVersion = v }).ToList());
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

        sutProvider.GetDependency<ISecretVersionRepository>().GetManyDetailsByIdsAsync(versionIds)
            .Returns(versions.Select(v => new SecretVersionDetails { SecretVersion = v }).ToList());
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
}
