using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;

namespace Bit.Core.Test.OrganizationFeatures.OrgUser.Invitation.Invite
{
    [SutProviderCustomize]
    public class OrganizationUserInviteCommandTests
    {
        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.User,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_Passes(Organization organization,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites,
            OrganizationUser invitor,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            // Autofixture will add collections for all of the invites, remove the first and for all the rest set all access false
            invites.First().invite.AccessAll = true;
            invites.First().invite.Collections = null;
            invites.Skip(1).ToList().ForEach(i => i.invite.AccessAll = false);
            var expectedInviteCount = invites.SelectMany(i => i.invite.Emails).Count();

            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, });

            sutProvider.GetDependency<IOrganizationUserInviteAccessPolicies>()
                .CanInviteAsync(organization, invites.Select(i => i.invite), default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<IOrganizationUserInvitationService>().InviteUsersAsync(default, default, default)
                .ReturnsForAnyArgs(
                    invites.SelectMany(i => i.invite.Emails).Select(e => new OrganizationUser()).ToList());


            await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, invites);

            await sutProvider.GetDependency<IOrganizationUserMailer>().Received(1)
                .SendInvitesAsync(
                    Arg.Is<IEnumerable<(OrganizationUser invite, ExpiringToken token)>>(m =>
                        m.Count() == expectedInviteCount),
                    organization);
        }
    }
}
