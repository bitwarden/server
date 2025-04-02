using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class InitPendingOrganizationCommandTests
{
    [Theory, BitAutoData]
    public async Task Init_Organization_Success(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org)
    {
        org.PrivateKey = null;
        org.PublicKey = null;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, "");

        await organizationServcie.Received().ValidateSignUpPoliciesAsync(userId);
        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);
        await collectionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_With_CollectionName_Success(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, string collectionName)
    {
        org.PrivateKey = null;
        org.PublicKey = null;
        org.Id = orgId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, collectionName);

        await organizationServcie.Received().ValidateSignUpPoliciesAsync(userId);
        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);

        await collectionRepository.Received().CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Enabled(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org)

    {
        org.Enabled = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, ""));

        Assert.Equal("Organization is already enabled.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Not_Pending(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org)

    {
        org.Status = Enums.OrganizationStatusType.Created;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, ""));

        Assert.Equal("Organization is not on a Pending status.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Public_Key(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org)

    {
        org.PublicKey = publicKey;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, ""));

        Assert.Equal("Organization already has a Public Key.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Private_Key(Guid userId, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org)

    {
        org.PublicKey = null;
        org.PrivateKey = privateKey;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(userId, orgId, orgUserId, publicKey, privateKey, ""));

        Assert.Equal("Organization already has a Private Key.", exception.Message);

    }
}
