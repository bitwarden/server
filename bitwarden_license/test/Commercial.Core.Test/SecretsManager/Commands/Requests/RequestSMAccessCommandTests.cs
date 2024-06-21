using Bit.Commercial.Core.SecretsManager.Commands.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Requests;

[SutProviderCustomize]
public class RequestSMAccessCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SendRequestAccessToSM_Success(
          User user,
          Organization organization,
          ICollection<OrganizationUserUserDetails> orgUsers,
          string emailContent,
          SutProvider<RequestSMAccessCommand> sutProvider)
    {
        foreach (OrganizationUserUserDetails userDetails in orgUsers)
        {
            userDetails.Type = OrganizationUserType.Admin;
        }

        await sutProvider.Sut.SendRequestAccessToSM(organization, orgUsers, user, emailContent);

        var adminEmailList = orgUsers
            .Where(o => o.Type == OrganizationUserType.Admin) // Filter by Admin type
            .Select(a => a.Email)
            .Distinct()
            .ToList();

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendRequestSMAccessToAdminEmailAsync(adminEmailList, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task SendRequestAccessToSM_NoAdmins_ThrowsUnauthorizedAccess(
       User user,
       Organization organization,
       ICollection<OrganizationUserUserDetails> orgUsers,
       string emailContent,
       SutProvider<RequestSMAccessCommand> sutProvider)
    {
        // Set OrgUsers so they are only users, no admins or owners
        foreach (OrganizationUserUserDetails userDetails in orgUsers)
        {
            userDetails.Type = OrganizationUserType.User;
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.SendRequestAccessToSM(organization, orgUsers, user, emailContent));
    }


    [Theory]
    [BitAutoData]
    public async Task SendRequestAccessToSM_SomeAdmins_EmailListIsAsExpected(
       User user,
       Organization organization,
       ICollection<OrganizationUserUserDetails> orgUsers,
       string emailContent,
       SutProvider<RequestSMAccessCommand> sutProvider)
    {
        foreach (OrganizationUserUserDetails userDetails in orgUsers)
        {
            userDetails.Type = OrganizationUserType.User;
        }

        // Make the first orgUser an admin so it's a mix of Admin + Users
        orgUsers.First().Type = OrganizationUserType.Admin;

        var adminEmailList = orgUsers
            .Where(o => o.Type == OrganizationUserType.Admin) // Filter by Admin type
            .Select(a => a.Email)
            .Distinct()
            .ToList();

        await sutProvider.Sut.SendRequestAccessToSM(organization, orgUsers, user, emailContent);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendRequestSMAccessToAdminEmailAsync(adminEmailList, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
