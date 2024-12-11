using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationAuth.Models;

[SutProviderCustomize]
public class AuthRequestUpdateProcessorTests
{
    [Theory]
    [BitAutoData]
    public void Process_NoAuthRequestLoaded_Throws(
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        var sut = new AuthRequestUpdateProcessor(null, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestIsAlreadyApproved_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        authRequest = Approve(authRequest);
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestIsAlreadyDenied_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        authRequest = Deny(authRequest);
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestIsExpired_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        processorConfiguration.AuthRequestExpiresAfter = new TimeSpan(0, 10, 0);
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-60);
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_UpdateDoesNotMatch_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        while (authRequest.Id == update.Id)
        {
            authRequest.Id = new Guid();
        }
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_AuthRequestAndOrganizationIdMismatch_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        while (authRequest.OrganizationId == processorConfiguration.OrganizationId)
        {
            authRequest.OrganizationId = new Guid();
        }
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<AuthRequestUpdateCouldNotBeProcessedException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestApproved_NoKey_Throws(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = true;
        update.Key = null;
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        Assert.ThrowsAny<ApprovedAuthRequestIsMissingKeyException>(() => sut.Process());
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestApproved_ValidInput_Works(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = true;
        update.Key = "key";
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        sut.Process();
        Assert.True(sut.ProcessedAuthRequest.Approved);
        Assert.Equal(sut.ProcessedAuthRequest.Key, update.Key);
        Assert.NotNull(sut.ProcessedAuthRequest.ResponseDate);
    }

    [Theory]
    [BitAutoData]
    public void Process_RequestDenied_ValidInput_Works(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = false;
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        sut.Process();
        Assert.False(sut.ProcessedAuthRequest.Approved);
        Assert.Null(sut.ProcessedAuthRequest.Key);
        Assert.NotNull(sut.ProcessedAuthRequest.ResponseDate);
    }

    [Theory]
    [BitAutoData]
    public async Task SendPushNotification_RequestIsDenied_DoesNotSend(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = false;
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, Task>>();
        sut.Process();
        await sut.SendPushNotification(callback);
        await callback.DidNotReceiveWithAnyArgs()(sut.ProcessedAuthRequest);
    }

    [Theory]
    [BitAutoData]
    public async Task SendPushNotification_RequestIsApproved_DoesSend(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = true;
        update.Key = "key";
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, Task>>();
        sut.Process();
        await sut.SendPushNotification(callback);
        await callback.Received()(sut.ProcessedAuthRequest);
    }

    [Theory]
    [BitAutoData]
    public async Task SendApprovalEmail_RequestIsDenied_DoesNotSend(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        update.Approved = false;
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, string, Task>>();
        sut.Process();
        await sut.SendApprovalEmail(callback);
        await callback.DidNotReceiveWithAnyArgs()(sut.ProcessedAuthRequest, "string");
    }

    [Theory]
    [BitAutoData]
    public async Task SendApprovalEmail_RequestIsApproved_DoesSend(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
    {
        (authRequest, processorConfiguration) = UnrespondAndEnsureValid(
            authRequest,
            update,
            processorConfiguration
        );
        authRequest.RequestDeviceType = DeviceType.iOS;
        authRequest.RequestDeviceIdentifier = "device-id";
        update.Approved = true;
        update.Key = "key";
        var sut = new AuthRequestUpdateProcessor(authRequest, update, processorConfiguration);
        var callback = Substitute.For<Func<OrganizationAdminAuthRequest, string, Task>>();
        sut.Process();
        await sut.SendApprovalEmail(callback);
        await callback.Received()(sut.ProcessedAuthRequest, "iOS - device-id");
    }

    private static T Approve<T>(T authRequest)
        where T : AuthRequest
    {
        authRequest.Key = "key";
        authRequest.Approved = true;
        authRequest.ResponseDate = DateTime.UtcNow;
        return authRequest;
    }

    private static T Deny<T>(T authRequest)
        where T : AuthRequest
    {
        authRequest.Approved = false;
        authRequest.ResponseDate = DateTime.UtcNow;
        return authRequest;
    }

    private (
        T AuthRequest,
        AuthRequestUpdateProcessorConfiguration ProcessorConfiguration
    ) UnrespondAndEnsureValid<T>(
        T authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration processorConfiguration
    )
        where T : AuthRequest
    {
        authRequest.Id = update.Id;
        authRequest.OrganizationId = processorConfiguration.OrganizationId;
        authRequest.Key = null;
        authRequest.Approved = null;
        authRequest.ResponseDate = null;
        authRequest.AuthenticationDate = null;
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-1);
        processorConfiguration.AuthRequestExpiresAfter = new TimeSpan(1, 0, 0);
        return (authRequest, processorConfiguration);
    }
}
