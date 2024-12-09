using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class SsoConfigServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_ExistingItem_UpdatesRevisionDateOnly(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var ssoConfig = new SsoConfig
        {
            Id = 1,
            Data = "{}",
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<ISsoConfigRepository>()
            .UpsertAsync(ssoConfig).Returns(Task.CompletedTask);

        await sutProvider.Sut.SaveAsync(ssoConfig, organization);

        await sutProvider.GetDependency<ISsoConfigRepository>().Received()
            .UpsertAsync(ssoConfig);

        Assert.Equal(utcNow.AddDays(-10), ssoConfig.CreationDate);
        Assert.True(ssoConfig.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NewItem_UpdatesCreationAndRevisionDate(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = "{}",
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<ISsoConfigRepository>()
            .UpsertAsync(ssoConfig).Returns(Task.CompletedTask);

        await sutProvider.Sut.SaveAsync(ssoConfig, organization);

        await sutProvider.GetDependency<ISsoConfigRepository>().Received()
            .UpsertAsync(ssoConfig);

        Assert.True(ssoConfig.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(ssoConfig.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_PreventDisablingKeyConnector(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var oldSsoConfig = new SsoConfig
        {
            Id = 1,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        var newSsoConfig = new SsoConfig
        {
            Id = 1,
            Data = "{}",
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow,
        };

        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(oldSsoConfig);
        ssoConfigRepository.UpsertAsync(newSsoConfig).Returns(Task.CompletedTask);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(organization.Id)
            .Returns(new[] { new OrganizationUserUserDetails { UsesKeyConnector = true } });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(newSsoConfig, organization));

        Assert.Contains("Key Connector cannot be disabled at this moment.", exception.Message);

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_AllowDisablingKeyConnectorWhenNoUserIsUsingIt(
        SutProvider<SsoConfigService> sutProvider, Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var oldSsoConfig = new SsoConfig
        {
            Id = 1,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        var newSsoConfig = new SsoConfig
        {
            Id = 1,
            Data = "{}",
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow,
        };

        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(oldSsoConfig);
        ssoConfigRepository.UpsertAsync(newSsoConfig).Returns(Task.CompletedTask);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(organization.Id)
            .Returns(new[] { new OrganizationUserUserDetails { UsesKeyConnector = false } });

        await sutProvider.Sut.SaveAsync(newSsoConfig, organization);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_KeyConnector_SingleOrgNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(ssoConfig, organization));

        Assert.Contains("Key Connector requires the Single Organization policy to be enabled.", exception.Message);

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_KeyConnector_SsoPolicyNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
            Arg.Any<Guid>(), PolicyType.SingleOrg).Returns(new Policy
            {
                Enabled = true
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(ssoConfig, organization));

        Assert.Contains("Key Connector requires the Single Sign-On Authentication policy to be enabled.", exception.Message);

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_KeyConnector_SsoConfigNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = false,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
            Arg.Any<Guid>(), Arg.Any<PolicyType>()).Returns(new Policy
            {
                Enabled = true
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(ssoConfig, organization));

        Assert.Contains("You must enable SSO to use Key Connector.", exception.Message);

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_KeyConnector_KeyConnectorAbilityNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        organization.UseKeyConnector = false;
        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
            Arg.Any<Guid>(), Arg.Any<PolicyType>()).Returns(new Policy
            {
                Enabled = true,
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(ssoConfig, organization));

        Assert.Contains("Organization cannot use Key Connector.", exception.Message);

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_KeyConnector_Success(SutProvider<SsoConfigService> sutProvider,
        Organization organization)
    {
        var utcNow = DateTime.UtcNow;

        organization.UseKeyConnector = true;
        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
            CreationDate = utcNow.AddDays(-10),
            RevisionDate = utcNow.AddDays(-10),
        };

        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
            Arg.Any<Guid>(), Arg.Any<PolicyType>()).Returns(new Policy
            {
                Enabled = true,
            });

        await sutProvider.Sut.SaveAsync(ssoConfig, organization);

        await sutProvider.GetDependency<ISsoConfigRepository>().ReceivedWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_Tde_Enable_Required_Policies(SutProvider<SsoConfigService> sutProvider, Organization organization)
    {
        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
            }.Serialize(),
            Enabled = true,
            OrganizationId = organization.Id,
        };

        await sutProvider.Sut.SaveAsync(ssoConfig, organization);

        await sutProvider.GetDependency<ISavePolicyCommand>().Received(1)
            .SaveAsync(
                Arg.Is<PolicyUpdate>(t => t.Type == PolicyType.SingleOrg &&
                    t.OrganizationId == organization.Id &&
                    t.Enabled)
            );

        await sutProvider.GetDependency<ISavePolicyCommand>().Received(1)
            .SaveAsync(
                Arg.Is<PolicyUpdate>(t => t.Type == PolicyType.ResetPassword &&
                    t.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled &&
                    t.OrganizationId == organization.Id &&
                    t.Enabled)
            );

        await sutProvider.GetDependency<ISavePolicyCommand>().Received(1)
            .SaveAsync(
                Arg.Is<PolicyUpdate>(t => t.Type == PolicyType.RequireSso &&
                    t.OrganizationId == organization.Id &&
                    t.Enabled)
            );

        await sutProvider.GetDependency<ISsoConfigRepository>().ReceivedWithAnyArgs()
            .UpsertAsync(default);
    }
}
