using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

[SutProviderCustomize]
public class CreateAdminInitiatedSponsorshipHandlerTests : FamiliesForEnterpriseTestsBase
{
    [Theory]
    [BitAutoData]
    public async Task HandleAsync_MissingManageUsersPermission_ThrowsUnauthorizedException(
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, string sponsoredEmail, string friendlyName,
        Guid currentUserId, SutProvider<CreateAdminInitiatedSponsorshipHandler> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions(),
                Type = OrganizationUserType.Admin
            }
        ]);

        var request = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, null);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await sutProvider.Sut.HandleAsync(request));

        Assert.Equal("You do not have permissions to send sponsorships on behalf of the organization.", actual.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    public async Task HandleAsync_InvalidUserType_ThrowsUnauthorizedException(
        OrganizationUserType organizationUserType,
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, string sponsoredEmail,
        string friendlyName, Guid currentUserId,
        SutProvider<CreateAdminInitiatedSponsorshipHandler> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions
                {
                    ManageUsers = true,
                },
                Type = organizationUserType
            }
        ]);

        var request = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, null);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await sutProvider.Sut.HandleAsync(request));

        Assert.Equal("You do not have permissions to send sponsorships on behalf of the organization.", actual.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task HandleAsync_CreatesAdminInitiatedSponsorship(
        OrganizationUserType organizationUserType, Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        string sponsoredEmail, string friendlyName, Guid currentUserId, string notes,
        SutProvider<CreateAdminInitiatedSponsorshipHandler> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions
                {
                    ManageUsers = true,
                },
                Type = organizationUserType
            }
        ]);

        var request = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, notes);

        var actual = await sutProvider.Sut.HandleAsync(request);

        var expectedSponsorship = new OrganizationSponsorship
        {
            IsAdminInitiated = true,
            Notes = notes
        };

        AssertHelper.AssertPropertyEqual(expectedSponsorship, actual);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleAsync_ThrowsBadRequestException_WhenFeatureFlagIsDisabled(
        OrganizationUserType organizationUserType, Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        string sponsoredEmail, string friendlyName, Guid currentUserId, string notes,
        SutProvider<CreateAdminInitiatedSponsorshipHandler> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM17772_AdminInitiatedSponsorships))
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions
                {
                    ManageUsers = true,
                },
                Type = organizationUserType
            }
        ]);

        var request = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, notes);

        var actual = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.HandleAsync(request));

        Assert.Equal("Feature 'pm-17772-admin-initiated-sponsorships' is not enabled.", actual.Message);
    }
}
