using System.Text.Json.Nodes;
using Bit.Commercial.Core.SecretsManager.Commands.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
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
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendRequestSMAccessToAdminEmailAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
