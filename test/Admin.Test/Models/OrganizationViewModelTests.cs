using Bit.Admin.AdminConsole.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Admin.Test.Models;

public class OrganizationViewModelTests
{
    [Theory]
    [BitAutoData]
    public void OwnersDetails_GivenPendingOrganization_WhenOwnerIsConfirmed_ThenOwnerIsShown(
        Organization org,
        OrganizationUserUserDetails owner)
    {
        // Regression: Pending orgs with Confirmed owners previously showed no owner in the admin portal
        org.Status = OrganizationStatusType.Pending;
        owner.Type = OrganizationUserType.Owner;
        owner.Status = OrganizationUserStatusType.Confirmed;

        var viewModel = BuildViewModel(org, [owner]);

        Assert.Contains(owner, viewModel.OwnersDetails);
    }

    [Theory]
    [BitAutoData]
    public void OwnersDetails_GivenOrganizationWithOwnerAndAdmin_WhenBuilt_ThenOnlyOwnersAreIncluded(
        Organization org,
        OrganizationUserUserDetails owner,
        OrganizationUserUserDetails admin)
    {
        org.Status = OrganizationStatusType.Created;
        owner.Type = OrganizationUserType.Owner;
        owner.Status = OrganizationUserStatusType.Confirmed;
        admin.Type = OrganizationUserType.Admin;
        admin.Status = OrganizationUserStatusType.Confirmed;

        var viewModel = BuildViewModel(org, [owner, admin]);

        Assert.Contains(owner, viewModel.OwnersDetails);
        Assert.DoesNotContain(admin, viewModel.OwnersDetails);
        Assert.Contains(admin, viewModel.AdminsDetails);
    }

    private static OrganizationViewModel BuildViewModel(
        Organization org,
        IEnumerable<OrganizationUserUserDetails> orgUsers)
        => new(org, null, [], orgUsers, [], [], null, null, 0, 0, 0, 0);
}
