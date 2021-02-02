using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.SendFixtures;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class SendServiceTests
    {
        private void SaveSendAsync_Setup(SendType sendType, OrganizationUserType userType,
            SutProvider<SendService> sutProvider, Send send, List<Policy> policies,
            OrganizationUserOrganizationDetails organizationDetails)
        {
            send.Id = default;
            send.Type = sendType;

            organizationDetails.Enabled = true;
            organizationDetails.UsePolicies = true;
            organizationDetails.Type = userType;

            policies.First().Type = PolicyType.DisableSend;
            policies.First().Enabled = true;

            sutProvider.GetDependency<IPolicyRepository>().GetManyByUserIdAsync(send.UserId.Value).Returns(policies);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetDetailsByUserAsync(send.UserId.Value,
                policies.First().OrganizationId, OrganizationUserStatusType.Confirmed).Returns(organizationDetails);
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.User)]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Manager)]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Custom)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.User)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Manager)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Custom)]
        public async void SaveSendAsync_DisableSend_NonAdmin_throws(SendType sendType, OrganizationUserType userType,
            SutProvider<SendService> sutProvider, Send send, List<Policy> policies,
            OrganizationUserOrganizationDetails organizationDetails)
        {
            SaveSendAsync_Setup(sendType, userType, sutProvider, send, policies, organizationDetails);

            await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveSendAsync(send));
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.User)]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Manager)]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Custom)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.User)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Manager)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Custom)]
        public async void SaveSendAsync_DisableSend_DisabledPolicy_NonAdmin_success(SendType sendType, OrganizationUserType userType,
            SutProvider<SendService> sutProvider, Send send, List<Policy> policies,
            OrganizationUserOrganizationDetails organizationDetails)
        {
            SaveSendAsync_Setup(sendType, userType, sutProvider, send, policies, organizationDetails);
            foreach (var policy in policies.Where(p => p.Type == PolicyType.DisableSend))
            {
                policy.Enabled = false;
            }

            await sutProvider.Sut.SaveSendAsync(send);

            await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Admin)]
        [InlineUserSendAutoData(SendType.File, OrganizationUserType.Owner)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Admin)]
        [InlineUserSendAutoData(SendType.Text, OrganizationUserType.Owner)]
        public async void SaveSendAsync_DisableSend_Admin_success(SendType sendType, OrganizationUserType userType,
            SutProvider<SendService> sutProvider, Send send, List<Policy> policies,
            OrganizationUserOrganizationDetails organizationDetails)
        {
            SaveSendAsync_Setup(sendType, userType, sutProvider, send, policies, organizationDetails);

            await sutProvider.Sut.SaveSendAsync(send);

            await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
        }
    }
}
