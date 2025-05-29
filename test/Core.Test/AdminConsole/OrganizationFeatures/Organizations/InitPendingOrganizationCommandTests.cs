using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class InitPendingOrganizationCommandTests
{

    private readonly IOrgUserInviteTokenableFactory _orgUserInviteTokenableFactory = Substitute.For<IOrgUserInviteTokenableFactory>();
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, BitAutoData]
    public async Task Init_Organization_Success(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);
        await collectionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_With_CollectionName_Success(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, string collectionName, OrganizationUser orgUser)
    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PrivateKey = null;
        org.PublicKey = null;
        org.Id = orgId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var organizationServcie = sutProvider.GetDependency<IOrganizationService>();
        var collectionRepository = sutProvider.GetDependency<ICollectionRepository>();

        await sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, collectionName, token);

        await organizationRepository.Received().GetByIdAsync(orgId);
        await organizationServcie.Received().UpdateAsync(org);

        await collectionRepository.Received().CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<List<CollectionAccessSelection>>(l => l == null),
            Arg.Is<List<CollectionAccessSelection>>(l => l.Any(i => i.Manage == true)));

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Enabled(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.Enabled = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization is already enabled.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Is_Not_Pending(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {


        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.Status = Enums.OrganizationStatusType.Created;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization is not on a Pending status.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Public_Key(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {
        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PublicKey = publicKey;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization already has a Public Key.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task Init_Organization_When_Organization_Has_Private_Key(User user, Guid orgId, Guid orgUserId, string publicKey,
            string privateKey, SutProvider<InitPendingOrganizationCommand> sutProvider, Organization org, OrganizationUser orgUser)

    {

        var token = CreateToken(orgUser, orgUserId, sutProvider);

        org.PublicKey = null;
        org.PrivateKey = privateKey;
        org.Enabled = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(orgId).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InitPendingOrganizationAsync(user, orgId, orgUserId, publicKey, privateKey, "", token));

        Assert.Equal("Organization already has a Private Key.", exception.Message);

    }

    public string CreateToken(OrganizationUser orgUser, Guid orgUserId, SutProvider<InitPendingOrganizationCommand> sutProvider)
    {
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        _orgUserInviteTokenableFactory.CreateToken(orgUser).Returns(new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        });

        var orgUserInviteTokenable = _orgUserInviteTokenableFactory.CreateToken(orgUser);
        var protectedToken = _orgUserInviteTokenDataFactory.Protect(orgUserInviteTokenable);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUserId).Returns(orgUser);

        return protectedToken;
    }
}
