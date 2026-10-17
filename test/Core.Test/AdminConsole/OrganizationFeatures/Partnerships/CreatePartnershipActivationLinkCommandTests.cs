using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class CreatePartnershipActivationLinkCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string VaultUrl = "https://vault.example.com";
    private const string RegisteredOrigin = "https://partner.example.com";
    private const string ExternalId = "customer-1";

    private readonly DataProtectorTokenFactory<PartnershipActivationTokenable> _tokenFactory = new(
        PartnershipActivationTokenable.ClearTextPrefix, PartnershipActivationTokenable.DataProtectorPurpose,
        new EphemeralDataProtectionProvider(),
        Substitute.For<ILogger<DataProtectorTokenFactory<PartnershipActivationTokenable>>>());

    [Fact]
    public async Task CreateAsync_Provisioned_ReturnsVaultActivationUrlCarryingToken()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        var link = result.AsSuccess;
        var prefix = $"{VaultUrl}/#/partner-activation?token=";
        Assert.StartsWith(prefix, link.ActivationUrl);
        Assert.Equal(link.Token, Uri.UnescapeDataString(link.ActivationUrl[prefix.Length..]));
    }

    [Fact]
    public async Task CreateAsync_Provisioned_TokenIdentifiesEntitlementAndExpiresInFifteenMinutes()
    {
        var (sutProvider, partnership, entitlement) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        var link = result.AsSuccess;
        var tokenable = _tokenFactory.Unprotect(link.Token);
        Assert.Equal(partnership.Id, tokenable.OrganizationPartnershipId);
        Assert.Equal(entitlement.Id, tokenable.OrganizationPartnershipEntitlementId);
        Assert.Equal(_now.AddMinutes(15), link.ExpiresAt);
        Assert.Equal(link.ExpiresAt, tokenable.ExpirationDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_VaultUrlWithTrailingSlash_DoesNotDoubleTheSlash()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);
        sutProvider.GetDependency<GlobalSettings>().BaseServiceUri.Vault = VaultUrl + "/";

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.StartsWith($"{VaultUrl}/#/partner-activation?token=", result.AsSuccess.ActivationUrl);
    }

    [Fact]
    public async Task CreateAsync_CanceledWithHeldBindingInsideResumeWindow_ReturnsLink()
    {
        var (sutProvider, partnership, entitlement) = Arrange(PartnershipEntitlementState.Canceled);
        entitlement.UserId = Guid.NewGuid();
        entitlement.ResumeWindowExpirationDate = _now.AddMinutes(1);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_CanceledWithoutBinding_ReturnsNotProvisioned()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Canceled);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.Equal("not_provisioned", Assert.IsType<EntitlementNotProvisioned>(result.AsError).Code);
    }

    [Fact]
    public async Task CreateAsync_CanceledWithBindingPastResumeWindow_ReturnsNotResumable()
    {
        var (sutProvider, partnership, entitlement) = Arrange(PartnershipEntitlementState.Canceled);
        entitlement.UserId = Guid.NewGuid();
        entitlement.ResumeWindowExpirationDate = _now;

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.Equal("entitlement_not_resumable", Assert.IsType<EntitlementNotResumable>(result.AsError).Code);
    }

    [Theory]
    [InlineData(PartnershipEntitlementState.Active)]
    [InlineData(PartnershipEntitlementState.Suspended)]
    public async Task CreateAsync_Bound_ReturnsAlreadyBound(PartnershipEntitlementState state)
    {
        var (sutProvider, partnership, entitlement) = Arrange(state);
        entitlement.UserId = Guid.NewGuid();

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.Equal("already_bound", Assert.IsType<EntitlementAlreadyBound>(result.AsError).Code);
    }

    [Fact]
    public async Task CreateAsync_UnknownExternalId_ReturnsNotFound()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership) with { ExternalId = "someone-else" });

        Assert.IsType<EntitlementNotFound>(result.AsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(Constants.DatabaseFieldProtectedPrefix + "abc")]
    public async Task CreateAsync_InvalidExternalId_ReturnsInvalidExternalId(string externalId)
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership) with { ExternalId = externalId });

        Assert.Equal("invalid_external_id", Assert.IsType<InvalidExternalId>(result.AsError).Code);
    }

    [Fact]
    public async Task CreateAsync_ExternalIdTooLong_ReturnsInvalidExternalId()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);
        var externalId = new string('a', OrganizationPartnershipEntitlement.ExternalIdMaxLength + 1);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership) with { ExternalId = externalId });

        Assert.IsType<InvalidExternalId>(result.AsError);
    }

    [Fact]
    public async Task CreateAsync_InactivePartnership_ReturnsPartnershipNotActive()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);
        partnership.Status = PartnershipStatus.Inactive;

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.IsType<PartnershipNotActive>(result.AsError);
    }

    [Fact]
    public async Task CreateAsync_NoReturnUrl_TokenHasNoReturnUrl()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership));

        Assert.Null(_tokenFactory.Unprotect(result.AsSuccess.Token).ReturnUrl);
    }

    [Theory]
    [InlineData("https://partner.example.com")]
    [InlineData("https://partner.example.com/account/done?ref=abc")]
    [InlineData("https://PARTNER.Example.com/done")]
    [InlineData("https://partner.example.com:443/done")]
    public async Task CreateAsync_ReturnUrlOnRegisteredOrigin_IsCarriedInToken(string returnUrl)
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership, returnUrl));

        var carried = _tokenFactory.Unprotect(result.AsSuccess.Token).ReturnUrl;
        Assert.Equal(new Uri(returnUrl), new Uri(carried!));
    }

    [Theory]
    [InlineData("http://partner.example.com/done")]
    [InlineData("https://other.example.com/done")]
    [InlineData("https://sub.partner.example.com/done")]
    [InlineData("https://partner.example.com.evil.com/done")]
    [InlineData("https://partner.example.com:8443/done")]
    [InlineData("https://user@partner.example.com/done")]
    [InlineData("/done")]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    public async Task CreateAsync_ReturnUrlNotOnRegisteredHttpsOrigin_ReturnsInvalidReturnUrl(string returnUrl)
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership, returnUrl));

        Assert.Equal("invalid_return_url", Assert.IsType<InvalidReturnUrl>(result.AsError).Code);
    }

    [Fact]
    public async Task CreateAsync_ReturnUrlTooLong_ReturnsInvalidReturnUrl()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);
        var returnUrl = $"{RegisteredOrigin}/{new string('a', CreatePartnershipActivationLinkRequest.ReturnUrlMaxLength)}";

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership, returnUrl));

        Assert.IsType<InvalidReturnUrl>(result.AsError);
    }

    [Fact]
    public async Task CreateAsync_ReturnUrlWithNoRegisteredOrigins_ReturnsInvalidReturnUrl()
    {
        var (sutProvider, partnership, _) = Arrange(PartnershipEntitlementState.Provisioned);
        partnership.SetRegisteredReturnOrigins([]);

        var result = await sutProvider.Sut.CreateAsync(CreateRequest(partnership, RegisteredOrigin + "/done"));

        Assert.IsType<InvalidReturnUrl>(result.AsError);
    }

    private (SutProvider<CreatePartnershipActivationLinkCommand> SutProvider, OrganizationPartnership Partnership,
        OrganizationPartnershipEntitlement Entitlement) Arrange(PartnershipEntitlementState state)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.BaseServiceUri.Vault = VaultUrl;
        var sutProvider = new SutProvider<CreatePartnershipActivationLinkCommand>()
            .SetDependency(globalSettings)
            .SetDependency<IDataProtectorTokenFactory<PartnershipActivationTokenable>>(_tokenFactory)
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        var partnership = new OrganizationPartnership
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = PartnershipStatus.Active,
        };
        partnership.SetRegisteredReturnOrigins([RegisteredOrigin]);

        var entitlement = new OrganizationPartnershipEntitlement
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = partnership.Id,
            ExternalId = ExternalId,
            ExternalIdHash = "hash",
            State = state,
        };
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetByExternalIdAsync(partnership.Id, ExternalId)
            .Returns(entitlement);

        return (sutProvider, partnership, entitlement);
    }

    private static CreatePartnershipActivationLinkRequest CreateRequest(
        OrganizationPartnership partnership, string? returnUrl = null) =>
        new() { Partnership = partnership, ExternalId = ExternalId, ReturnUrl = returnUrl };
}
