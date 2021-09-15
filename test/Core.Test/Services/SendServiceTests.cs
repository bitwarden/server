using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Context;
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
using System.Text.Json;

namespace Bit.Core.Test.Services
{
    public class SendServiceTests
    {
        private void SaveSendAsync_Setup(SendType sendType, bool disableSendPolicyAppliesToUser,
            SutProvider<SendService> sutProvider, Send send)
        {
            send.Id = default;
            send.Type = sendType;

            sutProvider.GetDependency<IPolicyRepository>().GetCountByTypeApplicableToUserIdAsync(
                Arg.Any<Guid>(), PolicyType.DisableSend).Returns(disableSendPolicyAppliesToUser ? 1 : 0);
        }

        // Disable Send policy check

        [Theory]
        [InlineUserSendAutoData(SendType.File)]
        [InlineUserSendAutoData(SendType.Text)]
        public async void SaveSendAsync_DisableSend_Applies_throws(SendType sendType,
            SutProvider<SendService> sutProvider, Send send)
        {
            SaveSendAsync_Setup(sendType, disableSendPolicyAppliesToUser: true, sutProvider, send);

            await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveSendAsync(send));
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File)]
        [InlineUserSendAutoData(SendType.Text)]
        public async void SaveSendAsync_DisableSend_DoesntApply_success(SendType sendType,
            SutProvider<SendService> sutProvider, Send send)
        {
            SaveSendAsync_Setup(sendType, disableSendPolicyAppliesToUser: false, sutProvider, send);

            await sutProvider.Sut.SaveSendAsync(send);

            await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
        }

        // Send Options Policy - Disable Hide Email check

        private void SaveSendAsync_HideEmail_Setup(bool disableHideEmailAppliesToUser,
            SutProvider<SendService> sutProvider, Send send, Policy policy)
        {
            send.HideEmail = true;

            var sendOptions = new SendOptionsPolicyData
            {
                DisableHideEmail = disableHideEmailAppliesToUser
            };
            policy.Data = JsonSerializer.Serialize(sendOptions, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            sutProvider.GetDependency<IPolicyRepository>().GetManyByTypeApplicableToUserIdAsync(
                Arg.Any<Guid>(), PolicyType.SendOptions).Returns(new List<Policy>
                {
                    policy,
                });
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File)]
        [InlineUserSendAutoData(SendType.Text)]
        public async void SaveSendAsync_DisableHideEmail_Applies_throws(SendType sendType,
            SutProvider<SendService> sutProvider, Send send, Policy policy)
        {
            SaveSendAsync_Setup(sendType, false, sutProvider, send);
            SaveSendAsync_HideEmail_Setup(true, sutProvider, send, policy);

            await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveSendAsync(send));
        }

        [Theory]
        [InlineUserSendAutoData(SendType.File)]
        [InlineUserSendAutoData(SendType.Text)]
        public async void SaveSendAsync_DisableHideEmail_DoesntApply_success(SendType sendType,
            SutProvider<SendService> sutProvider, Send send, Policy policy)
        {
            SaveSendAsync_Setup(sendType, false, sutProvider, send);
            SaveSendAsync_HideEmail_Setup(false, sutProvider, send, policy);

            await sutProvider.Sut.SaveSendAsync(send);

            await sutProvider.GetDependency<ISendRepository>().Received(1).CreateAsync(send);
        }
    }
}
