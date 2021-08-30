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
using Newtonsoft.Json;

namespace Bit.Core.Test.Services
{
    public class SendServiceTests
    {
        // Disable Send policy check

        private void SaveSendAsync_Setup(SendType sendType, bool disableSendPolicyAppliesToUser,
            SutProvider<SendService> sutProvider, Send send, bool hideSendEmail = false)
        {
            send.Id = default;
            send.Type = sendType;
            send.HideEmail = hideSendEmail;

            sutProvider.GetDependency<IPolicyService>().PolicyAppliesToCurrentUserAsync(PolicyType.DisableSend,
                null).Returns(disableSendPolicyAppliesToUser);
        }

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

        // SendOptionsPolicy.DisableHideEmail check
        // Filtering the policy data is too closely tied to implementation logic, so we just check to make sure
        // the policyService is called

        [Theory]
        [InlineUserSendAutoData(SendType.File)]
        [InlineUserSendAutoData(SendType.Text)]
        public async void SaveSendAsync_DisableHideEmail_IsChecked(SendType sendType,
            SutProvider<SendService> sutProvider, Send send, List<Policy> policies)
        {
            SaveSendAsync_Setup(sendType, disableSendPolicyAppliesToUser: false, sutProvider, send, hideSendEmail: true);

            await sutProvider.Sut.SaveSendAsync(send);

            await sutProvider.GetDependency<IPolicyService>().Received(1).PolicyAppliesToCurrentUserAsync(PolicyType.SendOptions,
                Arg.Any<Func<Policy, bool>>());
        }
    }
}
